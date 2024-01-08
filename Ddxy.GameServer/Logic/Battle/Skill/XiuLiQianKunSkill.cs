using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class XiuLiQianKunSkill : BaseSkill
    {
        public XiuLiQianKunSkill() : base(SkillId.XiuLiQianKun, "袖里乾坤")
        {
            Type = SkillType.Feng;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var level = request.Level.GetValueOrDefault();
            var profic = request.Profic.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                TargetNum = Math.Min(5, (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 5 / 100))),
                Hurt = MathF.Floor(100 * level * (MathF.Pow(profic, 0.4f) * 2.8853998118144273f / 100 + 1))
            };
            // 伤害衰减1/3
            effectData.Hurt *= 0.667f;

            // 加强风
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqFeng);
                if (value > 0)
                {
                    var percent = value / 100.0f;
                    effectData.Hurt = (int) MathF.Round(effectData.Hurt * (1 + percent));
                }
            }

            if (request.Attrs != null && request.OrnamentSkills != null)
            {
                var lingXing = (int) request.Attrs.Get(AttrType.LingXing);
                if (lingXing >= 550)
                {
                    var delta = 0;

                    if (request.OrnamentSkills.ContainsKey(2022))
                    {
                        delta += lingXing * 40;
                    }
                    else if (request.OrnamentSkills.ContainsKey(2021))
                    {
                        delta += lingXing * 20;
                    }

                    if (delta > 0) effectData.Hurt += delta;
                }
            }

            return effectData;
        }
    }
}