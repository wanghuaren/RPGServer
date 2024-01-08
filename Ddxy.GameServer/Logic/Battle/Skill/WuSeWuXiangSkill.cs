using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class WuSeWuXiangSkill : BaseSkill
    {
        public WuSeWuXiangSkill() : base(SkillId.WuSeWuXiang, "无色无相")
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
                YinShen = 1,
                Percent = 30
            };
        }
    }
}