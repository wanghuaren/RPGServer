using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class NiLinSkill : BaseSkill
    {
        // 每4回合可激活一次，改变形象、提升30%血量上限和30%法术伤害，释放所有法术有几率增加1-3个单位持续两回合
        public NiLinSkill() : base(SkillId.NiLin, "逆鳞")
        {
            Type = SkillType.BianShen;
            BuffType = SkillBuffType.Once;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.High;
            Cooldown = 4;
            LimitRound = 3;
        }
        // 施法效果--对自己（一定是对自己的效果）
        public override SkillEffectData GetEffectData2Self(GetEffectDataRequest request)
        {
            return new SkillEffectData
            {
                // 血量上限
                HpMaxPercent = 30,
                // 持续2回合
                Round = 2,
            };
        }
        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            return new SkillEffectData();
        }
    }
}