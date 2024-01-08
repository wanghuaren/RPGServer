using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 本方没有一个召唤兽战死，本次战斗中提升自己2%仙法，鬼火抗性，最多提升40%仙法鬼火抗性。（战斗结束后消失）
    /// </summary>
    public class FuLuShuangQuanSkill : BaseSkill
    {
        public FuLuShuangQuanSkill() : base(SkillId.FuLuShuangQuan, "福禄双全")
        {
            Kind = SkillId.FuLuShuangQuan;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData();
            return effectData;
        }
    }
}