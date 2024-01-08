using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class XuanRenSkill : BaseSkill
    {
        public XuanRenSkill() : base(SkillId.XuanRen, "悬刃")
        {
            Kind = SkillId.XuanRen;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Shen;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}