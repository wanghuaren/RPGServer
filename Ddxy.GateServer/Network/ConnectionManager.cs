using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;
using Ddxy.GrainInterfaces.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ddxy.GateServer.Network
{
    public class ConnectionManager
    {
        // 存储所有角色id-连接对象
        private readonly ConcurrentDictionary<uint, Connection> _connections = new();

        private readonly ILogger<ConnectionManager> _logger;
        private readonly SiloClient _client;

        public Task StopTask { get; private set; }

        public ConnectionManager(ILogger<ConnectionManager> logger,
            IHostApplicationLifetime lifetime,
            SiloClient client)
        {
            _logger = logger;

            _client = client;
            _client.OnRecvNotify += OnRecvNotify;
            _client.OnLostClusterConnect += OnLostClusterConnect;

            lifetime.ApplicationStopping.Register(OnApplicationStopping);
        }

        /// <summary>
        /// 新增WebSocket
        /// </summary>
        public async Task AddSocket(uint rid, uint[] roles, WebSocket socket)
        {
            if (roles != null)
            {
                // 1个用户同时只能一个角色在线, 其他角色只需下线, 没必要销毁
                foreach (var r in roles)
                {
                    if (_connections.ContainsKey(r))
                        await RemoveSocket(r, WebSocketCloseStatus.NormalClosure, true);
                }
            }

            var status = WebSocketCloseStatus.NormalClosure;
            var grain = _client.GetPlayerGrain(rid);
            if (grain != null)
            {
                // 登记connection, key为角色id
                var conn = new Connection(socket, grain);
                _connections.TryAdd(rid, conn);

                try
                {
                    // 开启socket的读循环, 这里会一直阻塞直到读循环结束
                    status = await conn.Run();
                }
                catch (OperationCanceledException)
                {
                    status = WebSocketCloseStatus.NormalClosure;
                }
                catch (Exception ex)
                {
                    status = WebSocketCloseStatus.InternalServerError;
                    _logger.LogError(ex, "{Msg}", ex.Message);
                }
            }

            // 读循环结束了, 需要关闭该socket
            await RemoveSocket(rid, status, true);
        }

        /// <summary>
        /// 移除并关闭Socket
        /// </summary>
        /// <param name="id">用户id</param>
        /// <param name="status">关闭状态码</param>
        /// <param name="notify">是否通知player下线</param>
        public async Task RemoveSocket(uint id, WebSocketCloseStatus status, bool notify)
        {
            _connections.TryRemove(id, out var conn);
            if (conn == null) return;
            await conn.Stop(status, notify);
        }

        /// <summary>
        /// 关闭所有的连接, 并通知后端
        /// </summary>
        public Task RemoveAllSocket(WebSocketCloseStatus status, bool notify)
        {
            var list = _connections.Values.Select(p => p.Stop(status, notify));
            _connections.Clear();
            return Task.WhenAll(list);
        }

        // 接收来自GameServer的推送消息
        private void OnRecvNotify(NotifyMessage msg)
        {
            if (msg.Id == 0) return;

            if (msg.CloseStatus > 0)
            {
                _logger.LogDebug("Player({Id}) recv CloseStatus:{Status}", msg.Id, msg.CloseStatus);
                _ = RemoveSocket(msg.Id, msg.CloseStatus, msg.Notify);
                return;
            }

            // 转发给对应的conn
            if (_connections.TryGetValue(msg.Id, out var conn) && conn != null)
            {
                _ = conn.SendMessageAsync(msg.Payload);
            }
        }

        // 与游戏服务器失去联系了, 让所有socket断线告知
        private void OnLostClusterConnect()
        {
            // 关闭所有连接, 并通知后端Grain
            _ = RemoveAllSocket(WebSocketCloseStatus.EndpointUnavailable, false);
        }

        private void OnApplicationStopping()
        {
            // 关闭所有连接, 并通知后端Grain
            StopTask = RemoveAllSocket(WebSocketCloseStatus.NormalClosure, true);
        }
    }
}