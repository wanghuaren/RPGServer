using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class LiSheDaChuanSkill : BaseSkill
    {
        public LiSheDaChuanSkill() : base(SkillId.LiSheDaChuan, "利涉大川")
        {
            Kind = SkillId.LiSheDaChuan;
            ActionType = SkillActionType.Passive;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}