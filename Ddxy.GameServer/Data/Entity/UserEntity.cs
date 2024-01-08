using System.Text.Json.Serialization;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    /// <summary>
    /// 用户状态
    /// </summary>
    public enum UserStatus : byte
    {
        /// <summary>
        /// 正常状态
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 冻结状态
        /// </summary>
        Frozen = 1,
    }

    public enum UserType : byte
    {
        Normal = 0,
        Gm = 1,
        Robot = 2
    }

    [Table(Name = "user")]
    public class UserEntity
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        [JsonIgnore]
        [Column(Name = "username")]
        public string UserName { get; set; }

        /// <summary>
        /// 用户密码
        /// </summary>
        [JsonIgnore]
        public string Password { get; set; }

        /// <summary>
        /// 密码盐
        /// </summary>
        [JsonIgnore]
        public string PassSalt { get; set; }

        /// <summary>
        /// 用户状态
        /// </summary>
        [JsonIgnore]
        [Column(MapType = typeof(byte))]
        public UserStatus Status { get; set; }

        /// <summary>
        /// 用户类型
        /// </summary>
        [Column(MapType = typeof(byte))]
        public UserType Type { get; set; }

        /// <summary>
        /// 所属代理, 0表示直属运营商
        /// </summary>
        [JsonIgnore]
        public uint ParentId { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public uint CreateTime { get; set; }

        /// <summary>
        /// 上次登录IP, 注意要IP地址转整数
        /// </summary>
        [JsonIgnore]
        public string LastLoginIp { get; set; }

        /// <summary>
        /// 上次登录时间
        /// </summary>
        [JsonIgnore]
        public uint LastLoginTime { get; set; }

        /// <summary>
        /// 上次使用的角色id
        /// </summary>
        public uint LastUseRoleId { get; set; }
    }
}