using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 用自己的气血 换 对方的法力
    /// </summary>
    public class HighFenGuangHuaYingSkill : BaseSkill
    {
        public HighFenGuangHuaYingSkill() : base(SkillId.HighFenGuangHuaYing, "高级分光化影")
        {
            Kind = SkillId.FenGuangHuaYing;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
        }

        public override bool UseSkill(BattleMember member, out string error)
        {
            if (member.Hp < 10000)
            {
                error = "气血不足，无法释放";
                return false;
            }
            var sub = MathF.Ceiling(member.Hp * 0.95f);
            member.AddHp(-sub);
            error = null;
            return true;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();

            var percent = MathF.Floor(15.1f + 3 * (relive * 0.4f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                                         MathF.Pow(intimacy, 0.166666f) * 10 /
                                                                         (100 + relive * 20)));
            // 在调用GetEffectData之前就调用了UseSkill, 减少了95%的血量, 这里要换算出去
            var preHp = MathF.Ceiling(request.Attrs.Get(AttrType.Hp) / 0.05f);
            // 算出牺牲值
            var delta = preHp - request.Attrs.Get(AttrType.Hp);

            var effectData = new SkillEffectData
            {
                MpHurt = MathF.Round(delta * percent / 100)
            };
            return effectData;
        }
    }
}