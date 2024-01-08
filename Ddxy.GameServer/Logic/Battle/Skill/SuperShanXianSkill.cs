using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class SuperShanXianSkill : BaseSkill
    {
        public SuperShanXianSkill() : base(SkillId.SuperShanXian, "超级闪现")
        {
            Kind = SkillId.ShanXian;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.Final;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {            
            return new SkillEffectData
            {
                Percent = 100
            };
        }
    }
}