using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "recharge_role")]
    public class RechargeRoleEntity
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 操作员id
        /// </summary>
        public uint OpId { get; set; }

        /// <summary>
        /// 操作员昵称
        /// </summary>
        public string OpName { get; set; }

        /// <summary>
        /// 操作员邀请码
        /// </summary>
        public string OpInvitCode { get; set; }

        /// <summary>
        /// 角色id
        /// </summary>
        public uint RoleId { get; set; }

        /// <summary>
        /// 角色所属代理id
        /// </summary>
        public uint ParentId { get; set; }

        /// <summary>
        /// 额度
        /// </summary>
        public int Money { get; set; }

        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public uint CreateTime { get; set; }
    }
}