using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Config;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.Protocol;
using Orleans.Concurrency;
using Ddxy.GameServer.Util;

namespace Ddxy.GameServer.Logic.Equip
{
    public class EquipManager
    {
        private PlayerGrain _player;

        public Dictionary<uint, Equip> Equips { get; private set; }
        public Dictionary<uint, Ornament> Ornaments { get; private set; }
        public Dictionary<uint, PetOrnament> PetOrnaments { get; private set; }

        /// <summary>
        /// 背包中装备数量
        /// </summary>
        public int BagCount => Equips.Count(p => p.Value.Place != EquipPlace.Repo) +
                               Ornaments.Count(p => p.Value.Place != EquipPlace.Repo);

        /// <summary>
        /// 仓库中装备数量
        /// </summary>
        public int RepoCount => Equips.Count(p => p.Value.Place == EquipPlace.Repo) +
                                Ornaments.Count(p => p.Value.Place == EquipPlace.Repo);

        /// <summary>
        /// 仓库中装备数量
        /// </summary>
        public int PetOrnamentCount => PetOrnaments.Count(p => p.Value.Place > 0);

        public EquipManager(PlayerGrain player)
        {
            _player = player;
            Equips = new Dictionary<uint, Equip>(10);
            Ornaments = new Dictionary<uint, Ornament>(10);
            PetOrnaments = new Dictionary<uint, PetOrnament>(10);
        }

        public async Task Init()
        {
            var entities = await DbService.QueryEquips(_player.RoleId);
            foreach (var entity in entities)
            {
                if (entity == null || entity.CfgId == 0) continue;
                var data = new Equip(_player, entity);
                Equips.Add(data.Id, data);
            }

            var entities2 = await DbService.QueryOrnaments(_player.RoleId);
            foreach (var entity in entities2)
            {
                var data = new Ornament(_player, entity);
                Ornaments.Add(data.Id, data);
            }

            var entities3 = await DbService.QueryPetOrnaments(_player.RoleId);
            foreach (var entity in entities3)
            {
                var data = new PetOrnament(_player, entity);
                PetOrnaments.Add(data.Id, data);
            }
        }

        public async Task Destroy()
        {
            var tasks = from p in Equips.Values select p.Destroy();
            await Task.WhenAll(tasks);

            tasks = from p in Ornaments.Values select p.Destroy();
            await Task.WhenAll(tasks);

            tasks = from p in PetOrnaments.Values select p.Destroy();
            await Task.WhenAll(tasks);

            Equips.Clear();
            Equips = null;
            Ornaments.Clear();
            Ornaments = null;
            PetOrnaments.Clear();
            PetOrnaments = null;

            _player = null;
        }

        public async Task SaveData()
        {
            var tasks = from p in Equips.Values select p.SaveData();
            await Task.WhenAll(tasks);

            tasks = from p in Ornaments.Values select p.SaveData();
            await Task.WhenAll(tasks);

            tasks = from p in PetOrnaments.Values select p.SaveData();
            await Task.WhenAll(tasks);
        }

        public void CheckPlace(List<uint> wearEquips, List<uint> wearOrnaments)
        {
            // 先把所有非repo的都设置为bag
            foreach (var equip in Equips.Values)
            {
                if (equip.Place == EquipPlace.Repo) continue;
                equip.Place = EquipPlace.Bag;
            }

            foreach (var ornament in Ornaments.Values)
            {
                if (ornament.Place == EquipPlace.Repo) continue;
                ornament.Place = EquipPlace.Bag;
            }

            foreach (var eid in wearEquips)
            {
                Equips.TryGetValue(eid, out var equip);
                if (equip != null) equip.Place = EquipPlace.Wear;
            }

            foreach (var oid in wearOrnaments)
            {
                Ornaments.TryGetValue(oid, out var ornament);
                if (ornament != null) ornament.Place = EquipPlace.Wear;
            }
        }

