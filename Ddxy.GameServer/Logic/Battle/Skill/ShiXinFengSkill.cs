using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class ShiXinFengSkill : BaseSkill
    {
        public ShiXinFengSkill() : base(SkillId.ShiXinFeng, "失心疯")
        {
            Type = SkillType.Forget;
            BuffType = SkillBuffType.Once;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                Percent = 85,
                Round = (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 7 / 100))
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