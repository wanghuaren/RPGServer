using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class HighShanXianSkill : BaseSkill
    {
        public HighShanXianSkill() : base(SkillId.HighShanXian, "高级闪现")
        {
            Kind = SkillId.ShanXian;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var intimacy = request.Intimacy.GetValueOrDefault();
            var percent = 35 + MathF.Floor(intimacy * 1.5f / 100000000);
            
            return new SkillEffectData
            {
                Percent = percent
            };
        }
    }
}