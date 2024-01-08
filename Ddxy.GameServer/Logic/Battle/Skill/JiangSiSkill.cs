using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 离场时解除己方所有召唤兽和伙伴的异常状态（异常状态是指所有负面状态）
    /// </summary>
    public class JiangSiSkill : BaseSkill
    {
        public JiangSiSkill() : base(SkillId.JiangSi, "将死")
        {
            Kind = SkillId.JiangSi;
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