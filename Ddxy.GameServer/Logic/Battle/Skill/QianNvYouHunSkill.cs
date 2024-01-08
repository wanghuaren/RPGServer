using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class QianNvYouHunSkill : BaseSkill
    {
        public QianNvYouHunSkill() : base(SkillId.QianNvYouHun, "倩女幽魂")
        {
            Type = SkillType.SubDefense;
            BuffType = SkillBuffType.Once;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                KongZhi2 = MathF.Round(1.3f * (MathF.Pow(profic, 0.35f) * 20 / 100 + 1)),
                Round = (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.35f) * 5 / 100)),
                TargetNum = Math.Min(7, (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 8 / 100)))
            };
            
            // 加强魅惑
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqMeiHuo);
                if (value > 0)
                {
                    var percent = value / 100.0f;
                    effectData.KongZhi2 += effectData.KongZhi2 * percent;
                }
            }
            
            return effectData;
        }
    }
}