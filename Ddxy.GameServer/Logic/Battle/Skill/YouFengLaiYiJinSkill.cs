using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class YouFengLaiYiJinSkill : BaseSkill
    {
        public YouFengLaiYiJinSkill() : base(SkillId.YouFengLaiYiJin, "高级炊金馔玉")
        {
            Kind = SkillId.YouFengLaiYiJin;
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
                AttrType = AttrType.Jin,
                AttrValue = MathF.Round(50 + 4.6f * (relive * 0.5f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                                           MathF.Pow(intimacy, 0.166666f) * 10 /
                                                                           (100 + relive * 20)))
            };
            return effectData;
        }
    }
}