namespace Ddxy.Common.Orleans
{
    public class OrleansOptions
    {
        /// <summary>
        /// 集群的唯一id，所有使用该id的client和silo可以直接互相通讯。
        /// </summary>
        public string ClusterId { get; set; }

        /// <summary>
        /// 应用程序唯一id，该id可能被一些provider使用，比如persistence providers，部署后不得修改。
        /// </summary>
        public string ServiceId { get; set; }

        public int SiloPort { get; set; }

        public int GatewayPort { get; set; }

        public string SmsProvider { get; set; }

        public string PubSubStore { get; set; }

        public string StreamNameSpace { get; set; }

        public int DashboardPort { get; set; }
        public string DashboardBasePath { get; set; }
        public string DashboardUserName { get; set; }
        public string DashboardPassword { get; set; }
    }
}