using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class YiHuaJieMuSkill : BaseSkill
    {
        public YiHuaJieMuSkill() : base(SkillId.YiHuaJieMu, "移花接木")
        {
            Kind = SkillId.YiHuaJieMu;
            Type = SkillType.Swap;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.All;
            Cooldown = 5;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData();
            return effectData;
        }
    }
}