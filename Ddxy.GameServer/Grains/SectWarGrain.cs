using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Logic.SectWar;
using Ddxy.GameServer.Option;
using Ddxy.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;
using Ddxy.GameServer.Util;

namespace Ddxy.GameServer.Grains
{
    /// <summary>
    /// 帮战玩法
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class SectWarGrain : Grain, ISectWarGrain
    {
        private uint _serverId;
        private IServerGrain _serverGrain;
        private AppOptions _options;

        private bool _isActive;
        private SectWarEntity _entity;

        private uint _openTime;
        private uint _fightTime;

        private SectWarState _state;
        private IDisposable _tempTick;

        private Dictionary<uint, SectWarSect> _sects; // 允许参加帮战的所有帮派
        private Dictionary<uint, SectWarMember> _players; // 所有已入场的玩家

        private TimeSpan _enterDuration = TimeSpan.FromMinutes(30); // 入场阶段时长

        private ILogger<SectWarGrain> _logger;

        private const string Name = "帮战";

        // 队伍至少需要2个队员(加上队长共计3人)
        private const int MinTeamMemberCount = 2;

        public SectWarGrain(ILogger<SectWarGrain> logger, IOptions<AppOptions> options)
        {
            _logger = logger;
            _options = options.Value;
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

        public async Task StartUp()
        {
            if (_isActive) return;
            _isActive = true;
            _serverGrain = GrainFactory.GetGrain<IServerGrain>(_serverId);

            _sects = new Dictionary<uint, SectWarSect>(100);
            _players = new Dictionary<uint, SectWarMember>(1000);

            _entity = await DbService.QuerySectWar(_serverId);
            if (_entity == null)
            {
                _entity = new SectWarEntity
                {
                    ServerId = _serverId,
                    Season = 0,
                    Turn = 0,
                    LastTime = 0
                };
                await DbService.InsertEntity(_entity);
            }

            LogDebug("激活成功");

            CheckOpenTime(true);
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _isActive = false;
            _serverGrain = null;

            _tempTick?.Dispose();
            _tempTick = null;

            if (_sects != null)
            {
                foreach (var sect in _sects.Values)
                {
                    sect.Dispose();
                }
                _sects.Clear();
                _sects = null;
            }

            if (_players != null)
            {
                foreach (var player in _players.Values)
                {
                    player.Dispose();
                }
                _players.Clear();
                _players = null;
            }
            _state = SectWarState.Close;

            LogDebug("注销成功");

            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new(_isActive);
        }

        public async Task<string> GmOpen(bool open, uint opUid)
        {
            await Task.CompletedTask;
            if (!_isActive) return "尚未激活";
            if (open)
            {
                if (_state == SectWarState.Close)
                {
                    // 1s钟后开启入场
                    _tempTick?.Dispose();
                    _tempTick = RegisterTimer(OnActivityEnter, null, TimeSpan.FromSeconds(0.1),
                        TimeSpan.FromSeconds(1));
                    LogInfo($"后台用户[{opUid}]开启");
                }
            }
            else
            {
                if (_state != SectWarState.Close)
                {
                    // 1s钟后结束
                    _tempTick?.Dispose();
                    _tempTick = RegisterTimer(OnActivityFinish, null, TimeSpan.FromSeconds(0.1),
                        TimeSpan.FromSeconds(1));
                    LogInfo($"后台用户[{opUid}]关闭");
                }
            }

            return null;
        }

        public ValueTask<byte> State()
        {
            return new ValueTask<byte>((byte) _state);
        }

        public Task<Immutable<byte[]>> GetActivityInfo()
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            var resp = new SectWarActivityInfo
            {
                Season = _entity.Season,
                Turn = _entity.Turn,
                State = _state,
                OpenTime = _openTime,
                FightTime = _fightTime
            };
            if (resp.State == SectWarState.Close)
            {
                resp.OpenTime = 0;
                resp.FightTime = 0;
            }

            var bytes = new Immutable<byte[]>(Packet.Serialize(resp));
            return Task.FromResult(bytes);
        }

        public async Task<Immutable<byte[]>> Enter(Immutable<byte[]> reqBytes)
        {
            await Task.CompletedTask;
            var resp = new SectWarApproachResponse();
            var req = SectWarApproachRequest.Parser.ParseFrom(reqBytes.Value);
            
            if (!_isActive)
            {
                resp.Error = "尚未激活";
                return new Immutable<byte[]>(Packet.Serialize(resp));
            }

            if (req.Id == 0 || req.SectId == 0)
            {
                resp.Error = "参数错误";
                return new Immutable<byte[]>(Packet.Serialize(resp));
            }

            if (_state != SectWarState.Enter)
            {
                resp.Error = "您已错过入场时间, 期待参加下次帮战";
                return new Immutable<byte[]>(Packet.Serialize(resp));
            }

            if (_players.ContainsKey(req.Id))
            {
                resp.Error = "您已经入场";
                return new Immutable<byte[]>(Packet.Serialize(resp));
            }

            _sects.TryGetValue(req.SectId, out var sect);
            if (sect == null)
            {
                LogDebug($"玩家[{req.Id}, {req.SectId}]申请入场但是所在帮派不具备参赛资格");
                resp.Error = "您所在的帮派不具备参与本次帮战的资格";
                return new Immutable<byte[]>(Packet.Serialize(resp));
            }

            var roleData = new SectWarRoleData
            {
                Camp = sect.Camp,
                Id = req.Id,
                Name = req.Name
            };
            var player = new SectWarMember(roleData, req.SectId, GrainFactory.GetGrain<IPlayerGrain>(roleData.Id));
            _players.Add(player.Id, player);
            sect.Members.Add(player);

            LogDebug($"玩家[{req.Id}]入场");

            resp.SectWarId = sect.SectWarId;
            resp.Camp = sect.Camp;
            resp.Place = player.Place;
            resp.State = player.State;
            return new Immutable<byte[]>(Packet.Serialize(resp));
        }

        // 強制退出帮战
        private async Task PlayerForceExit(uint roleId)
        {
            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            if (playerGrain != null)
            {
                await playerGrain.OnExitSectWar();
            }
        }

        public async Task Exit(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive)
            {
                await PlayerForceExit(roleId);
                return;
            }
            _players.TryGetValue(roleId, out var player);
            // if (player is not {Place: SectWarPlace.JiDi}) return;
            if (!_players.Remove(roleId))
            {
                await PlayerForceExit(roleId);
                return;
            }
            LogDebug($"玩家[{roleId}]离场");

            _sects.TryGetValue(player.SectId, out var sect);
            if (sect != null)
            {
                var idx = sect.Members.FindIndex(p => p.Id == roleId);
                if (idx >= 0)
                {
                    sect.Members.RemoveAt(idx);
                    if (player.IsTeamLeader)
                    {
                        foreach (var swm in player.Members)
                        {
                            _players.Remove(swm.Id);
                            LogDebug($"玩家[{swm.Id}]离场");

                            idx = sect.Members.FindIndex(p => p.Id == swm.Id);
                            if (idx >= 0) sect.Members.RemoveAt(idx);
                            swm.Dispose();
                        }
                    }

                    // 通知本帮剩下的成员
                    sect.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarExit, new S2C_SectWarExit
                    {
                        Data = player.Data
                    })));

