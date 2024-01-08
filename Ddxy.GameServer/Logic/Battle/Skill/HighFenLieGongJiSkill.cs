using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HighFenLieGongJiSkill : BaseSkill
    {
        public HighFenLieGongJiSkill() : base(SkillId.HighFenLieGongJi, "高级分裂攻击")
        {
            Kind = SkillId.FenLieGongJi;
            ActionType = SkillActionType.Passive;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var intimacy = request.Intimacy.GetValueOrDefault();
            var percent = 30 + MathF.Floor(intimacy * 2.0f / 10000000);
            if (percent > 50.0f) percent = 50.0f;

            return new SkillEffectData
            {
                Percent = percent
            };
        }
    }
}