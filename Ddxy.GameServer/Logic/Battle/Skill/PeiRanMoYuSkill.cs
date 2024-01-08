using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class PeiRanMoYuSkill : BaseSkill
    {
        // 对友方单位施加治愈状态，回合结束时，恢复8160+7.4%的生命值,若已倒地则恢复生命值后状态消失，目标数1人,持续2个回合，对已倒地队友释放无效
        public PeiRanMoYuSkill() : base(SkillId.PeiRanMoYu, "沛然莫御")
        {
            Type = SkillType.Resume;
            BuffType = SkillBuffType.Once;
            TargetType = SkillTargetType.Self;
            Quality = SkillQuality.High;
        }
        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            var profic = request.Profic.GetValueOrDefault();
            // 加强治愈
            var JqZhiYu = request.Attrs.Get(AttrType.JqZhiYu) / 100 + request.Member.GetXTXBAdd(AttrType.Hp);
            var effectData = new SkillEffectData
            {
                // 持续2回合效果不叠加
                Round = 2,
                // 生命
                Hp = Convert.ToInt32((8160.0f + profic) * (1.0f + JqZhiYu)),
                // 生命
                HpPercent = (float)(7.4f + (26.0f - 7.4f) * profic / 25000.0f) * (1.0f + JqZhiYu),
                // 目标单元
                TargetNum = 1,
            };
            return effectData;
        }
    }
}