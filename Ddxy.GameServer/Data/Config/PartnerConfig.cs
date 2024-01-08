using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Data.Config
{
    public class PartnerConfig
    {
        public uint Id { get; set; }
        public byte Race { get; set; }
        public byte Sex { get; set; }
        public uint Res { get; set; }
        public string Name { get; set; }
        public string Desc { get; set; }
        public byte Unlock { get; set; }
        public NaturalConfig Natural { get; set; }
        public uint[] Skills { get; set; }

        // 每个等级映射的属性, 由ConfigService填充
        [JsonIgnore] public Dictionary<uint, Dictionary<AttrType, float>> LevelAttrs { get; set; }

        public class NaturalConfig
        {
            public uint HpMax { get; set; }
            public uint Spd { get; set; }
            public uint WindRageOdds { get; set; }
        }
    }

    public class PartnerPowerConfig
    {
        public uint Id { get; set; }
        public uint PartnerId { get; set; }
        public uint Level { get; set; }
        public JsonElement Attrs { get; set; }
    }
}