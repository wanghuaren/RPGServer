using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class ZuoBiShangGuanSkill : BaseSkill
    {
        public ZuoBiShangGuanSkill() : base(SkillId.ZuoBiShangGuan, "作壁上观")
        {
            Type = SkillType.Seal;
            BuffType = SkillBuffType.Once;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            return new SkillEffectData
            {
                Round = (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 7 / 100))
            };
        }
    }
}