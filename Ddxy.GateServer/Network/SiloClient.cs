using System;
using System.Threading.Tasks;
using Ddxy.Common.Orleans;
using Ddxy.GateServer.Option;
using Ddxy.GrainInterfaces;
using Ddxy.GrainInterfaces.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Streams;

namespace Ddxy.GateServer.Network
{
    /// <summary>
    /// 保持和SiloHost集群通讯
    /// </summary>
    public class SiloClient
    {
        private readonly ILogger<SiloClient> _logger;
        private readonly OrleansOptions _orleansOptions;
        private readonly AppOptions _appOptions;

        private IClusterClient _client;
        private StreamSubscriptionHandle<NotifyMessage> _handler;
        public event Action<NotifyMessage> OnRecvNotify;
        public event Action OnLostClusterConnect;

        /// <summary>
        /// 当前是否成功连接
        /// </summary>
        public bool IsConnnected => _client is {IsInitialized: true};

        private bool _shutDownRequest;
        private bool _isConnecting;

        public SiloClient(ILogger<SiloClient> logger, IOptions<OrleansOptions> orleansOptions,
            IOptions<AppOptions> appOptions)
        {
            _logger = logger;
            _orleansOptions = orleansOptions.Value;
            _appOptions = appOptions.Value;
        }

        /// <summary>
        /// 连接SiloHost
        /// </summary>
        public async Task Connect()
        {
            if (_isConnecting) return;
            _isConnecting = true;

            try
            {
                _logger.LogInformation("连接SiloHost...");
                // 构建IClusterClient
                _client = new ClientBuilder()
                    .UseLocalhostClustering(_orleansOptions.GatewayPort, _orleansOptions.ServiceId,
                        _orleansOptions.ClusterId)
                    .Configure<ConnectionOptions>(options =>
                    {
                        options.OpenConnectionTimeout = TimeSpan.FromSeconds(3);
                    })
                    .Configure<ClientMessagingOptions>(options =>
                    {
                        options.ResponseTimeout = TimeSpan.FromSeconds(15);
                        options.ResponseTimeoutWithDebugger = TimeSpan.FromMinutes(5);
                    })
                    .AddSimpleMessageStreamProvider(_orleansOptions.SmsProvider)
                    .AddClusterConnectionLostHandler(OnClusterConnectionLost)
                    .Build();

                // 如果连接失败, 就间隔3s后不断的重试
                await _client.Connect(async ex =>
                {
                    if (_shutDownRequest) return false;
                    _logger.LogError("连接SiloHost失败, 3s后重试");
                    await Task.Delay(TimeSpan.FromSeconds(3));
                    return true;
                });

                if (_shutDownRequest) return;
                // Guid.Parse(_appOptions.StreamId)
                var stream = _client.GetStreamProvider(_orleansOptions.SmsProvider)
                    .GetStream<NotifyMessage>(Guid.Empty, _orleansOptions.StreamNameSpace);
                _handler = await stream.SubscribeAsync(RecvNotify);
                _logger.LogInformation("连接SiloHost成功");
            }
            catch (Exception ex)
            {
                _logger.LogInformation("连接SiloHost出错, 3s后重试");
                _logger.LogError(ex, "{Msg}", ex.Message);
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public async Task Shutdown()
        {
            _shutDownRequest = true;
            // 由于是主动关闭, 所以要把OnLostClusterConnect赋值为null，防止立马重连
            OnLostClusterConnect = null;
            OnRecvNotify = null;

            if (_handler != null)
                await _handler.UnsubscribeAsync();
            _handler = null;

            if (_client != null)
                await _client.Close();
            _client = null;
        }

        public IPlayerGrain GetPlayerGrain(uint id)
        {
            return GetGrain<IPlayerGrain>(id);
        }

        public T GetGrain<T>(long primaryKey, string grainClassNamePrefix = null) where T : IGrainWithIntegerKey
        {
            if (!IsConnnected) return default;
            return _client.GetGrain<T>(primaryKey);
        }

        public T GetGrain<T>(string primaryKey, string grainClassNamePrefix = null) where T : IGrainWithStringKey
        {
            if (!IsConnnected) return default;
            return _client.GetGrain<T>(primaryKey);
        }

        public T GetGrain<T>(Guid primaryKey, string grainClassNamePrefix = null) where T : IGrainWithGuidKey
        {
            if (!IsConnnected) return default;
            return _client.GetGrain<T>(primaryKey);
        }

        private void OnClusterConnectionLost(object sender, EventArgs e)
        {
            _logger.LogInformation("SiloHost连接已断开");

            _handler = null;
            _client?.Dispose();
            _client = null;

            // 如果是主动停服引起的，OnLostClusterConnect会为null
            OnLostClusterConnect?.Invoke();

            if (!_shutDownRequest)
            {
                // 立马进行重连
                if (_isConnecting)
                {
                    _logger.LogError("此时不能连接, Connecting={Flag}", _isConnecting);
                }
                else
                {
                    _ = Connect();
                }
            }
        }

        // 接收来自GameServer的推送消息
        private Task RecvNotify(NotifyMessage msg, StreamSequenceToken token)
        {
            // 这里不用await，防止阻塞
            OnRecvNotify?.Invoke(msg);
            return Task.CompletedTask;
        }
    }
}