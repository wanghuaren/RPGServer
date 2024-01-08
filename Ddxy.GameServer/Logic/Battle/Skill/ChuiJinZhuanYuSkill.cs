using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class ChuiJinZhuanYuSkill : BaseSkill
    {
        public ChuiJinZhuanYuSkill() : base(SkillId.ChuiJinZhuanYu, "炊金馔玉")
        {
            Kind = SkillId.ChuiJinZhuanYu;
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
                AttrType = AttrType.Jin,
                AttrValue = 50
            };
            return effectData;
        }
    }
}