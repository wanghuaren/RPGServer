using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 具有此技能的召唤兽在场时，所有人物回合开始时假如有召唤兽在场必须付出血量的代价（20000血量）人物血量不足时由召唤兽代扣（仅PVP有效）
    /// </summary>
    public class TaoMingSkill : BaseSkill
    {
        public TaoMingSkill() : base(SkillId.TaoMing, "讨命")
        {
            Kind = SkillId.TaoMing;
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