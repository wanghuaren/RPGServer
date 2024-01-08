namespace Ddxy.GameServer.Data.Config
{
    public class MountConfig
    {
        public uint Id { get; set; }
        public string Name { get; set; }
        public uint Res { get; set; }

        public byte Race { get; set; }

        // 1,2，3,4 攻血法敏
        public int Type { get; set; }

        public float[] Rate { get; set; }
        public int[] Spd { get; set; }
    }

    public class MountExpConfig
    {
        public uint Exp { get; set; }
    }
}