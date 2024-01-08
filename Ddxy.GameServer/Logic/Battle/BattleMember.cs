using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Logic.Battle.Skill;
using Ddxy.Protocol;
using Google.Protobuf;
using Orleans.Concurrency;

namespace Ddxy.GameServer.Logic.Battle
{
    public class BattleMember
    {
        /// <summary>
        /// 战场唯一id, 由BattleGrain生成
        /// </summary>
        public uint OnlyId { get; }

        /// <summary>
        /// 阵营id
        /// </summary>
        public uint CampId { get; }

        /// <summary>
        /// 原始数据
        /// </summary>
        public BattleMemberData Data { get; private set; }

        public LivingThingType Type => Data.Type;
        public bool IsPlayer => Data.Type == LivingThingType.Player;
        public bool IsMonster => Data.Type == LivingThingType.Monster;
        public bool IsPet => Data.Type == LivingThingType.Pet;
        public bool IsPartner => Data.Type == LivingThingType.Partner;

        public uint Id => Data.Id; // roleId/petId/partnerId

        public byte Relive => (byte) Data.Relive;

        public bool IsBb => Data.Catched;


        public int Pos; // -1 不可登场 0 等待登场 >0 战场所在位置
        public bool Online; //当前是否在线
        public bool Dead; //是否已死亡
        public bool BeCache; //宠物是否已经上场过了

        public Attrs Attrs { get; private set; }
        public Dictionary<SkillId, BattleSkillData> Skills { get; private set; }
        public Dictionary<SkillId, uint> UsedSkills { get; private set; }
        public SkillId LastSkill;
        public uint DefSkillTimes;
        public List<Buff> Buffs { get; private set; }

        private readonly uint _skillProfic;

        public Dictionary<uint, OrnamentSkillData> OrnamentSkills;


        public bool IsAction; // 是否得到了玩家行动指令
        public bool IsRoundAction; //在一回合内是否行动过
        public Action ActionData { get; private set; } //本轮回合的操作数据

        public uint PetOnlyId; // Player当前使用的Pet OnlyId
        public uint OwnerOnlyId; // Pet所属Player OnlyId

        private Random _random; //随机发生器
        public IPlayerGrain Grain; //PlayerGrain

        public uint KongZhiRound; //上次被控制的回合
        public uint ZhuiJiNum; //本回合追击的次数

        private Attrs _roundAttrs; //每回合临时增加的属性

        public Action LastAction; //上回合的指令，永的落纸云烟用得上
        public float ZhangHuangShiCuo; //是否处于张皇失措的状态, 每回合开始清空
        public int QiDingQianKun; //是否处于气定乾坤状态
        public float XiaoShenLiuZhi1; //是否处于销神流志状态，物理攻击时对目标造成多少当前法力值损伤
        public float XiaoShenLiuZhi2; //是否处于销神流志状态, 所有伤害增强百分比
        public uint EnterRound; //宠物上场时的Round
        public int DeadTimes; //死亡次数

        // 孩子技能
        private Dictionary<SkillId, int> ChildSkills = new();

        // 天策符技能
        public Dictionary<SkillId, BattleTianceSkill> TianceFuSkills = new();
        // 天策符 金石为开 计数和标记技能
        public SkillType jswk_type = SkillType.Physics;
        public uint jswk_count = 0;
        // 天策符 陌上开花 是否已经使用了2连击？
        public uint double_pugong_hited_round = 0;
        // 天策符 浩气凌霄 是否已经使用了2连击？
        public uint double_fashu_hited_round = 0;
        // 天策符 连续被控制次数
        public uint continue_be_kongzhi = 0;
        // 天策符 千钧符 闲庭信步
        // 己方在场站立单位数小于7或大于9时，释放增益法术（加攻加防加速魅惑治愈）时增强
        public uint alive_count = 0;
        public float GetXTXBAdd(AttrType attr)
        {
            if (IsPlayer && (alive_count <= 7 || alive_count >= 9))
            {
                var fskill = TianceFuSkills.GetValueOrDefault(SkillId.XianTingXinBu3);
                var grade = 3;
                if (fskill == null)
                {
                    fskill = TianceFuSkills.GetValueOrDefault(SkillId.XianTingXinBu2);
                    grade = 2;
                    if (fskill == null)
                    {
                        fskill = TianceFuSkills.GetValueOrDefault(SkillId.XianTingXinBu1);
                        grade = 1;
                    }
                }
                if (fskill != null)
                {
                    return 0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f;
                }
            }
            return 0;
        }
        // 天策符 载物符 觉醒符
        // 是否刚摆脱控制？
        public bool just_not_kongzhi = false;
        // 天策符 载物符 承天载物符
        // 受到伤害时一定几率减伤，己方倒地单位越多，概率越高（每回合最多触发3次）
        // 激活次数？
        public uint ctzw_active_times = 0;
        // 千钧符 堆月符
        // 魅惑、毒持续施法，可叠加增强效果
        public uint duiyue_last_charm_round = 0; 
        public uint duiyue_last_toxin_round = 0; 

        // 觉醒技 使用次数
        private Dictionary<SkillId, uint> _UsedJxSkill = new();
        public bool CanUseJxSkill(SkillId id)
        {
            return IsPet && Data.PetJxGrade > 0 && Data.PetJxSkill == id;
        }
        public bool IsUsedJxSkill(SkillId id)
        {
            return _UsedJxSkill.ContainsKey(id);
        }
        public uint UseJxSkillCount(SkillId id)
        {
            return _UsedJxSkill.GetValueOrDefault(id, (uint)0);
        }
        public void UseJxSkill(SkillId id)
        {
            _UsedJxSkill[id] = _UsedJxSkill.GetValueOrDefault(id, (uint)0) + 1;
        }
        // 觉醒技 黄泉一笑 本回合触发次数
        public uint huang_quan_yi_xiao_times = 0;

