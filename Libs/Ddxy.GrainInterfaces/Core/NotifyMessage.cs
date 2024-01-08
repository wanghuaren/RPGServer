using System;
using System.Net.WebSockets;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces.Core
{
    [Serializable]
    [Immutable]
    public class NotifyMessage
    {
        /// <summary>
        /// 发送者id
        /// </summary>
        public uint Id { get; set; }

        /// <summary>
        /// 关闭信号, 用于GameServer通知GateServer切断连接
        /// </summary>
        public WebSocketCloseStatus CloseStatus { get; set; }

        /// <summary>
        /// 如果CloseStatus不为0, 告诉网关, 是否还需要再通知一次GameServer
        /// </summary>
        public bool Notify { get; set; }

        /// <summary>
        /// 如果是Reply，需要填充该值
        /// </summary>
        public byte[] Payload { get; set; }
    }
}