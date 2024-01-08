using System.Collections.Generic;

namespace Ddxy.GameServer.Data.Config
{
    public class JItemShopGood
    {
        public uint id { get; set; }
        public uint item { get; set; }
        public uint num { get; set; }
        public uint price { get; set; }
    }

    // 礼包物品
    public class GiftItem
    {
        // 道具ID
        public uint id { get; set; }
        // 物品数量
        public uint num { get; set; }
    }
    public class GiftShopGood
    {
        public uint id { get; set; }
        public uint group { get; set; }
        public uint order { get; set; }

        //礼包中的物品列表
        public List<GiftItem> items { get; set; }

        public uint price { get; set; }
    }

}