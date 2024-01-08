using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "server")]
    public class ServerEntity
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 区服名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 区服状态
        /// </summary>
        [Column(MapType = typeof(byte))]
        public ServerStatus Status { get; set; }

        /// <summary>
        /// 是否设置为推荐
        /// </summary>
        public bool Recom { get; set; }

        /// <summary>
        /// 排序值, 越小越靠前
        /// </summary>
        public uint Rank { get; set; }

        /// <summary>
        /// 网关地址
        /// </summary>
        public string Addr { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public uint CreateTime { get; set; }

        /// <summary>
        /// 创角数
        /// </summary>
        [Column(IsIgnore = true)]
        public uint RegRoleNum { get; set; }
        
        /// <summary>
        /// 在线数
        /// </summary>
        [Column(IsIgnore = true)]
        public int OnlineNum { get; set; }
        
        /// <summary>
        /// 水路大会状态
        /// </summary>
        [Column(IsIgnore = true)]
        public string SldhInfo { get; set; }
        
        /// <summary>
        /// 王者之战状态
        /// </summary>
        [Column(IsIgnore = true)]
        public string WzzzInfo { get; set; }
        
        /// <summary>
        /// 帮战状态
        /// </summary>
        [Column(IsIgnore = true)]
        public string SectWarInfo { get; set; }
        
        /// <summary>
        /// 单人PK状态
        /// </summary>
        [Column(IsIgnore = true)]
        public string SinglePkInfo { get; set; }

        /// <summary>
        /// 神兽降临状态
        /// </summary>
        [Column(IsIgnore = true)]
        public string SsjlInfo { get; set; }
    }

    /// <summary>
    /// 区服状态
    /// </summary>
    public enum ServerStatus : byte
    {
        /// <summary>
        /// 正常状态
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 临时维护
        /// </summary>
        Stop = 1,

        /// <summary>
        /// 永久停服
        /// </summary>
        Dead = 2,
    }
}