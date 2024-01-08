using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class MoShenFuShenSkill : BaseSkill
    {
        public MoShenFuShenSkill() : base(SkillId.MoShenFuShen, "魔神附身")
        {
            Type = SkillType.Attack;
            BuffType = SkillBuffType.Once;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                AtkPercent = (int) MathF.Round(25 * (MathF.Pow(profic, 0.35f) * 3 / 100 + 1)),
                Hit = 15,
                Round = (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.35f) * 5 / 100)),
                TargetNum = Math.Min(7, (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 8 / 100)))
            };

            // 加强加攻
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var jqAtk = attrs.Get(AttrType.JqAtk);
                if (jqAtk > 0)
                {
                    var percent = jqAtk / 100.0f;
                    effectData.AtkPercent = (int) MathF.Round(effectData.AtkPercent * (1 + percent));
                }
            }

            return effectData;
        }
    }
}