using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class PiCaoRouHouSkill: BaseSkill
    {
        public PiCaoRouHouSkill() : base(SkillId.PiCaoRouHou, "皮糙肉厚")
        {
            Kind = SkillId.PiCaoRouHou;
            Type = SkillType.Physics;
            ActionType = SkillActionType.Passive;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new();
        }
    }
}