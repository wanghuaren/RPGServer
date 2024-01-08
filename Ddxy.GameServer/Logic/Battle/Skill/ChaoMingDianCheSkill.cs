using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class ChaoMingDianCheSkill : BaseSkill
    {
        public ChaoMingDianCheSkill() : base(SkillId.ChaoMingDianChe, "潮鸣电掣")
        {
            Kind = SkillId.ChaoMingDianChe;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Shen;
            EffectTypes.Add(AttrType.Spd);
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                Add = MathF.Floor(75 * (relive * 0.3f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                              MathF.Pow(intimacy, 0.166666f) * 10 /
                                                              (100 + relive * 20)))
            };
            return effectData;
        }
    }
}