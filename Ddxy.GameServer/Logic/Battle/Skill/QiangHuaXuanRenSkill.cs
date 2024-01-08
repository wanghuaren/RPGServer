using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class QiangHuaXuanRenSkill : BaseSkill
    {
        public QiangHuaXuanRenSkill() : base(SkillId.QiangHuaXuanRen, "强化悬刃")
        {
            Kind = SkillId.QiangHuaXuanRen;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Shen;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}