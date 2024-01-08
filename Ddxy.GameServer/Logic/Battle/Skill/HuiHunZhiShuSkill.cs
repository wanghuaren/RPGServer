using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HuiHunZhiShuSkill : BaseSkill
    {
        public HuiHunZhiShuSkill() : base(SkillId.HuiHunZhiShu, "回魂之术")
        {
            Type = SkillType.ThreeCorpse;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            // var level = request.Level.GetValueOrDefault();
            // var profic = request.Profic.GetValueOrDefault();

            // var xianHurt = MathF.Floor(65 * level * (MathF.Pow(profic, 0.4f) * 2.8853998118144273f / 100 + 1));
            // var hurt = xianHurt / 3;

            // var effectData = new SkillEffectData
            // {
            //     Hurt = hurt,
            //     TargetNum = 10
            // };
            
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();
            var maxMp = request.MaxMp.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                TargetNum = 10,
                Hurt = MathF.Floor(80 * level + maxMp / 100.0f * 4.5f * (relive * 0.6f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                    MathF.Pow(intimacy, 0.166666f) * 10 /
                    (100 + relive * 20))) * 10
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