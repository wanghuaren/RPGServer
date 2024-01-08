using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class TianWaiFeiMoSkill : BaseSkill
    {
        public TianWaiFeiMoSkill() : base(SkillId.TianWaiFeiMo, "天外飞魔")
        {
            Type = SkillType.Speed;
            BuffType = SkillBuffType.Once;
            TargetType = SkillTargetType.Self;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var level = request.Level.GetValueOrDefault();
            var profic = request.Profic.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                SpdPercent = (int) MathF.Round(15 + level / 5000),
                Round = (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.35f) * 5 / 100))
            };

            // 加强加速
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var jqSpd = attrs.Get(AttrType.JqSpd);
                if (jqSpd > 0)
                {
                    var percent = jqSpd / 100.0f;
                    effectData.SpdPercent = (int) MathF.Round(effectData.SpdPercent * (1 + percent));
                }
            }

            return effectData;
        }
    }
}