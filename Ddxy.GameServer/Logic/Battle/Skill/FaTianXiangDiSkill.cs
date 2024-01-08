using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class FaTianXiangDiSkill : BaseSkill
    {
        public FaTianXiangDiSkill() : base(SkillId.FaTianXiangDi, "法天象地")
        {
            Kind = SkillId.FaTianXiangDi;
            Type = SkillType.Physics;
            ActionType = SkillActionType.Initiative;
        }
        
        public override bool UseSkill(BattleMember member, out string error)
        {
            if (member.Mp < 100)
            {
                error = "法力不足，无法释放";
                return false;
            }

            var sub = MathF.Ceiling(member.Mp * 0.95f);
            // 天策符 千钧符  冥想符   减少耗蓝
            sub = GetNeedMpPost(member, sub);
            member.AddMp(-sub);
            error = null;
            return true;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            // var relive = request.Relive.GetValueOrDefault();
            // var level = request.Level.GetValueOrDefault();
            // var intimacy = request.Intimacy.GetValueOrDefault();
            // var atk = request.Atk.GetValueOrDefault();
            //
            // var percent = 30 + intimacy / 10000000.0f;
            // if (percent > 45.0f) percent = 45.0f;

            return new()
            {
                Hurt = uint.MaxValue
            };
        }
    }
}