                    player.Dispose();

                    // 如果本帮所有人都离开了
                    if (_state == SectWarState.Fight && !sect.HasPlayer)
                    {
                        var enemySect = sect.Enemy;
                        RemoveSect(sect);
                        // 直接算对方赢
                        if (enemySect != null)
                        {
                            SendReward(enemySect, true);
                            RemoveSect(enemySect);
                        }
                    }
                }
            }
            else
            {
                if (player.IsTeamLeader)
                {
                    foreach (var swm in player.Members)
                    {
                        _players.Remove(swm.Id);
                        swm.Dispose();
                    }
                }

                player.Dispose();
            }
        }

        public async Task Offline(uint roleId, uint teamLeader)
        {
            if (!_isActive) return;
            SectWarMember player;
            if (teamLeader > 0)
            {
                _players.TryGetValue(teamLeader, out var member);
                if (member == null) return;
                player = member.Members.Find(p => p.Id == roleId);
            }
            else
            {
                _players.TryGetValue(roleId, out player);
            }

            if (player == null) return;
            player.Online = false;

            await Task.CompletedTask;
        }

        public async Task Online(uint roleId, uint teamLeader, uint sectId)
        {
            if (!_isActive) return;
            SectWarMember player;
            // 判断是否入场
            if (teamLeader > 0)
            {
                _players.TryGetValue(teamLeader, out var member);
                if (member == null) return;
                if (roleId == teamLeader) player = member;
                else player = member.Members.Find(p => p.Id == roleId);
            }
            else
            {
                _players.TryGetValue(roleId, out player);
            }

            if (player != null) player.Online = true;

            if (_state == SectWarState.Enter)
            {
                // 入场阶段
                if (player == null)
                {
                    // 未入场
                    _sects.TryGetValue(sectId, out var sect);
                    if (sect != null)
                    {
                        var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                        _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarState,
                            new S2C_SectWarState
                            {
                                State = _state,
                                My = sect.Data,
                                Enemy = sect.Enemy?.Data
                            })));
                    }
                }
                else
                {
                    // 已入场
                    _sects.TryGetValue(player.SectId, out var sect);
                    if (sect != null)
                    {
                        player.SendPacket(GameCmd.S2CSectWarState, new S2C_SectWarState
                        {
                            State = _state,
                            My = sect.Data,
                            Enemy = sect.Enemy?.Data,
                            IsEnter = true
                        }, false);

                        player.SendPacket(GameCmd.S2CSectWarEnter, new S2C_SectWarEnter
                        {
                            Camp = sect.Camp,
                            Place = player.Place,
                            State = player.State
                        }, false);
                    }
                }
            }
            else if (_state == SectWarState.Fight && player != null)
            {
                _sects.TryGetValue(player.SectId, out var sect);
                if (sect != null)
                {
                    player.SendPacket(GameCmd.S2CSectWarState, new S2C_SectWarState
                    {
                        State = _state,
                        My = sect.Data,
                        Enemy = sect.Enemy?.Data,
                        IsEnter = true
                    }, false);

                    player.SendPacket(GameCmd.S2CSectWarEnter, new S2C_SectWarEnter
                    {
                        Camp = sect.Camp,
                        Place = player.Place,
                        State = player.State
                    }, false);

                    if (sect.Cannon.Graber != null)
                    {
                        player.SendPacket(GameCmd.S2CSectWarGrabCannon, new S2C_SectWarGrabCannon
                        {
                            Graber = sect.Cannon.Graber.Data,
                            Seconds = 10
                        }, false);
                    }

                    if (player.LockDoorTimer != null)
                    {
                        player.SendPacket(GameCmd.S2CSectWarLockDoor, new S2C_SectWarLockDoor
                        {
                            Locker = player.Data
                        }, false);
                    }
                }
            }

            await Task.CompletedTask;
        }

        public async Task CreateTeam(uint leaderRoleId, uint teamId)
        {
            if (!_isActive) return;
            if (teamId == 0 || leaderRoleId == 0) return;
            if (_state == SectWarState.Close) return;
            _players.TryGetValue(leaderRoleId, out var player);
            if (player == null || player.InTeam) return;
            player.BuildTeam(teamId);

            LogDebug($"玩家[{leaderRoleId}]创建队伍");
            await Task.CompletedTask;
        }

        public async Task DestroyTeam(uint leaderRoleId)
        {
            if (!_isActive) return;
            if (_state == SectWarState.Close) return;
            _players.TryGetValue(leaderRoleId, out var player);
            if (player == null || !player.IsTeamLeader) return;
            // 把成员都放出来
            foreach (var member in player.Members)
            {
                member.DestroyTeam();
                _players.TryAdd(member.Id, member);
            }

            player.DestroyTeam();

            LogDebug($"玩家[{leaderRoleId}]解散队伍");
            await Task.CompletedTask;
        }

        public async Task AddTeamMember(uint leaderRoleId, uint roleId)
        {
            if (!_isActive) return;
            if (_state == SectWarState.Close) return;
            _players.TryGetValue(leaderRoleId, out var player);
            if (player is not {IsTeamLeader: true}) return;
            // 从_players中移除, 加到player的Members中
            _players.Remove(roleId, out var member);
            if (member == null) return;
            player.AddTeamMember(member);

            LogDebug($"玩家[{leaderRoleId}]添加队伍成员[{roleId}]");
            await Task.CompletedTask;
        }

        public async Task DelTeamMember(uint leaderRoleId, uint roleId)
        {
            if (!_isActive) return;
            if (_state == SectWarState.Close) return;
            _players.TryGetValue(leaderRoleId, out var player);
            if (player is not {IsTeamLeader: true}) return;
            var member = player.DelTeamMember(roleId);
            if (member == null) return;
            _players.TryAdd(member.Id, member);

            LogDebug($"玩家[{leaderRoleId}]移除队伍成员[{roleId}]");
            await Task.CompletedTask;
        }

        public async Task SwapTeamLeader(uint oldLeader, uint newLeader)
        {
            if (!_isActive) return;
            if (_state == SectWarState.Close) return;
            _players.TryGetValue(oldLeader, out var player);
            if (player == null || !player.IsTeamLeader) return;
            var idx = player.Members.FindIndex(p => p.Id == newLeader);
            if (idx < 0) return;
            // 给新队长构建队伍, 把原队伍的成员加入到该队伍中
            var member = player.Members[idx];
            member.BuildTeam(player.Data.Team);
            foreach (var swm in player.Members)
            {
                member.AddTeamMember(swm);
            }

            member.AddTeamMember(player);

            _players.Remove(player.Id);
            _players.TryAdd(member.Id, member);

            LogDebug($"玩家[{oldLeader}]转移队长给玩家[{newLeader}]");
            await Task.CompletedTask;
        }

        public async ValueTask<bool> ChangePlace(uint roleId, byte placeValue)
        {
            await Task.CompletedTask;
            if (!_isActive) return false;
            if (placeValue > (byte) SectWarPlace.ZhanChang) return false;
            var place = (SectWarPlace) placeValue;
            // 查找是否已入场
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.Place == place) return false;
            if (player.State != SectWarRoleState.Idle) return false;
            _sects.TryGetValue(player.SectId, out var sect);
            if (sect == null) return false;
            // 如果游戏尚未开始
            if (_state != SectWarState.Fight)
            {
                var ts = TimeSpan.FromSeconds(_fightTime - TimeUtil.TimeStamp);
                player.SendNotice($"请等待 {ts.Minutes}分{ts.Seconds}秒帮战正式开始", false);
                return false;
            }

            // 如果在比武场, 要检查是否已经发起挑战
            if (player.Place == SectWarPlace.BiWuChang)
            {
                if (sect.Arena.Player1 != null && sect.Arena.Player1.Id == roleId ||
                    sect.Arena.Player2 != null && sect.Arena.Player2.Id == roleId)
                {
                    player.SendNotice("请取消挑战或等待挑战结束", false);
                    return false;
                }
            }

            // 检查失败时间
            if (player.Place == SectWarPlace.JiDi)
            {
                var surplus = player.CheckLostTime();
                if (surplus > 0)
                {
                    player.SendNotice($"请再耐心等待{surplus}秒", false);
                    return false;
                }
            }

            player.SyncPlace(place, true);
            return true;
        }

        public async Task ReadyPk(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.Idle) return;
            if (player.Place != SectWarPlace.BiWuChang)
            {
                player.SendNotice("请先进入比武场", false);
                return;
            }

            // 没有对手的帮派
            _sects.TryGetValue(player.SectId, out var sect);
            if (sect?.Enemy == null || sect.IsDead) return;

            var arena = sect.Arena;
            if (arena.Player1 != null && arena.Player2 != null)
            {
                player.SendNotice("当前正有PK在进行", false);
                return;
            }

            if (arena.Player1 != null && arena.Player1.Camp == player.Camp ||
                arena.Player2 != null && arena.Player2.Camp == player.Camp)
            {
                player.SendNotice("我方已有单位在等待PK", false);
                return;
            }

            if (!arena.CheckEnable(roleId))
            {
                player.SendNotice("上轮刚刚PK过，请等待下一轮", false);
                return;
            }

            if (arena.Player1 == null)
            {
                arena.Player1 = player;

                // 通知两个帮派
                var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarReadyPk, new S2C_SectWarReadyPk
                {
                    Sender = arena.Player1.Data
                }));
                sect.Broadcast(bytes);
                sect.Enemy.Broadcast(bytes);

                // 5分钟内必须对方必须接, 否则视对方失败
                arena.ForceTimer?.Dispose();
                arena.ForceTimer = RegisterTimer(OnPkTimeout, arena.Player1.SectId, TimeSpan.FromMinutes(5),
                    TimeSpan.FromSeconds(1));
            }
            else if (arena.Player2 == null)
            {
                arena.Player2 = player;
                arena.ForceTimer?.Dispose();
                arena.ForceTimer = null;

                var resp = new S2C_SectWarReadyPk
                {
                    Sender = arena.Player1.Data,
                    Recver = arena.Player2.Data
                };

                // 通知两个帮派
                var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarReadyPk, resp));
                sect.Broadcast(bytes);
                sect.Enemy.Broadcast(bytes);
            }

            player.SyncState(SectWarRoleState.LockArena, true);

            // 如果两个位置都排满了, 就开始
            if (arena.Player1 != null && arena.Player2 != null)
            {
                _ = arena.Battle();

                arena.Player1.SyncState(SectWarRoleState.Battle, true);
                arena.Player2.SyncState(SectWarRoleState.Battle, true);
            }
        }

        public async Task CancelPk(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.LockArena) return;
            _sects.TryGetValue(player.SectId, out var sect);
            if (sect?.Enemy == null || sect.IsDead) return;

            var arena = sect.Arena;
            // 只能是发起者取消
            if (arena.Player1 != null && arena.Player1.Id == roleId)
            {
                player.SyncState(SectWarRoleState.Idle, true);

                var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarCancelPk,
                    new S2C_SectWarCancelPk
                    {
                        Sender = arena.Player1.Data
                    }));
                sect.Broadcast(bytes);
                // 通知敌方帮派
                sect.Enemy?.Broadcast(bytes);

                arena.Reset(false);
            }
        }

        public async Task OnPkWin(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.Battle) return;
            _sects.TryGetValue(player.SectId, out var winSect);
            if (winSect?.Enemy == null || winSect.IsDead) return;

            var arena = winSect.Arena;
            var ret = arena.Player1 != null && arena.Player2 != null &&
                      (arena.Player1.Id == roleId || arena.Player2.Id == roleId);
            if (!ret) return;

            var win = arena.Player1;
            var lost = arena.Player2;
            if (arena.Player2.Id == roleId)
            {
                win = arena.Player2;
                lost = arena.Player1;
            }

            win.BattleRoleId = 0;
            win.SyncState(SectWarRoleState.Idle, true);
            win.SyncPlace(SectWarPlace.JiDi, true);

            lost.BattleRoleId = 0;
            lost.SyncState(SectWarRoleState.Idle, true);
            lost.SyncPlace(SectWarPlace.JiDi, true);
            lost.SetLostTime();

            // 清空比武场
            arena.Reset(false);

            var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarPkResult, new S2C_SectWarPkResult
            {
                Win = win.Data,
                Lost = lost.Data
            }));
            // 广播比武结果
            winSect.Broadcast(bytes);

            var lostSect = winSect.Enemy;
            if (lostSect != null)
            {
                lostSect.Broadcast(bytes);
                AddHp(lostSect, -100);
            }
        }

        private async Task OnPkTimeout(object obj)
        {
            if (!_isActive) return;
            var sectId = (uint) obj;
            _sects.TryGetValue(sectId, out var winSect);
            if (winSect == null) return;
            winSect.Arena.ForceTimer?.Dispose();
            winSect.Arena.ForceTimer = null;
            // 只能是发起者不为空，没受邀者
            if (winSect.Arena.Player1 == null || winSect.Arena.Player2 != null) return;

            var arena = winSect.Arena;
            var win = arena.Player1;
            win.BattleRoleId = 0;
            win.SyncState(SectWarRoleState.Idle, true);
            win.SyncPlace(SectWarPlace.JiDi, true);

            // 清空比武场
            arena.Reset(false);

            var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarPkResult, new S2C_SectWarPkResult
            {
                Win = win.Data,
                Lost = null
            }));
            // 广播比武结果
            winSect.Broadcast(bytes);

            var lostSect = winSect.Enemy;
            if (lostSect != null)
            {
                lostSect.Broadcast(bytes);
                AddHp(lostSect, -100);
            }

            await Task.CompletedTask;
        }

        public async Task GrabCannon(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.Idle) return;
            if (player.Place != SectWarPlace.ZhanChang)
            {
                player.SendNotice("请先进入战场", false);
                return;
            }

            // 必须要3个人的队伍
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (MinTeamMemberCount > 0 && (player.Members == null || player.Members.Count < MinTeamMemberCount))
            {
                player.SendNotice("需要至少3人的队伍", false);
                return;
            }

            // 没有对手的帮派
            _sects.TryGetValue(player.SectId, out var sect);
            if (sect?.Enemy == null || sect.IsDead) return;

            var cannon = sect.Cannon;
            if (cannon.Graber == null)
            {
                cannon.Graber = player;
                cannon.GrabTime = TimeUtil.TimeStamp;
                // 龙神大炮的点燃时间为20s
                cannon.Timer = RegisterTimer(OnCannonFire, player.Id, TimeSpan.FromSeconds(SectWarCannon.Seconds),
                    TimeSpan.FromSeconds(1));
                // 通知双方帮派
                var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarGrabCannon,
                    new S2C_SectWarGrabCannon
                    {
                        Graber = player.Data,
                        Seconds = SectWarCannon.Seconds
                    }));
                sect.Broadcast(bytes);
                sect.Enemy.Broadcast(bytes);
            }
            else if (cannon.Graber.Camp == player.Camp)
            {
                player.SendNotice("我方已占领炮台", false);
            }
            else
            {
                // 两个人打一架
                _ = cannon.Battle(player);
                cannon.Graber.SyncState(SectWarRoleState.Battle, true);
                player.SyncState(SectWarRoleState.Battle, true);

                // 通知双方帮派
                var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarBreakCannon,
                    new S2C_SectWarBreakCannon
                    {
                        Graber = cannon.Graber.Data,
                        Breaker = player.Data
                    }));
                sect.Broadcast(bytes);
                sect.Enemy.Broadcast(bytes);

                cannon.Reset();
            }
        }

        public async Task OnCannonWin(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.Battle) return;
            player.SyncState(SectWarRoleState.Idle, true);
            _players.TryGetValue(player.BattleRoleId, out var lostPlayer);
            if (lostPlayer != null)
            {
                lostPlayer.BattleRoleId = 0;
                lostPlayer.SyncState(SectWarRoleState.Idle, true);
                // 失败者退回到基地
                lostPlayer.SyncPlace(SectWarPlace.JiDi, true);
                lostPlayer.SetLostTime();
            }

            player.BattleRoleId = 0;

            _sects.TryGetValue(player.SectId, out var winSect);
            if (winSect != null)
            {
                // 失败的一方直接扣50点城门体力
                AddHp(winSect.Enemy, -30);
            }
        }

        private async Task OnCannonFire(object roleIdValue)
        {
            if (!_isActive) return;
            var roleId = (uint) roleIdValue;
            _players.TryGetValue(roleId, out var player);
            if (player == null) return;
            player.SyncState(SectWarRoleState.Idle, true);

            _sects.TryGetValue(player.SectId, out var sect);
            if (sect == null) return;
            var cannon = sect.Cannon;
            cannon.Graber = null;
            cannon.Timer?.Dispose();
            cannon.Timer = null;

            var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarFireCannon,
                new S2C_SectWarFireCannon
                {
                    Graber = player.Data
                }));
            sect.Broadcast(bytes);

            if (sect.Enemy != null)
            {
                sect.Enemy.Broadcast(bytes);
                // 神龙炮
                AddHp(sect.Enemy, -30);
            }

            await Task.CompletedTask;
        }

        public async Task LockDoor(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.Idle) return;
            if (player.Place != SectWarPlace.ZhanChang)
            {
                player.SendNotice("请先进入战场", false);
                return;
            }

            // 必须要3个人的队伍
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (MinTeamMemberCount > 0 && (player.Members == null || player.Members.Count < MinTeamMemberCount))
            {
                player.SendNotice("需要至少3人的队伍", false);
                return;
            }

            player.SyncState(SectWarRoleState.LockDoor, true);
            player.LockDoorTimer?.Dispose();
            player.LockDoorTimer = RegisterTimer(OnDoorFire, player.Id, TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(5));

            // 没有对手的帮派
            _sects.TryGetValue(player.SectId, out var sect);
            if (sect?.Enemy == null || sect.IsDead) return;

            var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarLockDoor,
                new S2C_SectWarLockDoor
                {
                    Locker = player.Data
                }));
            sect.Broadcast(bytes);
            sect.Enemy.Broadcast(bytes);
        }

        public async Task CancelDoor(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.LockDoor || player.LockDoorTimer == null) return;
            if (player.Place != SectWarPlace.ZhanChang)
            {
                player.SendNotice("请先进入战场", false);
                return;
            }

            player.SyncState(SectWarRoleState.Idle, true);
            player.LockDoorTimer?.Dispose();
            player.LockDoorTimer = null;

            // 没有对手的帮派
            _sects.TryGetValue(player.SectId, out var sect);
            if (sect?.Enemy == null || sect.IsDead) return;

            var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarCancelDoor,
                new S2C_SectWarCancelDoor
                {
                    Locker = player.Data
                }));
            sect.Broadcast(bytes);
            sect.Enemy.Broadcast(bytes);
        }

        public async Task BreakDoor(uint roleId, uint targetRoleId)
        {
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.Idle) return;
            if (player.Place != SectWarPlace.ZhanChang)
            {
                player.SendNotice("请先进入战场", false);
                return;
            }

            // 必须要3个人的队伍
            // ReSharper disable once RedundantLogicalConditionalExpressionOperand
            if (MinTeamMemberCount > 0 && (player.Members == null || player.Members.Count < MinTeamMemberCount))
            {
                player.SendNotice("需要至少3人的队伍", false);
                return;
            }

            _players.TryGetValue(targetRoleId, out var targetPlayer);
            if (targetPlayer == null || targetPlayer.State != SectWarRoleState.LockDoor) return;

            // 关闭状态
            player.LockDoorTimer?.Dispose();
            player.LockDoorTimer = null;
            targetPlayer.LockDoorTimer?.Dispose();
            targetPlayer.LockDoorTimer = null;

            // 推送打断城门消息
            _sects.TryGetValue(player.SectId, out var sect);
            if (sect != null)
            {
                var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarBreakDoor,
                    new S2C_SectWarBreakDoor
                    {
                        Locker = targetPlayer.Data,
                        Breaker = player.Data
                    }));

                sect.Broadcast(bytes);
                sect.Enemy?.Broadcast(bytes);
            }

            _ = player.StartPvp(targetRoleId, BattleType.SectWarDoor);
            player.SyncState(SectWarRoleState.Battle, true);
            player.BattleRoleId = targetRoleId;
            targetPlayer.SyncState(SectWarRoleState.Battle, true);
            targetPlayer.BattleRoleId = roleId;

            await Task.CompletedTask;
        }

        public async Task OnDoorWin(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.Battle) return;
            player.SyncState(SectWarRoleState.Idle, true);
            _players.TryGetValue(player.BattleRoleId, out var lostPlayer);
            if (lostPlayer != null)
            {
                lostPlayer.BattleRoleId = 0;
                lostPlayer.SyncState(SectWarRoleState.Idle, true);
                lostPlayer.SyncPlace(SectWarPlace.JiDi, true);
                lostPlayer.SetLostTime();
            }

            player.BattleRoleId = 0;

            _sects.TryGetValue(player.SectId, out var winSect);
            if (winSect != null)
            {
                // 失败的一方直接扣10点城门体力
                AddHp(winSect.Enemy, -5);
            }
        }

        public async Task FreePk(uint roleId, uint targetRoleId)
        {
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.Idle) return;
            if (player.Place != SectWarPlace.ZhanChang)
            {
                player.SendNotice("请先进入战场", false);
                return;
            }

            _players.TryGetValue(targetRoleId, out var targetPlayer);
            if (targetPlayer == null || targetPlayer.State != SectWarRoleState.Idle) return;
            if (targetPlayer.Place != SectWarPlace.ZhanChang) return;

            // 推送自由PK消息
            _sects.TryGetValue(player.SectId, out var sect);
            if (sect != null)
            {
                var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarFreePk,
                    new S2C_SectWarFreePk
                    {
                        Role1 = player.Data,
                        Role2 = targetPlayer.Data
                    }));

                sect.Broadcast(bytes);
                sect.Enemy?.Broadcast(bytes);
            }

            // 启动PVP
            _ = player.StartPvp(targetRoleId, BattleType.SectWarFreePk);
            player.SyncState(SectWarRoleState.Battle, true);
            player.BattleRoleId = targetRoleId;
            targetPlayer.SyncState(SectWarRoleState.Battle, true);
            targetPlayer.BattleRoleId = roleId;

            await Task.CompletedTask;
        }

        public async Task OnFreePkWin(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var player);
            if (player == null || player.State != SectWarRoleState.Battle) return;
            player.SyncState(SectWarRoleState.Idle, true);
            _players.TryGetValue(player.BattleRoleId, out var lostPlayer);
            if (lostPlayer != null)
            {
                lostPlayer.BattleRoleId = 0;
                lostPlayer.SyncState(SectWarRoleState.Idle, true);
                lostPlayer.SyncPlace(SectWarPlace.JiDi, true);
                lostPlayer.SetLostTime();
            }

            player.BattleRoleId = 0;

            _sects.TryGetValue(player.SectId, out var winSect);
            if (winSect != null)
            {
                // 失败的一方直接扣2点城门体力
                AddHp(winSect.Enemy, -2);
            }
        }

        public async Task<Immutable<byte[]>> QuerySectInfo(uint roleId, uint sectId)
        {
            await Task.CompletedTask;
            if (!_isActive) return new Immutable<byte[]>(null);
            _sects.TryGetValue(sectId, out var sect);
            if (sect == null) return new Immutable<byte[]>(null);
            var idx = sect.Members.FindIndex(p => p.Id == roleId);
            if (idx < 0) return new Immutable<byte[]>(null);
            var resp = new S2C_SectWarInfo
            {
                My = new SectWarInfoitem
                {
                    Id = sect.SectId,
                    Name = sect.SectName,
                    OwnerId = sect.Data.OwnerId,
                    Contrib = sect.Data.Contrib,
                    CreateTime = sect.Data.CreateTime,
                    MemberNum = (uint) sect.Members.Count
                }
            };
            if (sect.Enemy != null)
            {
                resp.Enemy = new SectWarInfoitem
                {
                    Id = sect.Enemy.SectId,
                    Name = sect.Enemy.SectName,
                    OwnerId = sect.Enemy.Data.OwnerId,
                    Contrib = sect.Enemy.Data.Contrib,
                    CreateTime = sect.Enemy.Data.CreateTime,
                    MemberNum = (uint) sect.Enemy.Members.Count
                };
            }

            return new Immutable<byte[]>(Packet.Serialize(resp));
        }

        public Task OnWarArenaEnter(uint battleId, uint roleId1, uint roleId2)
        {
            if (!_isActive) return Task.CompletedTask;
            _players.TryGetValue(roleId1, out var p1);
            _players.TryGetValue(roleId2, out var p2);
            if (p1 != null && p2 != null)
            {
                _sects.TryGetValue(p1.SectId, out var s1);
                _sects.TryGetValue(p2.SectId, out var s2);
                if (s1 == null || s2 == null) return Task.CompletedTask;

                // 构造消息
                var msg1 = new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Msg = Json.SafeSerialize(new
                    {
                        type = "SectWarArena",
                        msg = $"<color=#00A8FF>[{p1.Name}]{p1.Id}</color>带领<color=#00A8FF>[{s1.SectName}]</color>帮挑战<color=#00A8FF>[{p2.Name}]{p2.Id}</color>带领的[{s2.SectName}]帮<color=#00bf00>[点击观战]</color>"
                    }),
                    BattleInfo = new InBattleInfo() { BattleId = battleId, CampId = 1, IsSectWar = true }
                };
                var bytes1 = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg1 }));
                // 构造消息
                var msg2 = new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Msg = Json.SafeSerialize(new
                    {
                        type = "SectWarArena",
                        msg = $"<color=#00A8FF>[{p2.Name}]{p2.Id}</color>带领<color=#00A8FF>[{s2.SectName}]</color>帮迎战<color=#00A8FF>[{p1.Name}]{p1.Id}</color>带领的[{s1.SectName}]帮<color=#00bf00>[点击观战]</color>"
                    }),
                    BattleInfo = new InBattleInfo() { BattleId = battleId, CampId = 2, IsSectWar = true }
                };
                var bytes2 = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg2 }));
                foreach (var p in _players.Values)
                {
                    if (p == null || p.Id == p1.Id || p.Id == p2.Id) continue;
                    if (p.SectId == p1.SectId)
                    {
                        p.SendMessage(bytes1, false);
                    }
                    else if (p.SectId == p2.SectId)
                    {
                        p.SendMessage(bytes2, false);
                    }
                    else
                    {
                        p.SendMessage(bytes1, false);
                        p.SendMessage(bytes2, false);
                    }
                }
            }
            return Task.CompletedTask;
        }

        private async Task OnDoorFire(object roleIdValue)
        {
            if (!_isActive) return;
            var roleId = (uint) roleIdValue;
            _players.TryGetValue(roleId, out var player);
            if (player == null) return;
            _sects.TryGetValue(player.SectId, out var sect);
            if (sect?.Enemy == null || sect.Enemy.IsDead)
            {
                player.LockDoorTimer?.Dispose();
                player.LockDoorTimer = null;
                player.SyncState(SectWarRoleState.Idle, true);
                return;
            }

            // var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarFireDoor,
            //     new S2C_SectWarFireDoor
            //     {
            //         Camp = sect.Enemy.Camp
            //     }));
            // sect.Broadcast(bytes);
            // sect.Enemy.Broadcast(bytes);

            // 扣血
            AddHp(sect.Enemy, -5);
            await Task.CompletedTask;
        }

        /// <summary>
        /// 每周2、4、6, 日的19:30到20:00报名
        /// </summary>
        private void CheckOpenTime(bool firstTime)
        {
            if (!_isActive) return;
            if (_options.FastSectWar)
            {
                _enterDuration = TimeSpan.FromMinutes(1);
                var delayTsx = firstTime ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(30);
                _tempTick = RegisterTimer(OnActivityEnter, null, delayTsx, TimeSpan.FromSeconds(1));
                return;
            }

            var now = DateTimeOffset.Now;
            var lastNow = DateTimeOffset.FromUnixTimeSeconds(_entity.LastTime).AddHours(8);
            var isLastSameDay = lastNow.Year == now.Year && lastNow.DayOfYear == now.DayOfYear;
            var is246 = now.DayOfWeek is DayOfWeek.Tuesday or DayOfWeek.Thursday or DayOfWeek.Saturday;
            // 今天晚上7点30
            var nextOpenTime = new DateTimeOffset(now.Year, now.Month, now.Day, 19, 30, 0, TimeSpan.FromHours(8));

            if (isLastSameDay || is246 && now.Hour >= 20)
            {
                if (now.DayOfWeek == DayOfWeek.Saturday)
                {
                    // 星期六等3天
                    nextOpenTime = nextOpenTime.AddDays(3);
                }
                else
                {
                    // 星期二、星期四 等2天
                    nextOpenTime = nextOpenTime.AddDays(2);
                }
            }
            else if (!is246)
            {
                if (now.DayOfWeek == DayOfWeek.Sunday)
                {
                    // 星期天等2天
                    nextOpenTime = nextOpenTime.AddDays(2);
                }
                else
                {
                    // 星期一、星期三、星期五等1天
                    nextOpenTime = nextOpenTime.AddDays(1);
                }
            }

            var delayTs = nextOpenTime.Subtract(now);
            if (delayTs.TotalSeconds <= 0) delayTs = TimeSpan.FromSeconds(1);
            LogInfo($"{delayTs.Days}日{delayTs.Hours}时{delayTs.Minutes}分{delayTs.Seconds}秒后开启");

            // 防止Timer等待太久而休眠, 超过1个小时的等待就用1个小时后再次来检查时间
            if (delayTs.TotalHours >= 1)
            {
                RegisterTimer(_ =>
                {
                    CheckOpenTime(false);
                    return Task.CompletedTask;
                }, null, TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(-1));
            }
            else
            {
                _tempTick = RegisterTimer(OnActivityEnter, null, delayTs, TimeSpan.FromSeconds(1));
            }
        }

        // 开始报名, 30分钟后结束报名，进行匹配
        private async Task OnActivityEnter(object _)
        {
            if (!_isActive) return;
            _tempTick?.Dispose();
            _tempTick = null;

            if (_state == SectWarState.Enter) return;
            _state = SectWarState.Enter;
            _sects.Clear();
            _players.Clear();

            var now = DateTimeOffset.Now;
            _openTime = (uint) now.ToUnixTimeSeconds();
            _fightTime = (uint) now.Add(_enterDuration).ToUnixTimeSeconds();

            // 开启新的一轮
            _entity.Turn++;
            _entity.LastTime = TimeUtil.TimeStamp;
            await DbService.Sql.Update<SectWarEntity>()
                .Where(it => it.Id == _entity.Id)
                .Set(it => it.Turn, _entity.Turn)
                .Set(it => it.LastTime, _entity.LastTime)
                .ExecuteAffrowsAsync();

            // 获取所有帮派，分配好相关阵营
            var respBytes = await _serverGrain.QuerySectsForSectWar();
            if (respBytes.Value == null)
            {
                LogDebug("没有任何帮派可以参与帮战, 结束帮战！");
                await OnActivityFinish(null);
                return;
            }

            var resp = QuerySectsForSectWarResponse.Parser.ParseFrom(respBytes.Value);
            if (resp.List.Count == 0)
            {
                LogDebug("没有任何帮派可以参与帮战, 结束帮战！");
                await OnActivityFinish(null);
                return;
            }

            // 分配阵营
            Allot(resp.List.ToList());

            // 全服通知
            BroadcastServer(GameCmd.S2CSectWarState, new S2C_SectWarState {State = _state});

            // 1分钟后开始
            _tempTick?.Dispose();
            _tempTick = RegisterTimer(OnActivityFight, null, _enterDuration, TimeSpan.FromSeconds(1));
            LogDebug("开始入场");
        }

        // 报名结束, 正式开始
        private async Task OnActivityFight(object state)
        {
            if (!_isActive) return;
            _tempTick?.Dispose();
            _tempTick = null;
            if (_state == SectWarState.Fight) return;
            _state = SectWarState.Fight;

            // 广播状态及其敌方阵营信息
            foreach (var sect in _sects.Values)
            {
                LogDebug($"帮派[{sect.SectId}][{sect.SectName}]入场人数[{sect.EnterMemeberNum}]");
                if (!sect.HasPlayer) continue;
                sect.Broadcast(GameCmd.S2CSectWarState, new S2C_SectWarState
                {
                    State = _state,
                    My = sect.Data,
                    Enemy = sect.Enemy?.Data,
                    IsEnter = true
                });
            }

            // 轮空检查, 包括对方队伍没有任何人入场的情况
            foreach (var sid in _sects.Keys.ToList())
            {
                _sects.TryGetValue(sid, out var sect);
                if (sect == null) continue;

                if (!sect.HasPlayer)
                {
                    if (sect.Enemy is {HasPlayer: true})
                    {
                        // 算sect2轮空赢
                        SendReward(sect.Enemy, true);
                        LogDebug($"帮派[{sect.Enemy.SectId}][{sect.Enemy.SectName}]轮空赢");
                    }

                    // 移除Sect
                    var enemySect = sect.Enemy;
                    RemoveSect(sect);
                    RemoveSect(enemySect);
                }
                else if (sect.Enemy == null || !sect.HasPlayer)
                {
                    // 算sect1轮空赢
                    SendReward(sect, true);
                    LogDebug($"帮派[{sect.SectId}][{sect.SectName}]轮空赢");

                    // 移除Sect
                    var enemySect = sect.Enemy;
                    RemoveSect(sect);
                    RemoveSect(enemySect);
                }
            }

            if (_sects.Count == 0 || _players.Count == 0)
            {
                // 结束
                LogDebug("全部轮空");
                await OnActivityFinish(null);
            }

            // 防止出故障，最多让持续2个小时，就强行关闭 
            var delayTs = _options.FastSectWar ? TimeSpan.FromMinutes(10) : TimeSpan.FromHours(2);
            _tempTick = RegisterTimer(OnActivityFinish, null, delayTs, TimeSpan.FromSeconds(1));
            LogDebug("正式开始");
        }

        private void CheckFinish()
        {
            if (!_isActive) return;
            if (_sects.Count == 0 && _state != SectWarState.Close)
            {
                _ = OnActivityFinish(null);
            }
        }

        private async Task OnActivityFinish(object state)
        {
            if (!_isActive) return;
            _tempTick?.Dispose();
            _tempTick = null;
            if (_state == SectWarState.Close) return;
            _state = SectWarState.Close;

            // 全服通知
            BroadcastServer(GameCmd.S2CSectWarState, new S2C_SectWarState {State = _state});

            // 清算结果, 比城门剩余体力
            foreach (var sid in _sects.Keys.ToList())
            {
                _sects.TryGetValue(sid, out var sect);
                if (sect == null) continue;
                if (sect.HasPlayer)
                {
                    if (sect.Enemy is not {HasPlayer: true})
                    {
                        SendReward(sect, true);
                        LogDebug($"帮派[{sect.SectId}][{sect.SectName}]轮空赢");
                    }
                    else if (sect.DoorHp > sect.Enemy.DoorHp)
                    {
                        SendReward(sect, true);
                        SendReward(sect.Enemy, false);
                        LogDebug($"帮派[{sect.SectId}][{sect.SectName}]赢 帮派[{sect.Enemy.SectId}][{sect.Enemy.SectName}]");
                    }
                    else if (sect.DoorHp < sect.Enemy.DoorHp)
                    {
                        SendReward(sect, false);
                        SendReward(sect.Enemy, true);
                        LogDebug($"帮派[{sect.Enemy.SectId}][{sect.Enemy.SectName}]赢 帮派[{sect.SectId}][{sect.SectName}]");
                    }
                    else if (sect.DoorHp == sect.Enemy.DoorHp)
                    {
                        // 两方都算赢
                        SendReward(sect, true);
                        SendReward(sect.Enemy, true);
                        LogDebug($"帮派[{sect.SectId}][{sect.SectName}]平 帮派[{sect.Enemy.SectId}][{sect.Enemy.SectName}]");
                    }
                }
                else if (sect.Enemy is {HasPlayer: true})
                {
                    SendReward(sect.Enemy, true);
                    LogDebug($"帮派[{sect.Enemy.SectId}][{sect.Enemy.SectName}]赢 帮派[{sect.SectId}][{sect.SectName}]");
                }

                // 防止重复计算
                var enemySect = sect.Enemy;
                RemoveSect(sect); // 这里会把sect.Enemy赋值为null
                RemoveSect(enemySect);
            }

            _sects.Clear();

            // 清空players
            foreach (var player in _players.Values)
            {
                player.Dispose();
            }

            _players.Clear();

            // 等待下次开启
            CheckOpenTime(false);
            await Task.CompletedTask;
        }

        private void Allot(IReadOnlyCollection<SectData> list)
        {
            if (!_isActive) return;
            foreach (var sd in list)
            {
                if (sd == null) continue;
                _sects.Add(sd.Id, new SectWarSect(sd, GrainFactory.GetGrain<ISectGrain>(sd.Id)));
            }

            // 随机匹配
            // var tempList = _sects.Values.ToList().OrderByDescending(p => p.Total).ToList();
            var tempList = new List<SectWarSect>(_sects.Values);
            var rnd = new Random();
            for (var i = 0; i < tempList.Count; i++)
            {
                var idx = rnd.Next(0, tempList.Count);
                if (idx != i)
                {
                    var tmp = tempList[i];
                    tempList[i] = tempList[idx];
                    tempList[idx] = tmp;
                }
            }

            var pairNum = (int) MathF.Ceiling(tempList.Count * 0.5f);
            for (var i = 0; i < pairNum; i++)
            {
                var s1 = tempList[i * 2];
                SectWarSect s2 = null;
                if (i * 2 + 1 < tempList.Count)
                {
                    s2 = tempList[i * 2 + 1];
                }

                // 构造公用的炮台和比武场
                var arena = new SectWarArena();
                var cannon = new SectWarCannon();

                s1.SectWarId = (uint) i + 1;
                s1.Camp = 1;
                s1.Enemy = s2;
                s1.Arena = arena;
                s1.Cannon = cannon;

                if (s2 != null)
                {
                    s2.SectWarId = s1.SectWarId;
                    s2.Camp = 2;
                    s2.Enemy = s1;
                    s2.Arena = arena;
                    s2.Cannon = cannon;
                }

                if (s2 == null)
                    LogDebug($"帮派[{s1.SectId}][{s1.SectName}]轮空");
                else
                    LogDebug($"帮派[{s1.SectId}][{s1.SectName}] VS 帮派[{s2.SectId}][{s2.SectName}]");
            }
        }

        private void SendReward(SectWarSect sect, bool win)
        {
            if (!_isActive) return;
            if (sect is not {HasPlayer: true}) return;
            sect.OnSectWarResult(win);
            LogDebug($"帮派[{sect.SectId}]胜败[{win}]");
        }

        private void RemoveSect(SectWarSect sect)
        {
            if (!_isActive) return;
            if (sect == null) return;
            _sects.Remove(sect.SectId);
            LogDebug($"移除帮派[{sect.SectId}]");
            foreach (var player in sect.Members)
            {
                _players.Remove(player.Id);
                player.Dispose();
            }

            // 解除敌对关系
            if (sect.Enemy != null) sect.Enemy.Enemy = null;
            sect.Enemy = null;

            sect.Dispose();

            CheckFinish();
        }

        private void AddHp(SectWarSect sect, int value)
        {
            if (!_isActive) return;
            if (sect == null) return;
            // 城门体力扣除
            var delta = sect.AddHp(value);
            if (delta != 0)
            {
                // 广播城门体力
                var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSectWarDoorHp,
                    new S2C_SectWarDoorHp
                    {
                        Camp = sect.Camp,
                        DoorHp = sect.DoorHp,
                        Add = delta
                    }));
                sect.Broadcast(bytes);
                sect.Enemy?.Broadcast(bytes);
            }

            // 1方失败了
            if (sect.DoorHp <= 0)
            {
                var enemySect = sect.Enemy;
                SendReward(sect, false);
                RemoveSect(sect);

                if (enemySect != null)
                {
                    SendReward(enemySect, true);
                    RemoveSect(enemySect);
                }
            }
        }

        private void BroadcastServer(GameCmd cmd, IMessage msg)
        {
            if (!_isActive) return;
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(cmd, msg)));
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"帮战[{_serverId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"帮战[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"帮战[{_serverId}]:{msg}");
        }
    }
}