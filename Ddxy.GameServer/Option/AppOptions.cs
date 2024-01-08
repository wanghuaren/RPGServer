namespace Ddxy.GameServer.Option
{
    public class AppOptions
    {
        /// <summary>
        /// 实例编号，最大不能超过1024, 主要是考虑到SnowFlake算法
        /// </summary>
        public ushort MachineId { get; set; }

        /// <summary>
        /// 本地存放Config的目录
        /// </summary>
        public string ConfigDir { get; set; }

        public bool FastSldh { get; set; }

        public bool FastWzzz { get; set; }
        
        public bool PrintSldh { get; set; }

        public bool FastSectWar { get; set; }
        
        public bool FastSinglePk { get; set; }

        public bool FastDaLuanDou { get; set; }
        /// <summary>
        /// 创角时是否给所有物品
        /// </summary>
        public bool AllItems { get; set; }

        public bool TestPay { get; set; }

        /// <summary>
        /// 队伍不需要至少3个人的限制
        /// </summary>
        public bool TeamUnLimit { get; set; }
    }
}