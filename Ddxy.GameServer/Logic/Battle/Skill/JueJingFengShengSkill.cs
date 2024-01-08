using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class JueJingFengShengSkill : BaseSkill
    {
        public JueJingFengShengSkill() : base(SkillId.JueJingFengSheng, "绝境逢生")
        {
            Kind = SkillId.MiaoShouHuiChun;
            Type = SkillType.Resume;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.Final;
            LimitRound = 5;
            LimitTimes = 1;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                TargetNum = 10,
                HpPercent = 60,
                MpPercent = 60
            };
            return effectData;
        }
    }
}