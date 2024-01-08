using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class GongXingTianFaSkill : BaseSkill
    {
        public GongXingTianFaSkill() : base(SkillId.GongXingTianFa, "恭行天罚")
        {
            Kind = SkillId.GongXingTianFa;
            ActionType = SkillActionType.Passive;
            EffectTypes.Add(AttrType.PmingZhong);
            EffectTypes.Add(AttrType.PkuangBao);
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                Add = MathF.Floor(0.1f + 4 * (relive * 0.4f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                              MathF.Pow(intimacy, 0.166666f) * 10 /
                                                              (100 + relive * 20)))
            };
            return effectData;
        }
    }
}