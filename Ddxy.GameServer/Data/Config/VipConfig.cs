using System.Collections.Generic;
namespace Ddxy.GameServer.Data.Config
{
    public class VipGiftItem
    {
        public uint id { get; set; }
        public uint num { get; set; }
    }
    public class VipGift
    {
        public uint money { get; set; }
        public List<VipGiftItem> item { get; set; }
    }
    public class VipConfig
    {
        public uint level { get; set; }

        public uint next { get; set; }

        public VipGift gift { get; set; }
    }
}