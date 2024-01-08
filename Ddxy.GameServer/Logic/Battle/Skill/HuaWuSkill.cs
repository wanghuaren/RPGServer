using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HuaWuSkill : BaseSkill
    {
        public HuaWuSkill() : base(SkillId.HuaWu, "化无")
        {
            Kind = SkillId.HuaWu;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Final;
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