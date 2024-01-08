using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Config;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Data.Fields;
using Ddxy.GameServer.Grains;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Equip
{
    public class Ornament
    {
        private PlayerGrain _player;
        public OrnamentEntity Entity { get; private set; }
        private OrnamentEntity _lastEntity; //上一次更新的Entity

        public uint Id => Entity.Id;

        public byte Grade => Entity.Grade;

        public OrnamentConfig Cfg { get; private set; }

        public uint Score { get; private set; }

        public EquipPlace Place
        {
            get => (EquipPlace) Entity.Place;
            set => Entity.Place = (byte) value;
        }

        public Attrs Attrs { get; private set; }

        private List<AttrPair> _baseAttrs;

        // 重铸临时数据
        private List<AttrPair> _recastData;

        public Ornament(PlayerGrain player, OrnamentEntity entity)
        {
            _player = player;
            Entity = entity;
            _lastEntity = new OrnamentEntity();
            _lastEntity.CopyFrom(Entity);

            Cfg = ConfigService.Ornaments[Entity.CfgId];

            _baseAttrs = new ();
            Attrs = new Attrs();
            CalculateAttribute();
            RefreshScore();

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

            _baseAttrs.Clear();
            _baseAttrs = null;
            Attrs.Dispose();
            Attrs = null;

            _recastData?.Clear();
            _recastData = null;
        }

        public OrnamentData BuildPbData()
        {
            var pbData = new OrnamentData
            {
                Id = Entity.Id,
                CfgId = Entity.CfgId,
                Grade = Entity.Grade,
                Place = (EquipPlace) Entity.Place,
                Score = Score,
                BaseAttrs = {_baseAttrs}
            };
            return pbData;
        }

        public Task SendInfo()
        {
            return _player.SendPacket(GameCmd.S2COrnamentInfo, new S2C_OrnamentInfo {Data = BuildPbData()});
        }

        // 分享的时候缓存
        public async Task Cache()
        {
            var bytes = Packet.Serialize(BuildPbData());
            await RedisService.SetOrnamentInfo(Entity.Id, bytes);
        }

        public async Task SaveData(bool copy = true)
        {
            if (Entity.Equals(_lastEntity)) return;
            var ret = await DbService.UpdateEntity(_lastEntity, Entity);
            if (ret && copy) _lastEntity.CopyFrom(Entity);
        }

        // 检查装备能否穿戴到角色上
        public bool CheckEnable()
        {
            if (Cfg.Race is {Length: > 0} && !Cfg.Race.Contains(_player.Entity.Race) ||
                Cfg.Sex is {Length: > 0} && !Cfg.Sex.Contains(_player.Entity.Sex))
            {
                _player.SendNotice("角色不匹配，不能使用");
                return false;
            }

            // if (_player.Entity.Relive < Cfg.NeedRelive)
            // {
            //     _player.SendNotice("转生等级不够，不能使用");
            //     return false;
            // }

            // if (_player.Entity.Relive <= Cfg.NeedRelive && _player.Entity.Level < Cfg.NeedLevel)
            // {
            //     _player.SendNotice("等级不够，不能使用");
            //     return false;
            // }

            return true;
        }

        public async Task SendPreviewData()
        {
            // 重铸预览数据
            if (_recastData is {Count: > 0})
            {
                var resp = new S2C_OrnamentProperty
                {
                    Id = Entity.Id,
                    List = {_recastData.ToList()},
                    Score = CalcOrnamentScore(_recastData)
                };

                await _player.SendPacket(GameCmd.S2COrnamentProperty, resp);
            }
        }

        /// <summary>
        /// 定制
        /// </summary>
        public async Task DingZhi(List<int> attrs)
        {
            // //配饰炼化 把定制取消改为 重铸100次 保底，点击保底之后可以选择定制属性
            // uint times = await RedisService.GetOperateTimes(_player.RoleId, RoleInfoFields.OperatePeiShiXiLian);
            // if (times < 100)
            // {
            //     return;
            // }
            uint itemCfgId = 500053;
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
            if (attrs.Count > 4)
            {
                _player.SendNotice("最多选择4条属性");
                return;
            }
#else
            if (attrs.Count != 4)
            {
                _player.SendNotice("必须选择4条属性");
                return;
            }
#endif
            ConfigService.OrnamentDingZhiAttrs.TryGetValue(Cfg.Index, out var dconfig);
            if (dconfig == null)
            {
                _player.SendNotice("配饰配置错误");
                return;
            }
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
            await _player.AddItem(itemCfgId, -1, true, String.Format("定制配饰id={0}", Id));

            Entity.BaseAttrs = FormatAttrPairs(dingzhiData);
            CalculateAttribute();
            RefreshScore();
            await SendInfo();

            // 刷新属性方案
            foreach (var scheme in _player.SchemeMgr.All)
            {
                if (!scheme.Ornaments.Contains(Entity.Id)) continue;
                await scheme.OnOrnamentUpdate(Entity.Id);
            }

            _player.SendNotice("定制成功");
            //配饰定制成功后，洗炼次数置零。
            await RedisService.SetOperateTimes(_player.RoleId, RoleInfoFields.OperatePeiShiXiLian, 0);
        }

        /// <summary>
        /// 重铸, 刷初始属性
        /// </summary>
        public async Task Recast(bool confirm, List<AttrType> locks)
        {
            if (confirm)
            {
                if (_recastData == null || _recastData.Count == 0)
                {
                    _player.SendNotice("请先预览");
                    return;
                }
                var oldAttrsCount = _baseAttrs.Count;
                if (oldAttrsCount != locks.Count)
                {
                    _player.SendNotice("客户端错误，不能炼化");
                    return;
                }
                var itemCount = _player.GetBagItemNum(9902);
                var newAttrsCount = _recastData.Count;
                var maxAttrsCount = Math.Max(oldAttrsCount, newAttrsCount);
                var attrs = new List<AttrPair>();
                var locksCount = 0;
                for (int i = 0; i < maxAttrsCount; i++)
                {
                    // 添加锁定的属性
                    if (i < oldAttrsCount && (int)locks[i] == 1)
                    {
                        locksCount++;
                        if (locksCount > itemCount)
                        {
                            _player.SendNotice("炼化锁不够");
                            return;
                        }
                        attrs.Add(_baseAttrs[i]);
                    }
                    // 添加预览的属性
                    else if (i < newAttrsCount)
                    {
                        attrs.Add(_recastData[i]);
                    }
                }
                // 减少炼化锁
                if (locksCount > 0)
                {
                    var okay = await _player.AddItem(9902, -locksCount, true, String.Format("重铸配饰id={0}", Id));
                    if (!okay)
                    {
                        _player.SendNotice("扣除炼化锁失败，不能炼化");
                        return;
                    }
                }

                _recastData.Clear();
                _recastData = null;

                Entity.BaseAttrs = FormatAttrPairs(attrs);
                SyncRecast();
                CalculateAttribute();
                RefreshScore();
                await SendInfo();

                // 刷新属性方案
                foreach (var scheme in _player.SchemeMgr.All)
                {
                    if (!scheme.Ornaments.Contains(Entity.Id)) continue;
                    await scheme.OnOrnamentUpdate(Entity.Id);
                }

                _player.SendNotice("重铸成功");
            }
            else
            {
                // 无量琉璃1，玉符5, 80w银币
                if (_player.GetBagItemNum(100100) < 1 || _player.GetBagItemNum(100101) < 5)
                {
                    _player.SendNotice("缺少重铸材料");
                    return;
                }

                if (!_player.CheckMoney(MoneyType.Silver, GameDefine.OrnamentRecastCostSilver)) return;

                _recastData = GetOrnamentRecastAttrs(Cfg.Index, Entity.Grade);
                if (_recastData == null || _recastData.Count == 0)
                {
                    _player.SendNotice("重铸预览失败");
                    return;
                }

                // 扣除道具
                var ret = await _player.AddBagItem(100100, -1);
                if (!ret)
                {
                    _recastData = null;
                    return;
                }

                ret = await _player.AddBagItem(100101, -5);
                if (!ret)
                {
                    _recastData = null;
                    return;
                }

                // 扣除银币 修改为扣除20000仙玉
                // await _player.AddMoney(MoneyType.Silver, -GameDefine.OrnamentRecastCostSilver, "重铸配饰消耗");
                await _player.AddMoney(MoneyType.Jade, -20000, "重铸配饰消耗");

                SyncRecast();

                // 下发预览数据数据
                var resp = new S2C_OrnamentProperty
                {
                    Id = Entity.Id,
                    List = {_recastData.ToList()},
                    Score = CalcOrnamentScore(_recastData)
                };
                await _player.SendPacket(GameCmd.S2COrnamentProperty, resp);
            }
            //配饰重铸洗练成功，加计数
            await RedisService.AddOperateTimes(_player.RoleId, RoleInfoFields.OperatePeiShiXiLian, 1);
        }

        private void RefreshScore()
        {
            Score = CalcOrnamentScore(_baseAttrs);
        }

        private void InitRecast()
        {
            _recastData = null;
            if (!string.IsNullOrWhiteSpace(Entity.Recast))
            {
                _recastData = new List<AttrPair>();

                var list = Json.Deserialize<List<string>>(Entity.Recast);
                foreach (var line in list)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var arr = line.Split('_');
                    if (arr.Length != 2) continue;
                    byte.TryParse(arr[0], out var key);
                    float.TryParse(arr[1], out var value);
                    _recastData.Add(new AttrPair {Key = (AttrType) key, Value = value});
                }

                if (_recastData.Count == 0) _recastData = null;
            }
        }

        private void SyncRecast()
        {
            if (_recastData == null || _recastData.Count == 0)
            {
                Entity.Recast = string.Empty;
            }
            else
            {
                var list = new List<string>();
                foreach (var pair in _recastData)
                {
                    if (pair.Key != AttrType.Unkown && pair.Value > 0)
                    {
                        list.Add($"{(byte) pair.Key}_{pair.Value}");
                    }
                }

                if (list.Count == 0)
                {
                    Entity.Recast = string.Empty;
                }
                else
                {
                    Entity.Recast = Json.SafeSerialize(list);
                }
            }
        }

        private void CalculateAttribute()
        {
            if (Entity.BaseAttrs.StartsWith('[')) {
                _baseAttrs = ParseAttrPairs(Entity.BaseAttrs);
            } else {
                var temp = new Attrs();
                temp.FromJson(Entity.BaseAttrs);
                _baseAttrs = temp.ToList();
                Entity.BaseAttrs = FormatAttrPairs(_baseAttrs);
            }
            // 从基础属性和炼化属性中统计Attr
            Attrs.Clear();
            foreach (var p in _baseAttrs)
            {
                var value = p.Value;
                if (!GameDefine.EquipNumericalAttrType.ContainsKey(p.Key))
                {
                    value = p.Value / 10;
                }

                Attrs.Add(p.Key, value);
            }
        }

        public static uint CalcOrnamentScore(Attrs baseAttrs)
        {
            // 计算得分
            var total = 0f;
            foreach (var (k, v) in baseAttrs)
            {
                var scale = GameDefine.AttrTypeCalcScoreScale.GetValueOrDefault(k, 1f);
                total += MathF.Ceiling(MathF.Abs(v * scale));
            }

            var score = (uint) MathF.Ceiling(total);
            return score;
        }

        public static uint CalcOrnamentScore(List<AttrPair> list)
        {
            var attrs = new Attrs();
            foreach (var pair in list)
            {
                attrs.Add(pair.Key, pair.Value);
            }

            return CalcOrnamentScore(attrs);
        }

        public static List<AttrPair> GetOrnamentRecastAttrs(uint pos, int grade)
        {
            var list = new List<AttrPair>();

            ConfigService.OrnamentAttrs.TryGetValue(pos, out var cfgList);
            if (cfgList == null || cfgList.Count == 0) return list;

            var rnd = new Random();

            var cnt = 4;
            var factor = 0f;
            if (grade == 2) factor = 0.3f;
            else if (grade == 3) factor = 0.5f;

            // 千分之一的概率允许产生相同的强力克属性
            // var allowSameQlk = rnd.Next(0, 1000) == 0;

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

                var next = false;

                switch (pos)
                {
                    case 1:
                    {
                        // 披风: 根骨灵性力量敏捷最多出现条3
                        if (attrType is >= AttrType.GenGu and <= AttrType.MinJie)
                        {
                            if (list.Exists(p => p.Key == attrType))
                            {
                                next = true;
                                break;
                            }

                            if (list.Count(p => p.Key is >= AttrType.GenGu and <= AttrType.MinJie) >= 3)
                            {
                                next = true;
                                break;
                            }
                        }

                        // 连击、命中率、狂暴率不可叠加
                        if (attrType is AttrType.PlianJiLv or AttrType.PmingZhong or AttrType.PkuangBao)
                        {
                            if (list.Exists(p =>
                                p.Key is AttrType.PlianJiLv or AttrType.PmingZhong or AttrType.PkuangBao))
                            {
                                next = true;
                                break;
                            }
                        }

                        // 抗仙法鬼火物理三尸不可叠加
                        if (attrType is AttrType.Dfeng or AttrType.Huo or AttrType.Shui or AttrType.Dlei or
                            AttrType.DguiHuo or AttrType.DwuLi or AttrType.DsanShi)
                        {
                            if (list.Exists(p =>
                                p.Key is AttrType.Dfeng or AttrType.Huo or AttrType.Shui or AttrType.Dlei or
                                    AttrType.DguiHuo or AttrType.DwuLi or AttrType.DsanShi))
                            {
                                next = true;
                                break;
                            }
                        }

                        // 气血百分比 最多叠加两条
                        if (attrType == AttrType.Ahp)
                        {
                            if (list.Count(p => p.Key == AttrType.Ahp) >= 2)
                            {
                                next = true;
                                break;
                            }
                        }

                        // 攻击力百分比不可叠加
                        if (attrType == AttrType.Aatk)
                        {
                            if (list.Count(p => p.Key == AttrType.Aatk) >= 1)
                            {
                                next = true;
                            }
                        }
                    }
                        break;
                    case 2:
                    {
                        // 挂件: 根骨灵性力量敏捷最多出现条3
                        if (attrType is >= AttrType.GenGu and <= AttrType.MinJie)
                        {
                            if (list.Exists(p => p.Key == attrType))
                            {
                                next = true;
                                break;
                            }

                            if (list.Count(p => p.Key is >= AttrType.GenGu and <= AttrType.MinJie) >= 3)
                            {
                                next = true;
                                break;
                            }
                        }

                        // 强力克最多出现两条
                        if (attrType is AttrType.Qjin or AttrType.Qmu or AttrType.Qshui or AttrType.Qhuo or AttrType.Tu)
                        {
                            // 强力克属性最多出现3条
                            if (list.Count(p =>
                                p.Key is AttrType.Qjin or AttrType.Qmu or AttrType.Qshui or AttrType.Qhuo or AttrType
                                    .Tu) >= 2)
                            {
                                next = true;
                                break;
                            }
                        }

                        // 连击率或命中率或狂暴率不可叠加
                        if (attrType is AttrType.PlianJiLv or AttrType.PmingZhong or AttrType.PkuangBao)
                        {
                            if (list.Exists(p =>
                                p.Key is AttrType.PlianJiLv or AttrType.PmingZhong or AttrType.PkuangBao))
                            {
                                next = true;
                                break;
                            }
                        }

                        // 抗混乱遗忘封印昏睡不可叠加
                        if (attrType is AttrType.DhunLuan or AttrType.DyiWang or AttrType.DfengYin or AttrType.DhunShui)
                        {
                            if (list.Exists(p =>
                                p.Key is AttrType.DhunLuan or AttrType.DyiWang or AttrType.DfengYin or AttrType
                                    .DhunShui))
                            {
                                next = true;
                                break;
                            }
                        }

                        // 抗物理仙法鬼火三尸不可叠加
                        if (attrType is AttrType.Dfeng or AttrType.Huo or AttrType.Shui or AttrType.Dlei or
                            AttrType.DguiHuo or AttrType.DwuLi or AttrType.DsanShi)
                        {
                            if (list.Exists(p =>
                                p.Key is AttrType.Dfeng or AttrType.Huo or AttrType.Shui or AttrType.Dlei or
                                    AttrType.DguiHuo or AttrType.DwuLi or AttrType.DsanShi))
                            {
                                next = true;
                                break;
                            }
                        }

                        // 气血百分比增加最多叠加两条
                        if (attrType == AttrType.Ahp)
                        {
                            if (list.Count(p => p.Key == AttrType.Ahp) >= 2)
                            {
                                next = true;
                                break;
                            }
                        }

                        // 攻击力百分比不可叠加
                        if (attrType == AttrType.Aatk)
                        {
                            if (list.Exists(p => p.Key == AttrType.Aatk))
                            {
                                next = true;
                            }
                        }
                    }
                        break;
                    case 3:
                    {
                        // 腰带: 根骨灵性力量敏捷最多出现条3
                        if (attrType is >= AttrType.GenGu and <= AttrType.MinJie)
                        {
                            if (list.Exists(p => p.Key == attrType))
                            {
                                next = true;
                                break;
                            }

                            if (list.Count(p => p.Key is >= AttrType.GenGu and <= AttrType.MinJie) >= 3)
                            {
                                next = true;
                                break;
                            }
                        }

                        // 强力克最多出现两条
                        if (attrType is AttrType.Qjin or AttrType.Qmu or AttrType.Qshui or AttrType.Qhuo or AttrType.Tu)
                        {
                            // 强力克属性最多出现3条
                            if (list.Count(p =>
                                p.Key is AttrType.Qjin or AttrType.Qmu or AttrType.Qshui or AttrType.Qhuo or AttrType
                                    .Tu) >= 2)
                            {
                                next = true;
                                break;
                            }
                        }

                        // 抗遗忘鬼火三尸不可叠加
                        if (attrType is AttrType.DyiWang or AttrType.DguiHuo or AttrType.DsanShi)
                        {
                            if (list.Exists(p =>
                                p.Key is AttrType.DyiWang or AttrType.DguiHuo or AttrType.DsanShi))
                            {
                                next = true;
                            }
                        }

                        // 连击率命中率狂暴率不可叠加
                        if (attrType is AttrType.PlianJiLv or AttrType.PmingZhong or AttrType.PkuangBao)
                        {
                            if (list.Exists(p =>
                                p.Key is AttrType.PlianJiLv or AttrType.PmingZhong or AttrType.PkuangBao))
                            {
                                next = true;
                            }
                        }

                        // 气血百分比增加最多叠加两条
                        if (attrType == AttrType.Ahp)
                        {
                            if (list.Count(p => p.Key == AttrType.Ahp) >= 2)
                            {
                                next = true;
                            }
                        }
                    }
                        break;
                    case 4:
                    {
                        // 戒指: 根骨灵性力量敏捷最多出现条3，可叠加
                        if (attrType is >= AttrType.GenGu and <= AttrType.MinJie)
                        {
                            if (list.Count(p => p.Key is >= AttrType.GenGu and <= AttrType.MinJie) >= 3)
                            {
                                next = true;
                                break;
                            }
                        }

                        // 抗物理仙法鬼火狂暴最多出现一条不可叠加
                        if (attrType is AttrType.Dfeng or AttrType.Huo or AttrType.Shui or AttrType.Dlei or
                            AttrType.DguiHuo or AttrType.DwuLi or AttrType.PkuangBao)
                        {
                            if (list.Exists(p =>
                                p.Key is AttrType.Dfeng or AttrType.Huo or AttrType.Shui or AttrType.Dlei or
                                    AttrType.DguiHuo or AttrType.DwuLi or AttrType.PkuangBao))
                            {
                                next = true;
                            }
                        }
                    }
                        break;
                }

                if (next) continue;

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
        public static List<AttrPair> ParseAttrPairs(string text)
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
                    resList.Add(new AttrPair { Key = (AttrType)key, Value = value });
                }
            }

            return resList;
        }

        public static string FormatAttrPairs(IReadOnlyCollection<AttrPair> list)
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
                    tmpList.Add($"{(byte)pair.Key}_{pair.Value}");
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