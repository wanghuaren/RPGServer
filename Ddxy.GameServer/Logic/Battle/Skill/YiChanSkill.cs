using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class YiChanSkill : BaseSkill
    {
        //遗产：普通技能 ，宠物离场时，恢复自己主人70%的蓝量
        public YiChanSkill() : base(SkillId.YiChan, "遗产")
        {
            Kind = SkillId.YiChan;
            ActionType = SkillActionType.Passive;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.Low;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData();
            return effectData;
        }
    }
}