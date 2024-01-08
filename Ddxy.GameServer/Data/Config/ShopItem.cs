namespace Ddxy.GameServer.Data.Config
{
    public class ShopItemConfig
    {
        public uint Type { get; set; }

        public bool Wing { get; set; }

        public uint ItemId { get; set; }

        public uint Price { get; set; }
    }

    public class NpcShopItemConfig
    {
        public uint NpcId { get; set; }

        public uint ItemId { get; set; }

        public uint Price { get; set; }

        public uint Type { get; set; }

        public uint Cost { get; set; }
    }

    public class ZhenBuKuiShopItemConfig
    {
        public uint Id { get; set; }

        public uint ItemId { get; set; }
        
        public int Num { get; set; }
        
        public uint Price { get; set; }

        public uint Type { get; set; }

        public uint Cost { get; set; }
    }
}