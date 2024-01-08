using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "chat_msg")]
    public class ChatMsgEntity
    {
        /// <summary>
        /// id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 发送角色id
        /// </summary>
        public uint FromRid { get; set; }

        /// <summary>
        /// 发送角色id
        /// </summary>
        public uint ToRid { get; set; }

        /// <summary>
        /// 消息类型
        /// </summary>
        public byte MsgType { get; set; }

        /// <summary>
        /// 消息文本
        /// </summary>
        public string Msg { get; set; }

        /// <summary>
        /// 发送时间
        /// </summary>
        public uint SendTime { get; set; }
    }
}