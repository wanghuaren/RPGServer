using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class JieDaoShaRenSkill : BaseSkill
    {
        public JieDaoShaRenSkill() : base(SkillId.JieDaoShaRen, "借刀杀人")
        {
            Type = SkillType.Chaos;
            BuffType = SkillBuffType.Once;
            Quality = SkillQuality.Low;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            return new SkillEffectData
            {
                Round = (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 5 / 100))
            };
        }
    }
}