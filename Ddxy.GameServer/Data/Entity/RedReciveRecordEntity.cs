using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "red_recive_record")]
    public class RedReciveRecordEntity
    {
        /// <summary>
        /// id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 所属用户id
        /// </summary>
        [Column(Name = "sid")]
        public uint ServerId { get; set; }

        /// <summary>
        /// 接收角色ID
        /// </summary>
        public uint ReciveId { get; set; }

        /// <summary>
        /// 发送角色ID
        /// </summary>
        public uint SendId { get; set; }

        /// <summary>
        /// 红包ID
        /// </summary>
        public uint RedId { get; set; }

        /// <summary>
        /// 红包类型
        /// </summary>
        public byte RedType { get; set; }

        /// <summary>
        /// 仙玉
        /// </summary>
        public uint Jade { get; set; }

        /// <summary>
        /// 接收时间
        /// </summary>
        public uint ReciveTime { get; set; }
    }
}