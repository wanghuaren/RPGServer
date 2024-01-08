using System.Collections.Generic;
using System.Text.Json;

namespace Ddxy.GameServer.Data.Config
{
    public class AutoCreateConfig
    {
        public uint Map { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public uint Dir { get; set; }
    }

    public class NpcConfig
    {
        public uint Id { get; set; }

        public string Name { get; set; }

        public uint Res { get; set; }

        public uint Kind { get; set; }

        public uint MonsterGroup { get; set; }

        public AutoCreateConfig AutoCreate { get; set; }

        public Dictionary<string, JsonElement> Buttons { get; set; }

        public string Talk { get; set; }
    }
}