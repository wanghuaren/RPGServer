using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class QiangHuaYiHuanSkill : BaseSkill
    {
        public QiangHuaYiHuanSkill() : base(SkillId.QiangHuaYiHuan, "强化遗患")
        {
            Kind = SkillId.QiangHuaYiHuan;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Shen;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}