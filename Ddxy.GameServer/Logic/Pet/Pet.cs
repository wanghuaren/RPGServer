using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Config;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.GameServer.Logic.Battle.Skill;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Pet
{
    public class Pet
    {
        private PlayerGrain _player;

        public PetEntity Entity { get; private set; }
        private PetEntity _lastEntity; //上一次更新的Entity

        public uint Id => Entity.Id;

        public uint CfgId => Entity.CfgId;

        public PetConfig Cfg { get; private set; }

        public bool Active
        {
            get => Entity.Active;
            set => Entity.Active = value;
        }

        public uint SxOrder
        {
            get => Entity.SxOrder;
            set => Entity.SxOrder = value;
        }

        /// <summary>
        /// 宠物属性
        /// </summary>
        public Attrs Attrs { get; private set; }

        /// <summary>
        /// 加点属性
        /// </summary>
        public Attrs ApAttrs { get; private set; }

        /// <summary>
        /// 修炼属性
        /// </summary>
        public Attrs RefineAttrs { get; private set; }

        /// <summary>
        /// 当前剩余的潜能
        /// </summary>
        public uint Potential { get; private set; }

        public string Name => Cfg.Name;

        private List<PetSkillData> _skills; //已经学习的技能
        private ulong _expMax; //当前最大经验值

        private PetWashData _washData;

        // 最大熟练度
        private const uint MaxIntimacy = 2000000000U;

        // 天策符 对宠物成长率 提升
        private uint _tiancefuRateAddon = 0;

        // 激活的觉醒技能品阶
        private int _activeJxSkillGrade = 0;
        // 激活的觉醒技能等级
        private uint _activeJxSkillLevel = 0;

        public Pet(PlayerGrain player, PetEntity entity, bool autoStart = true)
        {
            _player = player;
            Entity = entity;
            _lastEntity = new PetEntity();
            _lastEntity.CopyFrom(Entity);

            ConfigService.Pets.TryGetValue(Entity.CfgId, out var xcfg);
            Cfg = xcfg ?? throw new Exception($"Pet({Entity.CfgId}) not exists in pet.json");

            Attrs = new Attrs();
            ApAttrs = new Attrs(Entity.ApAttrs);
            RefineAttrs = new Attrs(Entity.RefineAttrs);
            InitWashData();

            // 修复宠物属性
            if (Entity.Hp > Cfg.Hp[1])
            {
                Entity.Hp = Cfg.Hp[1];
            }

            if (Entity.Mp > Cfg.Mp[1])
            {
                Entity.Mp = Cfg.Mp[1];
            }

            if (Entity.Atk > Cfg.Atk[1])
            {
                Entity.Atk = Cfg.Atk[1];
            }

            if (Entity.Spd > Cfg.Spd[1])
            {
                Entity.Spd = Cfg.Spd[1];
            }

            // 修正等级和经验
            if (Entity.Relive > 4) Entity.Relive = 4;
            var maxLevel = ConfigService.GetPetMaxLevel(Entity.Relive);
            var minLevel = ConfigService.GetPetMinLevel(Entity.Relive);
            Entity.Level = Math.Clamp(Entity.Level, minLevel, maxLevel);
            _expMax = ConfigService.GetPetUpgradeExp(Entity.Relive, Entity.Level);
            if (Entity.Exp > _expMax) Entity.Exp = _expMax;

            if (autoStart) Start();
        }

        public void Start()
        {
            // 解析技能
            InitSkills();
            CheckPotential();
            CheckRefinePoint();
            CalculateAttrs();
        }

        public async Task Destroy()
        {
            await SaveData(false);
            _lastEntity = null;
            Entity = null;
            _player = null;
            Cfg = null;

            ApAttrs.Dispose();
            ApAttrs = null;
            RefineAttrs.Dispose();
            RefineAttrs = null;
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

        public PetData BuildPbData()
        {
            var pbData = new PetData
            {
                Id = Entity.Id,
                CfgId = Entity.CfgId,
                Name = Entity.Name,
                Relive = Entity.Relive,
                Level = Entity.Level,
                Exp = Entity.Exp,
                ExpMax = _expMax,

                Hp = Entity.Hp,
                HpMax = Cfg.Hp[1],
                Mp = Entity.Mp,
                MpMax = Cfg.Mp[1],
                Atk = Entity.Atk,
                AtkMax = Cfg.Atk[1],
                Spd = Entity.Spd,
                SpdMax = Cfg.Spd[1],
                Rate = GetCurRate(),
                RateMax = GetMaxRate(),

                Intimacy = Entity.Intimacy,
                Keel = Entity.Keel,
                MaxSkillNum = MaxSkillNum,
                Skills = {_skills},
                SsSkill = (SkillId) Entity.SsSkill,
                Fly = Entity.Fly,
                Color = Entity.Color,
                Active = Entity.Active,
                Score = 0,
                Quality = (PetQuality) Entity.Quality,

                Potential = Potential,
                ApAttrs = {ApAttrs.ToList()},

                RefineLevel = Entity.RefineLevel,
                RefineExp = Entity.RefineExp,
                RefineExpMax = PetRefine.GetMaxRefineExp(Entity.RefineLevel),
                RefineAttrs = {RefineAttrs.ToList()},

                Attrs = {Attrs.ToList()},

                AutoSkill = Entity.AutoSkill,

                JxLevel = Entity.JxLevel,
            };
            // 记录激活的觉醒技能品阶
            _activeJxSkillGrade = (int)_player.EquipMgr.GetPetJxSkillGrade(Id);
            pbData.JxSkill = _activeJxSkillGrade > 0 ? (SkillId)Cfg.JxSkill : SkillId.Unkown;
            // 记录激活的觉醒技能等级
            _activeJxSkillLevel = pbData.JxSkill != SkillId.Unkown ? pbData.JxLevel : 0;
            // 飞升影响属性和最大属性
            switch (Entity.Fly / 10)
            {
                case 1:
                    pbData.Hp += 60;
                    pbData.HpMax += 60;
                    break;
                case 2:
                    pbData.Mp += 60;
                    pbData.MpMax += 60;
                    break;
                case 3:
                    pbData.Atk += 60;
                    pbData.AtkMax += 60;
                    break;
                case 4:
                    pbData.Spd += 60;
                    pbData.SpdMax += 60;
                    break;
            }

            return pbData;
        }

        public Task SendInfo()
        {
            return _player.SendPacket(GameCmd.S2CPetInfo, new S2C_PetInfo {Data = BuildPbData()});
        }

        public async Task SendWashPreview()
        {
            if (_washData == null) return;

            var washData = _washData.Clone();
            switch (Entity.Fly / 10)
            {
                case 1:
                    washData.Hp += 60;
                    washData.HpMax += 60;
                    break;
                case 2:
                    washData.Mp += 60;
                    washData.MpMax += 60;
                    break;
                case 3:
                    washData.Atk += 60;
                    washData.AtkMax += 60;
                    break;
                case 4:
                    washData.Spd += 60;
                    washData.SpdMax += 60;
                    break;
            }

            washData.Rate += GetAddRate();
            washData.RateMax = GetMaxRate(); // 元气丹可以影响最大成长率

            await _player.SendPacket(GameCmd.S2CPetWashPreview, new S2C_PetWashPreview {Id = Id, Data = washData});
        }

        // 分享的时候缓存
        public async Task Cache()
        {
            var bytes = Packet.Serialize(BuildPbData());
            await RedisService.SetPetInfo(Entity.Id, bytes);
        }

        public BattleMemberData BuildBattleMemberData()
        {
            var data = new BattleMemberData
            {
                Type = LivingThingType.Pet,
                Pos = 1,
                OwnerId = _player.RoleId,

                Id = Entity.Id,
                CfgId = Entity.CfgId,
                Name = Entity.Name,
                Res = Cfg.Res,
                Relive = Entity.Relive,
                Level = Entity.Level,
                PetColor = Entity.Color,

                PetSsSkill = (SkillId) Entity.SsSkill,
                PetIntimacy = Entity.Intimacy,
                SxOrder = Entity.SxOrder,
                DefSkillId = (SkillId) Entity.AutoSkill
            };
            // 觉醒技能及套装效果
            if (_activeJxSkillGrade > 0)
            {
                data.PetJxSkill = (SkillId)Cfg.JxSkill;
                data.PetJxLevel = Entity.JxLevel;
                data.PetJxGrade = (uint)_activeJxSkillGrade;
            }

            foreach (var sk in _skills)
            {
                if (sk.Unlock && sk.Id != SkillId.Unkown)
                    data.Skills.Add((uint) sk.Id, 0);
            }

            foreach (var (k, v) in Attrs)
            {
                if (v != 0) data.Attrs.Add(new AttrPair {Key = k, Value = v});
            }
            // 战斗中天策符
            var level = _player.Tiance.level;
            foreach (var f in _player.TianceFuInBattle)
            {
                data.TianceSkillList.Add(new BattleTianceSkill() { SkillId = f.SkillId, Addition = f.Addition, TianYanCeLevel = level });
            }

            return data;
        }

        // 增加经验
        public async ValueTask<bool> AddExp(ulong value, bool send = true)
        {
            if (value == 0) return false;
            var maxLevel = ConfigService.GetPetMaxLevel(Entity.Relive);
            if (Entity.Relive >= _player.Entity.Relive)
            {
                // 最大不超过角色等级+10
                maxLevel = Math.Min(maxLevel, (byte)(_player.Entity.Level + 10));
            }

            var maxExp = ConfigService.GetPetUpgradeExp(Entity.Relive, Entity.Level);
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
                finalExp -= maxExp;
                finalLevel += 1;
                if (finalLevel > maxLevel) break;
                // 计算新等级的最大经验值
                maxExp = ConfigService.GetPetUpgradeExp(Entity.Relive, finalLevel);
            }

            if (finalLevel > maxLevel)
            {
                finalLevel = maxLevel;
                finalExp = ConfigService.GetPetUpgradeExp(Entity.Relive, finalLevel);
            }

            _expMax = ConfigService.GetPetUpgradeExp(Entity.Relive, finalLevel);

            if (finalExp >= _expMax) finalExp = _expMax;
            Entity.Exp = finalExp;

            if (send) _player.SendNotice($"获得 {value} 宠物经验");
            if (finalLevel != Entity.Level)
            {
                Entity.Level = finalLevel;
                if (send) _player.SendNotice($"您的宠物{Entity.Name}升到{Entity.Level}级");

                CheckPotential();
                CalculateAttrs();
            }

            if (send) await SendInfo();
            return true;
        }

        // 执行加点
        public async Task AddPoint(bool reset, IList<AttrPair> pairs)
        {
            if (reset)
            {
                ApAttrs.Clear();
            }
            else
            {
                if (pairs == null || pairs.Count == 0) return;

                // 检测总消耗
                var total = (uint) pairs.Sum(p => p.Value);
                if (total <= 0 || total > Potential)
                {
                    _player.SendNotice("潜能不足");
                    return;
                }

                var ret = false;
                foreach (var pair in pairs)
                {
                    if (pair.Value > 0 && GameDefine.ApAttrs.ContainsKey(pair.Key))
                    {
                        ApAttrs.Add(pair.Key, pair.Value);
                        ret = true;
                    }
                }

                if (!ret) return;
            }

            // 同步给Entity, 会自动入库
            Entity.ApAttrs = ApAttrs.ToJson();
            // 重新计算潜能点
            CheckPotential();
            CalculateAttrs();

            await _player.SendPacket(GameCmd.S2CPetAddPoint, new S2C_PetAddPoint
            {
                Id = Entity.Id,
                Potential = Potential,
                ApAttrs = {ApAttrs.ToList()},
                Attrs = {Attrs.ToList()}
            });
        }

        // 修炼
        public async Task Refine(bool reset, IList<AttrPair> pairs)
        {
            if (reset)
            {
                RefineAttrs.Clear();
            }
            else
            {
                if (pairs == null || pairs.Count == 0) return;

                var total = 0f;
                var dic = new Dictionary<AttrType, float>();
                foreach (var pair in pairs)
                {
                    if (pair.Value <= 0 || !PetRefine.MaxAttrValues.ContainsKey(pair.Key)) continue;
                    // 不能超过最大值
                    var maxV = PetRefine.GetMaxAttrValue(Entity.Relive, pair.Key);
                    if (pair.Value + RefineAttrs.Get(pair.Key) > maxV) continue;

                    total += pair.Value;
                    dic[pair.Key] = pair.Value;
                }

                if (dic.Count == 0 || total <= 0) return;

                // 检测总修炼点
                total += RefineAttrs.Values.Sum();
                if (total > PetRefine.GetLevelPoint(Entity.Relive, Entity.RefineLevel))
                {
                    _player.SendNotice("修炼点不足");
                    return;
                }

                foreach (var (k, v) in dic)
                {
                    RefineAttrs.Add(k, v);
                }
            }

            // 同步给Entity, 会自动入库
            Entity.RefineAttrs = RefineAttrs.ToJson();
            // 重新计算属性
            CalculateAttrs();

            await _player.SendPacket(GameCmd.S2CPetRefine, new S2C_PetRefine
            {
                Id = Entity.Id,
                RefineLevel = Entity.RefineLevel,
                RefineExp = Entity.RefineExp,
                RefineExpMax = PetRefine.GetMaxRefineExp(Entity.RefineLevel),
                RefineAttrs = {RefineAttrs.ToList()},

                Attrs = {Attrs.ToList()}
            });
        }

        public async Task ChangeName(string newName)
        {
            if (string.IsNullOrWhiteSpace(newName)) return;
            newName = newName.Trim();
            if (newName.Length > 6)
            {
                _player.SendNotice("宠物名字不能超过6个字符");
                return;
            }

            if (!TextFilter.CheckLimitWord(newName))
            {
                _player.SendNotice("宠物名称中包含非法字符");
                return;
            }

            newName = TextFilter.Filte(newName);
            Entity.Name = newName;
            await _player.SendPacket(GameCmd.S2CPetName, new S2C_PetName {Id = Id, Name = newName});
        }

        // 增加亲密度
        public bool AddIntimacy(uint value, bool send = true)
        {
            if (Entity.Intimacy >= MaxIntimacy) return false;
            var left = MaxIntimacy - Entity.Intimacy;
            if (value >= left) value = left;
            Entity.Intimacy += value;
            CalculateAttrs();
            if (send) SendInfo();
            return true;
        }

        // 使用根骨
        public bool AddKeel()
        {
            if (Entity.Keel < PetManager.GetMaxKeel(Entity.Relive))
            {
                Entity.Keel += 1;
                CheckPotential();
                CalculateAttrs();
                SendInfo();
                return true;
            }

            return false;
        }

        /// <summary>
        /// 使用凝魂丹, 增加修炼等级
        /// </summary>
        /// <returns></returns>
        public async ValueTask<bool> UseNingHunDan(uint exp)
        {
            var maxRefineLevel = PetRefine.GetMaxRefineLevel(Entity.Relive);
            var maxRefineExp = PetRefine.GetMaxRefineExp(Entity.RefineLevel);
            if (Entity.RefineLevel >= maxRefineLevel && Entity.RefineExp >= maxRefineExp)
            {
                Entity.RefineLevel = maxRefineLevel;
                Entity.RefineExp = maxRefineExp;
                _player.SendNotice("修炼等级到达上限");
                return false;
            }

            Entity.RefineExp += exp;
            while (Entity.RefineExp >= maxRefineExp)
            {
                Entity.RefineExp -= maxRefineExp;
                Entity.RefineLevel++;
                if (Entity.RefineLevel > maxRefineLevel) break;
                maxRefineExp = PetRefine.GetMaxRefineExp(Entity.RefineLevel);
            }

            if (Entity.RefineLevel > maxRefineLevel)
            {
                Entity.RefineLevel = maxRefineLevel;
                maxRefineExp = PetRefine.GetMaxRefineExp(Entity.RefineLevel);
                Entity.RefineExp = maxRefineExp;
            }
            else
            {
                maxRefineExp = PetRefine.GetMaxRefineExp(Entity.RefineLevel);
                Entity.RefineExp = Math.Min(Entity.RefineExp, maxRefineExp);
            }

            await SendInfo();
            return true;
        }

        /// <summary>
        /// 经验转魂魄
        /// <param name="expNum">多少点经验换取1点魂魄</param>
        /// </summary>
        public async ValueTask<bool> UseExp2HunPo(int expNum)
        {
            var maxRefineLevel = PetRefine.GetMaxRefineLevel(Entity.Relive);
            var maxRefineExp = PetRefine.GetMaxRefineExp(Entity.RefineLevel);
            if (Entity.RefineLevel >= maxRefineLevel && Entity.RefineExp >= maxRefineExp)
            {
                _player.SendNotice("修炼等级到达上限");
                return false;
            }

            // 由于Pet的Exp是ulong，这里先别急着直接转uint, 可能会导致数据溢出
            var refineExp = Entity.RefineExp + (ulong) MathF.Floor(Entity.Exp * 1.0f / expNum);
            Entity.Exp %= (ulong) expNum;
            while (refineExp >= maxRefineExp)
            {
                refineExp -= maxRefineExp;
                Entity.RefineLevel++;
                if (Entity.RefineLevel > maxRefineLevel) break;
                maxRefineExp = PetRefine.GetMaxRefineExp(Entity.RefineLevel);
            }

            if (Entity.RefineLevel > maxRefineLevel)
            {
                // 返还剩余经验
                Entity.Exp += (Entity.RefineLevel - maxRefineLevel) * (ulong) expNum;
                Entity.RefineLevel = maxRefineLevel;
                refineExp = PetRefine.GetMaxRefineExp(Entity.RefineLevel);
            }

            // 这里就可以放心转uint了，因为PetRefine的RefineExps中没有超过uint最大值的经验
            Entity.RefineExp = (uint) refineExp;

            await SendInfo();
            return true;
        }

        /// <summary>
        /// 使用元气丹, 变色
        /// </summary>
        public bool UseYuanQiDan(float rate)
        {
            // 能洗颜色的概率 
            var washRate = 100;
            if (Entity.Color == 0)
            {
                // 第一次吃元气丹, 有10%的概率能洗出颜色
                washRate = 10;
                Entity.Color = -1;

                // 第一次修改，因为成长率会增加，这里计算一次属性即可, 其他次变色并不会再叠加成长率, 所以不用计算属性
                CalculateAttrs();
            }

            if (_player.Random.Next(0, 100) < washRate || GetCurRate() == GetMaxRate())
            {
                Entity.Color = ChangeColor();
            }

            SendInfo();
            return true;
        }

        /// <summary>
        /// 洗练
        /// </summary>
        public async Task Wash()
        {
            // 消耗3个金柳露
            var ret = await _player.AddBagItem(10118, -3, tag: $"洗练宠物{Id}");
            if (!ret) return;

            var washData = PetManager.RandomWashData(Entity.CfgId);
            // 保存的属性不能+60 
            _washData = washData.Clone();
            SyncWashData();

            switch (Entity.Fly / 10)
            {
                case 1:
                    washData.Hp += 60;
                    washData.HpMax += 60;
                    break;
                case 2:
                    washData.Mp += 60;
                    washData.MpMax += 60;
                    break;
                case 3:
                    washData.Atk += 60;
                    washData.AtkMax += 60;
                    break;
                case 4:
                    washData.Spd += 60;
                    washData.SpdMax += 60;
                    break;
            }

            washData.Rate += GetAddRate();
            washData.RateMax = GetMaxRate(); // 元气丹可以影响最大成长率

            await _player.SendPacket(GameCmd.S2CPetWash, new S2C_PetWash {Id = Id, Data = washData});
        }

        public async Task SaveWash()
        {
            if (_washData == null) return;

            Entity.Rate = _washData.Rate;
            Entity.Hp = _washData.Hp;
            Entity.Mp = _washData.Mp;
            Entity.Atk = _washData.Atk;
            Entity.Spd = _washData.Spd;
            Entity.Quality = (byte) _washData.Quality;

            CalculateAttrs();
            await SendInfo();

            _washData = null;
            SyncWashData();
        }

        public async Task Relive()
        {
            if (Entity.Relive == 4)
            {
                _player.SendNotice("已到最高转生等级");
                return;
            }

            if (Entity.Level < ConfigService.GetPetMaxLevel(Entity.Relive))
            {
                _player.SendNotice("等级不足,无法转生");
                return;
            }

            var nextRelive = (byte) (Entity.Relive + 1);

            // 飞升，必须等角色飞升
            if (nextRelive >= 4 && nextRelive > _player.Entity.Relive)
            {
                _player.SendNotice("角色尚未飞升，请先飞升角色");
                return;
            }

            var maxRate = GetMaxRate();
            if (Entity.Rate > maxRate)
                Entity.Rate = maxRate;
            Entity.Relive = nextRelive;
            // 转生前的等级经验可以用
            var oldExp = Entity.Exp;
            Entity.Level = ConfigService.GetPetMinLevel(Entity.Relive);
            Entity.Exp = 0;
            _expMax = ConfigService.GetPetUpgradeExp(Entity.Relive, Entity.Level);
            if (oldExp > 0) await AddExp(oldExp, false);

            // 清空加点
            ApAttrs.Clear();
            Entity.ApAttrs = ApAttrs.ToJson();
            CheckPotential();
            CalculateAttrs();
            await SendInfo();

            _player.SendNotice($"您的召唤兽{Entity.Name} {Entity.Relive}转成功！");
        }

        public async Task UnlockSkill()
        {
            // 获取已经解锁的技能格子数量
            var unLockNum = _skills.Count(p => p.Unlock);

            // 检查是否已经全部解锁
            if (unLockNum >= _skills.Count)
            {
                _player.SendNotice("该宠物所有技能格均已解锁");
                return;
            }

            var idx = _skills.FindIndex(p => !p.Unlock);
            if (idx < 0)
            {
                _player.SendNotice("该宠物所有技能格均已解锁");
                return;
            }

            // 检查 聚魂丹10115, 1,1,1,2,2,4,8,16,32,64  从第5颗开始2的n次方
            int jhdNum;
            if (unLockNum <= 2) jhdNum = 1;
            else if (unLockNum == 3) jhdNum = 2;
            else jhdNum = (int) Math.Pow(2, unLockNum - 3);

            var ret = await _player.AddBagItem(10115, -jhdNum, tag: "解锁宠物技能格");
            if (!ret) return;

            // 解锁新格子
            var skill = _skills[idx];
            skill.Unlock = true;
            skill.Id = SkillId.Unkown;
            skill.Lock = false;
            SyncSkills();

            await _player.SendPacket(GameCmd.S2CPetUnlock, new S2C_PetUnlock
            {
                Id = Entity.Id,
                Index = idx
            });
        }

        /// <summary>
        /// 学习技能
        /// </summary>
        public async Task LearnSkill(int index, uint itemId)
        {
            if (index < 0 || index >= _skills.Count) return;
            var skData = _skills[index];
            if (!skData.Unlock)
            {
                _player.SendNotice("该位置尚未解锁");
                return;
            }

            if (skData.Id != SkillId.Unkown)
            {
                _player.SendNotice("该位置已经学习过技能");
                return;
            }

            // 检查道具id是否为技能书
            if ((int) MathF.Floor(itemId / 1000f) != 60) return;
            ConfigService.Items.TryGetValue(itemId, out var itemCfg);
            if (itemCfg == null || itemCfg.Type != 10) return;
            if (_player.GetBagItemNum(itemId) <= 0)
            {
                _player.SendNotice("技能书数量不足");
                return;
            }

            // 从技能书中获取到技能id
            var skId = (SkillId) itemCfg.Num;
            var skill = SkillManager.GetSkill(skId);
            if (skill == null)
            {
                _player.SendNotice($"技能{(uint) skId}不存在");
                return;
            }

            // 如果已经学过了
            if (_skills.Exists(p => p.Id == skId))
            {
                _player.SendNotice("已经学习过该技能");
                return;
            }

            // 不能为神兽技能
            if (itemCfg.Num is >= 3001 and <= 3006)
            {
                _player.SendNotice("该技能为神兽技");
                return;
            }

            // 宠物的自带技能
            var selfSkillId = Cfg.Skill;
            // 本次遗忘的技能索引和概率
            var forgetIndex = -1;
            var forgetRate = 0;

            // 相同类型的技能自动遗忘低品阶的
            for (var i = _skills.Count - 1; i >= 0; i--)
            {
                // 自带技能不能被遗忘
                if ((uint) _skills[i].Id == selfSkillId) continue;
                var info = SkillManager.GetSkill(_skills[i].Id);
                if (info != null && info.Kind == skill.Kind)
                {
                    // 同类型的比品质
                    if (info.Quality > skill.Quality)
                    {
                        // 说明已经学习了品质跟高的同类技能
                        _player.SendNotice($"该宠物已经学习过同类更高品级的技能:{info.Name}");
                        return;
                    }

                    // 遗忘低品质的技能
                    forgetIndex = i;
                    forgetRate = 100;
                    break;
                }
            }

            if (forgetIndex < 0)
            {
                // 第一个技能始终不能被遗忘
                for (var i = _skills.Count - 1; i >= 1; i--)
                {
                    var sk = _skills[i];
                    if (sk.Unlock && !sk.Lock && sk.Id != SkillId.Unkown && (uint) sk.Id != selfSkillId)
                    {
                        forgetIndex = i;
                        // 越后面的技能越容易被遗忘
                        forgetRate = (int) MathF.Floor(i * 100f / (MaxSkillNum - 1));
                        break;
                    }
                }
            }

            // 扣除道具
            var ret = await _player.AddBagItem(itemId, -1, tag: "学习技能");
            if (!ret) return;

            var needSyncShanXianList = SkillManager.IsShanXianSkill(skId);
            var needSyncAutoSkill = false;

            var forgetText = string.Empty;
            if (forgetIndex >= 0)
            {
                var rnd = _player.Random.Next(0, 100);
                if (rnd < forgetRate)
                {
                    var forgetSkill = _skills[forgetIndex];
                    var xxSkId = forgetSkill.Id;
                    var skCfg = SkillManager.GetSkill(forgetSkill.Id);
                    if (skCfg != null) forgetText = $", 遗忘了 {skCfg.Name} 技能";
                    forgetSkill.Id = SkillId.Unkown;
                    forgetSkill.Lock = false;

                    // 如果遗忘了闪现技能, 要调整闪现支援顺序
                    if (SkillManager.IsShanXianSkill(xxSkId)) needSyncShanXianList = true;
                    // 如果是自动技能被遗忘了要及时调整
                    needSyncAutoSkill = Entity.AutoSkill == (uint) xxSkId;
                }
            }

            // 新增技能
            _skills[index].Id = skId;
            SyncSkills();

            CalculateAttrs();
            await SendInfo();

            var notice = $"{Entity.Name}习得 {skill.Name}" + forgetText;
            _player.SendNotice(notice);
            _player.LogDebug($"{Entity.Id}({Entity.Name}) 习得{skill.Name}" + forgetText);

            if (needSyncShanXianList)
            {
                await _player.PetMgr.SendShanXianOrderList();
            }

            if (needSyncAutoSkill)
            {
                await SetAutoSkill((uint) SkillId.NormalAtk);
            }
        }

        public async Task ForgetSkill(SkillId skId)
        {
            var skill = SkillManager.GetSkill(skId);
            if (skill == null)
            {
                _player.SendNotice("技能不存在");
                return;
            }

            if ((uint) skId == Cfg.Skill)
            {
                _player.SendNotice("天生技能不能遗忘");
                return;
            }

            var idx = _skills.FindIndex(p => p.Id == skId);
            if (idx < 0)
            {
                _player.SendNotice("技能不存在");
                return;
            }

            _skills[idx].Id = SkillId.Unkown;
            // _skills[idx].Lock = false;
            SyncSkills();

            CalculateAttrs();
            await SendInfo();

            _player.SendNotice($"召唤兽{Entity.Name} 遗忘了 {skill.Name}");
            _player.LogDebug($"召唤兽{Entity.Id}({Entity.Name}) 遗忘了 {skill.Name}");

            if (SkillManager.IsShanXianSkill(skId))
            {
                await _player.PetMgr.SendShanXianOrderList();
            }

            if (Entity.AutoSkill == (uint) skId)
            {
                await SetAutoSkill((uint) SkillId.NormalAtk);
            }
        }

        public async Task LockSkill(SkillId skId, bool beLock)
        {
            var skill = SkillManager.GetSkill(skId);
            if (skill == null)
            {
                _player.SendNotice("技能不存在");
                return;
            }

            var idx = _skills.FindIndex(p => p.Id == skId);
            if (idx < 0)
            {
                _player.SendNotice("技能不存在");
                return;
            }

            if (beLock == _skills[idx].Lock) return;

            // 扣除仙玉
            if (beLock)
            {
                var jade = (uint) MathF.Pow(2, LockedSkillNum) * 1000;
                var ret = await _player.CostMoney(MoneyType.Jade, jade, tag: "锁定宠物技能消耗");
                if (!ret) return;
            }

            _skills[idx].Lock = beLock;
            SyncSkills();
            await _player.SendPacket(GameCmd.S2CPetLockSkill,
                new S2C_PetLockSkill {Id = Entity.Id, SkId = skId, Lock = beLock});

            _player.LogDebug($"召唤兽{Entity.Id}({Entity.Name}) 对技能:{skill.Name} 锁定:{beLock}");
        }

        public async Task ChangeSsSkill(SkillId skId)
        {
            var skill = SkillManager.GetSkill(skId);
            if (skill == null)
            {
                _player.SendNotice("技能不存在");
                return;
            }

            if (skill.Quality != SkillQuality.Shen)
            {
                _player.SendNotice($"{skill.Name} 不是神兽技");
                return;
            }

            if (Cfg.Grade < 3)
            {
                _player.SendNotice($"{Entity.Name} 不是神兽");
                return;
            }

            // 5000仙玉
            var ret = await _player.CostMoney(MoneyType.Jade, 5000, tag: "学习神兽技");
            if (!ret) return;
            var oldSsSkill = Entity.SsSkill;
            Entity.SsSkill = (uint) skId;
            CalculateAttrs();
            await SendInfo();

            // 检查神兽技是不是自动技能
            if (oldSsSkill == Entity.AutoSkill)
            {
                await SetAutoSkill((uint) SkillId.NormalAtk);
            }

            _player.SendNotice($"{Entity.Name} 学习了神兽技 {skill.Name}");
            _player.LogDebug($"召唤兽{Entity.Id}({Entity.Name}) 学习了神兽技:{skill.Name}");
        }

        // 飞升
        public async Task Fly(uint type)
        {
            if (Cfg.Grade < 3)
            {
                _player.SendNotice("宠物品阶太低,不能飞升");
                return;
            }

            if (Entity.Level >= 50 && Entity.Fly % 10 == 0)
            {
                Entity.Fly++;
                type = 0;
                _player.SendNotice("第一次飞升成功");
            }
            else if (Entity.Level >= 100 && Entity.Relive >= 1 && Entity.Fly % 10 == 1)
            {
                Entity.Fly++;
                type = 0;
                _player.SendNotice("第二次飞升成功");
            }
            else if (Entity.Level >= 120 && Entity.Relive >= 2 && Entity.Fly % 10 == 2)
            {
                if (!_player.CheckMoney(MoneyType.Silver, 5000000)) return;
                Entity.Fly++;
                _player.SendNotice("第三次飞升成功");
            }
            else if (Entity.Level >= 120 && Entity.Relive >= 2 && Entity.Fly % 10 == 3)
            {
                if (!_player.CheckMoney(MoneyType.Silver, 5000000)) return;
                _player.SendNotice("修改属性成功");
            }
            else
            {
                return;
            }

            if (Entity.Fly % 10 == 3 && type != 0 && type != (uint) MathF.Floor(Entity.Fly / 10f))
            {
                var ret = await _player.CostMoney(MoneyType.Silver, 5000000, tag: "宠物飞升消耗");
                if (!ret) return;
            }

            Entity.Fly = type * 10 + Entity.Fly % 10;
            CalculateAttrs();
            await SendInfo();
        }

        // 宠物洗颜色
        public int ChangeColor()
        {
            var color = 0;
            ConfigService.PetColors.TryGetValue(Cfg.Res, out var colorCfg);
            if (colorCfg == null)
            {
                _player.SendNotice("该宠物不支持变色");
                return color;
            }

            var normalColors = ArrayUtil.String2Array<int>(colorCfg.ColorValue); //普通颜色
            var specialColors = ArrayUtil.String2Array<int>(colorCfg.ColorNice); //特殊颜色

            // 普通颜色90%的概率
            if (normalColors.Count > 0 && _player.Random.Next(0, 100) < 90)
            {
                color = normalColors[_player.Random.Next(0, normalColors.Count)];
            }
            else if (specialColors.Count > 0)
            {
                color = specialColors[_player.Random.Next(0, specialColors.Count)];
            }

            _player.SendNotice("你的宠物成功变色");
            return color;
        }

        /// <summary>
        /// 被上架到摆摊
        /// </summary>
        public async ValueTask<bool> SetOnMall()
        {
            if (Active) return false;
            // 移除缓存
            var idx = _player.PetMgr.All.FindIndex(p => p.Id == Entity.Id);
            if (idx < 0) return false;
            _player.PetMgr.All.RemoveAt(idx);
            // 立即保存入库, 标记拥有者为0
            Entity.RoleId = 0;
            // 通知前端
            await _player.SendPacket(GameCmd.S2CPetDel, new S2C_PetDel {Id = Entity.Id});
            await Destroy();
            return true;
        }

        /// <summary>
        /// 当被坐骑托管/取消托管时
        /// </summary>
        public Task RefreshAttrs()
        {
            CalculateAttrs();
            return SendInfo();
        }

        public async Task SetAutoSkill(uint skill)
        {
            var skId = (SkillId) skill;
            if (skId == 0) skId = SkillId.NormalAtk;

            // 检查是否已注册，是否为主动技能
            var skInfo = SkillManager.GetSkill(skId);
            if (skInfo is not {ActionType: SkillActionType.Initiative}) return;

            // 检查自己是否已学习
            if (skId != SkillId.NormalAtk && skId != SkillId.NormalDef)
            {
                // FIXME: 去掉飞龙在天(风、雷、水、火)需要学习“飞龙在天”的限制。
#if false
                // 飞龙在天比较特殊
                if (skId is >= SkillId.FeiLongZaiTianFeng and <= SkillId.FeiLongZaiTianLei)
                {
                    if (!_skills.Exists(p => p.Id == SkillId.FeiLongZaiTian)) return;
                }
                else
                {
                    if (!_skills.Exists(p => p.Id == skId) && skill != Entity.SsSkill) return;
                }
#else
                if (!_skills.Exists(p => p.Id == skId) && skill != Entity.SsSkill) return;
#endif
            }

            Entity.AutoSkill = (uint) skId;

            await _player.SendPacket(GameCmd.S2CPetAutoSkill, new S2C_PetAutoSkill
            {
                Id = Id,
                Skill = (uint) skId
            });
        }

        // 觉醒等级 突破
        public async Task JxSkillTuPo(bool must)
        {
            if (_activeJxSkillGrade <= 0)
            {
                _player.SendNotice("觉醒技未激活");
                return;
            }
            if (Entity.JxLevel >= 6)
            {
                _player.SendNotice("觉醒程度已满级");
                return;
            }
            uint itemId = 500072;
            var itemNum = must ? 15 : 5;
            if (_player.GetBagItemNum(itemId) < itemNum)
            {
                _player.SendNotice("觉醒丹不足");
                return;
            }
            if (!await _player.AddBagItem(itemId, -itemNum, true, "觉醒技突破"))
            {
                _player.SendNotice("觉醒丹扣除失败");
                return;
            }
            if (must)
            {
                Entity.JxLevel += 1;
            }
            else if (_player.Random.Next(100) < 20)
            {
                Entity.JxLevel += 1;
            }
            else
            {
                _player.SendNotice("突破失败，再接再厉啊~");
                return;
            }
            await RefreshAttrs();
        }

        // 解析skills
        private void InitSkills()
        {
            // TODO 修改一下，天赋技能不要写数据库中，方便调整配置表
            _skills = new List<PetSkillData>((int) MaxSkillNum);
            // 从数据库中读取并覆盖数据
            if (!string.IsNullOrWhiteSpace(Entity.Skills))
            {
                // 解析数据
                var list = Json.Deserialize<List<PetSkillEntity>>(Entity.Skills);
                if (list is {Count: > 0})
                {
                    foreach (var entity in list)
                    {
                        var temp = entity.ToData();
                        if (!temp.Unlock)
                            temp.Id = SkillId.Unkown;

                        if (temp.Id == SkillId.Unkown)
                            temp.Lock = false;

                        _skills.Add(temp);
                    }
                }
            }

            // 补满
            while (_skills.Count < MaxSkillNum)
            {
                _skills.Add(new PetSkillData {Id = SkillId.Unkown, Lock = false, Unlock = false});
            }
        }

        // 同步skills到Entity
        private void SyncSkills()
        {
            if (_skills == null || _skills.Count == 0)
            {
                Entity.Skills = string.Empty;
                return;
            }

            var list = new List<PetSkillEntity>();
            foreach (var psd in _skills)
            {
                list.Add(new PetSkillEntity(psd));
            }

            Entity.Skills = Json.Serialize(list);
        }

        public void CalculateAttrs()
        {
            _tiancefuRateAddon = _player.checkPetTianceLonghyt(this);
            Attrs.Clear();

            var level = Entity.Level;

            Attrs.Set(AttrType.GenGu, Entity.Level + ApAttrs.Get(AttrType.GenGu));
            Attrs.Set(AttrType.LingXing, Entity.Level + ApAttrs.Get(AttrType.LingXing));
            Attrs.Set(AttrType.LiLiang, Entity.Level + ApAttrs.Get(AttrType.LiLiang));
            Attrs.Set(AttrType.MinJie, Entity.Level + ApAttrs.Get(AttrType.MinJie));

            var rate = GetCurRate() / 10000f;
            var calcHp = MathF.Round(level * rate * (level + ApAttrs.Get(AttrType.GenGu)) +
                                     0.7f * GetBaseProperty(AttrType.Hp) * level * rate + GetBaseProperty(AttrType.Hp));
            var calcMp = MathF.Round(level * rate * (level + ApAttrs.Get(AttrType.LingXing)) +
                                     0.7f * GetBaseProperty(AttrType.Mp) * level * rate + GetBaseProperty(AttrType.Mp));
            var calcAtk = MathF.Round(0.2f * level * rate * (level + ApAttrs.Get(AttrType.LiLiang)) +
                                      0.2f * 0.7f * GetBaseProperty(AttrType.Atk) * level * rate +
                                      GetBaseProperty(AttrType.Atk));
            var calcSpd = MathF.Round((GetBaseProperty(AttrType.Spd) + (level + ApAttrs.Get(AttrType.MinJie))) * rate);

            // var calcSpd =
            //     MathF.Round(GetBaseProperty(AttrType.Spd) + level + ApAttrs.Get(AttrType.MinJie) * rate);

            Attrs.Set(AttrType.Hp, calcHp);
            Attrs.Set(AttrType.HpMax, calcHp);
            Attrs.Set(AttrType.Mp, calcMp);
            Attrs.Set(AttrType.MpMax, calcMp);
            Attrs.Set(AttrType.Atk, calcAtk);
            Attrs.Set(AttrType.Spd, calcSpd);

            // 宠物的 命中 >= 80% 连击 >= 3 
            Attrs.Set(AttrType.PmingZhong, 80);
            Attrs.Set(AttrType.PlianJi, 3);
            // 连击率初始1% 每1kw亲密增加1% 这里最多不超过12%
            var lianjiLv = 1 + MathF.Floor(Entity.Intimacy / 10000000.0f);
            if (lianjiLv > 12) lianjiLv = 12;
            Attrs.Set(AttrType.PlianJiLv, lianjiLv);

            Attrs.Add(AttrType.DfengYin, RefineAttrs.Get(AttrType.DfengYin) * 4 + _player.checkPetTianceUseState(this, AttrType.DfengYin));
            Attrs.Add(AttrType.DhunLuan, RefineAttrs.Get(AttrType.DhunLuan) * 4 + _player.checkPetTianceUseState(this, AttrType.DhunLuan));
            Attrs.Add(AttrType.DhunShui, RefineAttrs.Get(AttrType.DhunShui) * 4 + _player.checkPetTianceUseState(this, AttrType.DhunShui));
            Attrs.Add(AttrType.DyiWang, RefineAttrs.Get(AttrType.DyiWang) * 4 + _player.checkPetTianceUseState(this, AttrType.DyiWang));

            Attrs.Add(AttrType.Dfeng, RefineAttrs.Get(AttrType.Dfeng) * 4);
            Attrs.Add(AttrType.Dshui, RefineAttrs.Get(AttrType.Dshui) * 4);
            Attrs.Add(AttrType.Dhuo, RefineAttrs.Get(AttrType.Dhuo) * 4);
            Attrs.Add(AttrType.Ddu, RefineAttrs.Get(AttrType.Ddu) * 4);
            Attrs.Add(AttrType.Dlei, RefineAttrs.Get(AttrType.Dlei) * 4);
            Attrs.Add(AttrType.DguiHuo, RefineAttrs.Get(AttrType.DguiHuo) * 4);
            Attrs.Add(AttrType.DsanShi, RefineAttrs.Get(AttrType.DsanShi) * 4);
            Attrs.Add(AttrType.PxiShou, RefineAttrs.Get(AttrType.PxiShou) * 4);

            Attrs.Add(AttrType.PmingZhong, RefineAttrs.Get(AttrType.PmingZhong) * 3f + _player.checkPetTianceUseState(this, AttrType.PmingZhong));
            Attrs.Add(AttrType.PshanBi, RefineAttrs.Get(AttrType.PshanBi) * 1.5f);
            Attrs.Add(AttrType.PlianJi, RefineAttrs.Get(AttrType.PlianJi) * 1f);
            Attrs.Add(AttrType.PlianJiLv, RefineAttrs.Get(AttrType.PlianJiLv) * 1.5f);
            Attrs.Add(AttrType.PkuangBao, RefineAttrs.Get(AttrType.PkuangBao) * 3f);
            Attrs.Add(AttrType.PpoFang, RefineAttrs.Get(AttrType.PpoFang) * 3 + _player.checkPetTianceUseState(this, AttrType.PpoFang));
            Attrs.Add(AttrType.PpoFangLv, RefineAttrs.Get(AttrType.PpoFangLv) * 3 + _player.checkPetTianceUseState(this, AttrType.PpoFangLv));
            Attrs.Add(AttrType.PfanZhen, RefineAttrs.Get(AttrType.PfanZhen) * 4);
            Attrs.Add(AttrType.PfanZhenLv, RefineAttrs.Get(AttrType.PfanZhenLv) * 4);

            // 五行属性
            if (Cfg.Elements is {Length: >= 5})
            {
                if (Cfg.Elements[0] > 0) Attrs.Add(AttrType.Jin, Cfg.Elements[0]);
                if (Cfg.Elements[1] > 0) Attrs.Add(AttrType.Mu, Cfg.Elements[1]);
                if (Cfg.Elements[2] > 0) Attrs.Add(AttrType.Shui, Cfg.Elements[2]);
                if (Cfg.Elements[3] > 0) Attrs.Add(AttrType.Huo, Cfg.Elements[3]);
                if (Cfg.Elements[4] > 0) Attrs.Add(AttrType.Tu, Cfg.Elements[4]);
            }

            // 计算技能
            foreach (var skill in _skills)
            {
                CalculateSkillAttrs(skill.Id);
            }

            if (Entity.SsSkill > 0) CalculateSkillAttrs((SkillId) Entity.SsSkill);

            // 坐骑管制技能
            var mount = _player.MountMgr.FindWhoControlPet(Entity.Id);
            if (mount != null && mount.Skills.Count > 0)
            {
                var dic = new Dictionary<AttrType, float>();
                foreach (var msk in mount.Skills.Values)
                {
                    foreach (var pair in msk.Attrs)
                    {
                        if (dic.ContainsKey(pair.Key))
                            dic[pair.Key] += pair.Value;
                        else
                            dic[pair.Key] = pair.Value;
                    }
                }
                // FIXME: 负值 生命值 不能小于99，保留1%生命值
                if (dic.ContainsKey(AttrType.Hp) && dic[AttrType.Hp] < 0)
                {
                    dic[AttrType.Hp] = Math.Max(dic[AttrType.Hp], -99);
                }
                if (dic.ContainsKey(AttrType.HpMax) && dic[AttrType.HpMax] < 0)
                {
                    dic[AttrType.HpMax] = Math.Max(dic[AttrType.HpMax], -99);
                }

                foreach (var (k, v) in dic)
                {
                    switch (k)
                    {
                        case AttrType.Hp:
                            Attrs.AddPercent(k, v / 100);
                            Attrs.Set(AttrType.HpMax, Attrs.Get(AttrType.Hp));
                            break;
                        case AttrType.HpMax:
                            Attrs.AddPercent(k, v / 100);
                            Attrs.Set(AttrType.Hp, Attrs.Get(AttrType.HpMax));
                            break;
                        case AttrType.Mp:
                            Attrs.AddPercent(k, v / 100);
                            Attrs.Set(AttrType.MpMax, Attrs.Get(AttrType.Mp));
                            break;
                        case AttrType.MpMax:
                            Attrs.AddPercent(k, v / 100);
                            Attrs.Set(AttrType.Mp, Attrs.Get(AttrType.MpMax));
                            break;
                        case AttrType.Spd:
                            Attrs.AddPercent(k, v / 100);
                            break;
                        case AttrType.Atk:
                            Attrs.AddPercent(k, v / 100);
                            break;
                        default:
                            Attrs.Add(k, v);
                            break;
                    }
                }
            }

// 配饰装备
            var ornamentAttrs = _player.EquipMgr.GetPetOrnamentAttr(Id);
            // 觉醒技
            if (_activeJxSkillGrade > 0)
            {
                switch ((SkillId)Cfg.JxSkill)
                {
                    // 斜风细雨
                    case SkillId.XieFengXiYu:
                        {
                            var baseValues = new List<float>() { 4, 8, 14, 20 };
                            var rangeValue = baseValues[_activeJxSkillGrade] - baseValues[_activeJxSkillGrade - 1];
                            var calcValue = baseValues[_activeJxSkillGrade - 1] + rangeValue * _activeJxSkillLevel / 6;
                            var key = AttrType.JqShui;
                            ornamentAttrs.Add(key, calcValue);
                        }
                        break;
                    // 水抗
                    case SkillId.ShuiKang:
                        {
                            var baseValues = new List<float>() { 4, 8, 14, 20 };
                            var rangeValue = baseValues[_activeJxSkillGrade] - baseValues[_activeJxSkillGrade - 1];
                            var calcValue = baseValues[_activeJxSkillGrade - 1] + rangeValue * _activeJxSkillLevel / 6;
                            var key = AttrType.Dshui;
                            ornamentAttrs.Add(key, calcValue);
                        }
                        break;
                    // 火树银花
                    case SkillId.HuoShuYinHua:
                        {
                            var baseValues = new List<float>() { 4, 8, 14, 20 };
                            var rangeValue = baseValues[_activeJxSkillGrade] - baseValues[_activeJxSkillGrade - 1];
                            var calcValue = baseValues[_activeJxSkillGrade - 1] + rangeValue * _activeJxSkillLevel / 6;
                            var key = AttrType.JqHuo;
                            ornamentAttrs.Add(key, calcValue);
                        }
                        break;
                    // 八面玲珑
                    case SkillId.BaMianLingLong:
                        {
                            var baseValues = new List<float>() { 12, 24, 42, 60 };
                            var rangeValue = baseValues[_activeJxSkillGrade] - baseValues[_activeJxSkillGrade - 1];
                            var calcValue = baseValues[_activeJxSkillGrade - 1] + rangeValue * _activeJxSkillLevel / 6;
                            var key = AttrType.Spd;
                            ornamentAttrs.Add(key, calcValue);
                        }
                        break;
                    // 好运连绵
                    case SkillId.HaoYunLianMian:
                        {
                            var baseValues = new List<float>() { 3, 6, 10.5f, 15 };
                            var rangeValue = baseValues[_activeJxSkillGrade] - baseValues[_activeJxSkillGrade - 1];
                            var calcValue = baseValues[_activeJxSkillGrade - 1] + rangeValue * _activeJxSkillLevel / 6;
                            var keys = new List<AttrType>() { AttrType.PlianJiLv, AttrType.PkuangBao };
                            foreach (var key in keys)
                            {
                                ornamentAttrs.Add(key, calcValue);
                            }
                        }
                        break;
                    // 安行疾斗
                    case SkillId.QiangHuaAnXingJiDou:
                        {
                            var baseValues = new List<float>() { 6, 12, 21, 30 };
                            var rangeValue = baseValues[_activeJxSkillGrade] - baseValues[_activeJxSkillGrade - 1];
                            var calcValue = baseValues[_activeJxSkillGrade - 1] + rangeValue * _activeJxSkillLevel / 6;
                            var keys = new List<AttrType>() { AttrType.Dfeng, AttrType.Dhuo, AttrType.Dshui, AttrType.Dlei, AttrType.DguiHuo };
                            foreach (var key in keys)
                            {
                                ornamentAttrs.AddPercent(key, calcValue);
                            }
                        }
                        break;
                }
            }
            // 配饰装备归类计算
            foreach (var (k, v) in ornamentAttrs)
            {
                switch (k)
                {
                    // 气血
                    case AttrType.Hp:
                        Attrs.Add(k, v);
                        Attrs.Set(AttrType.HpMax, Attrs.Get(AttrType.Hp));
                        break;
                    // 气血上限
                    case AttrType.HpMax:
                        Attrs.Add(k, v);
                        Attrs.Set(AttrType.Hp, Attrs.Get(AttrType.HpMax));
                        break;
                    // 法力
                    case AttrType.Mp:
                        Attrs.Add(k, v);
                        Attrs.Set(AttrType.MpMax, Attrs.Get(AttrType.Mp));
                        break;
                    // 法力上限
                    case AttrType.MpMax:
                        Attrs.Add(k, v);
                        Attrs.Set(AttrType.Mp, Attrs.Get(AttrType.MpMax));
                        break;
                    // 气血百分比
                    case AttrType.Ahp:
                        Attrs.AddPercent(AttrType.Hp, v / 100);
                        Attrs.Set(AttrType.HpMax, Attrs.Get(AttrType.Hp));
                        break;
                    // 法力百分比
                    case AttrType.Amp:
                        Attrs.AddPercent(AttrType.Mp, v / 100);
                        Attrs.Set(AttrType.MpMax, Attrs.Get(AttrType.Mp));
                        break;
                    // 攻击百分比
                    case AttrType.Patk:
                        Attrs.AddPercent(AttrType.Atk, v / 100);
                        break;
                    default:
                        Attrs.Add(k, v);
                        break;
                }
            }
            // 天策符
            var tianceAttrs = _player.GetTianceAttrPet();
            if (tianceAttrs != null)
            {
                foreach (var (k, v) in tianceAttrs)
                {
                    Attrs.Add(k, v);
                }
            }
            // 天策符--特殊处理
            foreach (var f in _player.Tiance.list)
            {
                // 跳过没有装备的
                if (f.State == TianceFuState.Unknown) continue;
                var skCfg = ConfigService.TianceSkillList.GetValueOrDefault(f.SkillId, null);
                if (skCfg == null)
                {
                    _player.LogError($"没有找到天策符技能配置（宠物特殊加成）RoleId:{_player.RoleId}, fid:{f.Id}, skillId:{f.SkillId}, name:{f.Name}");
                    continue;
                }
                var skillId = f.SkillId;
                // 天演策等级加成
                float additionTlv = _player.GetTianyanceLvAddition(f.Grade);
                // 诛神符后处理--对宠物加成
                if (skillId >= SkillId.ZhuShen1 && skillId <= SkillId.ZhuShen3)
                {
                    foreach (var a in skCfg.attr)
                    {
                        var ak = GameDefine.EquipAttrTypeMap.GetValueOrDefault(a.Key, AttrType.Unkown);
                        if (ak != AttrType.Unkown)
                        {
                            float add = (float)(a.Value.increase ? a.Value.baseAddition * f.Addition : a.Value.baseAddition) / 1000.0f;
#if false
                            // 非数值性，千分比
                            if (!GameDefine.EquipNumericalAttrType.ContainsKey(ak))
                            {
                                add = add / 10;
                            }
#endif
                            Attrs.Set(ak, Attrs.Get(ak) * (1 + add * (1 + additionTlv)));
                        }
                    }
                    continue;
                }
            }

            // 连击次数最多不超过6次
            if (Attrs.Get(AttrType.PlianJi) > 6) Attrs.Set(AttrType.PlianJi, 6);

            _expMax = ConfigService.GetPetUpgradeExp(Entity.Relive, Entity.Level);
        }

        // 天演策技能
        public float checkTianceSkill(SkillId skillId, uint addition, uint tlevel)
        {
            var liliang = Attrs.Get(AttrType.LiLiang);
            var lingxing = Attrs.Get(AttrType.LingXing);
            var gengu = Attrs.Get(AttrType.GenGu);
            // 猛攻符
            // 召唤兽每100点力量提高一定破防程度
            if (skillId == SkillId.MengGong1)
            {
                return (float)(Math.Floor(liliang / 100.0) * (1 + 0.01 * addition + 0.1 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            if (skillId == SkillId.MengGong2)
            {
                return (float)(Math.Floor(liliang / 100.0) * (2 + 0.02 * addition + 0.2 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            if (skillId == SkillId.MengGong3)
            {
                return (float)(Math.Floor(liliang / 100.0) * (3 + 0.03 * addition + 0.3 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            // 看破符
            // 召唤兽每100点力量提高一定破防概率
            if (skillId == SkillId.KanPo1)
            {
                return (float)(Math.Floor(liliang / 100.0) * (1 + 0.01 * addition + 0.1 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            if (skillId == SkillId.KanPo2)
            {
                return (float)(Math.Floor(liliang / 100.0) * (2 + 0.02 * addition + 0.2 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            if (skillId == SkillId.KanPo3)
            {
                return (float)(Math.Floor(liliang / 100.0) * (3 + 0.03 * addition + 0.3 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            // 精准符
            // 召唤兽每100点力量提高一定命中率
            if (skillId == SkillId.JingZhun1)
            {
                return (float)(Math.Floor(liliang / 100.0) * (1 + 0.01 * addition + 0.1 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            if (skillId == SkillId.JingZhun2)
            {
                return (float)(Math.Floor(liliang / 100.0) * (2 + 0.02 * addition + 0.2 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            if (skillId == SkillId.JingZhun3)
            {
                return (float)(Math.Floor(liliang / 100.0) * (3 + 0.03 * addition + 0.3 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            // 慧根符
            // 召唤兽每100点根骨或灵性，增加一定抗冰混睡忘
            if (skillId == SkillId.HuiGen1)
            {
                return (float)(Math.Floor(Math.Max(lingxing, gengu) / 100.0) * (1 + 0.01 * addition + 0.1 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            if (skillId == SkillId.HuiGen2)
            {
                return (float)(Math.Floor(Math.Max(lingxing, gengu) / 100.0) * (2 + 0.02 * addition + 0.2 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            if (skillId == SkillId.HuiGen3)
            {
                return (float)(Math.Floor(Math.Max(lingxing, gengu) / 100.0) * (3 + 0.03 * addition + 0.3 * tlevel / GameDefine.TianYanCeMaxLevel));
            }
            return 0;
        }

        private void CalculateSkillAttrs(SkillId skid)
        {
            var skill = SkillManager.GetSkill(skid);
            // 被动技能计算属性
            if (skill is {ActionType: SkillActionType.Passive})
            {
                var effectData = skill.GetEffectData(new GetEffectDataRequest
                {
                    Level = Entity.Level,
                    Relive = Entity.Relive,
                    Intimacy = Entity.Intimacy,
                    Attrs = Attrs
                });

                foreach (var attrType in skill.EffectTypes)
                {
                    if (effectData.Add > 0)
                    {
                        Attrs.Add(attrType, effectData.Add);
                        if (AttrType.HpMax == attrType)
                            Attrs.Set(AttrType.Hp, Attrs.Get(AttrType.HpMax));
                        if (AttrType.MpMax == attrType)
                            Attrs.Set(AttrType.Mp, Attrs.Get(AttrType.MpMax));
                    }

                    if (effectData.Del > 0)
                    {
                        Attrs.Add(attrType, -effectData.Del);
                    }
                }
            }
        }

        // 刷新潜能
        private void CheckPotential()
        {
            // 计算剩余的潜能
            var total = 4 * (uint) Entity.Level + 30 * (uint) Entity.Relive;
            var consume = (uint) MathF.Ceiling(ApAttrs.Values.Sum());
            if (consume > total)
            {
                // 清空所有加点
                ApAttrs.Clear();
                Potential = total;
            }
            else
            {
                Potential = total - consume;
            }
        }

        // 检查修炼点是否溢出
        private void CheckRefinePoint()
        {
            // 计算修炼点总和, 不能超过修炼等级
            var total = (uint) RefineAttrs.Values.Sum();
            if (total > Entity.RefineLevel)
            {
                // 清空所有加点
                RefineAttrs.Clear();
            }
        }

        public bool HasShanXian()
        {
            var ret = false;
            foreach (var psd in _skills)
            {
                if (psd != null && SkillManager.IsShanXianSkill(psd.Id))
                {
                    ret = true;
                    break;
                }
            }

            return ret;
        }

        private int LockedSkillNum => _skills.Count(p => p.Lock);

        // 获取当前成长率
        private uint GetCurRate()
        {
            var rate = Entity.Rate + GetAddRate() + _tiancefuRateAddon;
            var max = GetMaxRate();
            return Math.Min(rate, max);
        }

        // 元气丹可以影响的最大成长率
        private uint GetMaxRate()
        {
            return PetManager.GetMaxRate(Entity.CfgId) + GetAddRate() + _tiancefuRateAddon;
        }

        // 获取成长率增量
        private uint GetAddRate()
        {
            // 获取宠物吃元气丹成长的概率
            ConfigService.ItemPetRates.TryGetValue(Entity.CfgId, out var rate);
            if (Entity.Color == 0) rate = 0;

            var addRate = Entity.Relive * 1000 + Entity.Keel * 100 + rate * 10000;
            if (Entity.Fly % 10 >= 1) addRate += 1000;
            if (Entity.Fly % 10 >= 2) addRate += 500;
            return (uint) MathF.Floor(addRate);
        }

        // 获取基础属性
        private float GetBaseProperty(AttrType type)
        {
            var flyProp = Entity.Fly / 10;
            if (type == AttrType.Hp) return Entity.Hp + (flyProp == 1 ? 60 : 0);
            if (type == AttrType.Mp) return Entity.Mp + (flyProp == 2 ? 60 : 0);
            if (type == AttrType.Atk) return Entity.Atk + (flyProp == 3 ? 60 : 0);
            if (type == AttrType.Spd) return Entity.Spd + (flyProp == 4 ? 60 : 0);
            return 0;
        }

        private void InitWashData()
        {
            _washData = null;

            if (!string.IsNullOrWhiteSpace(Entity.WashData))
            {
                _washData = Json.SafeDeserialize<PetWashData>(Entity.WashData);
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

        private uint MaxSkillNum => PetManager.GetMaxSkillNum(Entity.CfgId);
    }
}