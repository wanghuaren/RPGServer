using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class MiHunZuiSkill : BaseSkill
    {
        public MiHunZuiSkill() : base(SkillId.MiHunZui, "迷魂醉")
        {
            Type = SkillType.Sleep;
            BuffType = SkillBuffType.Once;
            Quality = SkillQuality.Low;
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