using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class TianGangZhanQiSkill : BaseSkill
    {
        public TianGangZhanQiSkill() : base(SkillId.TianGangZhanQi, "天罡战气")
        {
            Kind = SkillId.TianGangZhanQi;
            ActionType = SkillActionType.Passive;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                Add = MathF.Floor(0.1f + 13 * (relive * 0.5f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                              MathF.Pow(intimacy, 0.166666f) * 10 /
                                                              (100 + relive * 20)))
            };
            return effectData;
        }
    }
}