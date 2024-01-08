using System.Text.Json;

namespace Ddxy.GameServer.Data.Config
{
    public class MonsterConfig
    {
        public uint Id { get; set; }

        public string Name { get; set; }

        public uint Res { get; set; }

        public uint Level { get; set; }

        public uint Pet { get; set; }

        public float Hp { get; set; }

        public float Atk { get; set; }

        public float Spd { get; set; }

        /// <summary>
        /// 熟练度
        /// </summary>
        public float Profic { get; set; }

        public bool Catch { get; set; }

        /// <summary>
        /// 附加属性
        /// </summary>
        public JsonElement? Attrs { get; set; }

        public uint[] Skills { get; set; }
    }
}