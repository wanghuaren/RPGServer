using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class FengHuoLiaoYuanSkill : BaseSkill
    {
        public FengHuoLiaoYuanSkill() : base(SkillId.FengHuoLiaoYuan, "烽火燎原")
        {
            Kind = SkillId.FengHuoLiaoYuan;
            ActionType = SkillActionType.Initiative;
            Quality = SkillQuality.High;
            Cooldown = 5;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                Round = 3,
                TargetNum = 10,
                AttrType = AttrType.Huo,
                AttrValue = 50
            };
            return effectData;
        }
    }
}