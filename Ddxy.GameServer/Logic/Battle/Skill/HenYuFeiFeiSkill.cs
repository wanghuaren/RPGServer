using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 对一个目标造成水法伤害，效果与召唤兽等级、亲密和法力上限有关。若攻击后目标气血值低于90%（包括击杀），
    /// 则释放单水对一个速度最高的其他目标进行追击，若再次低于90%可继续追击，每回合最多追击两次。每攻击一次后，本回合自身叠加10%水系狂暴率。
    /// </summary>
    public class HenYuFeiFeiSkill : BaseSkill
    {
        public HenYuFeiFeiSkill() : base(SkillId.HenYuFeiFei, "恨雨霏霏")
        {
            Kind = SkillId.HenYuFeiFei;
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

            // 3转180,1kw亲密度,50w最大法力，技能输出16w左右，比飞龙在天略高一丁点
            var hurt = (uint) MathF.Floor(80 * level + maxMp / 100.0f * 4.61f * (relive * 0.6f + 1) *
                (MathF.Pow(level, 0.5f) / 10 + MathF.Pow(intimacy, 0.166666f) * 10 / (100 + relive * 20)));


            var effectData = new SkillEffectData
            {
                Hurt = hurt,
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

            return effectData;
        }
    }
}