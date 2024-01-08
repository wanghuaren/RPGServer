using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class YanLuoZhuiMingSkill : BaseSkill
    {
        public YanLuoZhuiMingSkill() : base(SkillId.YanLuoZhuiMing, "阎罗追命")
        {
            Type = SkillType.Frighten;
            Quality = SkillQuality.High;
            TargetType = SkillTargetType.Enemy;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                HurtPercent = MathF.Round(25 * (MathF.Pow(profic, 0.35f) * 2 / 100 + 1)),
                MpPercent2 = MathF.Round(25 * (MathF.Pow(profic, 0.33f) * 2 / 100 + 1)),
                TargetNum = Math.Min(7, (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.35f) * 5 / 100)))
            };
            
            // 加强震慑
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqZhenShe);
                if (value > 0)
                {
                    effectData.HurtPercent += value;
                    effectData.MpPercent2 += value;
                }
            }

            // FIXME: 伤害百分比上限50%
            if (effectData.HurtPercent > 50) effectData.HurtPercent = 50;

            return effectData;
        }
    }
}