        // 下发全部的装备
        public async Task SendList()
        {
            {
                var resp = new S2C_EquipList();
                foreach (var equip in Equips.Values)
                {
                    resp.List.Add(equip.BuildPbData());
                }

                await _player.SendPacket(GameCmd.S2CEquipList, resp);
            }

            {
                var resp = new S2C_OrnamentList();
                foreach (var ornament in Ornaments.Values)
                {
                    resp.List.Add(ornament.BuildPbData());
                }

                await _player.SendPacket(GameCmd.S2COrnamentList, resp);
            }

            {
                var resp = new S2C_PetOrnamentList();
                foreach (var ornament in PetOrnaments.Values)
                {
                    resp.List.Add(ornament.BuildPbData());
                }

                await _player.SendPacket(GameCmd.S2CPetOrnamentList, resp);
            }
        }

        // 商品从摆摊系统下架，返还给卖家
        public async Task<Equip> AddEquip(EquipEntity entity)
        {
            var equip = new Equip(_player, entity);
            Equips.Add(equip.Id, equip);
            await _player.SendPacket(GameCmd.S2CEquipAdd, new S2C_EquipAdd {Data = equip.BuildPbData()});
            return equip;
        }

        public async Task<Equip> AddEquip(EquipCategory category, int index, int grade = 0, bool notice = true)
        {
            if (_player.IsBagFull)
            {
                _player.SendNotice("背包已满");
                return null;
            }

            // 查找匹配的配置
            var cfg = FindEquipConfig(category, index, grade);
            if (cfg == null) return null;
            var equip = await AddEquip(cfg, notice);
            return equip;
        }

        public Task<Equip> AddEquip(uint cfgId, bool notice = true)
        {
            ConfigService.Equips.TryGetValue(cfgId, out var cfg);
            return AddEquip(cfg, notice);
        }

        public async Task<Equip> AddEquip(EquipConfig cfg, bool notice = true)
        {
            if (cfg == null) return null;

            if (_player.IsBagFull)
            {
                _player.SendNotice("背包已满");
                return null;
            }

#if false
            // 翅膀的话不允许出现相同的
            if (cfg.Category == 0)
            {
                if (Equips.Values.ToList().Exists(p => p.CfgId == cfg.Id))
                {
                    return null;
                }
            }
#endif

            // 构建Entity插入数据库
            var entity = BuildEquipEntity((EquipCategory) cfg.Category, cfg);
            await DbService.InsertEntity(entity);
            if (entity.Id == 0) return null;
            // 构建EquipData
            var data = new Equip(_player, entity);
            Equips.Add(data.Id, data);
            // 通知前端，新获得一个装备
            await _player.SendPacket(GameCmd.S2CEquipAdd, new S2C_EquipAdd {Data = data.BuildPbData()});
            _player.SendNotice($"恭喜您获得了{data.Cfg.Desc}{data.Cfg.Name}");
            // 神兵 仙器需要广播
            if (notice && data.Category >= EquipCategory.Shen)
            {
                var text =
                    $"<color=#00ff00>{_player.Entity.NickName}</c><color=#ffffff>获得了</c><color=#0fffff>{data.Cfg.Desc}{data.Cfg.Name}</color>，<color=#ffffff>真是太幸运了</c>";
                _player.BroadcastScreenNotice(text, 0);
            }

            return data;
        }

        public async ValueTask<bool> DelEquip(uint equipId, bool force = false, bool unInlay = true)
        {
            Equips.TryGetValue(equipId, out var equip);
            // 如果是已经装备了的不能删除
            if (equip == null) return false;
            // 正常情况下只能丢弃背包里的装备
            if (!force && equip.Place != EquipPlace.Bag) return false;

            // 从数据库中移除
            await DbService.DeleteEntity<EquipEntity>(equip.Id);
            Equips.Remove(equipId);

            _player.LogDebug(
                $"DelEquip:{equip.Id} cfgId:{equip.CfgId} category:{equip.Category} grade:{equip.Grade} gem:{equip.Gem} pos:{equip.Cfg.Index}");

            // 如果需要拆卸宝石
            if (unInlay)
                await equip.UnInlay();

            await _player.SendPacket(GameCmd.S2CEquipDel, new S2C_EquipDel {Id = equipId});

            // 如果装备穿戴在身上, 要去刷Scheme
            if (equip.Place == EquipPlace.Wear)
            {
                foreach (var scheme in _player.SchemeMgr.All)
                {
                    if (!scheme.Equips.Contains(equipId)) continue;
                    await scheme.OnEquipDelete(equipId);
                }
            }

            return true;
        }

