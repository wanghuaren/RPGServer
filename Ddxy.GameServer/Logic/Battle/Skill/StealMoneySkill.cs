using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class StealMoneySkill : BaseSkill
    {
        public StealMoneySkill() : base(SkillId.StealMoney, "飞龙探云手")
        {
            Kind = SkillId.StealMoney;
            Type = SkillType.StealMoney;
            ActionType = SkillActionType.Initiative;
            Quality = SkillQuality.Low;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var rnd = new Random();
            var r = rnd.Next(0, 100);

            int money;
            if (r < 1)
            {
                money = rnd.Next(5000, 10000);
            }
            else if (r < 10)
            {
                money = rnd.Next(2000, 5000);
            }
            else
            {
                money = rnd.Next(100, 2000);
            }

            return new SkillEffectData
            {
                Hurt = 1,
                Money = money
            };
        }
    }
}