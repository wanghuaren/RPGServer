using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ddxy.GameServer.Data.Config
{
    public class MountSkillConfig
    {
        public uint Id { get; set; }

        // 属于技能1，2,3
        public int[] Type { get; set; }

        // 属于第几个宠物
        public int[] Grids { get; set; }

        public float Btgl { get; set; }

        public JsonElement[] Attrs { get; set; }

        [JsonIgnore] public Dictionary<byte, float>[] Attrs2 { get; set; }
        
        public MountSkillFaLianConfig[] FaLian { get; set; }
    }

    public class MountSkillFaLianConfig
    {
        public uint Cnt { get; set; }
        
        public uint[] Skills { get; set; }
    }
}