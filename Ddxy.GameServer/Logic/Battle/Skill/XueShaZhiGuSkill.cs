using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class XueShaZhiGuSkill : BaseSkill
    {
        public XueShaZhiGuSkill() : base(SkillId.XueShaZhiGu, "血煞之蛊")
        {
            Type = SkillType.ThreeCorpse;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var level = request.Level.GetValueOrDefault();
            var profic = request.Profic.GetValueOrDefault();

            var xianHurt = MathF.Floor(65 * level * (MathF.Pow(profic, 0.4f) * 2.8853998118144273f / 100 + 1));
            var hurt = xianHurt / 3;
            hurt *= 1.25f; //单法是群发的1.25倍

            var effectData = new SkillEffectData
            {
                Hurt = hurt
            };

            // 加强三尸
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqSanShi);
                if (value > 0)
                {
                    var percent = value / 100.0f;
                    effectData.Hurt = MathF.Round(effectData.Hurt * (1 + percent));
                }
            }
            
            return effectData;
        }
    }
}