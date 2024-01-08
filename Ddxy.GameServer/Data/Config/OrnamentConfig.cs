using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Data.Config
{
    public class OrnamentConfig
    {
        public uint Id { get; set; }
        
        public string Name { get; set; }

        public uint[] Suit { get; set; }

        public byte Index { get; set; }

        public int[] Race { get; set; }

        public int[] Sex { get; set; }

        public byte NeedRelive { get; set; }

        public byte NeedLevel { get; set; }
    }

    public class OrnamentSuitConfig
    {
        public uint Id { get; set; }

        public string Name { get; set; }

        public string Desc { get; set; }

        public int[] Race { get; set; }

        public int[] Sex { get; set; }

        public uint[] Ornaments { get; set; }

        public uint[] Skills { get; set; }
    }

    public class OrnamentSkillConfig
    {
        public uint Id { get; set; }

        public byte Level { get; set; }

        public JsonElement Attrs { get; set; }

        /// <summary>
        /// 解析配置的时候，自动把Attrs的内容解析成AttrType
        /// </summary>
        [JsonIgnore]
        public Dictionary<AttrType, float> Attrs2 { get; set; }
    }
}