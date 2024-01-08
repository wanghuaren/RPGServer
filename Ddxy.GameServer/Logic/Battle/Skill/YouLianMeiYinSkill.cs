using Ddxy.Protocol;
using System;
using System.Collections.Generic;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class YouLianMeiYinSkill : BaseSkill
    {
        public YouLianMeiYinSkill() : base(SkillId.YouLianMeiYin, "幽怜魅影")
        {
            Kind = SkillId.YouLianMeiYin;
            Type = SkillType.RaiseHurt;
            ActionType = SkillActionType.Initiative;
            TargetType = SkillTargetType.Enemy;
            Cooldown = 5;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                Round = 2,
                HurtRaisePercent = 30
            };
            // 强化幽怜魅影
            // 施放天生技能“幽怜魅影”时有10%/20%/35%/50%概率增加一个随机目标。
            if (request != null && request.Member != null && request.Member.CanUseJxSkill(SkillId.QiangHuaYouLianMeiYing))
            {
                var mb = request.Member;
                var baseValues = new List<float>() { 100, 250, 350, 500 };
                var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                if (new Random().Next(1000) < calcValue)
                {
                    effectData.TargetNum += 1;
                }
            }
            return effectData;
        }
    }
}