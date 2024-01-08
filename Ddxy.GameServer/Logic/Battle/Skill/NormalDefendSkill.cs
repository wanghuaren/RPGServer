using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class NormalDefendSkill : BaseSkill
    {
        public NormalDefendSkill() : base(SkillId.NormalDef, "防御")
        {
            BuffType = SkillBuffType.Once;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData
            {
                Round = 1,
                FangYu = 30
            };
        }
    }
}