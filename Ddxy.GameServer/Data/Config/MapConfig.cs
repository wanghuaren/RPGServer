using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Ddxy.GameServer.Data.Config
{
    public class MapConfig
    {
        public uint Id { get; set; }

        public string Name { get; set; }

        public uint Terrain { get; set; }
        
        public Vec2 StartPos { get; set; }
        
        public TransferConfig[] Transfers { get; set; }

        public uint[] Anlei { get; set; }

        /// <summary>
        /// 这个值需要遍历npc.json后根据autoCreate来填充
        /// </summary>
        [JsonIgnore]
        public List<uint> Npcs { get; set; }
    }
    
    public class Vec2
    {
        public int X { get; set; }
        public int Y { get; set; }
    }

    public class TransferConfig
    {
        public Vec2 Pos { get; set; }

        public TransferTo To { get; set; }

        public struct TransferTo
        {
            public uint Map { get; set; }

            public Vec2 Pos { get; set; }
        }
    }

    public class TerrainConfig
    {
        public uint Id { get; set; }
        public uint Width { get; set; }
        public uint Height { get; set; }
        public uint GridWidth { get; set; }
        public uint GridHeight { get; set; }
        public int Rows { get; set; }
        public int Cols { get; set; }
        public uint[][] Blocks { get; set; }
    }
}