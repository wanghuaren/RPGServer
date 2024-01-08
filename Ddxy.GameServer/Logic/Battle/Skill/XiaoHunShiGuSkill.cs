using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class XiaoHunShiGuSkill : BaseSkill
    {
        public XiaoHunShiGuSkill() : base(SkillId.XiaoHunShiGu, "销魂蚀骨")
        {
            Type = SkillType.Frighten;
            TargetType = SkillTargetType.Enemy;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                HurtPercent = MathF.Round(27 * (MathF.Pow(profic, 0.35f) * 2 / 100 + 1)),
                MpPercent2 = MathF.Round(38 * (MathF.Pow(profic, 0.33f) * 2 / 100 + 1))
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
            
            if (effectData.HurtPercent > 45) effectData.HurtPercent = 45;

            return effectData;
        }
    }
}