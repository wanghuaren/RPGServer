using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "error")]
    public class ErrorEntity
    {
        /// <summary>
        /// id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 用户id
        /// </summary>
        public uint Uid { get; set; }

        /// <summary>
        /// 角色Id
        /// </summary>
        public uint Rid { get; set; }

        /// <summary>
        /// 错误详情
        /// </summary>
        public string Error { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        /// <summary>
        /// 错误状态
        /// </summary>
        [Column(MapType = typeof(byte))]
        public ErrorStatus Status { get; set; }

        /// <summary>
        /// 发布时间
        /// </summary>
        public uint CreateTime { get; set; }
    }

    public enum ErrorStatus
    {
        Open = 0,
        Close = 1,
        Ignore = 2,
        Delay = 3
    }
}