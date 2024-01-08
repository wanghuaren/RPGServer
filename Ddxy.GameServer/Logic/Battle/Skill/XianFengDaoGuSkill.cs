using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class XianFengDaoGuSkill : BaseSkill
    {
        //仙风道骨：普通技能，宠物进场时（第一回合除外），恢复自己主人70%血量，10%蓝量。
        public XianFengDaoGuSkill() : base(SkillId.XianFengDaoGu, "仙风道骨")
        {
            Kind = SkillId.XianFengDaoGu;
            ActionType = SkillActionType.Passive;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.Low;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData();
            return effectData;
        }
    }
}