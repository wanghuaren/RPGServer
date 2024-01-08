using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class PeiYuGanLinSkill : BaseSkill
    {
        public PeiYuGanLinSkill() : base(SkillId.PeiYuGanLin, "沛雨甘霖")
        {
            Kind = SkillId.PeiYuGanLin;
            ActionType = SkillActionType.Passive;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}