        public BattleMember(uint campId, IPlayerGrain grain = null)
        {
            CampId = campId;
            Grain = grain;
            Data = new BattleMemberData() { Type = LivingThingType.Player };
        }
        public BattleMember(uint onlyId, uint campId, BattleMemberData data, BattleXingzhenData xingzhen = null, IPlayerGrain grain = null)
        {
            OnlyId = onlyId;
            CampId = campId;
            Data = data;
            Grain = grain;

            Pos = data.Pos;
            Online = data.Online;

            Attrs = new Attrs();
            foreach (var pair in data.Attrs)
            {
                if (pair.Value != 0) Attrs.Add(pair.Key, pair.Value);
            }
            // 孩子技能
            foreach (var cs in data.ChildSkills)
            {
                ChildSkills.Add(cs.SkillId, cs.Rate);
            }
            // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
            // 计算星阵加成
            // if (xingzhen != null && IsPlayer)
            // {
            //     var xAttrs = new Attrs(xingzhen.Attrs);
            //     foreach (var (k, v) in xAttrs)
            //     {
            //         if (v == 0) continue;
            //         // 血、蓝、攻、速
            //         if (k == AttrType.Ahp || k == AttrType.Amp || k == AttrType.Aatk || k == AttrType.Aspd)
            //         {
            //             if (GameDefine.AttrCalcMap.ContainsKey(k))
            //             {
            //                 var (attrType, calcType) = GameDefine.AttrCalcMap[k];
            //                 switch (calcType)
            //                 {
            //                     case AttrCalcType.AddPercent:
            //                         Attrs.AddPercent(attrType, v / 100f);
            //                         break;
            //                     case AttrCalcType.Percent:
            //                         Attrs.SetPercent(attrType, v / 100f);
            //                         break;
            //                     default:
            //                         Attrs.Add(attrType, v);
            //                         break;
            //                 }
            //             }
            //             else
            //             {
            //                 Attrs.Add(k, v);
            //             }
            //         }
            //         else
            //         {
            //             Attrs.Add(k, v);
            //         }
            //     }
            // }

            // if (Data.Id == 100001)
            // {
            //     Attrs.Add(AttrType.HpMax, 1000000);
            //     Attrs.Add(AttrType.Hp, Attrs.Get(AttrType.HpMax));
            // }

            // 临时代码, 仅供快速测试
            // if (campId == 1)
            // {
            //     Attrs.Set(AttrType.Atk, 99999999);
            //     Attrs.Set(AttrType.Spd, 99999999);
            //     Attrs.Set(AttrType.Hp, 99999999);
            //     Attrs.Set(AttrType.HpMax, 99999999);
            //     Attrs.Set(AttrType.Mp, 99999999);
            //     Attrs.Set(AttrType.MpMax, 99999999);
            //     Attrs.Set(AttrType.PshanBi, 100);
            //     Attrs.Set(AttrType.PkuangBao, 100);
            //     Attrs.Set(AttrType.PpoFang, 100);
            //     Attrs.Set(AttrType.PfanZhen, 100);
            //     Attrs.Set(AttrType.PlianJi, 100);
            // }

            // 初始化技能
            Skills = new Dictionary<SkillId, BattleSkillData>();
            if (data.Skills != null)
            {
                InitSkill(data.Skills, data.PetSsSkill);
            }

            UsedSkills = new Dictionary<SkillId, uint>();

            LastSkill = data.DefSkillId;
            {
                var skInfo = SkillManager.GetSkill(LastSkill);
                if (skInfo is not {ActionType: SkillActionType.Initiative}) LastSkill = SkillId.NormalAtk;
            }

            _skillProfic = data.SkillProfic;
            Buffs = new List<Buff>();

            _roundAttrs = new Attrs();

            OrnamentSkills = new Dictionary<uint, OrnamentSkillData>(3);
            if (Data.OrnamentSkills is {Count: > 0})
            {
                foreach (var sid in Data.OrnamentSkills)
                {
                    if (sid == 0) continue;
                    OrnamentSkills.Add(sid, new OrnamentSkillData(sid));
                }
            }

            // 天策符技能
            TianceFuSkills.Clear();
            foreach (var s in Data.TianceSkillList)
            {
                if (!TianceFuSkills.ContainsKey(s.SkillId))
                {
                    TianceFuSkills.Add(s.SkillId, s);
                }
            }

            _random = new Random();

            ResetRoundData();

            // 觉醒技 五行咒令
            // 自身在场时，额外增加主人最高的一项强力克属性8%/16%/28%/40%。
            if (CanUseJxSkill(SkillId.WuXingZhouLing))
            {
                AttrType type = AttrType.Unkown;
                float value = 0;
                List<AttrType> valid = new() { AttrType.Qjin, AttrType.Qmu, AttrType.Qshui, AttrType.Qhuo, AttrType.Qtu };
                foreach (var (k, v) in Attrs)
                {
                    if (valid.Contains(k) && v > value)
                    {
                        type = k;
                        value = v;
                    }
                }
                if (type != AttrType.Unkown)
                {
                    var baseValues = new List<float>() { 80, 160, 280, 400 };
                    var rangeValue = baseValues[(int)Data.PetJxGrade] - baseValues[(int)Data.PetJxGrade - 1];
                    var calcValue = baseValues[(int)Data.PetJxGrade - 1] + rangeValue * Data.PetJxLevel / 6;
                    Attrs.Set(type, value * (1 + (calcValue / 1000.0f)));
                }
            }
        }

        // 金翅大鹏
        private BattleMember _eagleLeft = null;
        private BattleMember _eagleRight = null;
        public void SetEagleLeftAndRight(BattleMember l, BattleMember r)
        {
            _eagleLeft = l;
            _eagleRight = r;
        }
        public bool IsEagle()
        {
            return this.IsEagleMain() || this.IsEagleLeft() || this.IsEagleRight();
        }
        public bool IsEagleMain() {
            return this.Data.CfgId == 8216;
        }
        public bool IsEagleLeft() {
            return this.Data.CfgId == 8214;
        }
        public bool IsEagleRight() {
            return this.Data.CfgId == 8215;
        }
        public void Destroy()
        {
            Data = null;
            _random = null;

            ActionData = null;
            LastAction = null;
            Attrs?.Clear();
            Attrs = null;
            Skills?.Clear();
            Skills = null;
            UsedSkills?.Clear();
            UsedSkills = null;
            Buffs?.Clear();
            Buffs = null;
            _roundAttrs?.Clear();
            _roundAttrs = null;
            Grain = null;
        }