        // 合成, 只能合成1级的装备
        public async Task Combine(EquipCategory category, int index)
        {
            if (_player.IsBagFull)
            {
                _player.SendNotice("背包已满");
                return;
            }

            if (index < 1 || index > 5) index = _player.Random.Next(1, 6);

            uint equipId = 0;

            if (category == EquipCategory.Shen)
            {
                if (_player.GetBagItemNum(10408) < 50)
                {
                    _player.SendNotice("神兵碎片不足");
                    return;
                }

                var equip = await AddEquip(EquipCategory.Shen, index, 1);
                if (equip == null)
                {
                    _player.SendNotice("合成神兵失败");
                    return;
                }

                equipId = equip.Id;

                // 创建成功了再来扣道具
                await _player.AddBagItem(10408, -50, tag: "合成神兵消耗");
            }
            else if (category == EquipCategory.Xian)
            {
                if (_player.GetBagItemNum(10406) < 40)
                {
                    _player.SendNotice("八荒遗风不足");
                    return;
                }

                // 花费1kw银两
                if (!_player.CheckMoney(MoneyType.Silver, 10000000)) return;

                var equip = await AddEquip(EquipCategory.Xian, index, 1);
                if (equip == null)
                {
                    _player.SendNotice("合成仙器失败");
                    return;
                }

                // 花费1kw银两
                var ret = await _player.CostMoney(MoneyType.Silver, 10000000, tag: "合成仙器消耗");
                if (!ret) return;

                equipId = equip.Id;

                // 创建成功了再来扣道具
                await _player.AddBagItem(10406, -40, tag: "合成仙器消耗");
            }

            // 通知合成协议
            await _player.SendPacket(GameCmd.S2CEquipCombine, new S2C_EquipCombine {Id = equipId});
        }

        public async Task SendEquipPreviewData(uint id, uint flag)
        {
            var equip = FindEquip(id);
            if (equip == null) return;
            await equip.GetPreviewData(flag);
        }
        public async Task SendEquipPreviewDataList(uint id, uint flag)
        {
            var equip = FindEquip(id);
            if (equip == null) return;
            await equip.GetPreviewDataList(flag);
        }

        public Equip FindEquip(uint id)
        {
            Equips.TryGetValue(id, out var data);
            return data;
        }

        private EquipConfig FindEquipConfig(EquipCategory category, int index, int grade)
        {
            var configs = category switch
            {
                EquipCategory.XinShou => ConfigService.Equips1,
                EquipCategory.High => ConfigService.Equips2,
                EquipCategory.Shen => ConfigService.Equips3,
                EquipCategory.Xian => ConfigService.Equips4,
                _ => null
            };
            if (configs == null) return null;
            // 过滤符合条件的集合
            var list = configs.Values.Where(v =>
                (v.OwnerRoleId == 0 || v.OwnerRoleId == _player.Entity.CfgId) &&
                (v.Race == 9 || v.Race == _player.Entity.Race) &&
                (v.Sex == 9 || v.Sex == _player.Entity.Sex) &&
                (index == 0 || v.Index == index) &&
                (grade == 0 || v.Grade == grade)
            ).ToList();

            if (list.Count == 0) return null;
            if (list.Count == 1) return list[0];
            return list[_player.Random.Next(0, list.Count)];
        }

