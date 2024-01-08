using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class YiHuanSkill : BaseSkill
    {
        public YiHuanSkill() : base(SkillId.YiHuan, "遗患")
        {
            Kind = SkillId.YiHuan;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Shen;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}