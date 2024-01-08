using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class KuMuFengChunSkill : BaseSkill
    {
        public KuMuFengChunSkill() : base(SkillId.KuMuFengChun, "枯木逢春")
        {
            Kind = SkillId.KuMuFengChun;
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
                AttrType = AttrType.Mu,
                AttrValue = 50
            };
            return effectData;
        }
    }
}