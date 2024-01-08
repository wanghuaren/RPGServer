using System;
using System.Collections.Generic;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class FeiLongZaiTianShuiSkill : BaseSkill
    {
        public FeiLongZaiTianShuiSkill() : base(SkillId.FeiLongZaiTianShui, "飞龙在天-水")
        {
            Kind = SkillId.FeiLongZaiTianShui;
            Type = SkillType.Shui;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();
            var maxMp = request.MaxMp.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                TargetNum = 2,
                Hurt = MathF.Floor(80 * level + maxMp / 100.0f * 4.5f * (relive * 0.6f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                    MathF.Pow(intimacy, 0.166666f) * 10 /
                    (100 + relive * 20)))
            };
            // 加强水
            var attrs = request.Attrs;
            if (attrs != null)
            {
                var value = attrs.Get(AttrType.JqShui);
                if (value > 0)
                {
                    var percent = value / 100.0f;
                    effectData.Hurt = (int)MathF.Round(effectData.Hurt * (1 + percent));
                }
            }
            // 觉醒技 龙跃于渊
            // 仙法攻击时有14%/28%/49%/70%概率附加与自身灵性、敏捷点数有关的伤害。法术连击时仅第一次攻击生效。
            var mb = request.Member;
            if (mb.CanUseJxSkill(SkillId.LongYueYuYuan))
            {
                var baseValues = new List<float>() { 140, 280, 490, 700 };
                var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                if (new Random().Next(1000) < calcValue)
                {
                    effectData.Hurt += attrs.Get(AttrType.LingXing) + attrs.Get(AttrType.MinJie);
                }
            }
            return effectData;
        }
    }
}