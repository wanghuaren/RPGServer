namespace Ddxy.GameServer.Data.Config
{
    public class PetConfig
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public uint Res { get; set; }
        public uint Skill { get; set; }
        public uint MaxSkillCnt { get; set; }
        public uint JxSkill { get; set; }
        public uint Grade { get; set; }
        public float[] Rate { get; set; }
        public uint[] Hp { get; set; }
        public uint[] Mp { get; set; }
        public uint[] Atk { get; set; }
        public int[] Spd { get; set; }
        public uint XieDaiLevel { get; set; }
        public uint XieDaiRelive { get; set; }

        /// <summary>
        /// 五行元素
        /// </summary>
        public uint[] Elements { get; set; }

        public uint[] NeedItem { get; set; }
        public string Intro { get; set; }
    }

    public class PetColorConfig
    {
        public string ColorValue { get; set; }
        public string ColorNice { get; set; }
    }
}