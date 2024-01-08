using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class NiuZhuanQianKunSkill: BaseSkill
    {
        public NiuZhuanQianKunSkill() : base(SkillId.NiuZhuanQianKun, "扭转乾坤")
        {
            Kind = SkillId.NiuZhuanQianKun;
            Type = SkillType.Swap;
            ActionType = SkillActionType.Passive;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}