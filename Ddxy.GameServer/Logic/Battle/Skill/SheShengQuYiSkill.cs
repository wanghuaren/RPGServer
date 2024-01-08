using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 牺牲自己，有几率使一个目标只能进行物理攻击，持续两回合。（仅pvp有效）
    /// </summary>
    public class SheShengQuYiSkill : BaseSkill
    {
        public SheShengQuYiSkill() : base(SkillId.SheShengQuYi, "舍生取义")
        {
            Kind = SkillId.SheShengQuYi;
            Type = SkillType.OnlyPhysic;
            ActionType = SkillActionType.Initiative;
            Quality = SkillQuality.High;
            TargetType = SkillTargetType.Enemy;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                Round = 2
            };
            return effectData;
        }
    }
}