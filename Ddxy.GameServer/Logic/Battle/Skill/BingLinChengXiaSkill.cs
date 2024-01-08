using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class BingLinChengXiaSkill : BaseSkill
    {
        public BingLinChengXiaSkill() : base(SkillId.BingLinChengXia, "兵临城下")
        {
            Type = SkillType.Physics;
            Kind = SkillId.BingLinChengXia;
            ActionType = SkillActionType.Initiative;
            Quality = SkillQuality.Shen;
        }

        public override bool UseSkill(BattleMember member, out string error)
        {
            var hp = member.Hp;
            var needHp = member.HpMax * 0.5f;
            var mp = member.Mp;
            var needMp = member.MpMax * 0.2f;

            if (hp < needHp)
            {
                error = "气血不足，无法释放";
                return false;
            }
            // 天策符 千钧符  冥想符   减少耗蓝
            needMp = GetNeedMpPost(member, needMp);
            if (mp < needMp)
            {
                error = "法力不足，无法释放";
                return false;
            }

            member.AddHp(-MathF.Round(needHp));
            member.AddMp(-MathF.Round(needMp));
            error = null;
            return true;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var atk = request.Atk.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                Hurt = atk * 2.5f
            };
            return effectData;
        }
    }
}