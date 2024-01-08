using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 单体水法攻击敌方目标，有一定几率攻击多个目标，最多攻击三个目标。
    /// </summary>
    public class FeiZhuJianYuSkill : BaseSkill
    {
        public FeiZhuJianYuSkill() : base(SkillId.FeiZhuJianYu, "飞珠溅玉")
        {
            Kind = SkillId.FeiZhuJianYu;
            Type = SkillType.Shui;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var atk = request.Atk.GetValueOrDefault();
            // var relive = request.Relive.GetValueOrDefault();
            // var level = request.Level.GetValueOrDefault();
            // var intimacy = request.Intimacy.GetValueOrDefault();
            //
            // var percent = MathF.Floor(30 + 15 * (relive * 0.4f + 1) * (MathF.Pow(level, 0.5f) / 10 +
            //                                                            MathF.Pow(intimacy, 0.166666f) * 10 /
            //                                                            (100 + relive * 20)));


            var effectData = new SkillEffectData
            {
                Hurt = atk * 2.5f,
                TargetNum = 1
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

            var rnd = new Random().Next(0, 100);
            if (rnd < 20) effectData.TargetNum = 3;
            else if (rnd < 50) effectData.TargetNum = 2;

            return effectData;
        }
    }
}