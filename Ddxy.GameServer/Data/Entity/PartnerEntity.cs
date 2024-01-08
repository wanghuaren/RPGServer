using System;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "partner")]
    public class PartnerEntity : IEquatable<PartnerEntity>
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 所属角色id
        /// </summary>
        [Column(Name = "rid")]
        public uint RoleId { get; set; }

        /// <summary>
        /// 配置id
        /// </summary>
        public uint CfgId { get; set; }

        /// <summary>
        /// 转生等级
        /// </summary>
        public byte Relive { get; set; }

        /// <summary>
        /// 等级
        /// </summary>
        public byte Level { get; set; }

        /// <summary>
        /// 经验值
        /// </summary>
        public ulong Exp { get; set; }

        /// <summary>
        /// 参战位置
        /// </summary>
        public uint Pos { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public uint CreateTime { get; set; }

        public void CopyFrom(PartnerEntity other)
        {
            Id = other.Id;
            RoleId = other.RoleId;
            CfgId = other.CfgId;
            Relive = other.Relive;
            Level = other.Level;
            Exp = other.Exp;
            Pos = other.Pos;
            CreateTime = other.CreateTime;
        }

        public bool Equals(PartnerEntity other)
        {
            if (other == null) return false;
            return Id == other.Id && RoleId == other.RoleId && CfgId == other.CfgId &&
                   Relive == other.Relive && Level == other.Level && Exp == other.Exp &&
                   Pos == other.Pos && CreateTime == other.CreateTime;
        }
    }
}