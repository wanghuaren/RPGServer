using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class JiQiBuYiSkill : BaseSkill
    {
        public JiQiBuYiSkill() : base(SkillId.JiQiBuYi, "击其不意")
        {
            Kind = SkillId.JiQiBuYi;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}