        public void ResetRoundData(bool clearRoundAttrs = true)
        {
            IsAction = false;
            IsRoundAction = false;
            ActionData = new Action
            {
                ActionType = BattleActionType.Unkown,
                ActionId = 0,
                Skill = 0,
                Target = 0
            };

            if (clearRoundAttrs)
            {
                ZhuiJiNum = 0;
                foreach (var (k, v) in _roundAttrs)
                {
                    Attrs.Add(k, -v);
                }

                _roundAttrs.Clear();
                ZhangHuangShiCuo = 0f;
            }
        }

        public void AddRoundAttr(AttrType type, float value)
        {
            _roundAttrs.Add(type, value);
            Attrs.Add(type, value);
        }

        /// <summary>
        /// key是技能id，value是该技能的熟练度
        /// </summary>
        private void InitSkill(IDictionary<uint, uint> dic, SkillId petSsSkill)
        {
            foreach (var (k, v) in dic)
            {
                var skid = (SkillId) k;
                if (!SkillManager.IsValid(skid)) continue;

                // 飞龙在天
                if (skid == SkillId.FeiLongZaiTian)
                {
                    Skills[SkillId.FeiLongZaiTianFeng] = new BattleSkillData(SkillId.FeiLongZaiTianFeng, v);
                    Skills[SkillId.FeiLongZaiTianHuo] = new BattleSkillData(SkillId.FeiLongZaiTianHuo, v);
                    Skills[SkillId.FeiLongZaiTianShui] = new BattleSkillData(SkillId.FeiLongZaiTianShui, v);
                    Skills[SkillId.FeiLongZaiTianLei] = new BattleSkillData(SkillId.FeiLongZaiTianLei, v);
                    continue;
                }

                // 有凤来仪
                // if (skid == SkillId.YouFengLaiYi)
                // {
                //     Skills[SkillId.YouFengLaiYiJin] = new BattleSkillData(SkillId.YouFengLaiYiJin, v);
                //     Skills[SkillId.YouFengLaiYiMu] = new BattleSkillData(SkillId.YouFengLaiYiMu, v);
                //     Skills[SkillId.YouFengLaiYiShui] = new BattleSkillData(SkillId.YouFengLaiYiShui, v);
                //     Skills[SkillId.YouFengLaiYiHuo] = new BattleSkillData(SkillId.YouFengLaiYiHuo, v);
                //     Skills[SkillId.YouFengLaiYiTu] = new BattleSkillData(SkillId.YouFengLaiYiTu, v);
                //     continue;
                // }

                // 泽披天下
                if (skid == SkillId.ZePiTianXia)
                {
                    Attrs.Add(AttrType.DhunLuan, 5);
                    Attrs.Add(AttrType.DfengYin, 5);
                    Attrs.Add(AttrType.DhunShui, 5);
                    Attrs.Add(AttrType.DyiWang, 5);
                }

                Skills[skid] = new BattleSkillData(skid, v);
            }

            // 飞升技能
            if (SkillManager.IsValid(petSsSkill))
            {
                Skills[petSsSkill] = new BattleSkillData(petSsSkill, 0);
            }
        }

        // 孩子技能概率？
        public int ChildSkillTargetNum(SkillId id)
        {
            var rate = ChildSkills.GetValueOrDefault(id, 0);
            if (rate <= 0 || _random.Next(100) >= rate)
            {
            // // FIXME: 测试
            // if (rate <= 0)
            // {
                return 0;
            }
            return 1;
        }

        // 分裂
        public bool FenLie()
        {
            BaseSkill skill = null;
            if (HasPassiveSkill(SkillId.FenLieGongJi))
            {
                skill = SkillManager.GetSkill(SkillId.FenLieGongJi);
            }

            if (HasPassiveSkill(SkillId.HighFenLieGongJi))
            {
                skill = SkillManager.GetSkill(SkillId.HighFenLieGongJi);
            }

            if (skill == null) return false;

            // 骷髅王100%分裂
            if (IsMonster && Data.CfgId == 6700) return true;

            var effectData = skill.GetEffectData(new GetEffectDataRequest
            {
                Level = Data.Level,
                Relive = Relive,
                Intimacy = Data.PetIntimacy,
                Profic = GetSkillProfic(skill.Id),
                Atk = Atk,
                Deadnum = 0,
                MaxMp = MpMax,
                Attrs = Attrs,
                OrnamentSkills = OrnamentSkills,
                Member = this
            });
            // 概率
            var r = _random.Next(0, 100);
            return r < effectData.Percent;
        }

        public uint GetSkillProfic(SkillId id)
        {
            uint profic = 0;
            if (id == SkillId.NormalAtk) return profic;
            if (IsPlayer)
            {
                Skills.TryGetValue(id, out var info);
                if (info != null) profic = info.Profic;
            }
            else if (IsPartner || IsPet)
            {
                profic = ConfigService.GetRoleSkillMaxExp(Relive);
            }
            else if (IsMonster)
            {
                profic = _skillProfic;
            }

            if (profic == 0) profic = 1;
            return profic;
        }

