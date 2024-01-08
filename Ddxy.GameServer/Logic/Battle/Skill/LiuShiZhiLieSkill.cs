using System;
using System.Collections.Generic;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 六识在体内灵体的加持下能感应瞬息变化，能极大提升反应能力，增加命中。相信在战斗中神兵也能够一如以往地成为侠士们最为得力的帮手，他的忠勇侠义定能带来更加出色的表现！
    /// </summary>
    public class LiuShiZhiLieSkill : BaseSkill
    {
        public LiuShiZhiLieSkill() : base(SkillId.LiuShiChiLie, "六识炽烈")
        {
            Kind = SkillId.LiuShiChiLie;
            Type = SkillType.MingZhong;
            ActionType = SkillActionType.Passive;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.High;
            EffectTypes.Add(AttrType.PmingZhong);
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            // var relive = request.Relive.GetValueOrDefault();
            // var level = request.Level.GetValueOrDefault();
            // var intimacy = request.Intimacy.GetValueOrDefault();

            // var percent = MathF.Floor(30 + 10 * (relive * 0.4f + 1) * (MathF.Pow(level, 0.5f) / 10 +
            //                                                            MathF.Pow(intimacy, 0.166666f) * 10 /
            //                                                            (100 + relive * 20)));

            var effectData = new SkillEffectData
            {
                Add = MathF.Floor(request.Attrs.Get(AttrType.PmingZhong) * 0.1f)
            };
            return effectData;
        }
    }
}