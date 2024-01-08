namespace Ddxy.GameServer.Data.Config
{
    public class MonsterGroupConfig
    {
        public uint Id { get; set; }

        public string Map { get; set; }

        public string Name { get; set; }

        // 是否允许携带Partner
        public byte Partner { get; set; }

        public uint Exp { get; set; }

        public uint Gold { get; set; }

        public uint[] Monsters { get; set; }

        public Item[] Items { get; set; }

        public class Item
        {
            public uint Rate { get; set; }
            public uint Id { get; set; }
            public uint[] Ids { get; set; } 
            public uint Num { get; set; }
        }
    }
}