        private EquipEntity BuildEquipEntity(EquipCategory category, EquipConfig cfg)
        {
            var entity = new EquipEntity
            {
                RoleId = _player.RoleId,
                Category = (byte) category,
                CfgId = cfg.Id,
                Gem = 0,
                Grade = cfg.Grade,
                Place = (byte) EquipPlace.Bag,
                BaseAttrs = "",
                RefineAttrs = "",
                NeedAttrs = "",
                Refine = "",
                RefineList = "",
                Recast = "",
                CreateTime = TimeUtil.TimeStamp
            };
            Equip.BuildEntityAttrs(entity);
            return entity;
        }

        public Ornament FindOrnament(uint id)
        {
            Ornaments.TryGetValue(id, out var data);
            return data;
        }

        // 鉴定配饰
        public Task<Ornament> AddOrnaments(int pos, uint suit = 0, int grade = 0)
        {
            var cfg = FindOrnamentConfig(pos, suit);
            return AddOrnaments(cfg, grade);
        }

        public Task<Ornament> AddOrnaments(uint cfgId, int grade = 0)
        {
            ConfigService.Ornaments.TryGetValue(cfgId, out var cfg);
            return AddOrnaments(cfg, grade);
        }

        public async Task<Ornament> AddOrnaments(OrnamentConfig cfg, int grade = 0)
        {
            if (cfg == null) return null;

            if (grade == 0)
            {
                grade = 1;
                var rnd = _player.Random.Next(0, 100);
                if (rnd < 5) grade = 3;
                else if (rnd < 20) grade = 2;
            }

            var attrList = Ornament.GetOrnamentRecastAttrs(cfg.Index, grade);

            var entity = new OrnamentEntity
            {
                CfgId = cfg.Id,
                RoleId = _player.RoleId,
                Grade = (byte) grade,
                Place = (byte) EquipPlace.Bag,
                BaseAttrs = Ornament.FormatAttrPairs(attrList),
                Recast = "",
                CreateTime = TimeUtil.TimeStamp
            };
            await DbService.InsertEntity(entity);
            if (entity.Id == 0) return null;

            var ornament = new Ornament(_player, entity);
            Ornaments.Add(ornament.Id, ornament);

            attrList.Clear();

            await _player.SendPacket(GameCmd.S2COrnamentAdd, new S2C_OrnamentAdd
            {
                Data = ornament.BuildPbData()
            });

            _player.LogDebug(
                $"AddOrnament:{ornament.Id} cfgId:{ornament.Cfg.Id} grade:{ornament.Grade}");

            return ornament;
        }

        public async Task DelOrnament(uint id, bool force = false)
        {
            Ornaments.TryGetValue(id, out var ornament);
            // 如果是已经装备了的不能删除
            if (ornament == null) return;
            // 正常情况下只能丢弃背包里的装备
            if (!force && ornament.Place != EquipPlace.Bag) return;

            // 从数据库中移除
            await DbService.DeleteEntity<OrnamentEntity>(id);
            Ornaments.Remove(id);

            await _player.SendPacket(GameCmd.S2COrnamentDel, new S2C_OrnamentDel {Id = id});
            // 如果佩饰穿戴在身上, 要去刷Scheme
            if (ornament.Place == EquipPlace.Wear)
            {
                foreach (var scheme in _player.SchemeMgr.All)
                {
                    if (scheme.Ornaments.Contains(id))
                    {
                        await scheme.OnOrnamentDelete(id);
                    }
                }
            }

            _player.LogDebug(
                $"DelOrnament:{ornament.Id} cfgId:{ornament.Cfg.Id} grade:{ornament.Grade}");
        }

        public async Task SendOrnamentProperty(uint id)
        {
            var ornament = FindOrnament(id);
            if (ornament == null) return;
            await ornament.SendPreviewData();
        }

        public async Task RecastOrnament(uint id, uint flag, List<AttrType> locks)
        {
            var ornament = FindOrnament(id);
            if (ornament == null) return;
            await ornament.Recast(flag > 0, locks);
        }

