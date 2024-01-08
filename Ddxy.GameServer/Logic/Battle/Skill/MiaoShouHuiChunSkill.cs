using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class MiaoShouHuiChunSkill : BaseSkill
    {
        public MiaoShouHuiChunSkill() : base(SkillId.MiaoShouHuiChun, "妙手回春")
        {
            Kind = SkillId.MiaoShouHuiChun;
            Type = SkillType.Resume;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.High;
            LimitRound = 5;
            LimitTimes = 1;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData
            {
                TargetNum = 3,
                HpPercent = 50,
                MpPercent = 50
            };
        }
    }
}