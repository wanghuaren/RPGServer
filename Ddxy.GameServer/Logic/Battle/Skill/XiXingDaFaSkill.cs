using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class XiXingDaFaSkill : BaseSkill
    {
        public XiXingDaFaSkill() : base(SkillId.XiXingDaFa, "吸星大法")
        {
            Type = SkillType.ThreeCorpse;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var level = request.Level.GetValueOrDefault();
            var profic = request.Profic.GetValueOrDefault();

            var xianHurt = MathF.Floor(65 * level * (MathF.Pow(profic, 0.4f) * 2.8853998118144273f / 100 + 1));
            var hurt = xianHurt / 3;

            var effectData = new SkillEffectData
            {
                Hurt = hurt,
                TargetNum = Math.Min(5, (int) MathF.Floor(3 * (1 + MathF.Pow(profic, 0.3f) * 5 / 100)))
            };
            
            // 加强三尸
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqSanShi);
                if (value > 0)
                {
                    var percent = value / 100.0f + request.Member.GetXTXBAdd(AttrType.JqSanShi);
                    effectData.Hurt = (int) MathF.Round(effectData.Hurt * (1 + percent));
                }
            }
            
            return effectData;
        }
    }
}