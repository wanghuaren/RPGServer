using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Logic.Battle.Skill;
using Ddxy.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    [CollectionAgeLimit(AlwaysActive = true)]
    public class MallGrain : Grain, IMallGrain
    {
        private readonly ILogger<MallGrain> _logger;
        private bool _isActive;
        private uint _serverId;
        private List<MallEntity> _list;
        private IDisposable _tickTimer;

        // 每件商品寄售时间,24小时
        private const uint Duration = 24 * 60 * 60;
        private const int PageSize = 8;

        public MallGrain(ILogger<MallGrain> logger)
        {
            _logger = logger;
        }

        public override Task OnActivateAsync()
        {
            _serverId = (uint) this.GetPrimaryKeyLong();
            return Task.CompletedTask;
        }

        public override Task OnDeactivateAsync()
        {
            return ShutDown();
        }

        public async Task StartUp()
        {
            if (_isActive) return;
            _isActive = true;

            await Reload();
            // 1分钟更新一次
            _tickTimer?.Dispose();
            _tickTimer = RegisterTimer(OnTick, null, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(30));
            LogInfo("激活成功");
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _isActive = false;

            _tickTimer?.Dispose();
            _tickTimer = null;
            _list?.Clear();
            _list = null;
            LogInfo("注销成功");
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new(_isActive);
        }

        public async Task Reload()
        {
            if (!_isActive) return;
            // 最多只取500条, 每页10条，50页
            _list = await DbService.QueryMalls(_serverId, 500);
        }

        public Task<Immutable<byte[]>> QueryItems(uint roleId, Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            var req = C2S_MallItems.Parser.ParseFrom(reqBytes.Value);
            var itemIdDic = new Dictionary<uint, bool>();
            foreach (var cfgId in req.CfgIds)
            {
                if (cfgId > 0) itemIdDic[cfgId] = true;
            }

            // 先过滤出符合条件的所有元素
            var filter = _list.Where(p =>
                    p != null && p.Kind == req.Kind && (itemIdDic.Count == 0 || itemIdDic.ContainsKey(p.CfgId)))
                .ToList();
            // 计算最大Page
            var maxPageIndex = (uint) MathF.Ceiling(filter.Count * 1.0f / PageSize);
            if (maxPageIndex < 1) maxPageIndex = 1;
            req.PageIndex = Math.Clamp(req.PageIndex, 1, maxPageIndex);
            // 排序后分页获取
            var ordered = req.Asc ? filter.OrderBy(p => p.Price) : filter.OrderByDescending(p => p.Price);
            var pagedList = ordered.Skip((int) (req.PageIndex - 1) * PageSize).Take(PageSize);
            var resp = new S2C_MallItems
            {
                PageIndex = req.PageIndex,
                Total = (uint) filter.Count
            };
            foreach (var entity in pagedList)
            {
                resp.List.Add(new MallItem
                {
                    Id = entity.Id,
                    Type = entity.Type,
                    CfgId = entity.CfgId,
                    Price = entity.Price,
                    Num = entity.Num
                });
            }

            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(resp)));
        }

        /// <summary>
        /// 上架商品, 返回商品信息
        /// </summary>
        public async Task<Immutable<byte[]>> AddItem(uint roleId, Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return new Immutable<byte[]>(null);
            // 不能超过8个
            var num = _list.Count(p => p != null && p.RoleId == roleId);
            if (num >= 8) return new Immutable<byte[]>(null);

            var req = MallAddItemRequest.Parser.ParseFrom(reqBytes.Value);
            var kind = CalcKind(req);
            if (kind == MallItemKind.Unkown) return new Immutable<byte[]>(null);

            var entity = new MallEntity
            {
                ServerId = _serverId,
                RoleId = roleId,
                DbId = req.DbId,
                CfgId = req.CfgId,
                Num = req.Num,
                SellNum = 0,
                Price = req.Price,
                Type = req.Type,
                Kind = kind,
                Detail = req.Details.ToByteArray(),
                CreateTime = TimeUtil.TimeStamp
            };
            // 这里要注意，目前FreeSql的byte[]不支持空数组插入
            if (entity.Detail is {Length: 0}) entity.Detail = null;

            // 插入数据库
            await DbService.InsertEntity(entity);
            if (entity.Id == 0) return new Immutable<byte[]>(null);

            // 加入到缓存中
            var idx = _list.FindIndex(p => p == null);
            if (idx >= 0) _list[idx] = entity;
            else _list.Add(entity);

            return new Immutable<byte[]>(Packet.Serialize(new MallItem
            {
                Id = entity.Id,
                Type = entity.Type,
                CfgId = entity.CfgId,
                Price = entity.Price,
                Num = entity.Num
            }));
        }

        public async Task<Immutable<byte[]>> DelItem(uint roleId, uint id)
        {
            if (!_isActive) return new Immutable<byte[]>(null);
            if (id == 0) return new Immutable<byte[]>(null);
            // 检查是否存在以及角色所属问题
            var idx = _list.FindIndex(p => p != null && p.Id == id && p.RoleId == roleId);
            if (idx < 0) return new Immutable<byte[]>(null);
            var entity = _list[idx];
            // 从数据库中删除
            await DbService.DeleteEntity<MallEntity>(entity.Id);
            // 从缓存中删除
            _list[idx] = null;
            // 返回商品信息
            var request = new MallItemUnShelfRequest
            {
                Id = entity.Id,
                DbId = entity.DbId,
                CfgId = entity.CfgId,
                Price = entity.Price,
                Num = entity.Num,
                Type = entity.Type
            };
            return new Immutable<byte[]>(Packet.Serialize(request));
        }

        /// <summary>
        /// 修改物品单价
        /// </summary>
        public async Task<bool> UpdateItem(uint roleId, uint id, uint price)
        {
            if (!_isActive) return false;
            if (id == 0 || price == 0) return false;
            var item = _list.FirstOrDefault(p => p != null && p.RoleId == roleId && p.Id == id);
            if (item == null) return false;
            item.Price = price;
            await Task.CompletedTask;
            return true;
        }

        public async Task<Immutable<byte[]>> BuyItem(uint roleId, uint id, uint num)
        {
            if (!_isActive) return new Immutable<byte[]>(null);
            // 检查商品信息
            var idx = _list.FindIndex(p => p != null && p.Id == id);
            if (idx < 0) return new Immutable<byte[]>(null);
            var entity = _list[idx];
            if (entity.Num == 0)
            {
                await UnShelf(entity);
                return new Immutable<byte[]>(null);
            }

            // 检查商品数量
            var buyNum = num;
            if (entity.Num < num) buyNum = entity.Num;
            entity.SellNum += buyNum;
            entity.Num -= buyNum;
            var needDelete = entity.Num == 0;
            if (needDelete)
            {
                // 售罄
                _list[idx] = null;
                await DbService.DeleteEntity<MallEntity>(entity.Id);
            }
            else
            {
                // 更新已购数量和剩余数量
                await DbService.Sql.Update<MallEntity>()
                    .Where(p => p.Id == entity.Id)
                    .Set(p => p.SellNum, entity.SellNum)
                    .Set(p => p.Num, entity.Num)
                    .ExecuteAffrowsAsync();
            }

            // 平台收取8%的税率
            var totalPrice = entity.Price * buyNum;
            var txt = (uint) MathF.Floor(totalPrice * 0.08f);
            var reward = totalPrice - txt;
            // 给卖方发奖励
            var grain = GrainFactory.GetGrain<IPlayerGrain>(entity.RoleId);
            _ = grain.OnMallItemSelled(new Immutable<byte[]>(Packet.Serialize(new OnMallItemSelledRequest
            {
                Id = entity.Id,
                Type = entity.Type,
                DbId = entity.DbId,
                CfgId = entity.CfgId,
                Reward = reward,
                Num = entity.Num
            })));

            // 回复买方
            var response = new S2C_MallBuy
            {
                Id = entity.Id,
                Type = entity.Type,
                DbId = entity.DbId,
                CfgId = entity.CfgId,
                Price = entity.Price,
                Num = buyNum,
                SpareNum = entity.Num
            };

            return new Immutable<byte[]>(Packet.Serialize(response));
        }

        public async Task<Immutable<byte[]>> GetItem(uint id)
        {
            if (!_isActive) return new Immutable<byte[]>(null);
            var entity = _list.FirstOrDefault(p => p != null && p.Id == id);
            if (entity == null) return new Immutable<byte[]>(null);
            // 检查是否已下架
            if (TimeUtil.TimeStamp - entity.CreateTime > Duration)
            {
                await UnShelf(entity);
                return new Immutable<byte[]>(null);
            }

            var resp = new MallItem
            {
                Id = entity.Id,
                Type = entity.Type,
                CfgId = entity.CfgId,
                Price = entity.Price,
                Num = entity.Num
            };
            return new Immutable<byte[]>(Packet.Serialize(resp));
        }

        public async Task<Immutable<byte[]>> GetItemDetail(uint id)
        {
            if (!_isActive) return new Immutable<byte[]>(null);
            var entity = _list.FirstOrDefault(p => p != null && p.Id == id);
            if (entity == null) return new Immutable<byte[]>(null);
            // 检查是否已下架
            if (TimeUtil.TimeStamp - entity.CreateTime > Duration)
            {
                await UnShelf(entity);
                return new Immutable<byte[]>(null);
            }

            var resp = new S2C_MallItemDetail
            {
                Id = entity.Id,
                Type = entity.Type,
                Detail = ByteString.CopyFrom(entity.Detail ?? new byte[0])
            };
            return new Immutable<byte[]>(Packet.Serialize(resp));
        }

        public Task<Immutable<byte[]>> GetMyItems(uint roleId)
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            var resp = new S2C_MallMyItems();

            var now = TimeUtil.TimeStamp;
            var list = _list.Where(p => p != null && p.RoleId == roleId);
            foreach (var entity in list)
            {
                var expireTime = entity.CreateTime + Duration;
                var item = new MallMyItem
                {
                    Id = entity.Id,
                    Type = entity.Type,
                    DbId = entity.DbId,
                    CfgId = entity.CfgId,
                    Price = entity.Price,
                    Num = entity.Num,
                    Time = now >= expireTime ? 0 : expireTime - now
                };
                resp.List.Add(item);
            }

            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(resp)));
        }

        private async Task OnTick(object _)
        {
            if (!_isActive) return;
            var now = TimeUtil.TimeStamp;
            // 找到过期的商品
            var list = _list.Where(p => p != null && now - p.CreateTime > Duration).ToList();
            for (var i = 0; i < list.Count; i++)
            {
                // 防止一次消耗过多，每次最多只处理10条
                if (i >= 9) break;
                await UnShelf(list[i]);
            }
        }

        // 下架商品
        private async Task UnShelf(MallEntity entity)
        {
            if (!_isActive) return;
            if (entity == null) return;
            // 返回给卖家
            var request = new MallItemUnShelfRequest
            {
                Id = entity.Id,
                DbId = entity.DbId,
                CfgId = entity.CfgId,
                Price = entity.Price,
                Num = entity.Num,
                Type = entity.Type
            };
            var grain = GrainFactory.GetGrain<IPlayerGrain>(entity.RoleId);
            var ret = await grain.OnMallItemUnShelf(new Immutable<byte[]>(Packet.Serialize(request)));
            if (ret)
            {
                // 数据库删除
                await DbService.DeleteEntity<MallEntity>(entity.Id);
            }
        }

        private static MallItemKind CalcKind(MallAddItemRequest req)
        {
            var kind = MallItemKind.Unkown;
            switch (req.Type)
            {
                case MallItemType.Item:
                {
                    ConfigService.Items.TryGetValue(req.CfgId, out var cfg);
                    if (cfg == null) return MallItemKind.Unkown;
                    switch (cfg.Type)
                    {
                        case 1:
                            // 五行天书
                            kind = MallItemKind.WuXingTianShu;
                            break;
                        case 3:
                            // 药品道具
                            kind = MallItemKind.YaoPinDaoJu;
                            break;
                        case 10:
                        {
                            // 技能书
                            var skill = SkillManager.GetSkill((SkillId) cfg.Num);
                            if (skill == null) return MallItemKind.Unkown;
                            if (skill.Quality == SkillQuality.Final || skill.Quality == SkillQuality.Shen)
                            {
                                // 终极技能书
                                kind = MallItemKind.ZhongJiJiNengShu;
                            }
                            else if (skill.Quality == SkillQuality.High)
                            {
                                // 高级技能书
                                kind = MallItemKind.GaoJiJiNengShu;
                            }
                            else
                            {
                                // 普通技能书
                                kind = MallItemKind.PuTongJiNengShu;
                            }
                        }
                            break;
                    }

                    switch (req.CfgId)
                    {
                        case 10301:
                        case 10302:
                        case 10303:
                        {
                            // 见闻录
                            kind = MallItemKind.QiZhenYiBao;
                            break;
                        }
                        default:
                        {
                            // 元气丹
                            if ((int) MathF.Floor(cfg.Id / 100f) == 105)
                                kind = MallItemKind.YuanQiDan;
                        }
                            break;
                    }
                }
                    break;
                case MallItemType.Equip:
                {
                    ConfigService.Equips.TryGetValue(req.CfgId, out var cfg);
                    if (cfg is {Category: (byte) EquipCategory.Shen or (byte) EquipCategory.Xian})
                    {
                        kind = MallItemKind.ZhenPinZhuangBei;
                    }
                }
                    break;
                case MallItemType.Pet:
                {
                    ConfigService.Pets.TryGetValue(req.CfgId, out var cfg);
                    if (cfg is {Grade: >= 2})
                    {
                        kind = MallItemKind.ZhenPinChongWu;
                    }
                }
                    break;
            }

            if (kind == MallItemKind.Unkown) kind = MallItemKind.QiangHuaCaiLiao;

            return kind;
        }
        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"摆摊[{_serverId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"摆摊[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"摆摊[{_serverId}]:{msg}");
        }
    }
}