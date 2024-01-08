using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class YouFengLaiYiTuSkill : BaseSkill
    {
        public YouFengLaiYiTuSkill() : base(SkillId.YouFengLaiYiTu, "高级西天净土")
        {
            Kind = SkillId.YouFengLaiYiTu;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
            Cooldown = 5;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                Round = 3,
                TargetNum = 10,
                AttrType = AttrType.Tu,
                AttrValue = MathF.Round(50 + 4.6f * (relive * 0.5f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                                           MathF.Pow(intimacy, 0.166666f) * 10 /
                                                                           (100 + relive * 20)))
            };
            return effectData;
        }
    }
}