        public async Task DecomposeOrnament(uint id)
        {
            var ornament = FindOrnament(id);
            if (ornament == null) return;
            if (ornament.Place == EquipPlace.Wear)
            {
                _player.SendNotice("穿戴的配饰不能分解");
                return;
            }

            var num = ornament.Grade switch
            {
                1 =>
                    // 1-3
                    _player.Random.Next(1, 4),
                2 =>
                    // 2-4
                    _player.Random.Next(2, 5),
                3 =>
                    // 3-5
                    _player.Random.Next(3, 6),
                _ => 0
            };

            if (num == 0) return;

            // 删除
            await DelOrnament(id);

            // 获得玉符
            await _player.AddBagItem(100101, num, tag: "分解佩饰");

            _player.LogDebug(
                $"DecomposeOrnament:{ornament.Id} cfgId:{ornament.Cfg.Id} grade:{ornament.Grade}");
        }

        /// <summary>
        /// 鉴定佩饰
        /// </summary>
        public async Task AppraisalOrnament(uint itemId)
        {
            ConfigService.Items.TryGetValue(itemId, out var cfg);
            if (cfg == null) return;
            if (_player.GetBagItemNum(itemId) < 1) return;
            // 盘古精铁、补天神石、玉符
            if (_player.GetBagItemNum(10405) < 1 ||
                _player.GetBagItemNum(10407) < 1 ||
                _player.GetBagItemNum(100101) < 1)
            {
                _player.SendNotice("材料不足");
                return;
            }

            const uint subSilver = 200000;
            if (!_player.CheckMoney(MoneyType.Silver, subSilver)) return;

            var ornament = await AddOrnaments(cfg.Num);
            if (ornament == null)
            {
                _player.SendNotice("鉴定佩饰失败");
                return;
            }

            await _player.AddBagItem(itemId, -1, tag: "鉴定配饰消耗");
            await _player.AddBagItem(10405, -1, tag: "鉴定配饰消耗");
            await _player.AddBagItem(10407, -1, tag: "鉴定配饰消耗");
            await _player.AddBagItem(100101, -1, tag: "鉴定配饰消耗");
            await _player.CostMoney(MoneyType.Silver, subSilver, tag: "鉴定配饰消耗");
        }

        public async Task GetShareOrnamentInfo(uint id)
        {
            var resp = new S2C_ShareOrnamentInfo();
            var bytes = await RedisService.GetOrnamentInfo(id);
            if (bytes is {Length: > 0})
            {
                resp.Data = OrnamentData.Parser.ParseFrom(bytes);
            }

            await _player.SendPacket(GameCmd.S2CShareOrnamentInfo, resp);
        }
        //获取操作的次数   1->孩子定制 2->星阵定制 3->配饰炼化
        public async Task GetOperateTimes(uint id)
        {
            var resp = new S2C_GetOperateTimes();
            var times = await RedisService.GetOperateTimes(_player.RoleId, id);
            resp.Times = times;
            await _player.SendPacket(GameCmd.S2CGetOperateTimes, resp);
        }

        private OrnamentConfig FindOrnamentConfig(int index, uint suit = 0)
        {
            if (index >= 5) index = 4;
            // 过滤符合条件的集合
            var list = ConfigService.Ornaments.Values.Where(v =>
                (v.Race == null || v.Race.Length == 0 || v.Race.Contains(_player.Entity.Race)) &&
                (v.Sex == null || v.Sex.Length == 0 || v.Sex.Contains(_player.Entity.Sex)) &&
                (index == 0 || v.Index == index) &&
                (suit == 0 || v.Suit.Contains(suit))
            ).ToList();

            if (list.Count == 0) return null;
            if (list.Count == 1) return list[0];
            return list[_player.Random.Next(0, list.Count)];
        }

