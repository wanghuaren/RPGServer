using System;
using System.Runtime.InteropServices;
using Ddxy.Common.Jwt;
using Ddxy.Common.Model;
using Ddxy.Common.Orleans;
using Ddxy.GameServer.HostedService;
using Ddxy.GameServer.Grains;
using Ddxy.GameServer.Option;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Extensions.Logging;
using Orleans;
using Orleans.ApplicationParts;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Statistics;
using OrleansDashboard;

namespace Ddxy.GameServer
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var logger = LogManager.GetCurrentClassLogger();
            try
            {
                logger.Info("构建Host...");
                var host = CreateHostBuilder(args).Build();
                host.Run();
            }
            catch (Exception ex)
            {
                logger.Error(ex, "Stopped server because of exception");
                throw;
            }
            finally
            {
                LogManager.Flush(TimeSpan.FromSeconds(10));
                LogManager.Shutdown();
            }
        }

        /// <summary>
        /// 创建泛型主机
        /// </summary>
        private static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseConsoleLifetime()
                .ConfigureLogging(logging =>
                {
                    logging.ClearProviders();
                    logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Warning);
                    logging.AddNLog();
                })
                .ConfigureServices((context, services) =>
                {
                    services.Configure<JwtOptions>(context.Configuration.GetSection("JwtOptions"));
                    services.Configure<OrleansOptions>(context.Configuration.GetSection("OrleansOptions"));
                    services.Configure<AppOptions>(context.Configuration.GetSection("AppOptions"));
                    services.Configure<XinPayOptions>(context.Configuration.GetSection("XinPayOptions"));
                    services.AddHttpClient();
                    services.AddMemoryCache();
                    services.AddHostedService<AppHostedService>();
                })
                .UseOrleans((context, builder) =>
                {
                    var orleansOptions = new OrleansOptions();
                    context.Configuration.Bind("OrleansOptions", orleansOptions);

                    // 配置本地集群
                    builder.UseLocalhostClustering(orleansOptions.SiloPort, orleansOptions.GatewayPort, null,
                        orleansOptions.ServiceId,
                        orleansOptions.ClusterId);

                    builder.AddSimpleMessageStreamProvider(orleansOptions.SmsProvider);
                    builder.AddMemoryGrainStorage(orleansOptions.PubSubStore);
                    builder.UseInMemoryReminderService();
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    {
                        builder.UseLinuxEnvironmentStatistics();
                    }

                    builder.ConfigureApplicationParts(parts =>
                    {
                        parts.AddApplicationPart(typeof(GlobalGrain).Assembly).WithReferences();
                    });

                    // Dashboard
                    builder.UseDashboard(config =>
                    {
                        if (!string.IsNullOrWhiteSpace(orleansOptions.DashboardBasePath))
                            config.BasePath = orleansOptions.DashboardBasePath;
                        config.Port = orleansOptions.DashboardPort;
                        config.Username = orleansOptions.DashboardUserName;
                        config.Password = orleansOptions.DashboardPassword;
                    });
                });
    }
}