using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HighZhangYinDongDuSkill : BaseSkill
    {
        public HighZhangYinDongDuSkill() : base(SkillId.HighZhangYinDongDu, "高级帐饮东都")
        {
            Kind = SkillId.ZhangYinDongDu;
            ActionType = SkillActionType.Passive;
            EffectTypes.Add(AttrType.HpMax);
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                Add = MathF.Floor(6000 * (relive * 0.5f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                                MathF.Pow(intimacy, 0.166666f) * 10 /
                                                                (100 + relive * 20)))
            };
            return effectData;
        }
    }
}