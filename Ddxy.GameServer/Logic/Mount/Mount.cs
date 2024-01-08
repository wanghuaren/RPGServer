using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Data.Vo;
using Ddxy.GameServer.Grains;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Mount
{
    public class Mount
    {
        private PlayerGrain _player;
        public MountEntity Entity { get; private set; }
        private MountEntity _lastEntity; //上一次更新的Entity

        public uint Id => Entity.Id;

        public uint CfgId => Entity.CfgId;

        public Attrs Attrs { get; private set; }

        public Dictionary<int, MountSkillData> Skills { get; set; }

        public List<uint> Pets { get; set; }

        private uint _expMax;
        private MountWashData _washData;

        public bool Active
        {
            get => Entity.Active;
            set => Entity.Active = value;
        }
        public bool Locked
        {
            get => Entity.Locked;
            set => Entity.Locked = value;
        }

        public Mount(PlayerGrain player, MountEntity entity)
        {
            _player = player;
            Entity = entity;
            _lastEntity = new MountEntity();
            _lastEntity.CopyFrom(Entity);

            Attrs = new Attrs();

            Pets = new List<uint>(3);
            InitSkills();
            InitPets();
            InitWashData();

            CalculateAttrs(false);
        }

        public async Task Destroy()
        {
            await SaveData(false);
            _lastEntity = null;
            Entity = null;
            _player = null;
            Attrs.Dispose();
            Attrs = null;
            _washData = null;
        }

        public async Task SaveData(bool copy = true)
        {
            if (Entity.Equals(_lastEntity)) return;
            var ret = await DbService.UpdateEntity(_lastEntity, Entity);
            if (ret && copy) _lastEntity.CopyFrom(Entity);
        }

        public MountData BuildPbData()
        {
            var pbData = new MountData
            {
                Id = Entity.Id,
                CfgId = Entity.CfgId,
                Name = Entity.Name,
                Level = Entity.Level,
                Exp = Entity.Exp,
                ExpMax = _expMax,
                Hp = Entity.Hp,
                Spd = Entity.Spd,
                Rate = Entity.Rate,
                RateMax = MountManager.GetMaxRate(Entity.CfgId),
                Skills = {Skills.Values},
                Pets = {Pets},
                Score = 992,
                Active = Entity.Active,
                Locked = Entity.Locked,
            };
            return pbData;
        }

        public Task SendInfo()
        {
            return _player.SendPacket(GameCmd.S2CMountInfo, new S2C_MountInfo {Data = BuildPbData()});
        }

        public async Task SendWashPreview()
        {
            if (_washData == null) return;
            await _player.SendPacket(GameCmd.S2CMountWashPreview, new S2C_MountWashPreview {Id = Id, Data = _washData});
        }

        public void UpdateCfg(uint cfgId, string name)
        {
            Entity.CfgId = cfgId;
            Entity.Name = name;
        }

        // 增加经验
        public async ValueTask<bool> AddExp(uint value, bool send = true)
        {
            if (value == 0) return false;
            const byte maxLevel = MountManager.MaxLevel;
            var maxExp = ConfigService.GetMountUpgradeExp(Entity.Level);
            if (Entity.Level >= maxLevel && Entity.Exp >= maxExp)
            {
                // 超过本次转生的最大等级
                _player.SendNotice("超过最大经验值");
                return false;
            }

            var finalExp = Entity.Exp + value; //最终的经验
            var finalLevel = Entity.Level; //最终的等级
            //当前等级的最大经验值
            while (finalExp >= maxExp)
            {
                if (finalLevel + 1 > maxLevel) break;
                finalExp -= maxExp;
                finalLevel += 1;
                // 计算新等级的最大经验值
                maxExp = ConfigService.GetMountUpgradeExp(finalLevel);
            }

            if (finalLevel > maxLevel)
            {
                finalLevel = maxLevel;
                finalExp = ConfigService.GetMountUpgradeExp(finalLevel);
            }

            _expMax = ConfigService.GetMountUpgradeExp(finalLevel);
            if (finalExp >= _expMax) finalExp = _expMax;
            Entity.Exp = finalExp;

            if (finalLevel != Entity.Level)
            {
                Entity.Level = finalLevel;
                CalculateAttrs();
            }

            if (send) await SendInfo();
            return true;
        }

        /// <summary>
        /// 定制
        /// </summary>
        public async Task Dingzhi(List<uint> skillIdList)
        {
            if (_player.Entity.Relive == 0)
            {
                _player.SendNotice("1转后开放此功能");
                return;
            }
            if (Locked)
            {
                _player.SendNotice("请先解锁");
                return;
            }
            if (skillIdList.Count != 3)
            {
                _player.SendNotice("请先选择3个技能");
                return;
            }
            uint costItemId = 500054;
            int costItemNum = 1;
            if (_player.GetBagItemNum(costItemId) < costItemNum)
            {
                _player.SendNotice("定制卷数量不足");
                return;
            }
            // 检查指定cfgId能否用在这里
            ConfigService.Mounts.TryGetValue(CfgId, out var mountCfg);
            if (mountCfg == null)
            {
                _player.SendNotice("配置错误1，请稍候再试");
                return;
            }
            ConfigService.MountGroupedSkills.TryGetValue(mountCfg.Type, out var groupedSkills);
            var newSkills = new Dictionary<int, MountSkillData>(3);
            for (var grid = 1; grid <= skillIdList.Count && grid <= 3; grid++)
            {
                var skillId = skillIdList[grid - 1];
                List<Data.Config.MountSkillConfig> list = null;
                if (CfgId == 12345)
                {
                    ConfigService.MountGroupedPSkills.TryGetValue(grid, out var list1);
                    list = list1;
                }
                else
                {
                    groupedSkills.TryGetValue(grid, out var list1);
                    list = list1;
                }
                if (list == null || list.Find(s => s.Id == skillId) == null)
                {
                    _player.SendNotice("位置1配置错误");
                    return;
                }
                var sk = new MountSkillData() { CfgId = skillId, Exp = 0, ExpMax = 20000, Level = 1 };
                // 保留熟练度
                if (Skills.TryGetValue(grid, out var oldSk))
                {
                    sk.Exp = oldSk.Exp;
                    sk.ExpMax = oldSk.ExpMax;
                }

                newSkills.Add(grid, sk);
            }
            // 消耗定制卷
            var ret = await _player.AddBagItem(costItemId, -costItemNum, tag: $"定制坐骑{Entity.Id}");
            if (!ret)
            {
                _player.SendNotice("定制卷数量不足");
                return;
            }

            Entity.Rate = (uint)MathF.Floor(mountCfg.Rate[1] * 10000);
            Entity.Spd = mountCfg.Spd[1];
            Skills = newSkills;
            SyncSkills();
            CalculateAttrs();

            await SendInfo();

            _player.SendNotice("定制成功");
        }

        /// <summary>
        /// 洗练
        /// </summary>
        public async Task Wash()
        {
            if (_player.Entity.Relive == 0)
            {
                _player.SendNotice("1转后开放此功能");
                return;
            }
            if (Locked)
            {
                _player.SendNotice("请先解锁");
                return;
            }

            // 消耗3个净瓶玉露
            var ret = await _player.AddBagItem(100001, -3, tag: $"洗练坐骑{Entity.Id}");
            if (!ret) return;
            _washData = MountManager.RandomWashData(Entity.CfgId);
            SyncWashData();
            await _player.SendPacket(GameCmd.S2CMountWash, new S2C_MountWash {Id = Id, Data = _washData});
        }

        public async Task SaveWash()
        {
            if (_player.Entity.Relive == 0)
            {
                _player.SendNotice("1转后开放此功能");
                return;
            }
            if (Locked)
            {
                _player.SendNotice("请先解锁");
                return;
            }

            if (_washData == null) return;

            Entity.Rate = _washData.Rate;
            Entity.Spd = _washData.Spd;

            var newSkills = new Dictionary<int, MountSkillData>(_washData.Skills.Count);
            for (var i = 0; i < _washData.Skills.Count; i++)
            {
                // 保留熟练度
                var sk = _washData.Skills[i];
                if (Skills.TryGetValue(i + 1, out var oldSk))
                {
                    sk.Exp = oldSk.Exp;
                    sk.ExpMax = oldSk.ExpMax;
                }

                newSkills.Add(i + 1, sk);
            }

            Skills = newSkills;
            SyncSkills();
            CalculateAttrs();

            await SendInfo();

            _washData = null;
            SyncWashData();
        }

        public async Task ControlPet(uint petId, bool add)
        {
            if (_player.Entity.Relive == 0)
            {
                _player.SendNotice("1转后开放此功能");
                return;
            }
            if (Locked)
            {
                _player.SendNotice("请先解锁");
                return;
            }

            Pet.Pet pet;
            if (add)
            {
                // 检查宠物是否存在
                pet = _player.PetMgr.FindPet(petId);
                if (pet == null)
                {
                    _player.SendNotice("宠物不存在");
                    return;
                }

                // 检查宠物是否已经被管制
                var mount = _player.MountMgr.FindWhoControlPet(petId);
                if (mount != null)
                {
                    await mount.ControlPet(petId, false);
                }

                Pets.Add(petId);
            }
            else
            {
                var idx = Pets.IndexOf(petId);
                if (idx < 0)
                {
                    _player.SendNotice("宠物没有被该坐骑管制");
                    return;
                }

                pet = _player.PetMgr.FindPet(Pets[idx]);
                Pets.RemoveAt(idx);
            }

            SyncPets();
            await _player.SendPacket(GameCmd.S2CMountControl, new S2C_MountControl
            {
                Id = Entity.Id,
                PetId = petId,
                Add = add
            });

            // 通知pet刷新属性
            if (pet != null)
            {
                await pet.RefreshAttrs();
            }
        }

        public async Task UpgradeSkill(int grid)
        {
            if (Locked)
            {
                _player.SendNotice("请先解锁");
                return;
            }
            Skills.TryGetValue(grid, out var sk);
            if (sk == null)
            {
                _player.SendNotice("指定索引无效");
                return;
            }

            if (sk.Exp >= sk.ExpMax)
            {
                _player.SendNotice("该技能熟练度已满");
                return;
            }

            // 扣除道具, 丹书秘券100000
            const uint cfgId = 100000;
            var ret = await _player.AddBagItem(cfgId, -3, tag: "升级坐骑技能");
            if (!ret) return;

            sk.Exp += 1000;
            if (sk.Exp >= sk.ExpMax) sk.Exp = sk.ExpMax;
            SyncSkills();

            CalculateSkillAttrs(sk);

            await _player.SendPacket(GameCmd.S2CMountUpgradeSkill, new S2C_MountUpgradeSkill
            {
                Id = Entity.Id,
                Grid = grid,
                Skill = sk
            });

            // 通知pet刷新属性
            if (Active)
            {
                foreach (var pid in Pets)
                {
                    var pet = _player.PetMgr.FindPet(pid);
                    if (pet != null)
                        await pet.RefreshAttrs();
                }
            }
        }

        public async ValueTask<bool> SetSkill(int idx, uint cfgId, byte level, uint skExp)
        {
            Skills.TryGetValue(idx, out var skData);
            if (skData == null) return false;
            // 检查指定cfgId能否用在这里
            ConfigService.Mounts.TryGetValue(CfgId, out var mountCfg);
            if (mountCfg == null) return false;
            if (CfgId != 12345) {
            ConfigService.MountGroupedSkills.TryGetValue(mountCfg.Type, out var dic);
            if (dic == null) return false;
            dic.TryGetValue(idx, out var list);
            if (list == null) return false;
            if (list.FindIndex(p => p.Id == cfgId) < 0) return false;
            }
            skData.Level = level;
            skData.CfgId = cfgId;
            skData.Exp = Math.Min(skExp, skData.ExpMax);
            SyncSkills();
            CalculateAttrs();
            
            if (_player.IsEnterServer)
            {
                await SendInfo();
            }

            return true;
        }

        private void InitSkills()
        {
            Skills = new Dictionary<int, MountSkillData>(3);
            if (!string.IsNullOrWhiteSpace(Entity.Skills))
            {
                var list = Json.Deserialize<List<MountSkillVo>>(Entity.Skills);
                for (var i = 0; i < list.Count; i++)
                {
                    var vo = list[i];
                    var msd = new MountSkillData
                    {
                        CfgId = vo.Id,
                        Exp = vo.Exp,
                        ExpMax = 20000,
                        Level = vo.Level
                    };

                    ConfigService.MountSkills.TryGetValue(msd.CfgId, out var cfg);
                    if (cfg == null) continue;
                    Skills.Add(i + 1, msd);
                }
            }
        }

        private void SyncSkills()
        {
            var list = new List<MountSkillVo>();
            foreach (var msd in Skills.Values)
            {
                list.Add(new MountSkillVo(msd));
            }

            Entity.Skills = Json.Serialize(list);
        }

        private void InitPets()
        {
            Pets = new List<uint>(3);
            if (!string.IsNullOrWhiteSpace(Entity.Pets))
            {
                var list = Json.Deserialize<List<uint>>(Entity.Pets);
                foreach (var pid in list)
                {
                    if (_player.PetMgr.FindPet(pid) != null)
                    {
                        Pets.Add(pid);
                    }
                }
            }
        }

        private void SyncPets()
        {
            if (Pets == null || Pets.Count == 0)
            {
                Entity.Pets = string.Empty;
                return;
            }

            Entity.Pets = Json.Serialize(Pets);
        }

        private void CalculateAttrs(bool checkPetAttrs = true)
        {
            // Attrs.Clear();
            // var level = _entity.Level;
            // var rate = _entity.Rate / 10000f;

            // var calcHp = MathF.Round(level * rate * level +
            //                          0.7f * GetBaseProperty(AttrType.Hp) * level * rate + GetBaseProperty(AttrType.Hp));
            // var calcSpd =
            //     MathF.Round(level * rate + GetBaseProperty(AttrType.Spd));

            // Attrs.Set(AttrType.Hp, calcHp);
            // Attrs.Set(AttrType.Spd, calcSpd);

            _expMax = ConfigService.GetMountUpgradeExp(Entity.Level);

            foreach (var sk in Skills.Values)
            {
                CalculateSkillAttrs(sk);
            }

            // 通知pet刷新属性
            if (checkPetAttrs && _player.PetMgr != null)
            {
                foreach (var pid in Pets)
                {
                    var pet = _player.PetMgr.FindPet(pid);
                    if (pet != null)
                        _ = pet.RefreshAttrs();
                }
            }
        }

        // 根据坐骑等级和技能熟练度来计算坐骑技能的属性值
        private void CalculateSkillAttrs(MountSkillData sk)
        {
            if (sk == null) return;
            sk.Attrs.Clear();
            ConfigService.MountSkills.TryGetValue(sk.CfgId, out var cfg);
            if (cfg == null || sk.Level < 0 || sk.Level >= cfg.Attrs2.Length) return;
            var attrs = cfg.Attrs2[sk.Level];
            var rate = Entity.Rate / 10000f;
            foreach (var (k, v) in attrs)
            {
                var attrType = (AttrType) k;
                float attrValue;
                if (attrType == AttrType.Spd)
                {
                    attrValue = (v + v * rate) * (1 + Entity.Level * 0.025f + sk.Exp * 5.0f / sk.ExpMax);
                }
                else
                {
                    attrValue = (v + v * rate) * (1 + Entity.Level * 0.025f + sk.Exp * 7.5f / sk.ExpMax);
                }

                sk.Attrs.Add(new AttrPair {Key = attrType, Value = attrValue});
            }
        }

        private void InitWashData()
        {
            _washData = null;

            if (!string.IsNullOrWhiteSpace(Entity.WashData))
            {
                _washData = Json.SafeDeserialize<MountWashData>(Entity.WashData);
            }
        }

        private void SyncWashData()
        {
            if (_washData == null)
            {
                Entity.WashData = string.Empty;
                return;
            }

            Entity.WashData = Json.SafeSerialize(_washData);
        }
    }
}