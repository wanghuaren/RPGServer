using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HunLuanSkill : BaseSkill
    {
        public HunLuanSkill() : base(SkillId.HunLuan, "混乱")
        {
            Kind = SkillId.HunLuan;
            Type = SkillType.Chaos;
            ActionType = SkillActionType.Passive;
            BuffType = SkillBuffType.Once;
            Quality = SkillQuality.Low;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();

            var percent = MathF.Round(0.1f + 1.5f * (relive * 0.4f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                                           MathF.Pow(intimacy, 0.166666f) * 10 /
                                                                           (100 + relive * 20)));

            var effectData = new SkillEffectData();
            var rate = new Random().Next(0, 100);
            if (rate <= percent) effectData.SkillId = SkillId.JieDaoShaRen;
            return effectData;
        }
    }
}