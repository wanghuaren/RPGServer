using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Battle.Skill
{
    public class FeiLongZaiTianSkill : BaseSkill
    {
        public FeiLongZaiTianSkill() : base(SkillId.FeiLongZaiTian, "飞龙在天")
        {
            Kind = SkillId.FeiLongZaiTian;
            ActionType = SkillActionType.Passive;
            Quality = SkillQuality.High;
        }

        public override SkillEffectData GetEffectData(GetEffectDataRequest request)
        {
            // var relive = request.Relive.GetValueOrDefault();
            // var level = request.Level.GetValueOrDefault();
            // var intimacy = request.Intimacy.GetValueOrDefault();
            // var maxMp = request.MaxMp.GetValueOrDefault();
            //
            // var effectData = new SkillEffectData
            // {
            //     TargetNum = 3,
            //     Hurt = MathF.Floor(80 * level + maxMp / 100 * 6 * (relive * 0.6f + 1) * (MathF.Pow(level, 0.5f) / 10 +
            //         MathF.Pow(intimacy, 0.166666f) * 10 /
            //         (100 + relive * 20)))
            // };
            // return effectData;

            return new SkillEffectData();
        }
    }
}