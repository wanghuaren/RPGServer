using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "mail")]
    public class MailEntity
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
        /// 发送者角色id, 0表示系统
        /// </summary>
        public uint Sender { get; set; }

        /// <summary>
        /// 接收者角色id, 0表示全区角色
        /// </summary>
        public uint Recver { get; set; }

        /// <summary>
        /// 后台发送时,后台账号id
        /// </summary>
        public uint Admin { get; set; }

        /// <summary>
        /// 邮件类型
        /// </summary>
        public byte Type { get; set; }

        /// <summary>
        /// 邮件内容
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// 邮件携带内容
        /// </summary>
        public string Items { get; set; }
        
        /// <summary>
        /// 最低转生等级
        /// </summary>
        public byte MinRelive { get; set; }

        /// <summary>
        /// 最低等级
        /// </summary>
        public uint MinLevel { get; set; }
        
        /// <summary>
        /// 最高转生等级
        /// </summary>
        public byte MaxRelive { get; set; }
        
        /// <summary>
        /// 最高等级
        /// </summary>
        public uint MaxLevel { get; set; }
        
        /// <summary>
        /// 备注
        /// </summary>
        public string Remark { get; set; }

        /// <summary>
        /// 发布时间
        /// </summary>
        public uint CreateTime { get; set; }
        
        /// <summary>
        /// 领取时间
        /// </summary>
        public uint PickedTime { get; set; }
        
        /// <summary>
        /// 删除时间
        /// </summary>
        public uint DeleteTime { get; set; }

        /// <summary>
        /// 过期时间
        /// </summary>
        public uint ExpireTime { get; set; }
    }
}