using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Option;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;

namespace Ddxy.GameServer.HostedService
{
    public class AppHostedService : IHostedService
    {
        private readonly IConfiguration _configuration;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly ILoggerFactory _loggerFactory;
        private readonly IGrainFactory _grainFactory;
        private readonly AppOptions _options;

        private readonly ILogger<AppHostedService> _logger;

        public AppHostedService(IConfiguration configuration,
            IHostApplicationLifetime appLifetime,
            ILoggerFactory loggerFactory,
            IGrainFactory grainFactory,
            IOptions<AppOptions> options)
        {
            _configuration = configuration;
            _appLifetime = appLifetime;
            _loggerFactory = loggerFactory;
            _grainFactory = grainFactory;
            _options = options.Value;

            _logger = _loggerFactory.CreateLogger<AppHostedService>();
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(OnApplicationStarted);
            _appLifetime.ApplicationStopping.Register(OnApplicationStopping);
            _appLifetime.ApplicationStopped.Register(OnApplicationStopped);

            // 初始化全局日志工厂, 这样可以摆脱DI的束缚 todo
            XLogFactory.Init(_loggerFactory);

            _logger.LogInformation("加载配置表...");
            await ConfigService.Load(_options.ConfigDir);
            _logger.LogInformation("连接mysql...");
            await DbService.Init(_configuration.GetConnectionString("DbConnection"));
            _logger.LogInformation("连接redis...");
            await RedisService.Init(_configuration.GetConnectionString("RedisConnection"), Convert.ToInt32(_configuration.GetConnectionString("RedisDb")));
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("关闭redis...");
            await RedisService.Close();
            _logger.LogInformation("关闭mysql...");
            await DbService.Close();
        }

        private void OnApplicationStarted()
        {
            _grainFactory.GetGrain<IGlobalGrain>(0).StartUp();

            var ids = DbService.ListNormalServerId().Result;
            foreach (var grain in ids.Select(id => _grainFactory.GetGrain<IServerGrain>(id)))
            {
                grain.Startup().Wait(TimeSpan.FromSeconds(10));
            }

            _logger.LogInformation("服务启动成功");
        }

        private void OnApplicationStopping()
        {
            _logger.LogInformation("准备停服(需要大约5s-10m)...");
        }

        private void OnApplicationStopped()
        {
            _logger.LogInformation("服务停止完成");
        }
    }
}