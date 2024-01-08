using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Ddxy.GateServer.Network
{
    public static class WebSocketManagerExtensions
    {
        public static IServiceCollection AddWebSocketManager(this IServiceCollection services)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));
            // 注入SiloClient
            services.TryAddSingleton<SiloClient>();
            // 注入ConnectionManager
            services.TryAddSingleton<ConnectionManager>();
            return services;
        }

        public static IApplicationBuilder MapWebSocketManager(this IApplicationBuilder app, PathString path)
        {
            var webSocketOptions = new WebSocketOptions
            {
                KeepAliveInterval = TimeSpan.FromMinutes(5),
            };
            app.UseWebSockets(webSocketOptions);

            return app.Map(path, builder => builder.UseMiddleware<WebSocketManagerMiddleware>());
        }
    }
}