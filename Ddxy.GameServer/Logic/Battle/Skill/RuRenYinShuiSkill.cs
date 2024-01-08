using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class RuRenYinShuiSkill : BaseSkill
    {
        public RuRenYinShuiSkill() : base(SkillId.RuRenYinShui, "如人饮水")
        {
            Kind = SkillId.RuRenYinShui;
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
                AttrType = AttrType.Shui,
                AttrValue = 50
            };
            return effectData;
        }
    }
}