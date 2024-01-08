namespace Ddxy.GameServer.Data.Config
{
    public class PetLevelConfig
    {
        public byte MinLv { get; set; }
        public byte MaxLv { get; set; }
    }

    public class RoleLevelConfig : PetLevelConfig
    {
        public uint MaxSkillExp { get; set; }
    }

    public class LevelRewardConfig
    {
        public byte Level { get; set; }

        public LevelRewardItemConfig Rewards { get; set; }
    }

    public class LevelRewardItemConfig
    {
        public uint Item { get; set; }
        public uint Num { get; set; }
    }
}