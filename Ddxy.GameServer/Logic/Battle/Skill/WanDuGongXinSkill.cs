using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class WanDuGongXinSkill : BaseSkill
    {
        public WanDuGongXinSkill() : base(SkillId.WanDuGongXin, "万毒攻心")
        {
            Type = SkillType.Toxin;
            BuffType = SkillBuffType.Loop;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var level = request.Level.GetValueOrDefault();
            var profic = request.Profic.GetValueOrDefault();
            var xianHurt = MathF.Floor(65 * level * (MathF.Pow(profic, 0.4f) * 2.8853998118144273f / 100 + 1));
            var hurt = MathF.Floor(xianHurt / 3);

            var effectData = new SkillEffectData
            {
                Hurt = hurt,
                Round = (int) MathF.Floor(2 * (1 + MathF.Pow(profic, 0.34f * 4 / 100))),
                TargetNum = Math.Min(7, (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.35f) * 4 / 100)))
            };

            // 加强毒
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqDu);
                if (value > 0)
                {
                    var percent = value / 100.0f;
                    effectData.Hurt = (int) MathF.Round(effectData.Hurt * (1.2f + percent));
                }
            }

            return effectData;
        }
    }
}