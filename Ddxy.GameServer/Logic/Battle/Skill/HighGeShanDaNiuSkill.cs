using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HighGeShanDaNiuSkill : BaseSkill
    {
        public HighGeShanDaNiuSkill() : base(SkillId.HighGeShanDaNiu, "高级隔山打牛")
        {
            Kind = SkillId.GeShanDaNiu;
            ActionType = SkillActionType.Passive;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();

            var percent = MathF.Round(0.1f + 20 * (relive * 0.4f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                                         MathF.Pow(intimacy, 0.166666f) * 10 /
                                                                         (100 + relive * 20)));

            var effectData = new SkillEffectData
            {
                Percent = percent
            };
            return effectData;
        }
    }
}