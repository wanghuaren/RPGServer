using System.Collections.Generic;

namespace Ddxy.GameServer.Data.Config
{
    public class TotalPayConfig
    {
        public uint Pay { get; set; }

        public List<TotalPayRewardConfig> Rewards { get; set; }
    }

    public class TotalPayRewardConfig
    {
        public uint Id { get; set; }

        public int Num { get; set; }

        // 是否为翅膀, 如果为true，id表示wing.json中的id
        public bool Wing { get; set; }
        
        // 是否为称号
        public bool Title { get; set; }
    }
}