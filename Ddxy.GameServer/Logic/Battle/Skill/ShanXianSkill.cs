using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class ShanXianSkill : BaseSkill
    {
        public ShanXianSkill() : base(SkillId.ShanXian, "闪现")
        {
            Kind = SkillId.ShanXian;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Low;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData
            {
                Percent = 25
            };
        }
    }
}