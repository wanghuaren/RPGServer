using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class LuoZhiYunYanSkill: BaseSkill
    {
        public LuoZhiYunYanSkill() : base(SkillId.LuoZhiYunYan, "落纸云烟")
        {
            Type = SkillType.Physics;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}