        public SkillId GetAiSkill()
        {
            if (IsPlayer || IsPet)
            {
                if (LastSkill == SkillId.Unkown) LastSkill = SkillId.NormalAtk;
                return LastSkill;
            }
            // 金翅大鹏
            if (IsEagleMain())
            {
                // false 则为测试技能
#if true
                var noLeft = _eagleLeft == null || _eagleLeft.Dead;
                var noRight = _eagleRight == null || _eagleRight.Dead;
                var list = new List<SkillId>();
                foreach (var (k, v) in Skills)
                {
                    // 如果左右翼之一死亡，就没有风卷残云 主
                    if (k == SkillId.FengJuanCanYunMain && (noLeft || noRight)) continue;
                    // 如果左右翼都没死亡，就没有诛天灭地
                    // if (k == SkillId.ZhuTianMieDi && !noLeft && !noRight) continue;
                    list.Add(k);
                }
                // 10% 物理攻击
                if (_random.Next(100) < 10)
                {
                    return SkillId.NormalAtk;
                }
                return list[_random.Next(list.Count)];
#else
                // 测试 诛天灭地
                return SkillId.ZhuTianMieDi;
                // 测试 风卷残云 主
                // return SkillId.FengJuanCanYunMain;
#endif
            }
            // 金翅大鹏 左右翼 30%几率释放技能，其他为防御
            if (IsEagleLeft() || IsEagleRight())
            {
                // false 则为测试技能
#if true
                var list = Skills.Keys.ToList();
                if (list.Count > 0)
                {
                    // 30% 防御
                    return _random.Next(0, 100) < 30 ? SkillId.NormalDef : list[_random.Next(list.Count)];
                }
                else
                {
                    return SkillId.NormalDef;
                }
#else
                // 测试 风卷残云 左 右
                return IsEagleLeft() ? SkillId.FengJuanCanYunLeft : SkillId.FengJuanCanYunRight;
#endif
            }

            var atkList = new List<SkillId>();
            var defList = new List<SkillId>();

            foreach (var (k, v) in Skills)
            {
                if (!v.CanUse || v.CoolDown > 0) continue;
                if (SkillManager.IsAtkSkill(k)) atkList.Add(k);
                if (SkillManager.IsSelfBuffSkill(k)) defList.Add(k);
                if (SkillManager.IsDebuffSkill(k)) atkList.Add(k);
            }

            if (defList.Count > 0)
            {
                if (DefSkillTimes % 3 == 0)
                {
                    DefSkillTimes++;
                    var skid = defList[_random.Next(0, defList.Count)];
                    return skid;
                }

                DefSkillTimes++;
            }

            if (atkList.Count > 0)
            {
                var skid = atkList[_random.Next(0, atkList.Count)];
                return skid;
            }

            return SkillId.NormalAtk;
        }

        public void AddLimitSkill(SkillId id)
        {
            if (UsedSkills.ContainsKey(id))
            {
                UsedSkills[id] += 1;
            }
            else
            {
                UsedSkills[id] = 1;
            }
        }

        public bool HasPassiveSkill(SkillId id)
        {
            return Skills.ContainsKey(id);
        }

        public bool HasSkill(SkillId id)
        {
            if (id == SkillId.NormalAtk || id == SkillId.NormalDef) return true;
            return Skills.ContainsKey(id);
        }

        public void CheckKongZhiRound(uint round)
        {
            foreach (var buff in Buffs)
            {
                if (SkillManager.IsKongZhiSkill(buff.SkillId))
                {
                    KongZhiRound = round;
                    break;
                }
            }
        }

        // 获得龙族治愈BUFF
        public List<Buff> GetLongBuffs()
        {
            var result = new List<Buff>();
            foreach (var b in Buffs)
            {
                if (b.SkillId == SkillId.PeiRanMoYu || b.SkillId == SkillId.ZeBeiWanWu)
                {
                    result.Add(b);
                }
            }
            return result;
        }

        public List<uint> GetBuffsSkillId()
        {
            return Buffs.Select(p => (uint) p.SkillId).ToList();
        }

        public Buff GetBuffByMagicType(SkillType type)
        {
            return Buffs.FirstOrDefault(p => p.SkillType == type);
        }

        public void AddBuff(Buff buff)
        {
            // 金翅大鹏 不受混乱、遗忘
            if ((buff.SkillType == SkillType.Chaos || buff.SkillType == SkillType.Forget) && IsEagle() ) {
                return;
            }
            if (HasBuff(SkillType.Seal)) return;
            if (buff.SkillType == SkillType.Seal)
            {
                RemoveAllBuff();
                Buffs.Add(buff);
                buff.OnAppend(this);
                return;
            }

            if (SkillManager.IsWuXingSkill(buff.SkillId))
            {
                // 移除已有的其他五行buff
                for (var i = Buffs.Count - 1; i >= 0; i--)
                {
                    var xb = Buffs[i];
                    if (SkillManager.IsWuXingSkill(xb.SkillId)) RemoveBuff(xb.Id);
                }
            }

            if (SkillManager.IsKongZhiSkill(buff.SkillId))
            {
                // 同时只能有一个控制技
                for (var i = Buffs.Count - 1; i >= 0; i--)
                {
                    var xb = Buffs[i];
                    if (SkillManager.IsKongZhiSkill(xb.SkillId)) RemoveBuff(xb.Id);
                }
            }

            // 龙族--震击技能BUFF替换
            var skillId = buff.SkillId;
            var isLongZhenJiBuff =
                            skillId == SkillId.CangHaiHengLiu
                         || skillId == SkillId.BaiLangTaoTian
                         || skillId == SkillId.FeiJuJiuTian
                         || skillId == SkillId.LingXuYuFeng;
            if (isLongZhenJiBuff)
            {
                var toRemove = new List<uint>();
                for (var i = 0; i < Buffs.Count; i++)
                {
                    var oldBuff = Buffs[i];
                    var oldSkillId = oldBuff.SkillId;
                    if (oldSkillId == SkillId.CangHaiHengLiu
                     || oldSkillId == SkillId.BaiLangTaoTian
                     || oldSkillId == SkillId.FeiJuJiuTian
                     || oldSkillId == SkillId.LingXuYuFeng)
                    {
                        toRemove.Add(oldBuff.Id);
                        // Console.WriteLine($"龙族震击技能BUFF删除{oldSkillId}");
                    }
                }
                foreach (var id in toRemove)
                {
                    RemoveBuff(id);
                }
                Buffs.Add(buff);
                buff.OnAppend(this);
                // Console.WriteLine($"龙族震击技能BUFF替换{buff.SkillId}");
                return;
            }
            // 龙族治愈技能BUFF特殊处理
            var isLongZhiYuBuff =
                            skillId == SkillId.PeiRanMoYu
                         || skillId == SkillId.ZeBeiWanWu;
            if (isLongZhiYuBuff)
            {
                var toRemove = new List<uint>();
                for (var i = 0; i < Buffs.Count; i++)
                {
                    var oldBuff = Buffs[i];
                    var oldSkillId = oldBuff.SkillId;
                    if (oldSkillId == SkillId.PeiRanMoYu
                     || oldSkillId == SkillId.ZeBeiWanWu)
                    {
                        toRemove.Add(oldBuff.Id);
                        // Console.WriteLine($"龙族治愈BUFF删除{oldSkillId}");
                    }
                }
                foreach (var id in toRemove)
                {
                    RemoveBuff(id);
                }
                Buffs.Add(buff);
                buff.OnAppend(this);
                // Console.WriteLine($"龙族治愈BUFF添加{buff.SkillId}");
                return;
            }

            var newIdx = -1;
            var allEq = true;

            for (var i = 0; i < Buffs.Count; i++)
            {
                var oldBuff = Buffs[i];
                if (oldBuff.SkillId == buff.SkillId)
                {
                    newIdx = i;

                    foreach (var (k, v) in oldBuff.EffectData)
                    {
                        // 如果新加的buff数值要大, 则替换掉使用大数值的buff
                        if (buff.EffectData[k] > v)
                        {
                            RemoveBuff(oldBuff.Id);
                            Buffs.Add(buff);
                            buff.OnAppend(this);
                            return;
                        }

                        if (Math.Abs(buff.EffectData[k] - v) > 0.001f)
                        {
                            allEq = false;
                        }
                    }
                }
            }

            if (newIdx == -1)
            {
                Buffs.Add(buff);
                buff.OnAppend(this);
            }
            else if (allEq)
            {
                // 数值完全一样，就重置buff的Round
                Buffs[newIdx].Round = 0;
                Buffs[newIdx].OnResetRound();
            }
        }

