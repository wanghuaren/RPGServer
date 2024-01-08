using System.Collections.Generic;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public abstract class BaseSkill
    {
        public SkillId Id { get; }
        public string Name { get; }

        public SkillId Kind { get; protected set; }
        public SkillType Type { get; protected set; }
        public SkillQuality Quality { get; protected set; }
        public SkillTargetType TargetType { get; protected set; }
        public SkillActionType ActionType { get; protected set; }
        public SkillBuffType BuffType { get; protected set; }

        /// <summary>
        /// 技能影响的属性，直接修改宠物的属性(被动技)
        /// </summary>
        public List<AttrType> EffectTypes { get; }

        /// <summary>
        /// 技能冷却回合数
        /// </summary>
        public uint Cooldown { get; set; }

        /// <summary>
        /// 技能回合限制 前几回合不能用
        /// </summary>
        public uint LimitRound { get; set; }

        /// <summary>
        /// 技能限制使用次数
        /// </summary>
        public uint LimitTimes { get; set; }

        protected BaseSkill(SkillId id, string name)
        {
            Id = id;
            Name = name;

            Kind = SkillId.Unkown;
            Type = SkillType.Physics;
            Quality = SkillQuality.Low;
            TargetType = SkillTargetType.All;
            ActionType = SkillActionType.Initiative;
            BuffType = SkillBuffType.None;

            EffectTypes = new List<AttrType>(3);
        }

        public virtual bool UseSkill(BattleMember member, out string error)
        {
            // 怪物不耗蓝
            if (member.IsMonster)
            {
                error = null;
                return true;
            }
            var profic = member.GetSkillProfic(Id);
            // 计算技能所需的魔法值
            var needMp = Quality switch
            {
                SkillQuality.Low => profic * 0.13f,
                SkillQuality.High => profic * 0.42f,
                _ => 0f
            };
            // 天策符 千钧符  冥想符
            // 降低师门法术所需蓝耗
            needMp = GetNeedMpPost(member, needMp);
            // 检查魔法是否足够
            if (member.Mp < needMp)
            {
                error = "法力不足，无法释放";
                return false;
            }

            member.AddMp(-needMp);
            error = null;
            return true;
        }
        // 天策符 处理后的耗蓝
        // 降低师门法术所需蓝耗
        public float GetNeedMpPost(BattleMember member, float needMp)
        {
            if (member.IsPlayer)
            {
                var fskill = member.TianceFuSkills.GetValueOrDefault(SkillId.MingXiang3);
                var grade = 3;
                if (fskill == null)
                {
                    fskill = member.TianceFuSkills.GetValueOrDefault(SkillId.MingXiang2);
                    grade = 2;
                    if (fskill == null)
                    {
                        fskill = member.TianceFuSkills.GetValueOrDefault(SkillId.MingXiang1);
                        grade = 1;
                    }
                }
                if (fskill != null)
                {
                    needMp *= 0.9f - ((grade - 1) * 0.2f + fskill.Addition * 0.01f) * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel;
                }
            }
            return needMp;
        }
        // 施法效果--对自己（一定是对自己的效果）
        public virtual SkillEffectData GetEffectData2Self(GetEffectDataRequest request)
        {
            return null;
        }
        // 施法效果--对其他（自己、敌人、队友）
        public abstract SkillEffectData GetEffectData(GetEffectDataRequest request);
    }

    public class GetEffectDataRequest
    {
        public byte? Relive;
        public uint? Atk;
        public uint? Level;
        public float? Profic;
        public uint? Deadnum;
        public uint? Intimacy;
        public uint? MaxMp;

        public Attrs Attrs;
        public Dictionary<uint, OrnamentSkillData> OrnamentSkills;
        public BattleType BattleType;
        public BattleMember Member;
    }
}