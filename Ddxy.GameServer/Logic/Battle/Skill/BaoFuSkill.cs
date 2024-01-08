using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 有几率在敌方单位使用法术时对其造成伤害。（仅PVP有效）
    /// </summary>
    public class BaoFuSkill : BaseSkill
    {
        public BaoFuSkill() : base(SkillId.BaoFu, "报复")
        {
            Kind = SkillId.BaoFu;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                Percent = 30
            };
            return effectData;
        }
    }
}