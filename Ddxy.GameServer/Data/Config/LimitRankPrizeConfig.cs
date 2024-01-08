using System.Collections.Generic;
namespace Ddxy.GameServer.Data.Config
{
    public class LimitRankPrizeItem
    {
        public uint id { get; set; }
        public uint num { get; set; }
    }
    public class LimitRankPrizeConfig
    {
        public uint rank { get; set; }

        public List<LimitRankPrizeItem> prize1 { get; set; }

        public List<LimitRankPrizeItem> prize2 { get; set; }
    }
}