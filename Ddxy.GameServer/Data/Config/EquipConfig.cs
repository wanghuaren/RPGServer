using System.Text.Json;

namespace Ddxy.GameServer.Data.Config
{
    public class EquipConfig
    {
        public uint Id { get; set; }

        public JsonElement? BaseAttr { get; set; }

        public float? AttrFactor { get; set; }

        public string[] AttrLib { get; set; }

        public uint NeedGrade { get; set; }

        public uint NeedRei { get; set; }

        public JsonElement NeedAttr { get; set; }

        public uint NextId { get; set; }

        public int RndWeight { get; set; }

        public int[] RanRange { get; set; }

        public int Valuable { get; set; }

        public uint BaseScore { get; set; }

        public string Desc { get; set; }

        public string Detail { get; set; }

        public int Dynamic { get; set; }

        public byte Grade { get; set; }

        public int Index { get; set; }

        public int MaxEmbedGemCnt { get; set; }

        public int MaxEndure { get; set; }

        public string Name { get; set; }

        public int Overlap { get; set; }

        public uint OwnerRoleId { get; set; }

        public string Quan { get; set; }

        public byte Race { get; set; }

        public int Rarity { get; set; }

        public byte Sex { get; set; }

        public uint Shape { get; set; }

        public byte Category { get; set; }
    }

    public class EquipAttrConfig
    {
        public EquipAttrLibAttrItem[] BaseAttr { get; set; }

        public EquipAttrLibRangeItem[] RndRange { get; set; }
    }

    public class EquipAttrLibAttrItem
    {
        public string Key { get; set; }

        public int Min { get; set; }

        public int Max { get; set; }
    }

    public class EquipAttrLibRangeItem
    {
        public int Min { get; set; }

        public int Max { get; set; }

        public int Rate { get; set; }
    }

    public class EquipRefinConfig
    {
        public string Attr { get; set; }

        public string Name { get; set; }

        public string Pos1 { get; set; }

        public string Pos2 { get; set; }

        public string Pos3 { get; set; }

        public string Pos4 { get; set; }

        public string Pos5 { get; set; }

        public int Isper { get; set; }
    }
}