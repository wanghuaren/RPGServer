using System;
using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Berrysoft.XXTea;
using Ddxy.GrainInterfaces;
using NLog;
using Orleans;

namespace Ddxy.GateServer.Network
{
    public class Connection : IDisposable
    {
        /// <summary>
        /// websocket对象
        /// </summary>
        public WebSocket Socket { get; private set; }

        /// <summary>
        /// 用户对象
        /// </summary>
        public IPlayerGrain Player { get; private set; }

        /// <summary>
        /// Player id
        /// </summary>
        public uint Id { get; }

        private ILogger _logger = LogManager.GetCurrentClassLogger();
        private XXTeaCryptor _cryptor;


        /// <summary>
        /// 网络加密/解密 密钥
        /// </summary>
        private static readonly byte[] XxTeaKey = Encoding.UTF8.GetBytes(@"GepTfn!1Ubaj&@8i^z");

        public Connection(WebSocket socket, IPlayerGrain player)
        {
            Socket = socket;
            Player = player;
            Id = (uint) player.GetPrimaryKeyLong();

            _cryptor = new XXTeaCryptor();
        }

        public void Dispose()
        {
            Socket = null;
            Player = null;
            _cryptor = null;
            _logger = null;
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 启动Socket的读循环
        /// </summary>
        public async Task<WebSocketCloseStatus> Run()
        {
            _logger.Debug($"Connection {Id} opened");
            // 启动
            if (!await Player.StartUp())
            {
                return WebSocketCloseStatus.NormalClosure;
            }

            // 通知Grain, 网络连接上了
            await Player.Online();

            // 每次读取的数据包最大不能超过4096;
            var buffer = new ArraySegment<byte>(new byte[4096]);
            while (Socket.State == WebSocketState.Open)
            {
                // 超过5分钟没有消息就关闭socket, 防止长期霸占着资源
                var result = await Socket
                    .ReceiveAsync(buffer, new CancellationTokenSource(TimeSpan.FromMinutes(5)).Token);
                if (result.CloseStatus.HasValue)
                {
                    // 说明出错了
                    return result.CloseStatus.Value;
                }

                if (!result.EndOfMessage)
                {
                    // 说明发送量超标了，需要终止
                    return WebSocketCloseStatus.MessageTooBig;
                }

                if (result.MessageType != WebSocketMessageType.Binary)
                {
                    return WebSocketCloseStatus.InvalidMessageType;
                }

                // 仅处理Binary消息
                if (!HandlePacket(buffer[..result.Count]))
                {
                    return WebSocketCloseStatus.NormalClosure;
                }
            }

            return WebSocketCloseStatus.NormalClosure;
        }

        /// <summary>
        /// 关闭Socket连接, notify表示是否通知后端Grain, destroy表示是否让后端立马销毁
        /// </summary>
        public async Task Stop(WebSocketCloseStatus status, bool notify)
        {
            if (Socket == null) return;

            try
            {
                await Socket.CloseAsync(status, "Closed by the Server", CancellationToken.None);
            }
            catch (Exception)
            {
                // ignore
            }

            if (Player != null && notify)
            {
                // 通知PlayerGrain网络断线
                _ = Player.Offline();
            }

            if (_logger != null) _logger.Debug($"Connection {Id} closed");

            Dispose();
        }

        /// <summary>
        /// 发送字节流给客户端
        /// </summary>
        public async Task SendMessageAsync(byte[] buffer)
        {
            if (buffer == null || buffer.Length == 0) return;
            // 加密 xxtea
            buffer = _cryptor.Encrypt(buffer, XxTeaKey);
            // 发送消息, 3s超时
            await Socket.SendAsync(buffer, WebSocketMessageType.Binary, true,
                new CancellationTokenSource(TimeSpan.FromSeconds(3)).Token).ConfigureAwait(false);

            // 输出日志
            // var command = BinaryPrimitives.ReadUInt16BigEndian(buffer.Slice(0, 2));
            // Logger.Debug($"{Id} send command: {command} bytes: {buffer.Count}");
        }

        /// <summary>
        /// 接收来自客户端的字节流，直接传递给GameServer, 这里不做Protobuf解析
        /// </summary>
        private bool HandlePacket(ArraySegment<byte> buffer)
        {
            if (Player == null) return false;

            // 解密, xxtea
            var safeBytes = _cryptor.Decrypt(buffer, XxTeaKey);
            if (safeBytes.Length == 0) return false;
            buffer = new ArraySegment<byte>(safeBytes);

            // 读取2个字节的头部
            var command = BinaryPrimitives.ReadUInt16BigEndian(buffer[..2]);
            var payload = buffer.Slice(2, buffer.Count - 2).ToArray();
            // Logger.Debug($"{Id} recv command: {command} bytes: {buffer.Count}");

            // 不要直接await Orleans处理结果, 否则就阻塞了
            _ = Player.HandlePacket(command, payload);
            return true;
        }
    }
}