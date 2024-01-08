using System;
using System.Text.Json.Serialization;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "pet_ornament")]
    public class PetOrnamentEntity : IEquatable<PetOrnamentEntity>
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
        [JsonIgnore]
        public uint RoleId { get; set; }

        /// <summary>
        /// 锁定？
        /// </summary>
        public bool Locked { get; set; }

        /// <summary>
        /// 类型id
        /// </summary>
        public uint TypeId { get; set; }

        /// <summary>
        /// 品阶
        /// </summary>
        public byte Grade { get; set; }

        /// <summary>
        /// 位置，小于0-未装备, 大于0-装备了，则为宠物ID
        /// </summary>
        public uint Place { get; set; }

        /// <summary>
        /// 基础属性
        /// </summary>
        public string BaseAttrs { get; set; }

        /// <summary>
        /// 重铸预览数据
        /// </summary>
        public string Recast { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public uint CreateTime { get; set; }

        public void CopyFrom(PetOrnamentEntity other)
        {
            Id = other.Id;
            RoleId = other.RoleId;
            Locked = other.Locked;
            TypeId = other.TypeId;
            Grade = other.Grade;
            Place = other.Place;
            BaseAttrs = other.BaseAttrs;
            Recast = other.Recast;
            CreateTime = other.CreateTime;
        }

        public bool Equals(PetOrnamentEntity other)
        {
            if (null == other) return false;
            return Id == other.Id &&
                   RoleId == other.RoleId &&
                   Locked == other.Locked &&
                   TypeId == other.TypeId &&
                   Grade == other.Grade &&
                   Place == other.Place &&
                   BaseAttrs.Equals(other.BaseAttrs) &&
                   Recast.Equals(other.Recast) &&
                   CreateTime == other.CreateTime;
        }
    }
}