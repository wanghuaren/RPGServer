using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class RuHuTianYiSkill : BaseSkill
    {
        public RuHuTianYiSkill() : base(SkillId.RuHuTianYi, "如虎添翼")
        {
            Kind = SkillId.RuHuTianYi;
            Type = SkillType.Defense;
            ActionType = SkillActionType.Passive;
            BuffType = SkillBuffType.Once;
            Quality = SkillQuality.Shen;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                KongZhi = 5,
                FaShang = 15,
                FangYu = 15,
                Round = 2,
                TargetNum = 2
            };
            return effectData;
        }
    }
}