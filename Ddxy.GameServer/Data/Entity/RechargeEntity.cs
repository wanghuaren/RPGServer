using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "recharge")]
    public class RechargeEntity
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }
        
        /// <summary>
        /// 操作的管理员id
        /// </summary>
        public uint Operator { get; set; }

        /// <summary>
        /// 源id,如果是管理员则为0
        /// </summary>
        public uint From { get; set; }

        /// <summary>
        /// 目标代理id
        /// </summary>
        public uint To { get; set; }

        /// <summary>
        /// 额度
        /// </summary>
        public int Money { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public uint CreateTime { get; set; }
    }
}