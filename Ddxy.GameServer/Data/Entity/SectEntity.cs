using System;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "sect")]
    public class SectEntity : IEquatable<SectEntity>
    {
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 所属区服id
        /// </summary>
        [Column(Name = "sid")]
        public uint ServerId { get; set; }

        /// <summary>
        /// 帮派名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 帮派宗旨
        /// </summary>
        public string Desc { get; set; }

        /// <summary>
        /// 帮主角色id
        /// </summary>
        public uint OwnerId { get; set; }

        /// <summary>
        /// 人数
        /// </summary>
        public uint MemberNum { get; set; }

        /// <summary>
        /// 帮派总共享
        /// </summary>
        public uint Contrib { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public uint CreateTime { get; set; }

        public void CopyFrom(SectEntity other)
        {
            Id = other.Id;
            ServerId = other.ServerId;
            Name = other.Name;
            Desc = other.Desc;
            OwnerId = other.OwnerId;
            MemberNum = other.MemberNum;
            Contrib = other.Contrib;
            CreateTime = other.CreateTime;
        }

        public bool Equals(SectEntity other)
        {
            if (other == null) return false;
            return Id == other.Id && ServerId == other.ServerId && Name.Equals(other.Name) && Desc.Equals(other.Desc) &&
                   OwnerId == other.OwnerId && MemberNum == other.MemberNum &&
                   Contrib == other.Contrib && CreateTime == other.CreateTime;
        }
    }
}