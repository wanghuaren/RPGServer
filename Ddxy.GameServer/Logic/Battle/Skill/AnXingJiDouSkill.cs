using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class AnXingJiDouSkill: BaseSkill
    {
        public AnXingJiDouSkill() : base(SkillId.AnXingJiDou, "安行疾斗")
        {
            Kind = SkillId.AnXingJiDou;
            Type = SkillType.Physics;
            ActionType = SkillActionType.Passive;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new();
        }
    }
}