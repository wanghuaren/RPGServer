using Ddxy.Protocol;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "mall")]
    public class MallEntity
    {
        /// <summary>
        /// id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 区服id
        /// </summary>
        [Column(Name = "sid")]
        public uint ServerId { get; set; }

        /// <summary>
        /// 卖方角色id
        /// </summary>
        [Column(Name = "rid")]
        public uint RoleId { get; set; }

        /// <summary>
        /// 商品实例id
        /// </summary>
        public uint DbId { get; set; }

        /// <summary>
        /// 商品配置id
        /// </summary>
        public uint CfgId { get; set; }

        /// <summary>
        /// 剩余数量
        /// </summary>
        public uint Num { get; set; }

        /// <summary>
        /// 已出售的数量
        /// </summary>
        public uint SellNum { get; set; }

        /// <summary>
        /// 商品单价
        /// </summary>
        public uint Price { get; set; }

        /// <summary>
        /// 商品类型
        /// </summary>
        [Column(MapType = typeof(byte))]
        public MallItemType Type { get; set; }

        /// <summary>
        /// 商品分类
        /// </summary>
        [Column(MapType = typeof(byte))]
        public MallItemKind Kind { get; set; }

        /// <summary>
        /// 商品详情
        /// </summary>
        public byte[] Detail { get; set; }

        /// <summary>
        /// 上架时间
        /// </summary>
        public uint CreateTime { get; set; }
    }
}