using System.Collections.Generic;
using System.Text.Json.Serialization;
using Ddxy.Common.Model.Admin;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "admin")]
    public class AdminEntity
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 用户名
        /// </summary>
        [Column(Name = "username")]
        [JsonPropertyName("username")]
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
        /// 昵称
        /// </summary>
        [Column(Name = "nickname")]
        [JsonPropertyName("nickname")]
        public string NickName { get; set; }

        /// <summary>
        /// 用户状态
        /// </summary>
        [Column(MapType = typeof(byte))]
        public AdminStatus Status { get; set; }

        /// <summary>
        /// 用户状态
        /// </summary>
        [Column(MapType = typeof(byte))]
        public AdminCategory Category { get; set; }

        /// <summary>
        /// 余额
        /// </summary>
        public uint Money { get; set; }

        /// <summary>
        /// 总充值额
        /// </summary>
        public uint TotalPay { get; set; }

        /// <summary>
        /// 邀请码
        /// </summary>
        public string InvitCode { get; set; }

        /// <summary>
        /// 所属代理id, 0表示管理员
        /// </summary>
        [JsonIgnore]
        public uint ParentId { get; set; }

        /// <summary>
        /// 代理等级
        /// </summary>
        public uint Agency { get; set; }

        /// <summary>
        /// 上次登录IP
        /// </summary>
        public string LoginIp { get; set; }

        /// <summary>
        /// 上次登录时间
        /// </summary>
        public uint LoginTime { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public uint CreateTime { get; set; }

        [Navigate(nameof(ParentId))] public AdminEntity Parent { get; set; }
        [Navigate(nameof(ParentId))] public List<AdminEntity> Childs { get; set; }

        [Column(IsIgnore = true)] public string FatherName { get; set; }
        [Column(IsIgnore = true)] public string FatherInvitCode { get; set; }
    }

    /// <summary>
    /// 管理用户状态
    /// </summary>
    public enum AdminStatus : byte
    {
        /// <summary>
        /// 正常状态
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 冻结状态
        /// </summary>
        Frozen = 1
    }
}