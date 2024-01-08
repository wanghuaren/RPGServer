using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HighYuanQuanWanHuSkill : BaseSkill
    {
        public HighYuanQuanWanHuSkill() : base(SkillId.HighYuanQuanWanHu, "高级源泉万斛")
        {
            Kind = SkillId.YuanQuanWanHu;
            ActionType = SkillActionType.Passive;
            EffectTypes.Add(AttrType.MpMax);
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