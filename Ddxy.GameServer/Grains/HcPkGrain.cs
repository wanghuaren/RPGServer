using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    [CollectionAgeLimit(AlwaysActive = true)]
    public class HcPkGrain : Grain, IHcPkGrain
    {
        private uint _serverId;
        private bool _isActive;
        private IServerGrain _serverGrain;

        private List<S2C_HcPk> _pks; //挑战列表

        private IDisposable _updateTick;
        private ILogger<HcPkGrain> _logger;

        public HcPkGrain(ILogger<HcPkGrain> logger)
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
            return Task.CompletedTask;
        }

        public Task StartUp()
        {
            if (_isActive) return Task.CompletedTask;
            _isActive = true;
            _serverGrain = GrainFactory.GetGrain<IServerGrain>(_serverId);
            _pks = new List<S2C_HcPk>(100);
            _updateTick = RegisterTimer(OnUpdate, null, TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(1));
            return Task.CompletedTask;
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _isActive = false;
            _updateTick?.Dispose();
            _updateTick = null;
            _pks.Clear();
            _pks = null;
            _serverGrain = null;
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new(_isActive);
        }

        public ValueTask<bool> CheckPk(uint roleId)
        {
            if (!_isActive) return new ValueTask<bool>(true);
            return new ValueTask<bool>(_pks.Exists(p => p != null && (p.Sender.Id == roleId || p.Recver.Id == roleId)));
        }

        public Task<Immutable<byte[]>> FindPk(uint roleId)
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            var pk = _pks.FirstOrDefault(p => p != null && (p.Sender.Id == roleId || p.Recver.Id == roleId));
            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(pk)));
        }

        public async ValueTask<bool> AddPk(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return false;
            if (reqBytes.Value == null) return false;
            var req = S2C_HcPk.Parser.ParseFrom(reqBytes.Value);
            if (req.Sender == null || req.Recver == null) return false;

            // 每个人同时只能存在一条挑战信息, 无论是发起者还是受邀
            if (_pks.Exists(p => p != null && (p.Sender.Id == req.Sender.Id || p.Recver.Id == req.Sender.Id)))
                return false;
            if (_pks.Exists(p => p != null && (p.Sender.Id == req.Recver.Id || p.Recver.Id == req.Recver.Id)))
                return false;
            // 插入队列
            var idx = _pks.FindIndex(p => p == null);
            if (idx >= 0) _pks[idx] = req;
            else _pks.Add(req);

            await Task.CompletedTask;
            _logger.LogDebug("玩家({Id1}-{Name1}) 对 玩家({Id2}-{Name2}) 发起皇城决斗",
                req.Sender.Id, req.Sender.Name, req.Recver.Id, req.Recver.Name);

            return true;
        }

        public async Task DelPk(uint roleId)
        {
            if (!_isActive) return;
            var idx = _pks.FindIndex(p => p != null && (p.Sender.Id == roleId || p.Recver.Id == roleId));
            if (idx < 0) return;

            var pk = _pks[idx];
            if (pk.Sender.Id == roleId)
            {
                pk.Sender.State = 2;
            }
            else
            {
                pk.Recver.State = 2;
            }

            await CancelPk(idx);
        }

        public async Task ReadyPk(uint roleId)
        {
            if (!_isActive) return;
            var pk = _pks.FirstOrDefault(p => p != null && (p.Sender.Id == roleId || p.Recver.Id == roleId));
            if (pk == null) return;

            pk.Sender.State = 1;
            pk.Recver.State = 1;
            pk.Seconds = 10; // 10s后开始

            // 广播给两者
            var bits = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CHcPk, pk));
            var grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Sender.Id);
            _ = grain.SendMessage(bits);

            grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Recver.Id);
            _ = grain.SendMessage(bits);

            _logger.LogDebug("玩家({Id1}-{Name1}) 同意了 玩家({Id2}-{Name2})发起的皇城决斗!", pk.Recver.Id,
                pk.Recver.Name, pk.Sender.Id, pk.Sender.Name);
            await Task.CompletedTask;
        }

        public async Task PkWin(uint roleId, int win)
        {
            if (!_isActive) return;
            var idx = _pks.FindIndex(p => p != null && (p.Sender.Id == roleId || p.Recver.Id == roleId));
            if (idx < 0) return;
            var pk = _pks[idx];
            var isWin = win == 1;
            var isDraw = win == 0;

            if (pk.Sender.Id == roleId)
            {
                if (isWin)
                {
                    pk.Win = 1;
                    // recv扣除0.2f最大经验
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Recver.Id);
                    _ = grain.OnHcPkResult(false);
                }
                else if (isDraw)
                {
                    pk.Win = 3;
                }
                else
                {
                    pk.Win = 2;
                    // send扣除0.2f最大经验
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Sender.Id);
                    _ = grain.OnHcPkResult(false);
                }
            }
            else
            {
                if (isWin)
                {
                    pk.Win = 2;
                    // send扣除0.2f最大经验
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Sender.Id);
                    _ = grain.OnHcPkResult(false);
                }
                else if (isDraw)
                {
                    pk.Win = 3;
                }
                else
                {
                    pk.Win = 1;
                    // recv扣除0.2f最大经验
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Recver.Id);
                    _ = grain.OnHcPkResult(false);
                }
            }

            var needBroadCast = !string.IsNullOrWhiteSpace(pk.Text);
            if (needBroadCast)
            {
                Broadcast(GameCmd.S2CHcPk, pk);
            }
            else
            {
                var bits = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CHcPk, pk));

                var grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Sender.Id);
                _ = grain.SendMessage(bits);

                grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Recver.Id);
                _ = grain.SendMessage(bits);
            }

            _pks[idx] = null;
            await Task.CompletedTask;
        }

        // 发送决斗双方列表
        public async Task SendRoleList(uint roleId)
        {
            if (!_isActive) return;
            var pk = _pks.FirstOrDefault(p => p != null && (p.Sender.Id == roleId || p.Recver.Id == roleId));
            if (pk == null) return;

            var sendGrain = GrainFactory.GetGrain<IPlayerGrain>(pk.Sender.Id);
            var recvGrain = GrainFactory.GetGrain<IPlayerGrain>(pk.Recver.Id);

            var resp = new S2C_HcRoleList();

            // 获取主动挑战者的队伍成员信息
            var bytes = await sendGrain.QueryRoleList();
            if (bytes.Value == null) return;
            var roles = RoleInfoList.Parser.ParseFrom(bytes.Value);
            resp.SenderList.AddRange(roles.List);

            // 获取被挑战者的队伍成员信息
            bytes = await recvGrain.QueryRoleList();
            if (bytes.Value == null) return;
            roles = RoleInfoList.Parser.ParseFrom(bytes.Value);
            resp.RecverList.AddRange(roles.List);

            // 推送给双方
            bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CHcRoleList, resp));
            _ = GrainFactory.GetGrain<IPlayerGrain>(roleId).SendMessage(bytes);

            // _ = sendGrain.SendMessage(bytes);
            // _ = recvGrain.SendMessage(bytes);
        }

        private async Task CancelPk(int index)
        {
            if (!_isActive) return;
            var pk = _pks[index];
            if (pk == null) return;

            var needBroadCast = !string.IsNullOrWhiteSpace(pk.Text);
            if (needBroadCast)
            {
                Broadcast(GameCmd.S2CHcPk, pk);
            }
            else
            {
                var bits = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CHcPk, pk));

                var grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Sender.Id);
                _ = grain.SendMessage(bits);

                grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Recver.Id);
                _ = grain.SendMessage(bits);
            }

            if (pk.Sender.State == 2 && pk.Recver.State == 2)
            {
                _logger.LogDebug("玩家({Id1}-{Name1}) 和 玩家({Id2}-{Name2}) 和解",
                    pk.Sender.Id, pk.Sender.Name, pk.Recver.Id, pk.Recver.Name);
            }

            if (pk.Sender.State == 2)
            {
                _logger.LogDebug("玩家({Id}-{Name}) 取消了皇城决斗", pk.Sender.Id, pk.Sender.Name);
            }
            else
            {
                _logger.LogDebug("玩家({Id}-{Name}) 取消了皇城决斗", pk.Recver.Id, pk.Recver.Name);
            }

            _pks[index] = null;
            await Task.CompletedTask;
        }

        private void Broadcast(GameCmd cmd, IMessage msg)
        {
            if (!_isActive) return;
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(cmd, msg)));
        }

        private async Task OnUpdate(object state)
        {
            if (!_isActive)
            {
                _updateTick?.Dispose();
                _updateTick = null;
                return;
            }

            for (var i = 0; i < _pks.Count; i++)
            {
                var pk = _pks[i];
                if (pk == null) continue;

                if (pk.Seconds > 0) pk.Seconds--;
                if (pk.Seconds == 0)
                {
                    if (pk.Sender.State == 1 && pk.Recver.State == 1)
                    {
                        // 可以引发战斗, 防止再次进入, 将seconds设置为负数
                        pk.Seconds = -1;

                        var grain = GrainFactory.GetGrain<IPlayerGrain>(pk.Sender.Id);
                        var ret = await grain.StartPvp(pk.Recver.Id, (byte) BattleType.HuangChengPk);
                        if (ret != 0)
                        {
                            // 取消
                            pk.Sender.State = 2;
                            pk.Recver.State = 2;
                            _ = CancelPk(i);
                        }
                    }
                    else
                    {
                        // 自动取消
                        if (pk.Sender.State != 1) pk.Sender.State = 2;
                        if (pk.Recver.State != 1) pk.Recver.State = 2;
                        _ = CancelPk(i);
                    }
                }
            }

            await Task.CompletedTask;
        }
    }
}