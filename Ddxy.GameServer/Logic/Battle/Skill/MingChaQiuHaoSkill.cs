using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 使敌方所有人物与召唤兽血量以及法量可见, 可见进度条但不可见数字
    /// </summary>
    public class MingChaQiuHaoSkill : BaseSkill
    {
        public MingChaQiuHaoSkill() : base(SkillId.MingChaQiuHao, "明察秋毫")
        {
            Kind = SkillId.MingChaQiuHao;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Final;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData();
            return effectData;
        }
    }
}