using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class ZiXuWuYouSkill : BaseSkill
    {
        public ZiXuWuYouSkill() : base(SkillId.ZiXuWuYou, "子虚乌有")
        {
            Kind = SkillId.ZiXuWuYou;
            Type = SkillType.YinShen;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.Final;
            Cooldown = 5;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                Round = 3,
                TargetNum = 2,
                YinShen = 1
            };
            return effectData;
        }
    }
}