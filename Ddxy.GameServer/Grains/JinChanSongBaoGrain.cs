using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    /// <summary>
    /// 金蟾送宝, 整点刷新
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class JinChanSongBaoGrain : Grain, IJinChanSongBaoGrain
    {
        private bool _isActive;
        private uint _serverId;
        private IServerGrain _serverGrain;

        private bool _isShutDownReq;
        private List<JinChanSongBao> _npcs;

        private IDisposable _refreshTick;
        private ILogger<JinChanSongBaoGrain> _logger;

        private const string Name = "金蟾送宝";

        public JinChanSongBaoGrain(ILogger<JinChanSongBaoGrain> logger)
        {
            _logger = logger;
        }

        public override Task OnActivateAsync()
        {
            _serverId = (uint) this.GetPrimaryKeyLong();
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

            _npcs = new List<JinChanSongBao>(NpcMapConfigs.Count);
            foreach (var (k, v) in NpcMapConfigs)
            {
                if (v <= 0) continue;
                for (var i = 0; i < v; i++)
                {
                    var item = new JinChanSongBao(GameDefine.JinChanSongBaoNpcCfgId, k);
                    _npcs.Add(item);
                }
            }

            var now = DateTimeOffset.Now;
            var m = now.Minute;
            var s = now.Second;
            var delay = (59 - m) * 60 + (60 - s);
            // 每60分钟刷新
            _refreshTick = RegisterTimer(Refresh, null, TimeSpan.FromSeconds(delay), TimeSpan.FromMinutes(60));

            // FIXME: 激活后马上刷新1次
            _ = Refresh(null);
            LogDebug("激活成功");
            return Task.CompletedTask;
        }

        public Task ShutDown()
        {
            if (!_isActive)return Task.CompletedTask;
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

            _serverGrain = null;
            LogDebug("注销成功");
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new ValueTask<bool>(_isActive);
        }

        public ValueTask<bool> IsJinChanSongBao(uint onlyId)
        {
            if (!_isActive) return new(false);
            var ret = _npcs.Exists(p => p.OnlyId == onlyId);
            return new ValueTask<bool>(ret);
        }

        public async ValueTask<int> ApplyChallenge(uint onlyId, uint roleId)
        {
            if (!_isActive) return 1;
            var ws = _npcs.FirstOrDefault(p => p.OnlyId == onlyId);
            if (ws == null) return 1;
            if (ws.Applies.Count > 0) return 2;

            // 检测当前是否在挑战其他星
            var xs = _npcs.Exists(it => it.Applies != null && it.Applies.Contains(roleId));
            if (xs) return 3;

            ws.Applies.Add(roleId);

            // 5s钟之后开始
            ws.BattleWaiter = RegisterTimer(async state =>
                {
                    if (state is not JinChanSongBao star) return;
                    star.BattleWaiter?.Dispose();
                    star.BattleWaiter = null;
                    if (_isShutDownReq) return;
                    if (star.Applies.Count == 0) return;
                    await TrigleBattle(star.OnlyId, star.Applies[0]);
                }, ws,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));

            await Task.CompletedTask;
            return 0;
        }

        public Task ChallengeResult(uint onlyId, bool win)
        {
            if (!_isActive) return Task.CompletedTask;
            var ws = _npcs.FirstOrDefault(p => p.OnlyId == onlyId);
            if (ws != null)
            {
                if (win)
                {
                    _ = _serverGrain.DeleteNpc(onlyId);
                    ws.Dead();
                }
                else
                {
                    ws.Reset();
                }
            }

            return Task.CompletedTask;
        }

        private async Task TrigleBattle(uint npcOnlyId, uint roleId)
        {
            if (!_isActive) return;
            if (_isShutDownReq) return;
            var ws = _npcs.FirstOrDefault(p => p.OnlyId == npcOnlyId);
            if (ws == null || ws.Applies.Count == 0 || ws.Applies[0] != roleId) return;
            var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            var ret = await grain.OnJinChanSongBaoBattle(npcOnlyId);
            if (!ret)
            {
                ws.Reset();
            }
        }

        private async Task Refresh(object args)
        {
            if (!_isActive) return;
            var total = 0;
            foreach (var ws in _npcs)
            {
                ws.Reset();

                if (ws.OnlyId > 0)
                {
                    // 防止意外, 距离创建时间超过1个小时强制进行销毁
                    if (TimeUtil.TimeStamp - ws.CreateTime < 3600)
                    {
                        continue;
                    }

                    // 强制删除
                    _ = _serverGrain.DeleteNpc(ws.OnlyId);
                    ws.Dead();
                }

                // 随机一个位置
                var pos = MapUtil.RandomPos(ws.MapId);
                if (pos == null)
                {
                    LogError($"在地图[{ws.MapId}]上未找到合适的位置放置金蟾");
                    continue;
                }

                // 去创建npc
                var onlyId = await _serverGrain.CreateNpc(new Immutable<byte[]>(Packet.Serialize(new CreateNpcRequest
                {
                    MapId = ws.MapId,
                    MapX = pos.X,
                    MapY = pos.Y,
                    CfgId = ws.CfgId,
                    Owner = new NpcOwner
                    {
                        Type = NpcOwnerType.Activity,
                        Value = (uint) ActivityId.JinChanSongBao
                    }
                })));
                ws.Birth(onlyId, pos.X, pos.Y);

                total++;
            }

            LogDebug($"刷新[{total}]个");
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"金蟾送宝[{_serverId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
             _logger?.LogDebug($"金蟾送宝[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
             _logger?.LogError($"金蟾送宝[{_serverId}]:{msg}");
        }

        private static readonly Dictionary<uint, int> NpcMapConfigs = new()
        {
            [1009] = 10, //龙宫
        };

        public class JinChanSongBao : IDisposable
        {
            public uint CfgId { get; }

            public uint MapId { get; }

            public uint OnlyId { get; private set; }

            public int MapX { get; private set; }

            public int MapY { get; private set; }

            public List<uint> Applies { get; private set; }

            public IDisposable BattleWaiter { get; set; }

            public uint CreateTime;

            public JinChanSongBao(uint cfgId, uint mapId)
            {
                CfgId = cfgId;
                MapId = mapId;

                OnlyId = 0;
                Applies = new List<uint>();
            }

            public void Dispose()
            {
                Applies.Clear();
                Applies = null;
            }

            public void Reset()
            {
                Applies.Clear();
                BattleWaiter?.Dispose();
                BattleWaiter = null;
            }

            public void Dead()
            {
                OnlyId = 0;
                Reset();
            }

            public void Birth(uint onlyId, int mapX, int mapY)
            {
                OnlyId = onlyId;
                MapX = mapX;
                MapY = mapY;
                CreateTime = TimeUtil.TimeStamp;
            }
        }
    }
}