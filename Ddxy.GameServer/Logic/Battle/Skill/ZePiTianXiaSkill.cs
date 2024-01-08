using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class ZePiTianXiaSkill : BaseSkill
    {
        public ZePiTianXiaSkill() : base(SkillId.ZePiTianXia, "泽披天下")
        {
            Kind = SkillId.ZePiTianXia;
            Type = SkillType.Protect;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.Self;
            BuffType = SkillBuffType.Once;
            Cooldown = 5;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            // var relive = request.Relive.GetValueOrDefault();
            // var level = request.Level.GetValueOrDefault();
            // var intimacy = request.Intimacy.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                Round = 3,
                TargetNum = 10,
                SuckHurtPercent = 30,
                HurtDecayPercent = 70
            };
            return effectData;
        }
    }
}