        // 被种了，荼蘼花开
        public Buff GetBuffTuMiHuaKai()
        {
            return Buffs.Find(p => p.SkillId == SkillId.TuMiHuaKai);
        }

        // 删除，荼蘼花开
        public void RemoveBuffTuMiHuaKai()
        {
            var idx = Buffs.FindIndex(p => p.SkillId == SkillId.TuMiHuaKai);
            if (idx < 0) return;
            RemoveBuff(idx);
        }

        public bool HasBuff(SkillType type)
        {
            return Buffs.Exists(p => p.SkillType == type);
        }

        public bool HasDeBuff()
        {
            return Buffs.Exists(p => SkillManager.IsDebuffSkill(p.SkillId));
        }

        public void RemoveAllBuff()
        {
            for (var i = Buffs.Count - 1; i >= 0; i--)
            {
                RemoveBuff(i);
            }

            Buffs.Clear();
        }

        public void RemoveBuff(uint id)
        {
            var idx = Buffs.FindIndex(p => p.Id == id);
            if (idx < 0) return;
            RemoveBuff(idx);
        }

        private void RemoveBuff(int index)
        {
            if (index < 0 || index >= Buffs.Count) return;
            var buff = Buffs[index];
            if (buff.SkillType == SkillType.Forget)
            {
                foreach (var (_, v) in Skills)
                {
                    v.CanUse = true;
                }
            }

            Buffs.RemoveAt(index);
            buff.OnRemove(this);
        }

        public void RemoveBuff(SkillType type)
        {
            var list = (from p in Buffs where p.SkillType == type select p.Id).ToList();
            foreach (var id in list)
            {
                RemoveBuff(id);
            }
        }

        public void RemoveDeBuff(uint round = 0, Dictionary<uint, BattleMember> members = null)
        {
            var list = Buffs.Where(p => SkillManager.IsDebuffSkill(p.SkillId)).ToList();
            foreach (var buff in list)
            {
                // 魅影缠身, 魅惑Buff前3回合不能被春回大地、春意盎然清除
                if (round is > 0 and <= 3 && members != null && buff.SkillType == SkillType.Charm)
                {
                    continue;
                }

                RemoveBuff(buff.Id);
            }
        }

        // 去掉加防、加攻、加速 BUFF
        public void RemoveFGSBuff()
        {
            var list = Buffs.Where(p => SkillManager.IsFGSSkill(p.SkillId)).Select(p => p.Id).ToList();
            foreach (var bid in list)
            {
                RemoveBuff(bid);
            }
        }

        // 是否有加防、加攻、加速 BUFF？
        public bool HasFGSBuff()
        {
            return Buffs.Where(p => SkillManager.IsFGSSkill(p.SkillId)).Select(p => p.Id).Count() > 0;
        }

        public void RemoveKongZhiBuff()
        {
            var list = Buffs.Where(p => SkillManager.IsKongZhiSkill(p.SkillId)).Select(p => p.Id).ToList();
            foreach (var bid in list)
            {
                RemoveBuff(bid);
            }
            this.continue_be_kongzhi = 0;
            // 是否刚摆脱控制？
            this.just_not_kongzhi = true;
        }

        // 是否被控制？
        public bool HasKongZhiBuff()
        {
            return Buffs.Where(p => SkillManager.IsKongZhiSkill(p.SkillId)).Select(p => p.Id).Count() > 0;
        }

        // 有特定技能的buff？
        public bool HasBuffBySkillId(SkillId skillId)
        {
            return Buffs.Where(p => p.SkillId == skillId).Select(p => p.Id).Count() > 0;
        }

