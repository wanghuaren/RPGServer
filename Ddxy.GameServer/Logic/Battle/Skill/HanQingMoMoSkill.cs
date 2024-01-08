using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HanQingMoMoSkill : BaseSkill
    {
        public HanQingMoMoSkill() : base(SkillId.HanQingMoMo, "含情脉脉")
        {
            Type = SkillType.Defense;
            BuffType = SkillBuffType.Once;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                KongZhi = MathF.Round(0.9f * (MathF.Pow(profic, 0.35f) * 20 / 100 + 1)),
                FaShang = MathF.Round(15 * (MathF.Pow(profic, 0.35f) * 2 / 100 + 1)),
                FangYu = MathF.Round(12 * (MathF.Pow(profic, 0.35f) * 2 / 100 + 1)),
                Round = (int) MathF.Floor(3 * (MathF.Pow(profic, 0.35f) * 5 / 100 + 1)),
                TargetNum = Math.Min(7, (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 8 / 100)))
            };

            // 加强加防
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqDefend);
                if (value > 0)
                {
                    var percent = value / 100.0f + request.Member.GetXTXBAdd(AttrType.JqDefend);
                    effectData.KongZhi = MathF.Round(effectData.KongZhi * (1 + percent));
                    effectData.FaShang = MathF.Round(effectData.FaShang * (1 + percent));
                    effectData.FangYu = MathF.Round(effectData.FangYu * (1 + percent));
                }
            }

            return effectData;
        }
    }
}