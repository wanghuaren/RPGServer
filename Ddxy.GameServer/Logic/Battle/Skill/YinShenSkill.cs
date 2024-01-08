using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class YinShenSkill : BaseSkill
    {
        public YinShenSkill() : base(SkillId.YinShen, "隐身")
        {
            Kind = SkillId.YinShen;
            Type = SkillType.YinShen;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData
            {
                Round = 3,
                YinShen = 1
            };
        }
    }
}