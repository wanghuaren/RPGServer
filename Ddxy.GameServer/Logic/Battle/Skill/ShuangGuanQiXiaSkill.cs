using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 场上所有敌方角色施法前必须付出血量（20000血量）的代价，若不够则目标讲无法施法。仅PVP有效
    /// </summary>
    public class ShuangGuanQiXiaSkill : BaseSkill
    {
        public ShuangGuanQiXiaSkill() : base(SkillId.ShuangGuanQiXia, "双管齐下")
        {
            Kind = SkillId.ShuangGuanQiXia;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Final;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                Hp = -20000
            };
            return effectData;
        }
    }
}