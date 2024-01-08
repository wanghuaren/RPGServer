using System;
using System.Buffers.Binary;
using System.IO;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Ddxy.GrainInterfaces.Core;
using Ddxy.Protocol;
using Google.Protobuf;
using Orleans.Streams;

namespace Ddxy.GameServer.Core
{
    public class Packet
    {
        private readonly IAsyncStream<NotifyMessage> _stream;

        public Packet(IAsyncStream<NotifyMessage> stream)
        {
            _stream = stream;
        }

        public Task SendStatus(uint roleId, WebSocketCloseStatus closeStatus, bool notify)
        {
            var nm = new NotifyMessage {Id = roleId, CloseStatus = closeStatus, Notify = notify};
            return _stream.OnNextAsync(nm);
        }

        public Task SendPacket(uint roleId, GameCmd command, IMessage msg = null)
        {
            var nm = new NotifyMessage {Id = roleId, Payload = Serialize(command, msg)};
            return _stream.OnNextAsync(nm);
        }
        
        public Task SendPacket(uint roleId, GameCmd command, byte[] payload)
        {
            var nm = new NotifyMessage {Id = roleId, Payload = Serialize(command, payload)};
            return _stream.OnNextAsync(nm);
        }

        public Task SendPacket(uint roleId, byte[] payload)
        {
            var nm = new NotifyMessage {Id = roleId, Payload = payload};
            return _stream.OnNextAsync(nm);
        }

        public static byte[] Serialize(GameCmd command, IMessage msg)
        {
            uint size = 2;
            if (msg != null)
                size += (uint) msg.CalculateSize();
            var bytes = new byte[size];
            BinaryPrimitives.WriteUInt16BigEndian(new Span<byte>(bytes, 0, 2), (ushort) command);
            if (msg != null)
            {
                // 序列化PB
                using var cos = new CodedOutputStream(new MemoryStream(bytes, 2, bytes.Length - 2));
                msg.WriteTo(cos);
            }

            return bytes;
        }

        public static byte[] Serialize(GameCmd command, byte[] payload)
        {
            uint size = 2;
            if (payload != null)
                size += (uint) payload.Length;
            var bytes = new byte[size];
            BinaryPrimitives.WriteUInt16BigEndian(new Span<byte>(bytes, 0, 2), (ushort) command);
            if (payload != null)
            {
                Array.Copy(payload, 0, bytes, 2, payload.Length);
            }

            return bytes;
        }

        public static byte[] Serialize(IMessage msg)
        {
            if (msg == null) return null;
            var bytes = new byte[msg.CalculateSize()];
            msg.WriteTo(new CodedOutputStream(bytes));
            return bytes;
        }
    }
}