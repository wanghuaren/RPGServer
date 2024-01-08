using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "sectWar")]
    public class SectWarEntity
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 区服id
        /// </summary>
        [Column(Name = "sid")]
        public uint ServerId { get; set; }

        /// <summary>
        /// 当前第几季
        /// </summary>
        public uint Season { get; set; }

        /// <summary>
        /// 当前第几轮
        /// </summary>
        public uint Turn { get; set; }

        /// <summary>
        /// 上次开始时间
        /// </summary>
        public uint LastTime { get; set; }
    }
}