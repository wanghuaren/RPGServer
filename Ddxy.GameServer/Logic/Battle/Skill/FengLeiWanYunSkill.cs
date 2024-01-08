using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class FengLeiWanYunSkill : BaseSkill
    {
        // 提升自身命中、破防概率与破防程度对敌方1个单位进行物理攻击。
        public FengLeiWanYunSkill() : base(SkillId.FengLeiWanYun, "风雷万钧")
        {
            Type = SkillType.Physics;
            BuffType = SkillBuffType.Once;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
        }
        // 施法效果--对自己（一定是对自己的效果）
        public override SkillEffectData GetEffectData2Self(GetEffectDataRequest request)
        {
            // 提升自身命中和破防概率与破防程度
            return new SkillEffectData
            {
                // 命中 绝对值
                MingZhong = 50,
                // 破防率 绝对值
                PoFangLv = 50,
                // 破防程度 绝对值
                PoFang = 50,
            };
        }
        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();
            var level = (int)request.Level;
            var relive = (int)request.Relive;
            var atk = request.Atk.GetValueOrDefault();
            var JqHengSao = request.Attrs.Get(AttrType.JqHengSao);
            var effectData = new SkillEffectData
            {
                // 目标单元
                TargetNum = 1
            };
            // 技能基础伤害
            float shurt = (float)Math.Floor(55 * level * (profic * 0.2 * 2.8853998 / 200 + 1)
            + Math.Floor(50 * relive * (profic * 0.1 * 2 / 280 + 1)));
            // 加强伤害
            float ahurt = shurt * (request.Attrs.Get(AttrType.JqAtk) + JqHengSao) / 100;
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