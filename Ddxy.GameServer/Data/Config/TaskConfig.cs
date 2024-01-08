using System.Text.Json;

namespace Ddxy.GameServer.Data.Config
{
    public class TaskConfig
    {
        public uint Id { get; set; }
        public byte Kind { get; set; }
        public string Name { get; set; }
        public uint Group { get; set; }
        public uint Daily { get; set; }

        public TaskLimitConfig Limits { get; set; }
        public TaskEventConfig[] Events { get; set; }
        public TaskFailEventConfig[] FailEvents { get; set; }
    }

    public class TaskLimitConfig
    {
        public byte? Relive { get; set; }
        
        public byte? Level { get; set; }

        public byte? Race { get; set; }

        public uint? PreTask { get; set; }

        public bool? Sect { get; set; }
    }

    public class TaskEventConfig
    {
        public byte Type { get; set; }
        /// <summary>
        /// exp,petExp,money,active,50001,50002,50003等这样的物品id
        /// </summary>
        public JsonElement Prizes { get; set; }

        public uint? Npc { get; set; }
        public string[] Speaks { get; set; }
        public bool? AutoTrigle { get; set; }
        public TaskCreateNpcConfig CreateNpc { get; set; }
        public uint? Map { get; set; }
        public int? X { get; set; }
        public int? Y { get; set; }
        public string Action { get; set; }
        public string Talk { get; set; }
        public uint? Item { get; set; }
        public uint? ItemNum { get; set; }
        public uint? FromNpc { get; set; }
        public uint? ToNpc { get; set; }
        public string Tip2 { get; set; }
    }

    public class TaskCreateNpcConfig
    {
        public uint Npc { get; set; }

        public uint Map { get; set; }

        public int X { get; set; }

        public int Y { get; set; }
    }

    public class TaskFailEventConfig
    {
        public byte Type { get; set; }
        public uint? DeadNum { get; set; }
        public uint? Duration { get; set; }
    }
}