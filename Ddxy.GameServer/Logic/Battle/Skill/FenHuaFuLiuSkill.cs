using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 物理攻击杀死一个单位时，有几率继续追击下个单位， 最多追击3个单位
    /// </summary>
    public class FenHuaFuLiuSkill : BaseSkill
    {
        public FenHuaFuLiuSkill() : base(SkillId.FenHuaFuLiu, "分花拂柳")
        {
            ActionType = SkillActionType.Passive;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var intimacy = request.Intimacy.GetValueOrDefault();
            var percent = 30 + intimacy / 10000000.0f;
            if (percent > 45.0f) percent = 45.0f;

            var effectData = new SkillEffectData
            {
                Percent = percent
            };

            return effectData;
        }
    }
}