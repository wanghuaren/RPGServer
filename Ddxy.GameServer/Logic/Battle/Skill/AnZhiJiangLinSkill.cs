using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class AnZhiJiangLinSkill : BaseSkill
    {
        public AnZhiJiangLinSkill() : base(SkillId.AnZhiJiangLin, "暗之降临")
        {
            Kind = SkillId.AnZhiJiangLin;
            ActionType = SkillActionType.Passive;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.High;
            EffectTypes.Add(AttrType.Atk);
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var effectData = new SkillEffectData
            {
                Add = MathF.Floor(request.Attrs.Get(AttrType.Atk) * 0.1f)
            };

            return effectData;
        }
    }
}