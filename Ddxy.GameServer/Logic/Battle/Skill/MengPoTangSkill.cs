using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class MengPoTangSkill : BaseSkill
    {
        public MengPoTangSkill() : base(SkillId.MengPoTang, "孟婆汤")
        {
            Type = SkillType.Forget;
            BuffType = SkillBuffType.Once;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            // 抗性要控制回合数量, 要确定抗性带来的回合数减少的公式
            // 概率，和熟练度有关系，也能被抗性抵抗
            // 打到目标身上后，随机遗忘3-6个技能
            var effectData = new SkillEffectData
            {
                Percent = 75,
                Round = (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 7 / 100)),
                TargetNum = Math.Min(7, (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.35f) * 5 / 100)))
            };

            // 加强遗忘
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqYiWang);
                if (value > 0)
                {
                    effectData.Percent += value;
                }
            }

            return effectData;
        }
    }
}