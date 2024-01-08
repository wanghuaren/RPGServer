using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class XiTianJingTuSkill : BaseSkill
    {
        public XiTianJingTuSkill() : base(SkillId.XiTianJingTu, "西天净土")
        {
            Kind = SkillId.XiTianJingTu;
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
                AttrType = AttrType.Tu,
                AttrValue = 50
            };
            return effectData;
        }
    }
}