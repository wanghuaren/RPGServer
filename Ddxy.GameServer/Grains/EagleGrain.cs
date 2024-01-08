using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.Protocol;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    /// <summary>
    /// 金翅大鹏, 每天晚上10点刷新
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class EagleGrain : Grain, IEagleGrain
    {
        private bool _isActive;
        private uint _serverId;
        private IServerGrain _serverGrain;

        private bool _isShutDownReq;
        private List<Eagle> _npcs;
        private Eagle _instance = null;
        private bool _killed = true;

        private IDisposable _refreshTick;
        private ILogger<EagleGrain> _logger;

        private const string Name = "金翅大鹏";

        public EagleGrain(ILogger<EagleGrain> logger)
        {
            _logger = logger;
        }

        public override Task OnActivateAsync()
        {
            _serverId = (uint)this.GetPrimaryKeyLong();
            return Task.CompletedTask;
        }

        public override Task OnDeactivateAsync()
        {
            return ShutDown();
        }

        public Task StartUp()
        {
            if (_isActive) return Task.CompletedTask;
            _isActive = true;
            _serverGrain = GrainFactory.GetGrain<IServerGrain>(_serverId);
            _isShutDownReq = false;

            _npcs = new List<Eagle>(NpcMapConfigs.Count);
            foreach (var (k, v) in NpcMapConfigs)
            {
                foreach (var pos in v)
                {
                    var item = new Eagle(GameDefine.EagleNpcCfgId, k, pos.X, pos.Y);
                    _npcs.Add(item);
                }
            }
            LogDebug("激活成功");
            // 开始检查晚上10点活动开始
            // CheckOpenTime();
            // 激活则开始，因为是全天活动，只是晚上十点创建新位置或重置死亡
            _ = OnActivityOpen(null);
            return Task.CompletedTask;
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _isActive = false;
            _isShutDownReq = true;
            _refreshTick?.Dispose();
            _refreshTick = null;

            foreach (var ws in _npcs)
            {
                ws.Dispose();
            }
            _npcs.Clear();
            _npcs = null;
            // 上面已经释放了，这里不需要再释放，只用赋null
            _instance = null;
            _killed = true;

            _serverGrain = null;
            LogDebug("注销成功");
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new ValueTask<bool>(_isActive);
        }

        public ValueTask<bool> IsEagle(uint onlyId)
        {
            if (!_isActive) return new(false);
            var ret = _instance != null && _instance.OnlyId == onlyId;
            return new ValueTask<bool>(ret);
        }

        public async ValueTask<int> ApplyChallenge(uint onlyId, uint roleId)
        {
            if (!_isActive) return 1;
            if (_instance == null || _instance.OnlyId != onlyId) return 2;
            if (_instance.Applies.Count > 0) return 3;
            _instance.Applies.Add(roleId);
            // 5s钟之后开始
            _instance.BattleWaiter = RegisterTimer(async state =>
                {
                    if (state is not Eagle star) return;
                    star.BattleWaiter?.Dispose();
                    star.BattleWaiter = null;
                    if (_isShutDownReq) return;
                    if (star.Applies.Count == 0) return;
                    await TrigleBattle(star.OnlyId, star.Applies[0]);
                }, _instance,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));

            await Task.CompletedTask;
            return 0;
        }

        public Task ChallengeResult(uint onlyId, bool win)
        {
            if (!_isActive) return Task.CompletedTask;
            if (_instance != null)
            {
                if (win)
                {
                    // 强制删除
                    _ = _serverGrain.DeleteNpc(onlyId);
                    // 可能现在得大鹏（1个小时整点生成的）不是玩家正在打得大鹏
                    if (_instance.OnlyId == onlyId)
                    {
                        _instance.Dead();
                        _killed = true;
                    }
                }
                else
                {
                    _instance.Reset();
                }
            }

            return Task.CompletedTask;
        }

        private void CheckOpenTime()
        {
            if (!_isActive) return;
            // 晚上10点
            var now = DateTimeOffset.Now;
            var today22 = new DateTimeOffset(now.Year, now.Month, now.Day, 22, 0, 0, TimeSpan.FromHours(8));
            var tomorrow22 = today22.AddDays(1);

            var nextOpenTime = now;
            if (now < today22)
            {
                nextOpenTime = today22;
            }
            else if (now > today22)
            {
                nextOpenTime = tomorrow22;
            }

            var delayTs = nextOpenTime.Subtract(now);
            if (delayTs.TotalSeconds <= 0) delayTs = TimeSpan.FromSeconds(1);
            LogInfo($"{delayTs.Days}日{delayTs.Hours}时{delayTs.Minutes}分{delayTs.Seconds}秒后开启");

            // 防止Timer等待太久而休眠, 超过1个小时的等待就用1个小时后再次来检查时间
            if (delayTs.TotalHours >= 1)
            {
                RegisterTimer(_ =>
                {
                    CheckOpenTime();
                    return Task.CompletedTask;
                }, null, TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(-1));
            }
            else
            {
                RegisterTimer(OnActivityOpen, null, delayTs, TimeSpan.FromMilliseconds(-1));
            }
        }

        private async Task OnActivityOpen(object args)
        {
            if (_instance != null && _instance.OnlyId > 0)
            {
                // 强制删除
                _ = _serverGrain.DeleteNpc(_instance.OnlyId);
                _instance.Dead();
            }
            _instance = _npcs[new Random().Next(_npcs.Count)];
            _instance.Dead();
            _killed = false;
            // 每60分钟刷新
            var now = DateTimeOffset.Now;
            var m = now.Minute;
            var s = now.Second;
            var delay = (59 - m) * 60 + (60 - s);
            _refreshTick?.Dispose();
            _refreshTick = RegisterTimer(Refresh, null, TimeSpan.FromSeconds(delay), TimeSpan.FromMinutes(60));
            // 马上刷新一下
            await Refresh(null);
        }

        private async Task TrigleBattle(uint npcOnlyId, uint roleId)
        {
            if (!_isActive) return;
            if (_isShutDownReq) return;
            if (_instance == null || _instance.Applies.Count == 0 || _instance.Applies[0] != roleId) return;
            var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            var ret = await grain.OnEagleBattle(npcOnlyId);
            if (!ret)
            {
                _instance.Reset();
            }
        }

        private async Task Refresh(object args)
        {
            if (!_isActive) return;
            // 大鹏死亡后等到下次活动开启再刷新
            // if (_instance == null || _killed) return;
            if (_instance != null)
            {
                if (_killed)
                {
                    LogDebug("上一小时的大鹏被击杀");
                }
                else
                {
                    LogDebug("上一小时的大鹏还活着");
                }
                // 从地图删掉上次的大鹏
                if (_instance.OnlyId > 0)
                {
                    // 强制删除
                    _ = _serverGrain.DeleteNpc(_instance.OnlyId);
                    _instance.Dead();
                }
            }
            // 直接重新生成1只，因为现在改为1小时1只了
            {
                _instance = _npcs[new Random().Next(_npcs.Count)];
                _instance.Dead();
                _killed = false;
            }

            _instance.Reset();

            if (_instance.OnlyId > 0)
            {
                // 防止意外, 距离创建时间超过1个小时强制进行销毁
                if (TimeUtil.TimeStamp - _instance.CreateTime >= 3600)
                {
                    // 强制删除
                    _ = _serverGrain.DeleteNpc(_instance.OnlyId);
                    _instance.Dead();
                    // 重新换个位置
                    _instance = _npcs[new Random().Next(_npcs.Count)];
                }
                else
                {
                    return;
                }
            }

            // 去创建npc
            var onlyId = await _serverGrain.CreateNpc(new Immutable<byte[]>(Packet.Serialize(new CreateNpcRequest
            {
                MapId = _instance.MapId,
                MapX = _instance.MapX,
                MapY = _instance.MapY,
                CfgId = _instance.CfgId,
                Owner = new NpcOwner
                {
                    Type = NpcOwnerType.Activity,
                    Value = (uint)ActivityId.Eagle
                }
            })));
            _instance.Birth(onlyId);

            // 广播铃铛
            if (_serverGrain != null)
            {
                var mapCfg = ConfigService.Maps.GetValueOrDefault(_instance.MapId, null);
                // 构造消息
                var msgBell = new ChatMessage
                {
                    Type = ChatMessageType.GmBell,
                    Msg = $"<color=#ffffff>身怀大量宝物的世界BOSS出现在</color><color=#00ff00>{mapCfg?.Name}</color><color=#ffffff>，请各位少侠前往消灭...</color>",
                    BellTimes = 3
                };
                _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msgBell })));
            }
            LogDebug("刷新成功");
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"金翅大鹏[{_serverId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"金翅大鹏[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"金翅大鹏[{_serverId}]:{msg}");
        }

        private static readonly Dictionary<uint, List<Pos>> NpcMapConfigs = new()
        {
            // 东海渔村
            [1010] = new() { new() { X = 149, Y = 75 }, new() { X = 45, Y = 59 }, new() { X = 94, Y = 99 }, new() { X = 102, Y = 15 } },
        };

        public class Eagle : IDisposable
        {
            public uint CfgId { get; }

            public uint MapId { get; }

            public uint OnlyId { get; private set; }

            public int MapX { get; private set; }

            public int MapY { get; private set; }

            public List<uint> Applies { get; private set; }

            public IDisposable BattleWaiter { get; set; }

            public uint CreateTime;

            public Eagle(uint cfgId, uint mapId, int mapX, int mapY)
            {
                CfgId = cfgId;
                MapId = mapId;
                MapX = mapX;
                MapY = mapY;

                OnlyId = 0;
                Applies = new List<uint>();
            }

            public void Dispose()
            {
                Applies?.Clear();
                Applies = null;
            }

            public void Reset()
            {
                Applies?.Clear();
                BattleWaiter?.Dispose();
                BattleWaiter = null;
            }

            public void Dead()
            {
                OnlyId = 0;
                Reset();
            }

            public void Birth(uint onlyId)
            {
                OnlyId = onlyId;
                CreateTime = TimeUtil.TimeStamp;
            }
        }
    }
}