using System;
using System.Linq;
using System.Net.WebSockets;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ddxy.Common.Jwt;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GrainInterfaces.Gate;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Ddxy.GateServer.Network
{
    public class WebSocketManagerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly JwtOptions _jwtOptions;
        private readonly ConnectionManager _connectionManager;
        private readonly SiloClient _siloClient;

        public WebSocketManagerMiddleware(RequestDelegate next, IOptions<JwtOptions> options,
            ConnectionManager connectionManager, SiloClient siloClient)
        {
            _next = next;
            _jwtOptions = options.Value;
            _connectionManager = connectionManager;
            _siloClient = siloClient;
        }

        // ReSharper disable once UnusedMember.Global
        public async Task InvokeAsync(HttpContext context)
        {
            // 检测是否为WebSocket请求
            if (!context.WebSockets.IsWebSocketRequest)
            {
                await _next(context);
                return;
            }

            // 如果发起了停服请求, 则拒绝接受任何新的连接
            if (!_siloClient.IsConnnected || _connectionManager.StopTask != null) return;

            // 接受连接, 后续校验的过程中，利用close的code和message来提供错误反馈
            using (var socket = await context.WebSockets.AcceptWebSocketAsync())
            {
                // 获取请求携带的token
                var tokenValues = context.Request.Query["token"];
                if (tokenValues.Count == 0)
                {
                    await PolicyViolation(socket);
                    return;
                }

                var queryToken = tokenValues[0];

                // 校验并解析token
                var principal = TokenUtil.ParseToken(_jwtOptions, queryToken);
                if (principal == null)
                {
                    await PolicyViolation(socket);
                    return;
                }

                // 从Claims中获取用户id
                uint.TryParse(principal.Claims.FirstOrDefault(p => p.Type == ClaimTypes.Sid)?.Value,
                    out var userId);
                if (userId == 0)
                {
                    await PolicyViolation(socket);
                    return;
                }

                var grain = _siloClient.GetGrain<IApiGateGrain>(0);
                if (grain == null)
                {
                    // 说明Silo没有连接上
                    await EndpointUnavailable(socket);
                    return;
                }

                // 检测uid和token是否匹配
                var valid = await grain.CheckUserToken(userId, queryToken, true);
                if (!valid)
                {
                    await PolicyViolation(socket);
                    return;
                }

                // 获取该用户所有的角色id和区服id
                var roles = await grain.QueryRoles(userId);
                if (roles == null || roles.Last == 0 || roles.All == null || roles.All.Count == 0)
                {
                    await PolicyViolation(socket);
                    return;
                }

                // 检测rid指定的server是否已经正常激活, 也要考虑在线停服的问题
                {
                    var globalGrain = _siloClient.GetGrain<IGlobalGrain>(0);
                    if (globalGrain == null)
                    {
                        // 说明Silo没有连接上
                        await EndpointUnavailable(socket);
                        return;
                    }

                    roles.All.TryGetValue(roles.Last, out var serverId);
                    if (serverId == 0)
                    {
                        await PolicyViolation(socket);
                        return;
                    }

                    var serverGrain = _siloClient.GetGrain<IServerGrain>(serverId);
                    if (serverGrain == null)
                    {
                        // 说明Silo没有连接上
                        await EndpointUnavailable(socket);
                        return;
                    }
                    // 检查分区是否已经维护, 或者分区未启动
                    var ret = await serverGrain.CheckActive();
                    if (!ret)
                    {
                        await PolicyViolation(socket);
                        return;
                    }
                }

                // 使用 WebSocket 时，“必须”在连接期间保持中间件管道运行。 如果在中间件管道结束后尝试发送或接收 WebSocket 消息，可能会遇到以下异常情况：
                // The remote party closed the WebSocket connection without completing the close handshake
                // 坚决不要使用 Task.Wait()、Task.Result 或类似阻塞调用来等待套接字完成，因为这可能导致严重的线程处理问题。 请始终使用 await。
                await _connectionManager.AddSocket(roles.Last, roles.All.Keys.ToArray(), socket);
            }
        }

        /// <summary>
        /// 提示前端重新登录
        /// </summary>
        private static Task PolicyViolation(WebSocket socket)
        {
            return CloseSocket(socket, WebSocketCloseStatus.PolicyViolation);
        }

        /// <summary>
        /// 提示前端服务尚未准备就绪
        /// </summary>
        private static Task EndpointUnavailable(WebSocket socket)
        {
            return CloseSocket(socket, WebSocketCloseStatus.EndpointUnavailable);
        }

        private static Task CloseSocket(WebSocket socket, WebSocketCloseStatus status)
        {
            return socket.CloseAsync(status, "",
                new CancellationTokenSource(TimeSpan.FromSeconds(1)).Token);
        }
    }
}