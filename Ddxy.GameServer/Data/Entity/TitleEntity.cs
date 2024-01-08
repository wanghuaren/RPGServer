using System;
using System.Text.Json.Serialization;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "title")]
    public class TitleEntity : IEquatable<TitleEntity>
    {
        /// <summary>
        /// id
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
        /// 称号模板id
        /// </summary>
        public uint CfgId { get; set; }

        /// <summary>
        /// 称号文本
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 是否穿戴
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public uint CreateTime { get; set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        public uint ExpireTime { get; set; }

        public void CopyFrom(TitleEntity other)
        {
            Id = other.Id;
            RoleId = other.RoleId;
            CfgId = other.CfgId;
            Text = other.Text;
            Active = other.Active;
            CreateTime = other.CreateTime;
            ExpireTime = other.ExpireTime;
        }

        public bool Equals(TitleEntity other)
        {
            if (null == other) return false;
            return Id == other.Id &&
                   RoleId == other.RoleId &&
                   CfgId == other.CfgId &&
                   Text.Equals(other.Text) &&
                   Active == other.Active &&
                   CreateTime == other.CreateTime &&
                   ExpireTime == other.ExpireTime;
        }
    }
}