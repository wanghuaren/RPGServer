using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class ShiXinKuangLuanSkill : BaseSkill
    {
        public ShiXinKuangLuanSkill() : base(SkillId.ShiXinKuangLuan, "失心狂乱")
        {
            Type = SkillType.Chaos;
            BuffType = SkillBuffType.Once;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            return new SkillEffectData
            {
                Round = (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 5 / 100)),
                TargetNum = Math.Min(5, (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.35f) * 3 / 100)))
            };
        }
    }
}