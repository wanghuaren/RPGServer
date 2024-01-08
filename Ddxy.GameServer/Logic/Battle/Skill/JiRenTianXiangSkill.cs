using Ddxy.Protocol;
using System;
using System.Collections.Generic;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    /// <summary>
    /// 抵御一次致死的伤害，血量降低到10，并清楚自己的所有状态（被动，每场战斗最多触发一次，前三回合不生效）
    /// </summary>
    public class JiRenTianXiangSkill : BaseSkill
    {
        public JiRenTianXiangSkill() : base(SkillId.JiRenTianXiang, "吉人天相")
        {
            Kind = SkillId.JiRenTianXiang;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.High;
            LimitRound = 3;
            LimitTimes = 1;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                Hp = 10
            };
            // 觉醒技 大难不死
            // 触发“吉人天相”时，有14%/28%/49%/70%概率将气血值降低至自身最大气血值的30%，而非10点。
            if (request != null && request.Member != null && request.Member.CanUseJxSkill(SkillId.DaNanBuSi))
            {
                var mb = request.Member;
                var baseValues = new List<float>() { 140, 280, 490, 700 };
                var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                if (new Random().Next(1000) < calcValue)
                {
                    effectData.Hp = (int)(mb.HpMax * 0.3f);
                }
            }
            return effectData;
        }
    }
}