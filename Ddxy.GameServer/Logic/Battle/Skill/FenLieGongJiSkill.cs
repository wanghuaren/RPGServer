using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class FenLieGongJiSkill : BaseSkill
    {
        public FenLieGongJiSkill() : base(SkillId.FenLieGongJi, "分裂攻击")
        {
            Kind = SkillId.FenLieGongJi;
            ActionType = SkillActionType.Passive;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.Low;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData
            {
                Percent = 15
            };
        }
    }
}