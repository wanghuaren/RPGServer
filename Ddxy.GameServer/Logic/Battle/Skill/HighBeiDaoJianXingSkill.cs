using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HighBeiDaoJianXingSkill : BaseSkill
    {
        public HighBeiDaoJianXingSkill() : base(SkillId.HighBeiDaoJianXing, "高级倍道兼行")
        {
            Kind = SkillId.BeiDaoJianXing;
            ActionType = SkillActionType.Passive;
            EffectTypes.Add(AttrType.Spd);
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                Add = MathF.Floor(50 * (relive * 0.3f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                              MathF.Pow(intimacy, 0.166666f) * 10 /
                                                              (100 + relive * 20)))
            };
            return effectData;
        }
    }
}