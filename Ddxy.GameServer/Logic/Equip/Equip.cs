using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Config;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Equip
{
    public class Equip
    {
        private PlayerGrain _player;

        public EquipEntity Entity { get; private set; }
        private EquipEntity _lastEntity; //上一次更新的Entity

        private Attrs _baseAttrs;
        private List<AttrPair> _refineAttrs;
        private Attrs _needAttrs;
        public Attrs Attrs { get; private set; }

        public uint Id => Entity.Id;

        public byte Gem => Entity.Gem;

        public byte Grade => Entity.Grade;

        public uint CfgId => Entity.CfgId;

        public EquipCategory Category => (EquipCategory) Entity.Category;

        public EquipConfig Cfg { get; private set; }

        private const AttrType AttrEquipCfgId = (AttrType) 1000;

        public EquipPlace Place
        {
            get => (EquipPlace) Entity.Place;
            set => Entity.Place = (byte) value;
        }

        public uint Score { get; private set; }

        // 临时数据
        private List<AttrPair> _refinePrevData;
        private List<List<AttrPair>> _refinePrevDataList;
        private Attrs _recastPrevData;

        public Equip(PlayerGrain player, EquipEntity entity)
        {
            _player = player;
            Entity = entity;
            _lastEntity = new EquipEntity();
            _lastEntity.CopyFrom(Entity);

            if (Entity.Category == 0)
                Cfg = ConfigService.Wings[Entity.CfgId];
            else
                Cfg = ConfigService.Equips[Entity.CfgId];

            _baseAttrs = new Attrs();
            _refineAttrs = new List<AttrPair>(4);
            _needAttrs = new Attrs();
            Attrs = new Attrs();
            CalculateAttribute();
            RefreshScore();

            InitRefine();
            InitRecast();
        }

        public async Task Destroy()
        {
            // 更新数据入库
            await SaveData(false);
            _lastEntity = null;
            Entity = null;

            _player = null;
            Cfg = null;

            _baseAttrs?.Dispose();
            _baseAttrs = null;
            _refineAttrs?.Clear();
            _refineAttrs = null;
            _needAttrs?.Dispose();
            _needAttrs = null;
            Attrs?.Dispose();
            Attrs = null;

            _refinePrevData?.Clear();
            _refinePrevData = null;
            CleanupRefinePrevDataList();
            _recastPrevData?.Dispose();
            _recastPrevData = null;
        }

        public async Task SaveData(bool copy = true)
        {
            if (Entity.Equals(_lastEntity)) return;
            var ret = await DbService.UpdateEntity(_lastEntity, Entity);
            if (ret && copy) _lastEntity.CopyFrom(Entity);
        }

        public EquipData BuildPbData()
        {
            var pbData = new EquipData
            {
                Id = Entity.Id,
                Category = (EquipCategory) Entity.Category,
                CfgId = Entity.CfgId,
                StarCount = Entity.StarCount,
                StarExp = Entity.StarExp,
                Gem = Entity.Gem,
                Grade = Entity.Grade,
                Place = (EquipPlace) Entity.Place,
                Score = Score,
                BaseAttrs = {_baseAttrs.ToList()},
                RefinAttrs = {_refineAttrs.ToList()},
                NeedAttrs = {_needAttrs.ToList()}
            };

            return pbData;
        }

        public Task SendInfo()
        {
            return _player.SendPacket(GameCmd.S2CEquipInfo, new S2C_EquipInfo {Data = BuildPbData()});
        }

        public async Task Cache()
        {
            var bytes = Packet.Serialize(BuildPbData());
            await RedisService.SetEquipInfo(Entity.Id, bytes);
        }

        public bool CheckEnable()
        {
            if (Cfg.Category == 0) return true;
            if (Cfg.OwnerRoleId > 0 && Cfg.OwnerRoleId != _player.Entity.CfgId)
            {
                _player.SendNotice("角色不匹配，不能使用");
                return false;
            }

            if (Cfg.Sex != 9 && Cfg.Sex != _player.Entity.Sex ||
                Cfg.Race != 9 && Cfg.Race != _player.Entity.Race)
            {
                _player.SendNotice("角色不匹配，不能使用");
                return false;
            }

            // 检查属性需求
            foreach (var (k, v) in _needAttrs)
            {
                if (_player.Attrs.Get(k) < v)
                {
                    _player.SendNotice("角色属性不足，尚不能使用");
                    return false;
                }
            }
            // 检查转生等级
            if (Cfg.NeedRei > _player.Entity.Relive)
            {
                _player.SendNotice($"装备需要{Cfg.NeedRei}转后才能使用");
                return false;
            }
            // 检查等级
            if (Cfg.NeedRei == _player.Entity.Relive && Cfg.NeedGrade > _player.Entity.Level)
            {
                _player.SendNotice($"装备需要{Cfg.NeedGrade}级后才能使用");
                return false;
            }

            return true;
        }

        // 神兵升级
        public async Task ShenBingUpgrade(uint use)
        {
            var material = _player.EquipMgr.FindEquip(use);
            // 当前装备大于1级，需要消耗材料
            if (Grade >= 2 && material == null)
            {
                _player.SendNotice("大于1级的神兵需要消耗神兵作为升级材料");
                return;
            }

            if (material != null)
            {
                if (material.Category != EquipCategory.Shen)
                {
                    _player.SendNotice("大于1级的神兵需要消耗神兵作为升级材料");
                    return;
                }

                if (material.Place != EquipPlace.Bag)
                {
                    _player.SendNotice("材料不在背包内");
                    return;
                }
            }

            if (!CanUpgrade)
            {
                _player.SendNotice("此装备不可升级");
                return;
            }

            await _player.AddMoney(MoneyType.Silver, -1000, "神兵升级");

            if (IsShenBingUpgradBroke)
            {
                await _player.EquipMgr.DelEquip(Id, true);
                if (material != null)
                {
                    await _player.EquipMgr.DelEquip(use, true);
                }

                // 给神兵碎片
                await _player.AddBagItem(10408, 5, tag: "神兵升级破碎后奖励");
                _player.SendNotice("神兵升级失败");
            }
            else
            {
                var nextId = Cfg.NextId;
                if (nextId == 0)
                {
                    _player.SendNotice("此装备不可升级");
                    return;
                }

                // 检查nextId是否存在
                ConfigService.Equips3.TryGetValue(nextId, out var newCfg);
                if (newCfg == null)
                {
                    _player.SendNotice("此装备不可升级");
                    return;
                }

                Cfg = newCfg;

                // 消耗材料
                if (material != null)
                {
                    await _player.EquipMgr.DelEquip(use, true);
                }

                Entity.CfgId = nextId;
                Entity.Grade++;
                Entity.BaseAttrs = string.Empty;
                Entity.NeedAttrs = string.Empty;
                BuildEntityAttrs(Entity);
                // 刷新属性
                CalculateAttribute();
                RefreshScore();
                await SendInfo();

                // 判断装备能否再穿戴
                if (Place == EquipPlace.Wear)
                {
                    if (CheckEnable())
                    {
                        // 刷新属性方案
                        foreach (var scheme in _player.SchemeMgr.All)
                        {
                            if (!scheme.Equips.Contains(Entity.Id)) continue;
                            await scheme.OnEquipUpdate(Entity.Id);
                        }
                    }
                    else
                    {
                        // 满足不了穿戴条件, 自动脱下
                        foreach (var scheme in _player.SchemeMgr.All)
                        {
                            if (!scheme.Equips.Contains(Entity.Id)) continue;
                            await scheme.OnEquipDelete(Entity.Id);
                        }
                    }
                }
            }
        }

        public async Task XianQiUpgrade(uint use1, uint use2)
        {
            // 检查升6阶仙器需要材料
            uint needItemId = 0;
            if (Cfg.Grade >= 5)
            {
                needItemId = GameDefine.EquipGrade6Items[Cfg.Index];
                if (_player.GetBagItemNum(needItemId) < 1)
                {
                    var itemCfg = ConfigService.Items.GetValueOrDefault(needItemId, null);
                    if (itemCfg != null)
                    {
                        _player.SendNotice($"缺少{itemCfg.Name}");
                    }
                    return;
                }
            }
            var material1 = _player.EquipMgr.FindEquip(use1);
            var material2 = _player.EquipMgr.FindEquip(use2);
            if (material1 == null || material2 == null) {
                _player.SendNotice("材料不足");
                return;
            }

            if (material1.Category != EquipCategory.Xian || material1.Grade != Grade ||
                material2.Category != EquipCategory.Xian || material2.Grade != Grade)
            {
                _player.SendNotice("仙器升级需要同等级的仙器作为材料");
                return;
            }

            if (material1.Place != EquipPlace.Bag || material2.Place != EquipPlace.Bag)
            {
                _player.SendNotice("材料不在背包内");
                return;
            }

            if (!CanUpgrade)
            {
                _player.SendNotice("此装备不可升级");
                return;
            }

            var nextId = Cfg.NextId;
            if (nextId == 0)
            {
                _player.SendNotice("此装备不可升级");
                return;
            }

            // 检查nextId是否存在
            ConfigService.Equips4.TryGetValue(nextId, out var nextCfg);
            if (nextCfg == null)
            {
                _player.SendNotice("此装备不可升级");
                return;
            }

            // 消耗6阶仙器材料
            if (needItemId > 0)
            {
                // 升阶6阶仙器，花费2kw仙玉
                var ret = await _player.CostMoney(MoneyType.Jade, 20000000, tag: "仙器升级");
                if (!ret) return;
                ret = await _player.AddBagItem(needItemId, -1, tag: "升6阶仙器");
                if (!ret) return;
            } else //6阶一下仙器
            {
                // 花费1kw银两
                var ret = await _player.CostMoney(MoneyType.Silver, 10000000, tag: "仙器升级");
                if (!ret) return;
            }
            Cfg = nextCfg;

            // 消耗材料
            await _player.EquipMgr.DelEquip(use1, true);
            await _player.EquipMgr.DelEquip(use2, true);

            // 更换cfgId和grade, 刷新baseAttr和needAttr
            Entity.CfgId = Cfg.Id;
            Entity.Grade = Cfg.Grade;
            Entity.BaseAttrs = string.Empty;
            Entity.NeedAttrs = string.Empty;
            BuildEntityAttrs(Entity);
            // 清理重铸属性
            _recastPrevData?.Clear();
            _recastPrevData = null;
            SyncRecast();

            // 刷新属性
            CalculateAttribute();
            RefreshScore();
            await SendInfo();

            // 判断装备能否再穿戴
            if (Place == EquipPlace.Wear)
            {
                if (CheckEnable())
                {
                    // 刷新属性方案
                    foreach (var scheme in _player.SchemeMgr.All)
                    {
                        if (!scheme.Equips.Contains(Entity.Id)) continue;
                        await scheme.OnEquipUpdate(Entity.Id);
                    }
                }
                else
                {
                    // 满足不了穿戴条件, 自动脱下
                    foreach (var scheme in _player.SchemeMgr.All)
                    {
                        if (!scheme.Equips.Contains(Entity.Id)) continue;
                        await scheme.OnEquipDelete(Entity.Id);
                    }
                }
            }
        }

        // 镶嵌
        public async Task Inlay(bool add)
        {
            if (add)
            {
                if (Gem >= Cfg.MaxEmbedGemCnt) return;
                var itemId = GetInlayItemId();
                // 扣除道具数量
                var ret = await _player.AddBagItem(itemId, -1, tag: "镶嵌");
                if (!ret) return;
                // 镶嵌等级加1
                Entity.Gem += 1;
            }
            else
            {
                // 拆卸
                await UnInlay();
            }

            // 刷新属性 下发信息
            CalculateAttribute();
            RefreshScore();
            await SendInfo();

            // 刷新属性方案
            foreach (var scheme in _player.SchemeMgr.All)
            {
                if (!scheme.Equips.Contains(Entity.Id)) continue;
                await scheme.OnEquipUpdate(Entity.Id);
            }
        }

        // 拆卸
        public async Task UnInlay()
        {
            var gemDic = GetGemList();
            foreach (var (k, v) in gemDic)
            {
                if (v <= 0) continue;
                await _player.AddBagItem(k, (int) v, tag: "拆卸");
            }

            Entity.Gem = 0;
        }

        public async ValueTask<bool> Refine(List<AttrPair> attrs)
        {
            // 替换炼化属性
            _refineAttrs.Clear();
            _refineAttrs.AddRange(attrs);
            Entity.RefineAttrs = FormatAttrPairs(attrs);

            CalculateAttribute();
            RefreshScore();
            await SendInfo();

            // 刷新属性方案
            foreach (var scheme in _player.SchemeMgr.All)
            {
                if (!scheme.Equips.Contains(Entity.Id)) continue;
                await scheme.OnEquipUpdate(Entity.Id);
            }

            _player.LogDebug("后台修炼");
            return true;
        }

        /// <summary>
        /// 炼化装备        todo: 装备炼化可以锁定某个属性（道具物品为 炼化锁）
        /// </summary>
        public async Task Refine(uint level, bool confirm)
        {
            if (confirm)
            {
                // 确认
                if (_refinePrevData == null)
                {
                    _player.SendNotice("请先预览");
                    return;
                }

                // 替换炼化属性
                _refineAttrs.Clear();
                _refineAttrs.AddRange(_refinePrevData);
                Entity.RefineAttrs = FormatAttrPairs(_refineAttrs);

                // 同步炼化预览数据
                _refinePrevData.Clear();
                _refinePrevData = null;
                SyncRefine();

                CalculateAttribute();
                RefreshScore();
                await SendInfo();

                // 刷新属性方案
                foreach (var scheme in _player.SchemeMgr.All)
                {
                    if (!scheme.Equips.Contains(Entity.Id)) continue;
                    await scheme.OnEquipUpdate(Entity.Id);
                }

                _player.SendNotice("炼化成功");
            }
            else
            {
                // 扣除材料 九彩云龙珠
                uint itemId = 10402;
                if (level == 1) itemId = 10403;
                else if (level == 2) itemId = 10404;
                var ret = await _player.AddBagItem(itemId, -1, tag: "装备炼化");
                if (!ret) return;

                // 随机炼化结果
                _refinePrevData = GetEquipRefineAttrs((uint) Cfg.Index, level);
                if (_refinePrevData == null || _refinePrevData.Count == 0)
                {
                    _player.SendNotice("炼化预览失败");
                    _refinePrevData = null;
                    // 返还材料
                    await _player.AddBagItem(itemId, 1);
                    return;
                }

                SyncRefine();

                // 下发预览数据数据
                var resp = new S2C_EquipProperty
                {
                    Id = Entity.Id,
                    Flag = 1,
                    List = {_refinePrevData},
                    Score = CalcEquipScore(_baseAttrs, _refinePrevData, Entity.Gem)
                };
                await _player.SendPacket(GameCmd.S2CEquipProperty, resp);
            }
        }

        /// <summary>
        /// 炼化装备（多次）
        /// </summary>
        public async Task RefineTimes(uint level, bool confirm, uint times, uint choiceIndex)
        {
            if (confirm)
            {
                // 确认
                if (_refinePrevDataList == null)
                {
                    _player.SendNotice("请先预览");
                    return;
                }
                if (choiceIndex >= _refinePrevDataList.Count)
                {
                    _player.SendNotice("错误参数");
                    return;
                }

                // 替换炼化属性
                _refineAttrs.Clear();
                _refineAttrs.AddRange(_refinePrevDataList[(int)choiceIndex]);
                Entity.RefineAttrs = FormatAttrPairs(_refineAttrs);

                // 同步炼化预览数据
                CleanupRefinePrevDataList();
                SyncRefine();

                CalculateAttribute();
                RefreshScore();
                await SendInfo();

                // 刷新属性方案
                foreach (var scheme in _player.SchemeMgr.All)
                {
                    if (!scheme.Equips.Contains(Entity.Id)) continue;
                    await scheme.OnEquipUpdate(Entity.Id);
                }

                _player.SendNotice("替换成功");
            }
            else
            {
                times = Math.Max(1, Math.Min(times, 5));
                // 扣除材料 九彩云龙珠
                uint itemId = 10402;
                if (level == 1) itemId = 10403;
                else if (level == 2) itemId = 10404;
                if (_player.GetBagItemNum(itemId) < times)
                {
                    _player.SendNotice("材料不足");
                    return;
                }
                var ret = await _player.AddBagItem(itemId, -(int)times, tag: "装备炼化");
                if (!ret)
                {
                    _player.SendNotice("消耗材料失败");
                    return;
                }
                CleanupRefinePrevDataList();
                _refinePrevDataList = new List<List<AttrPair>>();
                var resp = new S2C_EquipPropertyList
                {
                    Id = Entity.Id,
                    Flag = 1
                };
                // 随机炼化结果
                for (int i = 0; i < times; i++)
                {
                    var property = GetEquipRefineAttrs((uint)Cfg.Index, level);
                    if (property == null || property.Count == 0)
                    {
                        _player.SendNotice("炼化预览失败");
                        CleanupRefinePrevDataList();
                        // 返还材料
                        await _player.AddBagItem(itemId, (int)times);
                        return;
                    }
                    _refinePrevDataList.Add(property);
                    resp.List.Add(new EquipProperty()
                    {
                        List = { property },
                        Score = CalcEquipScore(_baseAttrs, property, Entity.Gem)
                    });
                }

                SyncRefine();
                // 下发预览数据数据
                await _player.SendPacket(GameCmd.S2CEquipPropertyList, resp);
            }
        }
        private void CleanupRefinePrevDataList()
        {
            if (_refinePrevDataList != null)
            {
                foreach (var item in _refinePrevDataList)
                {
                    item.Clear();
                }
                _refinePrevDataList.Clear();
                _refinePrevDataList = null;
            }
        }

        /// <summary>
        /// 定制
        /// </summary>
        public async Task DingZhi(List<int> attrs)
        {
            if (Category != EquipCategory.Xian && Category != 0 && (CfgId < 5001 || CfgId > 5024))
            {
                _player.SendNotice("你应该选择仙器或翅膀");
                return;
            }
            ConfigService.DingZhiNeedItems.TryGetValue(Cfg.Index, out var itemCfgId);
            if (_player.GetBagItemNum(itemCfgId) < 1)
            {
                ConfigService.Items.TryGetValue(itemCfgId, out var icfg);
                if (icfg != null)
                {
                    _player.SendNotice("缺少" + icfg.Name);
                }
                else
                {
                    _player.SendNotice("缺少定制券");
                }
                return;
            }
#if false
            if (attrs.Count <= 0) {
                _player.SendNotice("至少选择1条属性");
                return;
            }
            if (attrs.Count > 5)
            {
                _player.SendNotice("最多选择5条属性");
                return;
            }
#else
            if (attrs.Count != 5)
            {
                _player.SendNotice("必须选择5条属性");
                return;
            }
#endif
            if (!ConfigService.DingZhiAttrConfig.ContainsKey(Cfg.Index))
            {
                _player.SendNotice("装备配置错误");
                return;
            }
            var dconfig = ConfigService.DingZhiAttrConfig.GetValueOrDefault(Cfg.Index);
            // 确定炼化属性
            List<AttrPair> dingzhiData = new();
            foreach (var key in attrs)
            {
                AttrType a = (AttrType)key;
                if (!dconfig.ContainsKey(a))
                {
                    _player.SendNotice(String.Format("未配置的属性ID:{0}", key));
                    return;
                }
                dconfig.TryGetValue(a, out var v);
                dingzhiData.Add(new() { Key = a, Value = v });
            }
            // 减少一个定制券
            await _player.AddItem(itemCfgId, -1, true, String.Format("定制装备id={0}", Id));
            // 替换炼化属性
            _refineAttrs.Clear();
            _refineAttrs.AddRange(dingzhiData);
            Entity.RefineAttrs = FormatAttrPairs(_refineAttrs);

            // 同步炼化数据
            _refinePrevData = null;
            SyncRefine();

            CalculateAttribute();
            RefreshScore();
            await SendInfo();

            // 刷新属性方案
            foreach (var scheme in _player.SchemeMgr.All)
            {
                if (!scheme.Equips.Contains(Entity.Id)) continue;
                await scheme.OnEquipUpdate(Entity.Id);
            }

            _player.SendNotice("定制成功");
        }

        /// <summary>
        /// 升星
        /// </summary>
        public async Task StarUpgrade(uint itemId)
        {
            if (Entity.StarCount >= ConfigService.JingLianList.Count - 1)
            {
                _player.SendNotice("装备已经满星，无需升级");
                return;
            }
            if (CfgId >= 5001 && CfgId <= 5024)
            {
                _player.SendNotice("你应该选择非翅膀的装备");
                return;
            }
            // 灵气果
            if (itemId < 500047 || itemId > 500050)
            {
                _player.SendNotice("装备升星必须使用灵气果获得灵气");
                return;
            }
            var itemCfg = ConfigService.Items.GetValueOrDefault(itemId, null);
            if (_player.GetBagItemNum(itemId) < 1)
            {
                if (itemCfg != null)
                {
                    _player.SendNotice("缺少" + itemCfg.Name);
                }
                else
                {
                    _player.SendNotice("缺少灵气果");
                }
                return;
            }
            // 当前星级
            var jingLianConfig = ConfigService.JingLianList.GetValueOrDefault(Entity.StarCount, null);
            if (jingLianConfig == null)
            {
                _player.SendNotice("内部错误，请稍候再试");
                return;
            }
            if (jingLianConfig.exp == 0)
            {
                _player.SendNotice("请进阶");
                return;
            }

            // 减少一个灵气果
            await _player.AddItem(itemId, -1, true, String.Format("装备升星id={0}", Id));

            var oldStar = Entity.StarCount;
            // 计算星级
            Entity.StarExp += (uint)itemCfg.Num;
            while (jingLianConfig.exp > 0 && Entity.StarExp >= jingLianConfig.exp)
            {
                Entity.StarCount += 1;
                Entity.StarExp -= jingLianConfig.exp;
                jingLianConfig = ConfigService.JingLianList.GetValueOrDefault(Entity.StarCount, null);
            }
            // 星级变化则需要刷新属性和评分
            if (oldStar != Entity.StarCount)
            {
                CalculateAttribute();
                RefreshScore();
                // 刷新属性方案
                foreach (var scheme in _player.SchemeMgr.All)
                {
                    if (!scheme.Equips.Contains(Entity.Id)) continue;
                    await scheme.OnEquipUpdate(Entity.Id);
                }
            }
            // 发送新的装备属性
            await SendInfo();
        }

        /// <summary>
        /// 升阶
        /// </summary>
        public async Task GradeUpgrade()
        {
            // 当前星级
            var jingLianConfig = ConfigService.JingLianList.GetValueOrDefault(Entity.StarCount, null);
            if (jingLianConfig == null)
            {
                _player.SendNotice("内部错误，请稍候再试");
                return;
            }
            if (jingLianConfig.exp != 0)
            {
                _player.SendNotice("请先满星");
                return;
            }
            // 检查消耗物品数量
            if (_player.GetBagItemNum(500052) <= 0)
            {
                _player.SendNotice($"缺少材料{ConfigService.Items[500052].Name}");
                return;
            }

            // 减少一个灵气果
            await _player.AddItem(500052, -1, true, String.Format("装备金蟾升阶id={0}", Id));
            // 概率升阶
            if (_player.Random.Next(10000) <= GameDefine.JingLianGradeRate.GetValueOrDefault(jingLianConfig.grade))
            {
                // 升一阶
                Entity.StarCount += 1;

                CalculateAttribute();
                RefreshScore();
                // 刷新属性方案
                foreach (var scheme in _player.SchemeMgr.All)
                {
                    if (!scheme.Equips.Contains(Entity.Id)) continue;
                    await scheme.OnEquipUpdate(Entity.Id);
                }

                // 发送新的装备属性
                await SendInfo();
                // jingLianConfig = ConfigService.JingLianList.GetValueOrDefault(Entity.StarCount, null);
                // if (jingLianConfig != null)
                // {
                //     _player.SendNotice($"恭喜，{Cfg.Name}进阶{jingLianConfig.txt1}金蟾");
                // }
            }
            else
            {
                _player.SendNotice("很遗憾，金蟾进阶失败！");
            }
        }

        /// <summary>
        /// 重铸, 刷初始属性，继承宝石和炼化属性
        /// </summary>
        public async Task Recast(bool confirm)
        {
            if (Category != EquipCategory.High && Category != EquipCategory.Xian)
            {
                _player.SendNotice("新手装备和神兵不可重铸");
                return;
            }

            if (confirm)
            {
                if (_recastPrevData == null || _recastPrevData.Count == 0)
                {
                    _player.SendNotice("请先预览");
                    return;
                }

                // 重铸是有可能变更cfgId的
                Entity.CfgId = (uint) _recastPrevData.Get(AttrEquipCfgId);
                _recastPrevData.Remove(AttrEquipCfgId);
                Cfg = ConfigService.Equips[Entity.CfgId];
#if true
                // 不是通用装备，并种族不匹配如果再在身上则卸下
                if (Place == EquipPlace.Wear && Cfg.Race != 9 && Cfg.Race != (byte)_player.Entity.Race)
                {
                    _player.SendNotice("种族与装备不匹配，已自动放回背包！");
                    if (_player.SchemeMgr.Scheme.Equips.Contains(Entity.Id))
                    {
                        await _player.SchemeMgr.Scheme.SetEquip(0, Cfg.Index);
                    }
                    else
                    {
                        Place = EquipPlace.Bag;
                        await _player.FreshAllSchemeAttrs();
                    }
                }
#endif
                RefreshNeedAttrs(); //记得重新计算需求属性
                Entity.BaseAttrs = _recastPrevData.ToJson();
                _recastPrevData = null;
                SyncRecast();

                CalculateAttribute();
                RefreshScore();
                await SendInfo();

                // 刷新属性方案
                foreach (var scheme in _player.SchemeMgr.All)
                {
                    if (!scheme.Equips.Contains(Entity.Id)) continue;
                    await scheme.OnEquipUpdate(Entity.Id);
                }

                _player.SendNotice("重铸成功");
            }
            else
            {
                uint itemId = 10405; //盘古精铁
                if (Category == EquipCategory.Xian) itemId = 10401; //悔梦石
                if (_player.GetBagItemNum(itemId) == 0)
                {
                    _player.SendNotice("缺少重铸材料");
                    return;
                }

                if (!_player.CheckMoney(MoneyType.Silver, GameDefine.EquipRecastCostSilver)) return;

                _recastPrevData = GetEquipRecastData();
                if (_recastPrevData == null || _recastPrevData.Count == 0)
                {
                    _player.SendNotice("重铸预览失败");
                    return;
                }

                // 扣除道具
                var ret = await _player.AddBagItem(itemId, -1);
                if (!ret)
                {
                    _recastPrevData = null;
                    return;
                }

                // 扣除银币
                await _player.AddMoney(MoneyType.Silver, -GameDefine.EquipRecastCostSilver, "重铸装备消耗");

                SyncRecast();

                // 下发预览数据数据
                var resp = new S2C_EquipProperty
                {
                    Id = Entity.Id,
                    Flag = 2,
                    List = {_recastPrevData.ToList()},
                    Score = CalcEquipScore(_recastPrevData, _refineAttrs, Entity.Gem)
                };
                await _player.SendPacket(GameCmd.S2CEquipProperty, resp);
            }
        }

        public async Task GetPreviewData(uint flag)
        {
            if (flag == 1)
            {
                // 炼化预览数据
                if (_refinePrevData is {Count: > 0})
                {
                    var resp = new S2C_EquipProperty
                    {
                        Id = Entity.Id,
                        Flag = 1,
                        List = {_refinePrevData},
                        Score = CalcEquipScore(_baseAttrs, _refinePrevData, Entity.Gem)
                    };
                    await _player.SendPacket(GameCmd.S2CEquipProperty, resp);
                }
            }
            else if (flag == 2)
            {
                // 重铸预览数据
                if (_recastPrevData is {Count: > 0})
                {
                    var resp = new S2C_EquipProperty
                    {
                        Id = Entity.Id,
                        Flag = 2,
                        List = {_recastPrevData.ToList()},
                        Score = CalcEquipScore(_recastPrevData, _refineAttrs, Entity.Gem)
                    };
                    await _player.SendPacket(GameCmd.S2CEquipProperty, resp);
                }
            }
        }
        public async Task GetPreviewDataList(uint flag)
        {
            if (flag == 1)
            {
                var resp = new S2C_EquipPropertyList
                {
                    Id = Entity.Id,
                    Flag = 1
                };
                // 炼化预览数据
                if (_refinePrevDataList is { Count: > 0 })
                {
                    foreach (var p in _refinePrevDataList)
                    {
                        resp.List.Add(new EquipProperty()
                        {
                            List = { p },
                            Score = CalcEquipScore(_baseAttrs, p, Entity.Gem)
                        });
                    }
                }
                await _player.SendPacket(GameCmd.S2CEquipPropertyList, resp);
            }
        }

        /// <summary>
        /// 被上架到摆摊
        /// </summary>
        public async ValueTask<bool> SetOnMall()
        {
            if (Place != EquipPlace.Bag) return false;
            // 移除缓存
            _player.EquipMgr.Equips.Remove(Entity.Id);
            // 立即保存入库, 标记拥有者为0
            Entity.RoleId = 0;
            // 通知前端
            await _player.SendPacket(GameCmd.S2CEquipDel, new S2C_EquipDel {Id = Entity.Id});
            await Destroy();
            return true;
        }

        private bool CanUpgrade
        {
            get
            {
                switch (Category)
                {
                    case EquipCategory.Shen:
                    case EquipCategory.Xian:
                        return Cfg.NextId > 0;
                }

                return false;
            }
        }

        /// <summary>
        /// 检测是否升级破碎
        /// </summary>
        private bool IsShenBingUpgradBroke
        {
            get
            {
                if (Entity.Grade > GameDefine.Equip3BrokeProbs.Length) return false;
                var r = _player.Random.Next(0, 100);
                return r < GameDefine.Equip3BrokeProbs[Entity.Grade - 1];
            }
        }

        /// <summary>
        /// 获取镶嵌的所有宝石id及其数量
        /// </summary>
        public Dictionary<uint, uint> GetGemList()
        {
            var dic = new Dictionary<uint, uint>();
            if (Entity.Gem > 21) Entity.Gem = 21;
            // 每一级都是3颗宝石
            var gemLevel = (int) MathF.Floor(Entity.Gem / 3f);
            for (var i = 0; i < gemLevel; i++)
            {
                dic[GameDefine.EquipGems[Cfg.Index - 1][i]] = 3;
            }

            if (Entity.Gem % 3 != 0)
                dic[GameDefine.EquipGems[Cfg.Index - 1][gemLevel]] = (uint) (Entity.Gem % 3);

            return dic;
        }

        private uint GetInlayItemId()
        {
            var gemLevel = (int) MathF.Floor(Entity.Gem / 3f);
            var index1 = Math.Min(Cfg.Index - 1, GameDefine.EquipGems.Count() - 1);
            var index2 = Math.Min(gemLevel, GameDefine.EquipGems[index1].Count() - 1);
            return GameDefine.EquipGems[index1][index2];
        }

        private void CalculateAttribute()
        {
            _baseAttrs.FromJson(Entity.BaseAttrs);
            _refineAttrs = ParseAttrPairs(Entity.RefineAttrs);
            _needAttrs.FromJson(Entity.NeedAttrs);

            var jingLianConfig = ConfigService.JingLianList.GetValueOrDefault(Entity.StarCount, null);

            // 从基础属性和炼化属性中统计Attr
            Attrs.Clear();
            foreach (var (k, v) in _baseAttrs)
            {
                var value = v;
                if (!GameDefine.EquipNumericalAttrType.ContainsKey(k))
                {
                    value = v / 10;
                }
                var o = value;
                // 镶嵌一颗宝石增加3%的基础属性
                value *= 1 + 0.03f * Entity.Gem;
                // 升星加成
                if (jingLianConfig != null) {
                    value += o * jingLianConfig.baseAddition / 100f;
                }
                Attrs.Add(k, value);
            }

            // 炼化属性
            foreach (var pair in _refineAttrs)
            {
                var value = pair.Value;
                if (!GameDefine.EquipNumericalAttrType.ContainsKey(pair.Key))
                {
                    value = pair.Value / 10;
                }
                // 升星加成
                if (jingLianConfig != null) {
                    value *= 1 + (jingLianConfig.refineAddition / 100f);
                }
                Attrs.Add(pair.Key, value);
            }
        }

        public Attrs GetEquipRecastData()
        {
            var list = new List<EquipConfig>();
            var cfgs = Category == EquipCategory.High ? ConfigService.Equips2 : ConfigService.Equips4;
            foreach (var v in cfgs.Values)
            {
                if (v.OwnerRoleId > 0 && v.OwnerRoleId != _player.Entity.CfgId) continue;
                if ((v.Race == 9 || v.Race == _player.Entity.Race) &&
                    (v.Sex == 9 || v.Sex == _player.Entity.Sex) &&
                    (v.Index == Cfg.Index || Cfg.Index == 0) && v.Id != Entity.CfgId)
                {
                    // 重铸同等级的装备
                    if (v.Grade != 0 && v.Grade != Entity.Grade) continue;
                    list.Add(v);
                }
            }

            if (list.Count == 0) return null;
            // 随机一个
            var rnd = new Random();
            var cfg = list[rnd.Next(0, list.Count)];
            // 计算基础属性
            var entity = new EquipEntity
            {
                Category = cfg.Category,
                CfgId = cfg.Id,
                Gem = 0,
                Grade = cfg.Grade,
                BaseAttrs = "",
                RefineAttrs = "",
                NeedAttrs = ""
            };
            BuildEntityAttrs(entity, false);

            var attrs = new Attrs(entity.BaseAttrs);
            // 记录一下cfgId, 记得用1000为key来取
            attrs.Set(AttrEquipCfgId, cfg.Id);
            return attrs;
        }

        private void RefreshScore()
        {
            Score = CalcEquipScore(_baseAttrs, _refineAttrs, Entity.Gem);
        }

        private void InitRefine()
        {
            _refinePrevData = null;
            if (!string.IsNullOrWhiteSpace(Entity.Refine))
            {
                _refinePrevData = new List<AttrPair>();

                var list = Json.Deserialize<List<string>>(Entity.Refine);
                foreach (var line in list)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var arr = line.Split('_');
                    if (arr.Length != 2) continue;
                    byte.TryParse(arr[0], out var key);
                    float.TryParse(arr[1], out var value);
                    _refinePrevData.Add(new AttrPair {Key = (AttrType) key, Value = value});
                }

                if (_refinePrevData.Count == 0) _refinePrevData = null;
            }
            _refinePrevDataList = null;
            if (!string.IsNullOrWhiteSpace(Entity.RefineList))
            {
                _refinePrevDataList = new List<List<AttrPair>>();

                var list = Json.Deserialize<List<List<string>>>(Entity.RefineList);
                foreach (var one in list)
                {
                    var a = new List<AttrPair>();
                    foreach (var line in one)
                    {
                        if (string.IsNullOrWhiteSpace(line)) continue;
                        var arr = line.Split('_');
                        if (arr.Length != 2) continue;
                        byte.TryParse(arr[0], out var key);
                        float.TryParse(arr[1], out var value);
                        a.Add(new AttrPair { Key = (AttrType)key, Value = value });
                    }
                    _refinePrevDataList.Add(a);
                    if (_refinePrevDataList.Count >= 5)
                    {
                        break;
                    }
                }

                if (_refinePrevDataList.Count == 0)
                {
                    CleanupRefinePrevDataList();
                }
            }
        }

        private void SyncRefine()
        {
            if (_refinePrevData == null || _refinePrevData.Count == 0)
            {
                Entity.Refine = string.Empty;
            }
            else
            {
                var list = new List<string>();
                foreach (var pair in _refinePrevData)
                {
                    if (pair.Key != AttrType.Unkown && pair.Value > 0)
                    {
                        list.Add($"{(byte) pair.Key}_{pair.Value}");
                    }
                }

                if (list.Count == 0)
                {
                    Entity.Refine = string.Empty;
                }
                else
                {
                    Entity.Refine = Json.SafeSerialize(list);
                }
            }
            if (_refinePrevDataList == null || _refinePrevDataList.Count == 0)
            {
                Entity.RefineList = string.Empty;
            }
            else
            {
                var list = new List<List<string>>();
                foreach (var one in _refinePrevDataList)
                {
                    var a = new List<string>();
                    foreach (var pair in one)
                    {
                        if (pair.Key != AttrType.Unkown && pair.Value > 0)
                        {
                            a.Add($"{(byte)pair.Key}_{pair.Value}");
                        }
                        list.Add(a);
                    }
                    if (list.Count >= 5) {
                        break;
                    }
                }
                if (list.Count == 0)
                {
                    Entity.RefineList = string.Empty;
                    CleanupRefinePrevDataList();
                }
                else
                {
                    Entity.RefineList = Json.SafeSerialize(list);
                }
            }
        }

        private void InitRecast()
        {
            _recastPrevData = null;
            if (!string.IsNullOrWhiteSpace(Entity.Recast))
            {
                _recastPrevData = new Attrs(Entity.Recast);
                if (_recastPrevData.Count == 0) _recastPrevData = null;
            }
        }

        private void SyncRecast()
        {
            if (_recastPrevData == null)
            {
                Entity.Recast = string.Empty;
            }
            else
            {
                Entity.Recast = _recastPrevData.ToJson();
            }
        }

        private void RefreshNeedAttrs()
        {
            if (_needAttrs == null || Cfg == null) return;
            _needAttrs.Clear();
            _needAttrs = new Attrs();
            if (Cfg.NeedAttr.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in Cfg.NeedAttr.EnumerateObject())
                {
                    GameDefine.EquipAttrTypeMap.TryGetValue(property.Name, out var attrType);
                    if (attrType != AttrType.Unkown)
                        _needAttrs.Set(attrType, property.Value.GetSingle());
                }
            }

            Entity.NeedAttrs = _needAttrs.ToJson();
        }

        /// <summary>
        /// 构建Entity的基础属性和需求属性
        /// </summary>
        public static void BuildEntityAttrs(EquipEntity entity, bool includeNeedAttrs = true)
        {
            // TODO: 特殊处理翅膀
            EquipConfig cfg = null;
            if (entity.Category == 0)
            {
                ConfigService.Wings.TryGetValue(entity.CfgId, out var cfg_wing);
                cfg = cfg_wing;
            }
            if (cfg == null)
            {
                ConfigService.Equips.TryGetValue(entity.CfgId, out var cfg_equip);
                cfg = cfg_equip;
            }
            if (cfg == null)
            {
                return;
            }

            var rnd = new Random();
            // 统计BaseAttrs
            {
                Attrs attrs = null;

                if (cfg.Category == (byte) EquipCategory.High)
                {
                    // 高级装备去属性表里取
                    if (cfg.AttrFactor.HasValue && cfg.AttrLib is {Length: > 0})
                    {
                        attrs = GetHighEquipBaseAttr(cfg.AttrLib, cfg.AttrFactor.Value, rnd);
                    }
                }
                else if (cfg.BaseAttr.HasValue)
                {
                    attrs = new Attrs();
                    foreach (var property in cfg.BaseAttr.Value.EnumerateObject())
                    {
                        GameDefine.EquipAttrTypeMap.TryGetValue(property.Name, out var attrType);
                        if (attrType != AttrType.Unkown)
                            attrs.Set(attrType, property.Value.GetSingle());
                    }
                }

                if (attrs == null)
                {
                    entity.BaseAttrs = string.Empty;
                }
                else
                {
                    entity.BaseAttrs = attrs.ToJson();
                    attrs.Dispose();
                }
            }

            // 属性需求
            if (includeNeedAttrs)
            {
                var attrs = new Attrs();

                if (cfg.NeedAttr.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in cfg.NeedAttr.EnumerateObject())
                    {
                        GameDefine.EquipAttrTypeMap.TryGetValue(property.Name, out var attrType);
                        if (attrType != AttrType.Unkown)
                            attrs.Set(attrType, property.Value.GetSingle());
                    }
                }

                entity.NeedAttrs = attrs.ToJson();
                attrs.Dispose();
            }
        }

        /// <summary>
        /// 高级装备通过AttrLib和AttrFactor来获取基础属性
        /// </summary>
        private static Attrs GetHighEquipBaseAttr(IEnumerable<string> attrLib, float attrFactor, Random rnd)
        {
            var attrs = new Attrs();
            foreach (var lib in attrLib)
            {
                ConfigService.EquipAttrs.TryGetValue(lib, out var cfg);
                if (cfg?.BaseAttr == null || cfg.BaseAttr.Length == 0) continue;
                var item = cfg.BaseAttr.Length == 1
                    ? cfg.BaseAttr[0]
                    : cfg.BaseAttr[rnd.Next(0, cfg.BaseAttr.Length)];
                var range = CalcRange(cfg.RndRange, rnd);
                var value = item.Min + MathF.Floor(range * (item.Max - item.Min) / 100f);

                attrs.Set(GameDefine.EquipAttrTypeMap[item.Key], MathF.Floor(value * attrFactor));
            }

            return attrs;
        }

        private static int CalcRange(IReadOnlyCollection<EquipAttrLibRangeItem> item, Random rnd)
        {
            if (item == null || item.Count == 0) return 1;
            var rndValue = rnd.Next(1, 101);

            var minValue = 1; //当前概率的最小值
            var maxValue = 100; //当前概率的最大值
            var startValue = 0; //阶梯概率初始位置
            foreach (var ri in item)
            {
                if (rndValue > startValue && rndValue <= startValue + ri.Rate)
                {
                    minValue = ri.Min;
                    maxValue = ri.Max;
                    break;
                }

                startValue += ri.Rate;
            }

            return rnd.Next(minValue, maxValue + 1);
        }

        public static List<AttrPair> GetEquipRefineAttrs(uint pos, uint level)
        {
            var list = new List<AttrPair>();

            ConfigService.EquipRefins.TryGetValue(pos, out var cfgList);
            if (cfgList == null || cfgList.Count == 0) return list;

            var rnd = new Random();

            var cnt = 0; //随机[1,5]
            if (level == 0) cnt = rnd.Next(1, 6);
            else if (level == 1) cnt = rnd.Next(2, 6);
            else if (level == 2) cnt = rnd.Next(3, 6);

            var factor = 0f;
            if (level == 1) factor = 0.3f;
            else if (level == 2) factor = 0.5f;

            // 已经出现强力克的数量
            var qlkNum = 0;
            bool twoQlk; //是否允许出现2条强力克
            bool threeSameQlk; //是否允许出现3条相同强力克
            var attrKeys = new Dictionary<AttrType, uint>();
            if (pos == 1)
            {
                twoQlk = rnd.Next(0, 100) < 5;
                if (!twoQlk) threeSameQlk = false;
                else threeSameQlk = rnd.Next(0, 1000) < 5;
            }
            else
            {
                twoQlk = rnd.Next(0, 100) < 1;
                threeSameQlk = false;
            }

            while (cnt > 0)
            {
                var cfg = cfgList[rnd.Next(0, cfgList.Count)];
                if (!GameDefine.EquipAttrTypeMap.TryGetValue(cfg.Attr, out var attrType))
                {
                    // 配置出错
                    break;
                }

                var str = pos switch
                {
                    1 => cfg.Pos1,
                    2 => cfg.Pos2,
                    3 => cfg.Pos3,
                    4 => cfg.Pos4,
                    5 => cfg.Pos5,
                    _ => ""
                };
                if (string.IsNullOrWhiteSpace(str))
                {
                    // 位置错误
                    break;
                }

                // 武器最多出现3条强力克，出现2条强力克的概率是5%，出现3条相同强力克的概率是0.5%
                // 其他最多出现2条强力克，出现2条相同强力克的概率是1%
                if (attrType == AttrType.Qjin || attrType == AttrType.Qmu || attrType == AttrType.Qshui ||
                    attrType == AttrType.Qhuo || attrType == AttrType.Qtu)
                {
                    if (pos == 1)
                    {
                        // 强力克属性最多出现3条
                        if (qlkNum >= 3) continue;
                        if (qlkNum == 1 && !twoQlk) continue;
                        var c = list.Count(p => p.Key == attrType);
                        if (c == 2 && !threeSameQlk) continue;
                    }
                    else
                    {
                        // 强力克属性最多出现2条
                        if (qlkNum >= 2) continue;
                        if (qlkNum == 1 && !twoQlk) continue;
                    }

                    qlkNum++;
                } else {
                    // 不允许3条以上相同属性
                    uint value = attrKeys.GetValueOrDefault(attrType, (uint)0);
                    if (value >= 3) {
                        continue;
                    }
                    attrKeys[attrType] = value + 1;
                }

                var arr = str.Split(",");
                int.TryParse(arr[0], out var min);
                int.TryParse(arr[1], out var max);
                var deltaValue = max - min;
                var addValue = (float) Math.Floor(rnd.NextDouble() * deltaValue * 0.5f + deltaValue * factor);
                list.Add(new AttrPair {Key = attrType, Value = min + addValue});

                cnt--;
            }

            // 排序
            list.Sort((a, b) => (int) a.Key - (int) b.Key);

            return list;
        }

        public static uint CalcEquipScore(Attrs baseAttrs, List<AttrPair> refineAttrs, uint gem)
        {
            var attrs = new Attrs();
            foreach (var pair in refineAttrs)
            {
                attrs.Add(pair.Key, pair.Value);
            }

            return CalcEquipScore(baseAttrs, attrs, gem);
        }

        public static uint CalcEquipScore(Attrs baseAttrs, Attrs refineAttrs, uint gem)
        {
            // 基础分
            var dic = new Dictionary<AttrType, float>();
            foreach (var (k, v) in baseAttrs)
            {
                if (dic.ContainsKey(k))
                    dic[k] += v;
                else
                    dic[k] = v;
            }

            // 镶嵌加分
            if (gem > 0)
            {
                var scale = 1 + gem * 0.03f;
                foreach (var k in dic.Keys.ToList())
                {
                    dic[k] *= scale;
                }
            }

            // 炼化属性加分
            foreach (var (k, v) in refineAttrs)
            {
                if (dic.ContainsKey(k))
                    dic[k] += v;
                else
                    dic[k] = v;
            }

            // 计算得分
            var total = 0f;
            // 忽略cfgId的key
            dic.Remove(AttrEquipCfgId);
            foreach (var (k, v) in dic)
            {
                var scale = GameDefine.AttrTypeCalcScoreScale.GetValueOrDefault(k, 1f);
                total += MathF.Ceiling(MathF.Abs(v * scale));
            }

            var score = (uint) MathF.Ceiling(total);
            return score;
        }

        private static List<AttrPair> ParseAttrPairs(string text)
        {
            var resList = new List<AttrPair>();
            if (!string.IsNullOrWhiteSpace(text))
            {
                var list = Json.Deserialize<List<string>>(text);
                foreach (var line in list)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var arr = line.Split('_');
                    if (arr.Length != 2) continue;
                    byte.TryParse(arr[0], out var key);
                    float.TryParse(arr[1], out var value);
                    resList.Add(new AttrPair {Key = (AttrType) key, Value = value});
                }
            }

            return resList;
        }

        private static string FormatAttrPairs(IReadOnlyCollection<AttrPair> list)
        {
            if (list == null || list.Count == 0)
            {
                return string.Empty;
            }

            var tmpList = new List<string>();
            foreach (var pair in list)
            {
                if (pair.Key != AttrType.Unkown && pair.Value != 0)
                {
                    tmpList.Add($"{(byte) pair.Key}_{pair.Value}");
                }
            }

            if (tmpList.Count == 0)
            {
                return string.Empty;
            }

            return Json.SafeSerialize(tmpList);
        }
    }
}