        // 宠物配饰--分解
        public async Task PetOrnamentFenJie(List<uint> list)
        {
            var ornamentList = DelPetOrnament(list, true);
            Dictionary<uint, int> items = new();
            List<uint> filtered = new();
            foreach (var ornament in ornamentList)
            {
                filtered.Add(ornament.Id);
#if true
                // 把玩
                if (ornament.Grade == 1)
                {
                    items[10405] = items.GetValueOrDefault((uint)10405, 0) + 3;
                    items[10407] = items.GetValueOrDefault((uint)10407, 0) + 3;
                }
                // 珍藏
                else if (ornament.Grade == 2)
                {
                    items[10405] = items.GetValueOrDefault((uint)10405, 0) + 6;
                    items[10407] = items.GetValueOrDefault((uint)10407, 0) + 6;
                }
                // 无价
                else if (ornament.Grade == 3)
                {
                    items[10405] = items.GetValueOrDefault((uint)10405, 0) + 9;
                    items[10407] = items.GetValueOrDefault((uint)10407, 0) + 9;
                }
#else
                // 把玩
                if (ornament.Grade == 1)
                {
                    // 50%
                    if (_player.Random.Next(100) < 50) items[10405] = items.GetValueOrDefault((uint)10405, 0) + 1;
                    // 50%
                    if (_player.Random.Next(100) < 50) items[10407] = items.GetValueOrDefault((uint)10407, 0) + 1;
                    // 30%
                    if (_player.Random.Next(100) < 30) items[500073] = items.GetValueOrDefault((uint)500073, 0) + 1;
                    // 10%
                    if (_player.Random.Next(100) < 10) items[500074] = items.GetValueOrDefault((uint)500074, 0) + 1;
                }
                // 珍藏
                else if (ornament.Grade == 2)
                {
                    // 60%
                    if (_player.Random.Next(100) < 60) items[10405] = items.GetValueOrDefault((uint)10405, 0) + 1;
                    // 60%
                    if (_player.Random.Next(100) < 60) items[10407] = items.GetValueOrDefault((uint)10407, 0) + 1;
                    // 40%
                    if (_player.Random.Next(100) < 40) items[500073] = items.GetValueOrDefault((uint)500073, 0) + 1;
                    // 15%
                    if (_player.Random.Next(100) < 15) items[500074] = items.GetValueOrDefault((uint)500074, 0) + 1;
                }
                // 无价
                else if (ornament.Grade == 3)
                {
                    // 65%
                    if (_player.Random.Next(100) < 65) items[10405] = items.GetValueOrDefault((uint)10405, 0) + 1;
                    // 65%
                    if (_player.Random.Next(100) < 65) items[10407] = items.GetValueOrDefault((uint)10407, 0) + 1;
                    // 45%
                    if (_player.Random.Next(100) < 45) items[500073] = items.GetValueOrDefault((uint)500073, 0) + 1;
                    // 20%
                    if (_player.Random.Next(100) < 20) items[500074] = items.GetValueOrDefault((uint)500074, 0) + 1;
                    // 10%
                    else if (_player.Random.Next(100) < 10) items[500075] = items.GetValueOrDefault((uint)500075, 0) + 1;
                }
#endif
            }
            // 可分解大于0，但是上面的随机概率没有得到任何物品，则象征性的给点
            if (ornamentList.Count > 0 && items.Count == 0)
            {
                // 50%
                if (_player.Random.Next(100) < 50) items[10405] = items.GetValueOrDefault((uint)10405, 0) + ornamentList.Count;
                // 50%
                else items[10407] = items.GetValueOrDefault((uint)10407, 0) + ornamentList.Count;
            }
            DelPetOrnament(filtered, false);
            var resp = new S2C_PetOrnamentFenJie();
            foreach (var (id, num) in items)
            {
                resp.ItemList.Add(new ItemData() { Id = id, Num = (uint)num });
                _ = _player.AddBagItem(id, num, true, "分解宠物配饰");
            }
            resp.List.AddRange(filtered);
            await _player.SendPacket(GameCmd.S2CPetOrnamentFenJie, resp);
        }
        // 宠物配饰--打造
        public async Task PetOrnamentDaZhao(uint itemId)
        {
            uint costBindJade = 100000;
            var resp = new S2C_PetOrnamentDaZhao();
            if (_player.GetMoney(MoneyType.Jade) < costBindJade)
            {
                resp.Error = $"积分不足";
            }
            else if (_player.GetBagItemNum(10405) < 10)
            {
                resp.Error = $"材料（{ConfigService.Items[10405].Name}）不足";
            }
            else if (_player.GetBagItemNum(10407) < 10)
            {
                resp.Error = $"材料（{ConfigService.Items[10407].Name}）不足";
            }
            else if (_player.GetBagItemNum(500073) <= 0)
            {
                resp.Error = $"材料（{ConfigService.Items[500073].Name}）不足";
            }
            // 凤羽精粹-普通/高级 数量大于0
            else if ((itemId != 500074 && itemId != 500075) || _player.GetBagItemNum(itemId) <= 0)
            {
                resp.Error = "材料（凤羽精粹）不足";
            }
            else
            {
                await _player.CostMoney(MoneyType.Jade, costBindJade, false, "宠物配饰-打造");
                await _player.AddBagItem(10405, -10, true, "宠物配饰-打造");
                await _player.AddBagItem(10407, -10, true, "宠物配饰-打造");
                await _player.AddBagItem(500073, -1, true, "宠物配饰-打造");
                await _player.AddBagItem(itemId, -1, true, "宠物配饰-打造");
                PetOrnament ornament = null;
                // 出高品质配饰几率较高
                if (itemId == 500075)
                {
                    ornament = await AddPetOrnament((uint)_player.Random.Next(1, 4), _player.Random.Next(0, 100) < 50 ? 3 : 2);
                }
                else
                {
                    ornament = await AddPetOrnament((uint)_player.Random.Next(1, 4));
                }
                resp.Data = ornament.BuildPbData();
                // 无价配饰广播消息
                if (resp.Data.Grade >= 3)
                {
                    _ = ornament.Cache();
                    // 构造消息
                    var msg = new ChatMessage
                    {
                        Type = ChatMessageType.Bell,
                        Msg = Json.SafeSerialize(new
                        {
                            type = "PetOrnamentDaZhao",
                            info = new
                            {
                                id = resp.Data.Id,
                                type = (uint)resp.Data.Type,
                                grade = resp.Data.Grade
                            }
                        }),
                        From = _player.BuildRoleInfo(),
                        BellTimes = 0,
                    };
                    _ = _player.ServerGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
                }
            }
            await _player.SendPacket(GameCmd.S2CPetOrnamentDaZhao, resp);
        }
        // 宠物配饰--装备
        public async Task PetOrnamentEquip(uint id, uint petId)
        {
            var ornament = FindPetOrnament(id);
            if (ornament == null)
            {
                _player.SendNotice("配饰不存在");
                return;
            }
            var pet = _player.PetMgr.FindPet(petId);
            if (pet == null)
            {
                _player.SendNotice("宠物不存在");
                return;
            }
            if (!(pet.Entity.Relive > 1 || (pet.Entity.Relive == 1 && pet.Entity.Level >= 100)))
            {
                _player.SendNotice("宠物等级不足");
                return;
            }
            if (ornament.Place > 0 && ornament.Place != petId)
            {
                _player.SendNotice("配饰已装备在其他宠物身上了");
                return;
            }
            // 卸下当前类型装备了的配饰
            foreach (var o in this.PetOrnaments.Values)
            {
                if (o.Place == petId && o.Entity.TypeId == ornament.Entity.TypeId)
                {
                    await PetOrnamentUnEquip(o.Id, false);
                }
            }
            ornament.Place = petId;
            // 发送配饰信息
            await ornament.SendInfo();
            // 刷新宠物属性
            await pet.RefreshAttrs();
            _player.SendNotice("装备成功");
        }
        // 宠物配饰--卸载
        public async Task PetOrnamentUnEquip(uint id, bool notice = true)
        {
            var ornament = FindPetOrnament(id);
            if (ornament == null)
            {
                if (notice) _player.SendNotice("配饰不存在");
                return;
            }
            if (ornament.Place <= 0)
            {
                if (notice) _player.SendNotice("卸载成功");
                await _player.PetMgr.RecalculateAttrsAndSendList();
                return;
            }
            var pet = _player.PetMgr.FindPet(ornament.Place);
            ornament.Place = 0;
            // 发送配饰信息
            await ornament.SendInfo();
            // 刷新宠物属性
            if (pet != null)
            {
                await pet.RefreshAttrs();
            }
            if (notice) _player.SendNotice("卸载成功");
        }
        // 宠物配饰--锁定
        public async Task PetOrnamentLock(uint id)
        {
            var ornament = FindPetOrnament(id);
            if (ornament == null)
            {
                _player.SendNotice("配饰不存在");
                return;
            }
            ornament.Locked = !ornament.Locked;
            // 发送配饰信息
            await ornament.SendInfo();
            // _player.SendNotice(ornament.Locked ? "锁定成功" : "解锁成功");
        }
        public Attrs GetPetOrnamentAttr(uint petId)
        {
            var attrs = new Attrs();
            foreach (var (id, o) in this.PetOrnaments)
            {
                if (o.Place == petId)
                {
                    foreach (var a in o.Attrs)
                    {
                        attrs.Set(a.Key, attrs.Get(a.Key) + a.Value);
                    }
                }
            }
            return attrs;
        }
        public bool IsPetJxSkillActive(uint petId)
        {
            return this.GetPetJxSkillGrade(petId) > 0;
        }
        public uint GetPetJxSkillGrade(uint petId)
        {
            uint[] types = { 0, 0, 0 };
            foreach (var (id, o) in this.PetOrnaments)
            {
                if (o.Place == petId)
                {
                    types[o.Entity.TypeId - 1] = o.Grade;
                }
            }
            return Math.Min(Math.Min(types[0], types[1]), types[2]);
        }
        public PetOrnament FindPetOrnament(uint id)
        {
            PetOrnaments.TryGetValue(id, out var data);
            return data;
        }
        private List<PetOrnament> DelPetOrnament(List<uint> list, bool test = false)
        {
            List<PetOrnament> ornamentList = new();
            List<uint> ornamentIdList = new();
            foreach (var id in list)
            {
                PetOrnaments.TryGetValue(id, out var ornament);
                if (ornament == null) continue;
                // 如果是已经装备了的不能删除 只能丢弃背包里的配饰
                if (ornament.Place > 0) continue;
                ornamentList.Add(ornament);
                ornamentIdList.Add(id);
            }
            // 只是模拟删除？
            if (test) return ornamentList;
            // 从数据库中移除
            _ = DbService.DeleteEntity<PetOrnamentEntity>(ornamentIdList.ToArray());
            foreach (var ornament in ornamentList)
            {
                PetOrnaments.Remove(ornament.Id);
            }
            _ = _player.SendPacket(GameCmd.S2CPetOrnamentDel, new S2C_PetOrnamentDel { List = { ornamentIdList } });
            return ornamentList;
        }
        public async Task<PetOrnament> AddPetOrnament(uint pos, int grade = 0)
        {
            if (pos <= 0 || pos > 3) return null;
            if (grade == 0)
            {
                grade = 1;
                var rnd = _player.Random.Next(0, 100);
                if (rnd < 5) grade = 3;
                else if (rnd < 20) grade = 2;
            }

            var attrList = PetOrnament.GetOrnamentRecastAttrs(pos, grade);

            var entity = new PetOrnamentEntity
            {
                RoleId = _player.RoleId,
                Locked = false,
                TypeId = pos,
                Grade = (byte)grade,
                Place = 0,
                BaseAttrs = Ornament.FormatAttrPairs(attrList),
                Recast = "",
                CreateTime = TimeUtil.TimeStamp
            };
            await DbService.InsertEntity(entity);
            if (entity.Id == 0) return null;

            var ornament = new PetOrnament(_player, entity);
            PetOrnaments.Add(ornament.Id, ornament);

            attrList.Clear();

            await _player.SendPacket(GameCmd.S2CPetOrnamentAdd, new S2C_PetOrnamentAdd
            {
                Data = ornament.BuildPbData()
            });
            return ornament;
        }
    }
}