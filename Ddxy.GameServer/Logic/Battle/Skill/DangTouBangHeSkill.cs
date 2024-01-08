using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 进场时解除己方所有召唤兽和伙伴异常状态
    /// </summary>
    public class DangTouBangHeSkill : BaseSkill
    {
        public DangTouBangHeSkill() : base(SkillId.DangTouBangHe, "当头棒喝")
        {
            Kind = SkillId.DangTouBangHe;
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