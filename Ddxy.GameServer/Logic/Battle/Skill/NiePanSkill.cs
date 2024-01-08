using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class NiePanSkill : BaseSkill
    {
        public NiePanSkill() : base(SkillId.NiePan, "涅槃")
        {
            Kind = SkillId.NiePan;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Shen;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}