        public void CheckReplaceBuffRound(SkillId id, uint round)
        {
            foreach (var buff in Buffs)
            {
                if (buff.SkillId == id)
                {
                    if (buff.Round - buff.CurRound < round)
                    {
                        buff.CurRound = 0;
                        buff.Round = round;
                    }
                }
            }
        }

        public float GetKuangBaoPre(SkillType type)
        {
            // 宠物没有狂暴率
            var ret = type switch
            {
                SkillType.Physics => Attrs.Get(AttrType.PkuangBao),
                SkillType.Huo => Attrs.Get(AttrType.KhuoLv),
                SkillType.Shui => Attrs.Get(AttrType.KshuiLv),
                SkillType.Feng => Attrs.Get(AttrType.KfengLv),
                SkillType.Lei => Attrs.Get(AttrType.KleiLv),
                SkillType.ThreeCorpse => Attrs.Get(AttrType.KsanShiLv),
                SkillType.GhostFire => Attrs.Get(AttrType.KguiHuoLv),
                _ => 0f
            };
            return ret;
        }

        public float GetKuangBaoStr(SkillType type)
        {
            var ret = type switch
            {
                SkillType.Physics => 50,
                SkillType.Huo => 0,
                SkillType.Shui => 0,
                SkillType.Feng => 0,
                SkillType.Lei => 0,
                SkillType.ThreeCorpse => 0,
                SkillType.GhostFire => 0,
                _ => 0f
            };
            return ret;
        }

        public float GetPoFangPre()
        {
            return 0f;
        }

        public float GetPoFang()
        {
            var pflv = Attrs.Get(AttrType.PpoFangLv);
            if (_random.Next(0, 100) >= pflv) return 0;
            return Attrs.Get(AttrType.PpoFang);
        }

        public float GetKangWuLi()
        {
            return Attrs.Get(AttrType.DwuLi);
        }

        // 连击
        public int GetLianJi()
        {
            var num = 0;
            var rnd = new Random();
            var lv = Attrs.Get(AttrType.PlianJiLv);
            var r = rnd.Next(0, 100);
            if (r >= lv) return num;
            var max = (int) Attrs.Get(AttrType.PlianJi);
            num = Math.Clamp(max, 0, 6);

            // r = _random.Next(0, 100);
            // num = (int) MathF.Ceiling(r / (100 / max));
            return num;
        }

        // 隔山打牛
        public float GetGeShan()
        {
            BaseSkill skill = null;
            var rate = 0;

            if (HasPassiveSkill(SkillId.GeShanDaNiu))
            {
                skill = SkillManager.GetSkill(SkillId.GeShanDaNiu);
                rate = 35;
            }

            if (HasPassiveSkill(SkillId.HighGeShanDaNiu))
            {
                skill = SkillManager.GetSkill(SkillId.HighGeShanDaNiu);
                rate = 45;
            }

            if (skill == null) return 0;

            // 骷髅王100%隔山
            if (IsMonster && Data.CfgId == 6700) rate = 100;

            var r = _random.Next(0, 100);
            if (r > rate) return 0;

            var effectData = skill.GetEffectData(new GetEffectDataRequest
            {
                Atk = (uint) Attrs.Get(AttrType.Atk),
                Relive = Relive,
                Level = Data.Level,
                Intimacy = Data.PetIntimacy,
                Member = this
            });
            return effectData.Percent;
        }

        public SkillEffectData GetTianJiangTuoTu()
        {
            if (!HasPassiveSkill(SkillId.TianJiangTuoTu)) return null;
            var skill = SkillManager.GetSkill(SkillId.TianJiangTuoTu);
            var effectData = skill.GetEffectData(new GetEffectDataRequest
            {
                Atk = (uint) Attrs.Get(AttrType.Atk),
                Relive = Relive,
                Level = Data.Level,
                Intimacy = Data.PetIntimacy,
                Member = this
            });
            var r = _random.Next(0, 100);
            if (r > effectData.Percent) return null;
            return effectData;
        }

        // 确定是否可以释放 荼蘼花开
        public SkillEffectData GetTuMiHuaKai()
        {
            if (!HasPassiveSkill(SkillId.TuMiHuaKai)) return null;
            var skill = SkillManager.GetSkill(SkillId.TuMiHuaKai);
            var effectData = skill.GetEffectData(new GetEffectDataRequest
            {
                Atk = (uint) Attrs.Get(AttrType.Atk),
                Relive = Relive,
                Level = Data.Level,
                Intimacy = Data.PetIntimacy,
                Member = this
            });
            var r = _random.Next(0, 100);
            if (r > effectData.Percent) return null;
            return effectData;
        }

        public SkillEffectData GetHuanYingLiHun()
        {
            if (!HasPassiveSkill(SkillId.HuanYingLiHun)) return null;
            var skill = SkillManager.GetSkill(SkillId.HuanYingLiHun);
            var effectData = skill.GetEffectData(new GetEffectDataRequest
            {
                Atk = (uint) Attrs.Get(AttrType.Atk),
                Relive = Relive,
                Level = Data.Level,
                Intimacy = Data.PetIntimacy,
                Member = this
            });
            var r = _random.Next(0, 100);
            if (r > effectData.Percent) return null;
            return effectData;
        }

        // 涅槃
        public bool NiePan()
        {
            if (!HasPassiveSkill(SkillId.NiePan)) return false;
            if (UsedSkills.ContainsKey(SkillId.NiePan)) return false;

            var r = _random.Next(0, 100);
            if (r < 30) return false;
            Attrs.Set(AttrType.Hp, HpMax);
            Attrs.Set(AttrType.Mp, Attrs.Get(AttrType.MpMax));
            RemoveAllBuff();
            Dead = false;
            UsedSkills.Add(SkillId.NiePan, 1);
            return true;
        }

