using System;
using System.Text.Json.Serialization;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "ornament")]
    public class OrnamentEntity : IEquatable<OrnamentEntity>
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
        /// 配置id
        /// </summary>
        public uint CfgId { get; set; }

        /// <summary>
        /// 品阶
        /// </summary>
        public byte Grade { get; set; }

        /// <summary>
        /// 装备存放位置
        /// </summary>
        public byte Place { get; set; }

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

        [Column(IsIgnore = true)] public string Name { get; set; }

        public void CopyFrom(OrnamentEntity other)
        {
            Id = other.Id;
            RoleId = other.RoleId;
            CfgId = other.CfgId;
            Grade = other.Grade;
            Place = other.Place;
            BaseAttrs = other.BaseAttrs;
            Recast = other.Recast;
            CreateTime = other.CreateTime;
        }

        public bool Equals(OrnamentEntity other)
        {
            if (null == other) return false;
            return Id == other.Id && RoleId == other.RoleId && CfgId == other.CfgId &&
                   Grade == other.Grade && Place == other.Place &&
                   BaseAttrs.Equals(other.BaseAttrs) && Recast.Equals(other.Recast) &&
                   CreateTime == other.CreateTime;
        }
    }
}