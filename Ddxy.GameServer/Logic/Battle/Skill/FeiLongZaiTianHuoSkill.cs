using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class FeiLongZaiTianHuoSkill : BaseSkill
    {
        public FeiLongZaiTianHuoSkill() : base(SkillId.FeiLongZaiTianHuo, "飞龙在天-火")
        {
            Kind = SkillId.FeiLongZaiTianHuo;
            Type = SkillType.Huo;
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
                TargetNum = 3,
                Hurt = MathF.Floor(80 * level + maxMp / 100.0f * 4.5f * (relive * 0.6f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                    MathF.Pow(intimacy, 0.166666f) * 10 /
                    (100 + relive * 20)))
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