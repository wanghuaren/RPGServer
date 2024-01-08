using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class NvWaZhouNianSkill: BaseSkill
    {
        public NvWaZhouNianSkill() : base(SkillId.NvWaZhouNian, "女娲咒念")
        {
            Type = SkillType.Physics;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}