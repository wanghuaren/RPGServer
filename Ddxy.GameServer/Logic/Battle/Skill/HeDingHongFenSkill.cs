using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HeDingHongFenSkill : BaseSkill
    {
        public HeDingHongFenSkill() : base(SkillId.HeDingHongFen, "鹤顶红粉")
        {
            Type = SkillType.Toxin;
            BuffType = SkillBuffType.Loop;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var level = request.Level.GetValueOrDefault();
            var profic = request.Profic.GetValueOrDefault();
            var xianHurt = MathF.Floor(65 * level * (MathF.Pow(profic, 0.4f) * 2.8853998118144273f / 100 + 1));
            var hurt = xianHurt / 3f;
            hurt *= 1.25f;

            var effectData = new SkillEffectData
            {
                Hurt = hurt,
                Round = (int) MathF.Floor(2 * (1 + MathF.Pow(profic, 0.34f) * 4 / 100))
            };

            // 加强毒
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqDu);
                if (value > 0)
                {
                    var percent = value / 100.0f;
                    effectData.Hurt = (int) MathF.Round(effectData.Hurt * (1 + percent));
                }
            }

            return effectData;
        }
    }
}