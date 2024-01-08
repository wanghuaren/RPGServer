using System.Threading;
using System.Threading.Tasks;
using Ddxy.GateServer.Network;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ddxy.GateServer.HostedService
{
    public class AppHostedService : IHostedService
    {
        private readonly IHostEnvironment _environment;
        private readonly IConfiguration _configuration;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly ILogger<AppHostedService> _logger;
        private readonly SiloClient _siloClient;
        private readonly ConnectionManager _connectionManager;

        public AppHostedService(IHostEnvironment environment,
            IConfiguration configuration,
            IHostApplicationLifetime appLifetime,
            ILogger<AppHostedService> logger,
            SiloClient siloClient,
            ConnectionManager connectionManager)
        {
            _environment = environment;
            _configuration = configuration;
            _appLifetime = appLifetime;
            _logger = logger;
            _siloClient = siloClient;
            _connectionManager = connectionManager;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(OnApplicationStarted);
            _appLifetime.ApplicationStopping.Register(OnApplicationStopping);
            _appLifetime.ApplicationStopped.Register(OnApplicationStopped);

            await Task.CompletedTask;
        }

        private void OnApplicationStarted()
        {
            _logger.LogInformation("服务启动成功({Env}) {Url}", _environment.EnvironmentName, _configuration["Urls"]);
            _ = _siloClient.Connect();
        }

        private void OnApplicationStopping()
        {
            _logger.LogInformation("准备停止服务...");
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            var st = _connectionManager.StopTask;
            if (st != null) await st;
            
            await _siloClient.Shutdown();
        }

        private void OnApplicationStopped()
        {
            _logger.LogInformation("服务停止完成");
        }
    }
}