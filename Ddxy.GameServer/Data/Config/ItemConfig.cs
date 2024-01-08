using System.Text.Json;

namespace Ddxy.GameServer.Data.Config
{
    public class ItemConfig
    {
        public uint Id { get; set; }

        public string Name { get; set; }

        public uint Icon { get; set; }

        public uint Level { get; set; }

        public byte Effect { get; set; }

        public byte Type { get; set; }

        public JsonElement Json { get; set; }

        public string TypeDetail { get; set; }

        public string DetailShot { get; set; }

        public string UseDetail { get; set; }

        public byte Notice { get; set; }

        public int Num { get; set; }
        
        public int GuoShi { get; set; }

        public string Desc { get; set; }
    }
}