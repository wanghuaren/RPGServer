using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class ShenGongGuiLiSkill : BaseSkill
    {
        public ShenGongGuiLiSkill() : base(SkillId.ShenGongGuiLi, "神工鬼力")
        {
            Kind = SkillId.ShenGongGuiLi;
            ActionType = SkillActionType.Passive;
            EffectTypes.Add(AttrType.Atk);
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();

            var effectData = new SkillEffectData
            {
                Add = MathF.Floor(1875 * (relive * 0.5f + 1) * (MathF.Pow(level, 0.5f) / 10 +
                                                                MathF.Pow(intimacy, 0.166666f) * 10 /
                                                                (100 + relive * 20)))
            };
            return effectData;
        }
    }
}