        // 闪现
        public int ShanXian()
        {
            BaseSkill skill = null;
            if (Pos == -1) return 1;

            if (HasPassiveSkill(SkillId.ShanXian))
            {
                skill = SkillManager.GetSkill(SkillId.ShanXian);
            }

            if (HasPassiveSkill(SkillId.HighShanXian))
            {
                skill = SkillManager.GetSkill(SkillId.HighShanXian);
            }

            if (HasPassiveSkill(SkillId.SuperShanXian))
            {
                skill = SkillManager.GetSkill(SkillId.SuperShanXian);
            }

            // 该宠物没有学习闪现技能，继续检测下一个宠物
            if (skill == null) return 1;
            var effectData = skill.GetEffectData(new GetEffectDataRequest
            {
                Level = Data.Level,
                Relive = Relive,
                Intimacy = Data.PetIntimacy,
                Profic = GetSkillProfic(skill.Id),
                Atk = Atk,
                Deadnum = 0,
                MaxMp = MpMax,
                Attrs = Attrs,
                OrnamentSkills = OrnamentSkills,
                Member = this
            });
            var shanXianRate = effectData.Percent;
            var r = _random.Next(0, 100);
            // 概率没达到, 停止检测闪现
            if (r >= shanXianRate) return 2;
            // 成功闪现
            return 0;
        }

        // 分花拂柳
        public bool FenHuaFuLiu()
        {
            if (!HasPassiveSkill(SkillId.FenHuaFuLiu)) return false;
            // 骷髅王100%分花
            if (IsMonster && Data.CfgId == 6700) return true;

            var skill = SkillManager.GetSkill(SkillId.FenHuaFuLiu);
            var effectData = skill.GetEffectData(new GetEffectDataRequest
            {
                Level = Data.Level,
                Relive = Relive,
                Intimacy = Data.PetIntimacy,
                Member = this
            });

            var r = _random.Next(0, 100);
            return r < effectData.Percent;
        }

        // 吉人天相
        public bool JiRenTianXiang(uint round)
        {
            if (!HasSkill(SkillId.JiRenTianXiang)) return false;
            var skill = SkillManager.GetSkill(SkillId.JiRenTianXiang);
            if (skill == null) return false;
            if (round <= skill.LimitRound) return false;
            UsedSkills.TryGetValue(SkillId.JiRenTianXiang, out var cnt);
            if (cnt >= skill.LimitTimes) return false;
            AddLimitSkill(SkillId.JiRenTianXiang);

            var effectData = skill.GetEffectData(new GetEffectDataRequest());
            Attrs.Set(AttrType.Hp, Math.Min(effectData.Hp, HpMax));
            Dead = false;

            RemoveDeBuff();
            return true;
        }

        /// <summary>
        /// 拦截白泽的泽披天下
        /// </summary>
        public float Hurt(float value, Dictionary<uint, BattleMember> members, out BattleAttackData attackData)
        {
            attackData = null;

            // 泽披天下
            var zptx = GetBuffByMagicType(SkillType.Protect);
            if (members != null && zptx != null && zptx.Source != OnlyId)
            {
                // 查找白泽
                members.TryGetValue(zptx.Source, out var baize);
                if (baize != null)
                {
                    // 白泽受到的伤害
                    var hurt2 = MathF.Max(0, MathF.Floor(value * zptx.EffectData.SuckHurtPercent / 100));
                    if (hurt2 > 0)
                    {
                        // 受庇护的对象减少伤害
                        value = MathF.Max(0, value - hurt2);

                        // 白泽衰减伤害
                        hurt2 = MathF.Floor(hurt2 * zptx.EffectData.HurtDecayPercent / 100);
                        if (hurt2 > baize.Hp) hurt2 = baize.Hp;
                        if (hurt2 > 0)
                        {
                            baize.AddHp(-hurt2);

                            attackData = new BattleAttackData
                            {
                                OnlyId = baize.OnlyId,
                                Type = BattleAttackType.Hp,
                                Response = BattleResponseType.None,
                                Value = (int) -hurt2,
                                Hp = baize.Hp,
                                Mp = baize.Mp,
                                Dead = baize.Dead,
                                Buffs = {baize.GetBuffsSkillId()}
                            };
                        }
                    }
                }
            }

            // 幽怜魅影
            var ylmy = GetBuffByMagicType(SkillType.RaiseHurt);
            if (ylmy != null && ylmy.EffectData.HurtRaisePercent > 0)
            {
                value = MathF.Floor(value * (1 + ylmy.EffectData.HurtRaisePercent / 100));
            }

            // 皮糙肉厚
            if (Skills.ContainsKey(SkillId.PiCaoRouHou))
            {
                value *= 0.5f;
            }

            if (value >= Hp) value = Hp;
            AddHp(-value);
            return value;
        }

        public float AddHp(float value, Dictionary<uint, BattleMember> members = null)
        {
            // 金翅大鹏 如果左右翼没有被打掉的话，大鹏无敌（不减血）
            if (value < 0 && IsEagleMain() && ((_eagleLeft != null && !_eagleLeft.Dead) || (_eagleRight != null && !_eagleRight.Dead)))
            {
                return 0;
            }
            if (members != null && value > 0)
            {
                // 毒浸骨髓
                var buff = GetBuffByMagicType(SkillType.Toxin);
                if (buff != null)
                {
                    members.TryGetValue(buff.Source, out var enemy);
                    if (enemy != null &&
                        (enemy.OrnamentSkills.ContainsKey(1091) || enemy.OrnamentSkills.ContainsKey(1092)))
                    {
                        var percent = 0.3f;
                        if (enemy.OrnamentSkills.ContainsKey(1092))
                        {
                            percent += MathF.Floor(enemy.Attrs.Get(AttrType.GenGu) / 200) * 0.05f;
                        }

                        // 最高不超过50%
                        percent = MathF.Min(percent, 0.5f);
                        value = MathF.Ceiling(value * (1.0f - percent));
                    }
                }
            }
            var val = Attrs.Get(AttrType.Hp) + value;
            if (val > 0)
            {
                val = Math.Min(HpMax, val);
            }
            Dead = val <= 0;
            Attrs.Set(AttrType.Hp, val);
            if (Dead) DeadTimes++;
            return value;
        }

