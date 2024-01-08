namespace Ddxy.GameServer.Data.Config
{
    public class CszlConfig
    {
        public uint Id { get; set; }


        public uint MonsterGroup { get; set; }

        public Item[] Items { get; set; }

        public class Item
        {
            public uint Id { get; set; }
            public uint Num { get; set; }
        }
    }
}