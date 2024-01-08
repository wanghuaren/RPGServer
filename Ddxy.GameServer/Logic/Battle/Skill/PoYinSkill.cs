using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class PoYinSkill : BaseSkill
    {
        public PoYinSkill() : base(SkillId.PoYin, "破隐")
        {
            Kind = SkillId.PoYin;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            // var relive = request.Relive.GetValueOrDefault();
            // var level = request.Level.GetValueOrDefault();
            // var intimacy = request.Intimacy.GetValueOrDefault();
            // var maxMp = request.MaxMp.GetValueOrDefault();
            var effectData = new SkillEffectData
            {
                TargetNum = 10
            };
            return effectData;
        }
    }
}