        public float AddMp(float value, Dictionary<uint, BattleMember> members = null)
        {
            if (members != null && value > 0)
            {
                // 毒浸骨髓
                var buff = GetBuffByMagicType(SkillType.Toxin);
                if (buff != null)
                {
                    members.TryGetValue(buff.Source, out var enemy);
                    if (enemy != null &&
                        (enemy.OrnamentSkills.ContainsKey(1091) || enemy.OrnamentSkills.ContainsKey(1092)))
                    {
                        var percent = 0.3f;
                        if (enemy.OrnamentSkills.ContainsKey(1092))
                        {
                            percent += MathF.Floor(enemy.Attrs.Get(AttrType.GenGu) / 200) * 0.05f;
                        }

                        // 最高不超过50%
                        percent = MathF.Min(percent, 0.5f);
                        value = MathF.Ceiling(value * (1.0f - percent));
                    }
                }
            }
            var val = Math.Min(Math.Max(Attrs.Get(AttrType.MpMax), 0), Math.Max(Attrs.Get(AttrType.Mp) + value, 0));
            Attrs.Set(AttrType.Mp, val);

            return value;
        }

        public void AddMoney(MoneyType type, int value, string tag = "")
        {
            if (Grain != null)
                _ = Grain.AddMoney((byte) type, value, tag);
        }

        public void AddBagItem(uint cfgId, int num, bool notice)
        {
            if (Grain != null)
                _ = Grain.AddBagItem(cfgId, num, notice, "战斗消耗");
        }

        public ValueTask<uint> GetBagItemCount(uint cfgId) {
            if (Grain != null) {
                return Grain.GetBagItemCount(cfgId);
            }
            return ValueTask.FromResult((uint)0);
        }

        public void CreatePet(uint cfgId, string from = null)
        {
            if (Grain != null)
                _ = Grain.CreatePet(cfgId, from);
        }

        public void ExitBattle(ExitBattleRequest req)
        {
            if (Grain != null)
            {
                _ = Grain.ExitBattle(new Immutable<byte[]>(Packet.Serialize(req)));
            }
        }

        public void SendPacket(GameCmd command, IMessage msg)
        {
            if (Grain != null)
            {
                var bytes = Packet.Serialize(command, msg);
                _ = Grain.SendMessage(new Immutable<byte[]>(bytes));
            }
        }

        public void SendPacket(Immutable<byte[]> bytes)
        {
            if (Grain != null)
            {
                _ = Grain.SendMessage(bytes);
            }
        }

        public void Broadcast(GameCmd command, IMessage msg)
        {
            if (Grain != null)
            {
                var bytes = Packet.Serialize(command, msg);
                _ = Grain.BroadcastMessage(new Immutable<byte[]>(bytes));
            }
        }

        public float Hp => Attrs.Get(AttrType.Hp);

        public float HpMax => Math.Max(Attrs.Get(AttrType.HpMax), 1);

        public uint Mp => (uint) MathF.Floor(Attrs.Get(AttrType.Mp));

        public uint MpMax => (uint) MathF.Floor(Attrs.Get(AttrType.MpMax));

        public uint Atk => (uint) MathF.Floor(Attrs.Get(AttrType.Atk));

        public int Spd => (int) MathF.Floor(Attrs.Get(AttrType.Spd));

        public BattleObjectData BuildObjectData()
        {
            var objData = new BattleObjectData
            {
                OnlyId = OnlyId,
                Type = Data.Type,
                Res = Data.Res,
                Name = Data.Name,
                Pos = Pos,
                Hp = Hp,
                HpMax = HpMax,
                Mp = Mp,
                MpMax = MpMax,
                Relive = Data.Relive,
                Level = Data.Level,
                PetColor = Data.PetColor,
                Color1 = Data.Color1,
                Color2 = Data.Color2,
                LastSkill = LastSkill,
                Weapon = Data.Weapon,
                IsFight = Pos > 0,
                IsBaoBao = IsBb,
                OwnerId = OwnerOnlyId,
                InstId = Data.Id,
                CfgId = Data.CfgId,
                Skins = {Data.Skins},
                Wing = Data.Wing,
                Bianshen = Data.Bianshen,
                VipLevel = Data.VipLevel,
                QiegeLevel = Data.QiegeLevel,
                IsPs = Data.PetJxGrade > 0,
                ShenzhiliHurtLevel = Data.ShenzhiliHurtLevel,
            };

            if (IsPet)
            {
                foreach (var v in Skills.Values)
                {
                    objData.Skills.Add(v.Id);
                    // 被动技能就不下发了
                    // if (!SkillManager.IsPassiveSkill(v.Id))
                }
            }
            else
            {
                // 孩子信息--外观和名字
                if (Data.Child != null && Data.Child.Shape != 0)
                {
                    objData.Child = new BattleChildObjectData()
                    {
                        Shape = Data.Child.Shape,
                        Name = Data.Child.Name,
                        AniName = Data.Child.AniName,
                    };
                }
            }

            return objData;
        }
    }

    public class Action
    {
        // 1伤害 2治疗 3buff
        public BattleActionType ActionType;
        public uint ActionId;
        public SkillId Skill;
        public uint Target;

        public Action Clone()
        {
            return new Action
            {
                ActionType = ActionType,
                ActionId = ActionId,
                Skill = Skill,
                Target = Target
            };
        }

        public bool Like(Action other)
        {
            if (other == null) return false;
            if (ActionType != other.ActionType) return false;
            if (ActionType == BattleActionType.Skill && ActionId != other.ActionId) return false;
            return true;
        }
    }

    public class BattleSkillData
    {
        public readonly SkillId Id;
        public uint Profic;
        public bool CanUse;
        public uint CoolDown;

        public BattleSkillData(SkillId id, uint profic)
        {
            Id = id;
            Profic = profic;
            CanUse = true;
            CoolDown = 0;
        }
    }

    public class OrnamentSkillData
    {
        public uint Id { get; }

        public OrnamentSkillData(uint id)
        {
            Id = id;
        }
    }
}