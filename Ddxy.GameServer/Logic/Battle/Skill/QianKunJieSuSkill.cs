using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class QianKunJieSuSkill : BaseSkill
    {
        public QianKunJieSuSkill() : base(SkillId.QianKunJieSu, "乾坤借速")
        {
            Type = SkillType.Speed;
            BuffType = SkillBuffType.Once;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                SpdPercent = (int) MathF.Round(12 + 8 * profic / 25000),
                TargetNum = Math.Min(7, (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 8 / 100))),
                Round = (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.35f) * 5 / 100))
            };

            // 加强加速
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var jqSpd = attrs.Get(AttrType.JqSpd);
                if (jqSpd > 0)
                {
                    var percent = jqSpd / 100.0f + request.Member.GetXTXBAdd(AttrType.JqSpd);
                    effectData.SpdPercent = (int) MathF.Round(effectData.SpdPercent * (1 + percent));
                }
            }

            return effectData;
        }
    }
}