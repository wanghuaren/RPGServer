using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class LingXuYuFengSkill : BaseSkill
    {
        // 对敌方1个单位进行物理攻击，降低目标物理抗性持续2回合效果不叠加
        public LingXuYuFengSkill() : base(SkillId.LingXuYuFeng, "凌虚御风")
        {
            Type = SkillType.Physics;
            BuffType = SkillBuffType.Once;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
        }
        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();
            var level = (int)request.Level;
            var relive = (int)request.Relive;
            var atk = request.Atk.GetValueOrDefault();
            var JqPoJia = request.Attrs.Get(AttrType.JqPoJia);
            var effectData = new SkillEffectData
            {
                // 降低目标物理抗性持续2回合效果不叠加
                Round = 2,
                // 物理吸收
                PXiShou = -50,
                // 抗物理
                DWuLi = -50,
                // 目标单元
                TargetNum = 1
            };
            // 技能基础伤害
            float shurt = (float)Math.Floor(55 * level * (profic * 0.2 * 2.8853998 / 200 + 1)
            + Math.Floor(50 * relive * (profic * 0.1 * 2 / 280 + 1)));
            // 加强伤害
            float ahurt = shurt * (request.Attrs.Get(AttrType.JqAtk) + JqPoJia) / 100;
            // 普攻伤害
            float nhurt = atk;
            // 技能伤害 = 技能基础伤害 + 加强伤害 + 普攻伤害
            effectData.Hurt = (shurt + ahurt + nhurt) * 0.3f;
            // 逆鳞伤害提升
            effectData.Hurt = effectData.Hurt * (1.0f + (request.Member.HasBuff(SkillType.BianShen) ? 0.3f : 0f));
            return effectData;
        }
    }
}