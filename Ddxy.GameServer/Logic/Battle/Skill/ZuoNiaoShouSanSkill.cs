using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 当主人倒地时，且友方召唤兽只剩自身在场时，清除召唤兽任何状态并回复友方人物50%的血法后离场（仅PVP有效）
    /// </summary>
    public class ZuoNiaoShouSanSkill : BaseSkill
    {
        public ZuoNiaoShouSanSkill() : base(SkillId.ZuoNiaoShouSan, "作鸟兽散")
        {
            Kind = SkillId.ZuoNiaoShouSan;
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