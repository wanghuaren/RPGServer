using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class GeShanDaNiuSkill : BaseSkill
    {
        public GeShanDaNiuSkill() : base(SkillId.GeShanDaNiu, "隔山打牛")
        {
            Kind = SkillId.GeShanDaNiu;
            ActionType = SkillActionType.Passive;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.Low;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();
            var atk = request.Atk.GetValueOrDefault();

            var percent = MathF.Round(0.1f + 15 * (relive * 0.4f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                                         MathF.Pow(intimacy, 0.166666f) * 10 /
                                                                         (100 + relive * 20)));

            var effectData = new SkillEffectData
            {
                Hurt = MathF.Round(atk * percent / 100f)
            };
            return effectData;
        }
    }
}