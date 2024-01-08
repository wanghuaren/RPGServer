using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class TianHuoJiangShiSkill : BaseSkill
    {
        public TianHuoJiangShiSkill() : base(SkillId.TianHuoJiangShi, "天火降世")
        {
            Type = SkillType.Huo;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            // var level = request.Level.GetValueOrDefault();
            // var profic = request.Profic.GetValueOrDefault();

            // var effectData = new SkillEffectData
            // {
            //     TargetNum = 10,
            //     Hurt = MathF.Floor(100 * level * (MathF.Pow(profic, 0.4f) * 2.8853998118144273f / 100 + 1))
            // };
            // // 伤害衰减1/3
            // effectData.Hurt *= 0.667f;

            // // 加强火
            // var attrs = request.Attrs;
            // if (attrs != null)
            // {
            //     var value = attrs.Get(AttrType.JqHuo);
            //     if (value > 0)
            //     {
            //         var percent = value / 100.0f;
            //         effectData.Hurt = (int) MathF.Round(effectData.Hurt * (1 + percent));
            //     }
            // }

            // if (request.Attrs != null && request.OrnamentSkills != null)
            // {
            //     var lingXing = (int) request.Attrs.Get(AttrType.LingXing);
            //     if (lingXing >= 550)
            //     {
            //         var delta = 0;

            //         if (request.OrnamentSkills.ContainsKey(2022))
            //         {
            //             delta += lingXing * 40;
            //         }
            //         else if (request.OrnamentSkills.ContainsKey(2021))
            //         {
            //             delta += lingXing * 20;
            //         }

            //         if (delta > 0) effectData.Hurt += delta;
            //     }
            // }

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
            // 加强火
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqHuo);
                if (value > 0)
                {
                    var percent = value / 100.0f;
                    effectData.Hurt = (int)MathF.Round(effectData.Hurt * (1 + percent));
                }
            }
            return effectData;
        }
    }
}