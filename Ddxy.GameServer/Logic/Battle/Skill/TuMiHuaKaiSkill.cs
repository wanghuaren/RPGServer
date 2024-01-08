using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class TuMiHuaKaiSkill : BaseSkill
    {
        public TuMiHuaKaiSkill() : base(SkillId.TuMiHuaKai, "荼蘼花开")
        {
            Kind = SkillId.TuMiHuaKai;
            Type = SkillType.Physics;
            ActionType = SkillActionType.Passive;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();
            var atk = request.Atk.GetValueOrDefault();

            var percent = 30 + intimacy / 10000000.0f;
            if (percent > 45.0f) percent = 45.0f;

            var ret = new SkillEffectData
            {
                Percent = percent,
                TargetNum = new Random().Next(2, 4),
                Hurt = MathF.Floor(atk * (75 + (intimacy * 45 / 2000000000f)) / 100f)
            };
            ret["BaoFaHurt"] = MathF.Floor((atk * 30 / 100f) + (intimacy * 200000 / 2000000000f));
            return ret;
        }
    }
}