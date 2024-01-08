using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class NormalAttackSkill : BaseSkill
    {
        public NormalAttackSkill() : base(SkillId.NormalAtk, "普通攻击")
        {
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var atk = request.Atk.GetValueOrDefault();
            return new SkillEffectData {Hurt = (int) atk};
        }
    }
}