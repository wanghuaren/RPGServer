using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 消耗15400法力，群体解除本方全体异常状态，整场战斗只可使用一次，（前五回合不可使用）
    /// </summary>
    public class ChunHuiDaDiSkill : BaseSkill
    {
        public ChunHuiDaDiSkill() : base(SkillId.ChunHuiDaDi, "春回大地")
        {
            Kind = SkillId.ChunHuiDaDi;
            ActionType = SkillActionType.Initiative;
            Quality = SkillQuality.Final;
            LimitRound = 5;
            LimitTimes = 1;
        }
        
        public override bool UseSkill(BattleMember member, out string error)
        {
            var mp = member.Mp;
            var needMp = GetNeedMp(member);
            if (mp < needMp)
            {
                error = "法力不足，无法释放";
                return false;
            }
            error = null;
            return true;
        }

        private float GetNeedMp(BattleMember member)
        {
            // 天策符 千钧符  冥想符   减少耗蓝
            return GetNeedMpPost(member, 15400.0f);
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                Mp = -GetNeedMpPost(request.Member, 15400.0f)
            };
            return effectData;
        }
    }
}