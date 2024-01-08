using System;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class WanQianHuaShenSkill : BaseSkill
    {
        public WanQianHuaShenSkill() : base(SkillId.WanQianHuaShen, "万千化身")
        {
            Type = SkillType.Physics;
            BuffType = SkillBuffType.Once;
            TargetType = SkillTargetType.Enemy;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            // var profic = request.Profic.GetValueOrDefault();
            // var level = (int)request.Level;
            // var relive = (int)request.Relive;
            // var atk = request.Atk.GetValueOrDefault();
            // var JqPoJia = request.Attrs.Get(AttrType.JqPoJia);
            // var effectData = new SkillEffectData
            // {
            //     // 降低目标物理抗性持续2回合效果不叠加
            //     Round = 2,
            //     // 物理吸收
            //     PXiShou = -50,
            //     // 抗物理
            //     DWuLi = -50,
            //     // 目标单元
            //     // TargetNum = 3 + (request.Member.HasBuff(SkillType.BianShen) ? new Random().Next(3) + 1 : 0)
            //     TargetNum = 10
            // };
            // // 技能基础伤害
            // float shurt = (float)Math.Floor(55 * level * (profic * 0.2 * 2.8853998 / 200 + 1)
            // + Math.Floor(50 * relive * (profic * 0.1 * 2 / 280 + 1)));
            // // 加强伤害
            // float ahurt = shurt * (request.Attrs.Get(AttrType.JqAtk) + JqPoJia) / 100;
            // // 普攻伤害
            // float nhurt = atk;
            // // 技能伤害 = 技能基础伤害 + 加强伤害 + 普攻伤害
            // effectData.Hurt = (shurt + ahurt + nhurt) * 0.3f;
            // // // 逆鳞伤害提升
            // // effectData.Hurt = effectData.Hurt * (1.0f + (request.Member.HasBuff(SkillType.BianShen) ? 0.3f : 0f));
            // return effectData;

            var relive = request.Relive.GetValueOrDefault();
            var level = request.Level.GetValueOrDefault();
            var intimacy = request.Intimacy.GetValueOrDefault();
            var atk = request.Atk.GetValueOrDefault();

            var percent = 30 + intimacy / 10000000.0f;
            if (percent > 45.0f) percent = 45.0f;
            
            return new SkillEffectData
            {
                Percent = percent,
                TargetNum = 10,
                Hurt = MathF.Floor(80 * level + atk / 100.0f * 9f * (relive * 0.6f + 1) *
                    (MathF.Pow(level, 0.5f) / 10 +
                     MathF.Pow(intimacy, 0.166666f) * 10 /
                     (100 + relive * 20))) * 10
            };
        }
    }
}