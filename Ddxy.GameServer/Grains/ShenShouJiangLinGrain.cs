using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
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
    /// 神兽降临, 每天17:40开始报名, 18:00结束报名, 18:00正式打开, 19:00强制结束
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class ShenShouJiangLinGrain : Grain, IShenShouJiangLinGrain
    {
        private ILogger<ShenShouJiangLinGrain> _logger;
        private AppOptions _options;
        private bool _isActive;
        private uint _serverId;
        private IServerGrain _serverGrain;
        private SsjlEntity _entity;
        private SsjlState _state;
        private IDisposable _tempTicker;
        private IDisposable _checkBattleTicker;
        private IDisposable _forceStopTicker;
        private uint _shenShouId;
        private uint _winnerId;
        private uint _openTime;
        private uint _startTime;
        private uint _endTime;
        // 报名参赛的队伍
        private Dictionary<uint, SsjlTeamData> _teams;
        private Dictionary<uint, bool> _teamsInBattle;
        // 报名时长
        private TimeSpan _signDuration = TimeSpan.FromMinutes(20);
        // 抓捕时长
        private TimeSpan _catchDuration = TimeSpan.FromMinutes(60);
        private const string Name = "神兽降临";
        public ShenShouJiangLinGrain(ILogger<ShenShouJiangLinGrain> logger, IOptions<AppOptions> options)
        {
            _logger = logger;
            _options = options.Value;
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
        public async Task StartUp()
        {
            if (_isActive) return;
            _isActive = true;
            _serverGrain = GrainFactory.GetGrain<IServerGrain>(_serverId);
            _teams = new();
            _teamsInBattle = new();
            _state = SsjlState.Close;
            _entity = await DbService.QuerySsjl(_serverId);
            if (_entity == null)
            {
                _entity = new SsjlEntity
                {
                    ServerId = _serverId,
                    Season = 0,
                    LastTime = 0,
                    Reward = ""
                };
                await DbService.InsertEntity(_entity);
            }
            LogDebug("激活成功");
            CheckOpenTime();
            await Task.CompletedTask;
        }
        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _isActive = false;
            _checkBattleTicker?.Dispose();
            _checkBattleTicker = null;
            _tempTicker?.Dispose();
            _tempTicker = null;
            _forceStopTicker?.Dispose();
            _forceStopTicker = null;

            _serverGrain = null;
            _entity = null;
            _teams.Clear();
            _teams = null;
            _teamsInBattle.Clear();
            _teamsInBattle = null;

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
                if (_state == SsjlState.Close)
                {
                    // 1s后开启
                    _tempTicker?.Dispose();
                    _tempTicker = RegisterTimer(OnActivityOpen, null, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(1));
                    LogInfo($"后台用户[{opUid}]开启");
                }
            }
            else
            {
                if (_state != SsjlState.Close)
                {
                    // 1s后关闭
                    _tempTicker?.Dispose();
                    _tempTicker = RegisterTimer(OnActivityClose, null, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(1));
                    LogInfo($"后台用户[{opUid}]关闭");
                }
            }
            return null;
        }
        public async Task Online(uint roleId, uint teamId, uint season)
        {
            if (!_isActive) return;
            var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            if (season != _entity.Season)
            {
                _ = grain.OnShenShouJiangLinNewSeason(season);
            }
            if (_state == SsjlState.Sign)
            {
                var reward = ConfigService.CatchedMonstersForShenShouJiangLin.GetValueOrDefault(_shenShouId, null);
                _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSsjlState,
                    new S2C_SsjlState
                    {
                        State = _state,
                        Signed = _teams.ContainsKey(teamId),
                        Reward = reward != null ? reward.Pet : 0,
                        StartTime = _startTime,
                        EndTime = _endTime,
                    })));
            }
            await Task.CompletedTask;
        }
        public Task<Immutable<byte[]>> GetActivityInfo()
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            var resp = new SsjlActivityInfo
            {
                Season = _entity.Season,
                State = _state,
                OpenTime = _openTime,
                StartTime = _startTime,
                EndTime = _endTime
            };
            if (resp.State == SsjlState.Close)
            {
                resp.OpenTime = 0;
                resp.StartTime = 0;
                resp.EndTime = 0;
            }
            var bytes = new Immutable<byte[]>(Packet.Serialize(resp));
            return Task.FromResult(bytes);
        }
        public ValueTask<uint> GetSeason()
        {
            if (!_isActive) return new ValueTask<uint>(0);
            return new(_entity.Season);
        }
        public ValueTask<byte> GetState()
        {
            return new((byte)_state);
        }
        public Task<ShenShouJiangLinSignResult> Sign(Immutable<byte[]> reqBytes)
        {
            var result = new ShenShouJiangLinSignResult();
            if (!_isActive)
            {
                result.Error = "尚未激活";
                return Task.FromResult(result);
            }
            if (_state == SsjlState.Close)
            {
                result.Error = "神兽降临未开启";
                return Task.FromResult(result);
            }
            if (_state != SsjlState.Sign)
            {
                result.Error = "报名已结束";
                return Task.FromResult(result);
            }
            var req = SsjlTeamData.Parser.ParseFrom(reqBytes.Value);
            if (_teams.ContainsKey(req.TeamId))
            {
                result.Error = "已经报名了";
                return Task.FromResult(result);
            }
            _teams[req.TeamId] = req;
            _teamsInBattle[req.TeamId] = false;
            LogDebug($"队长[{req.LeaderId}]带领着队伍[{req.TeamId}][{req.Name}]报名");
            result.Error = string.Empty;
            result.State = (byte)_state;
            result.Reward = _shenShouId;
            result.StartTime = _startTime;
            result.EndTime = _endTime;
            return Task.FromResult(result);
        }
        public ValueTask<bool> UnSign(uint teamId)
        {
            if (!_isActive) return new ValueTask<bool>(false);
            if (_teams.Remove(teamId, out var team))
            {
                _teamsInBattle.Remove(teamId);
                LogDebug($"队长[{team.LeaderId}]带领着队伍[{teamId}][{team.Name}]离场");
                return new ValueTask<bool>(true);
            }
            return new ValueTask<bool>(false);
        }
        public Task<ShenShouJiangLinSignResult> CheckTeamActive(uint teamId)
        {
            var result = new ShenShouJiangLinSignResult();
             if (!_isActive)
            {
                result.Error = "尚未激活";
                return Task.FromResult(result);
            }
            if (_state == SsjlState.Close)
            {
                result.Error = "活动已关闭";
            }
            else if (!_teams.ContainsKey(teamId))
            {
                result.Error = "没报名";
            }
            else
            {
                result.Error = string.Empty;
                result.State = (byte)_state;
                result.Reward = _shenShouId;
                result.StartTime = _startTime;
                result.EndTime = _endTime;
            }
            return Task.FromResult(result);
        }
        private void CheckOpenTime()
        {
            _isActive = false;
            if (!_isActive) return;
            _tempTicker?.Dispose();
            _tempTicker = null;
            // 每天下午17：40活动开始报名
            var now = DateTime.Now;
            var next = new DateTime(now.Year, now.Month, now.Day, 17, 40, 0);
            if (now.Hour >= 18 || now.Hour == 17 && now.Minute >= 40)
            {
                next = next.AddDays(1);
            }
            var delayTs = next.Subtract(now);
            // 测试用等待时长，战斗结束后等待时长
            // var delayTs = TimeSpan.FromMinutes(1);
            if (delayTs.TotalSeconds <= 0) delayTs = TimeSpan.FromSeconds(1);
            LogInfo($"{delayTs.Days}日{delayTs.Hours}时{delayTs.Minutes}分{delayTs.Seconds}秒后开始报名");
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
                _tempTicker = RegisterTimer(OnActivityOpen, null, delayTs, TimeSpan.FromSeconds(1));
            }
        }
        private async Task OnActivityOpen(object state)
        {
            _isActive = false;
            if (!_isActive) return;
            _tempTicker?.Dispose();
            _tempTicker = null;

            if (_state == SsjlState.Sign) return;
            _state = SsjlState.Sign;
            _winnerId = 0;

            // 尝试再次释放锁
            await RedisService.UnLockShenShouJiangLinReward(_serverId);

            var now = DateTimeOffset.Now;
            var lastNow = DateTimeOffset.FromUnixTimeSeconds(_entity.LastTime).AddHours(24);

            var petList = ConfigService.CatchedMonstersIdForShenShouJiangLin;
            _shenShouId = petList[new Random().Next(petList.Count)];
            var ret = await RedisService.SetShenShouJiangLinReward(_serverId, (int)_shenShouId);
            if (ret)
            {
                LogInfo($"设置奖励[{_shenShouId}]成功");
            }
            else
            {
                var delayTs = TimeSpan.FromSeconds(30);
                LogError($"设置奖励[{_shenShouId}]失败，{delayTs.TotalSeconds}秒后再试");
                _tempTicker = RegisterTimer(_ =>
                  {
                      CheckOpenTime();
                      return Task.CompletedTask;
                  }, null, delayTs, TimeSpan.FromSeconds(1));
                return;
            }
            _openTime = (uint)now.ToUnixTimeSeconds();
            _startTime = (uint)now.Add(_signDuration).ToUnixTimeSeconds();
            _endTime = (uint)now.Add(_signDuration).Add(_catchDuration).ToUnixTimeSeconds();

            bool nextSeason = _entity.Season == 0 || !TimeUtil.IsSameDay(lastNow, now);
            if (nextSeason)
            {
                _entity.Season++;
                _entity.LastTime = TimeUtil.TimeStamp;
                await DbService.Sql.Update<SsjlEntity>()
                    .Where(it => it.Id == _entity.Id)
                    .Set(it => it.Season, _entity.Season)
                    .Set(it => it.LastTime, _entity.LastTime)
                    .ExecuteAffrowsAsync();
                NewSeason();
            }
            else
            {
                _entity.LastTime = TimeUtil.TimeStamp;
                await DbService.Sql.Update<SsjlEntity>()
                    .Where(it => it.Id == _entity.Id)
                    .Set(it => it.LastTime, _entity.LastTime)
                    .ExecuteAffrowsAsync();
            }

            // 广播系统消息
            BroadcastState();

            // 20分钟后开始抓捕
            _tempTicker?.Dispose();
            _tempTicker = RegisterTimer(OnActivityFight, null, _signDuration, TimeSpan.FromSeconds(1));

            // 1个小时后强行停止抓捕
            _forceStopTicker?.Dispose();
            _forceStopTicker = RegisterTimer(OnActivityClose, null, _signDuration.Add(_catchDuration), TimeSpan.FromSeconds(1));

            LogDebug("开始报名");
        }
        private async Task OnActivityFight(object _)
        {
            if (!_isActive) return;
            _tempTicker?.Dispose();
            _tempTicker = null;

            if (_state == SsjlState.Fight) return;
            _state = SsjlState.Fight;
            _winnerId = 0;
            // 广播系统消息
            BroadcastState();
            BroadcastStart2Teams();

            // 每1s更新
            _checkBattleTicker?.Dispose();
            _checkBattleTicker = RegisterTimer(CheckBattle, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));

            LogDebug("开始抓捕");
            await Task.CompletedTask;
        }
        private async Task OnActivityClose(object _)
        {
            if (!_isActive) return;
            _checkBattleTicker?.Dispose();
            _checkBattleTicker = null;
            _tempTicker?.Dispose();
            _tempTicker = null;
            _forceStopTicker?.Dispose();
            _forceStopTicker = null;
            if (_state == SsjlState.Close) return;
            _state = SsjlState.Close;
            _shenShouId = 0;
            _openTime = 0;
            _startTime = 0;
            _endTime = 0;

            // 广播系统消息
            BroadcastState();
            BroadcastStop2Teams();

            _teams.Clear();
            _teamsInBattle.Clear();
            LogDebug("结束抓捕");

            // 等待下次开启
            CheckOpenTime();

            await Task.CompletedTask;
        }
        private async Task CheckBattle(object _)
        {
            if (!_isActive) return;
            // 全部离场
            if (_teams.Count <= 0)
            {
                LogError("所有队伍离场，活动结束");
                _checkBattleTicker?.Dispose();
                _checkBattleTicker = null;
                await OnActivityClose(null);
                return;
            }
            if (_state != SsjlState.Fight)
            {
                _checkBattleTicker?.Dispose();
                _checkBattleTicker = null;
                await OnActivityClose(null);
                return;
            }
            // 检查是否已经有队伍获得了神兽
            if (_winnerId == 0)
            {
                var winnerId = await RedisService.GetShenShouJiangLinReward(_serverId);
                if (winnerId < 0)
                {
                    _winnerId = (uint)Math.Abs(winnerId);
                    _entity.Reward = Json.SafeSerialize(new Dictionary<string, uint>()
                    {
                        ["winner"] = _winnerId,
                        ["reward"] = _shenShouId
                    });

                    await DbService.Sql.Update<SsjlEntity>()
                   .Where(it => it.Id == _entity.Id)
                   .Set(it => it.Reward, _entity.Reward)
                   .ExecuteAffrowsAsync();

                   LogDebug($"神兽已被玩家[{_winnerId}]抓取");
                }
            }
            // 已经有队伍获得神兽
            if (_winnerId != 0)
            {
                if (!_teamsInBattle.ContainsValue(true))
                {
                    LogDebug($"神兽已被玩家[{_winnerId}]抓取，活动结束");
                    _checkBattleTicker?.Dispose();
                    _checkBattleTicker = null;
                    await OnActivityClose(null);
                }
            }
        }

        public Task OnBattleStart(uint teamId)
        {
            if (!_isActive) return Task.CompletedTask;
            if (_teams.ContainsKey(teamId))
            {
                _teamsInBattle[teamId] = true;
            }
            return Task.CompletedTask;
        }

        public Task OnBattleEnd(uint teamId)
        {
            if (!_isActive) return Task.CompletedTask;
            if (_teams.ContainsKey(teamId))
            {
                _teamsInBattle[teamId] = false;
                if (_winnerId > 0)
                {
                    GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{teamId}").StopShenShouJiangLin();
                }
            }
            return Task.CompletedTask;
        }

        private void NewSeason()
        {
            if (!_isActive) return;
            _ = _serverGrain.OnShuiLuDaHuiNewSeason(_entity.Season);
        }

        private void BroadcastState()
        {
            if (!_isActive) return;
            var reward = ConfigService.CatchedMonstersForShenShouJiangLin.GetValueOrDefault(_shenShouId, null);
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSsjlState,
             new S2C_SsjlState
             {
                 State = _state,
                 Reward = reward != null ? reward.Pet : 0,
                 Signed = false,
                 StartTime = _startTime,
                 EndTime = _endTime,
             }
            )));
        }

        private void BroadcastToTeams(GameCmd cmd, IMessage msg)
        {
            if (!_isActive) return;
            var bytes = new Immutable<byte[]>(Packet.Serialize(cmd, msg));
            foreach (var team in _teams)
            {
                GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{team.Key}").Broadcast(bytes);
            }
        }

        private void BroadcastStart2Teams()
        {
            if (!_isActive) return;
            var left = _endTime - _startTime;
            foreach (var team in _teams)
            {
                GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{team.Key}").StartShenShouJiangLin(left, _shenShouId, _serverId);
            }
        }

        private void BroadcastStop2Teams()
        {
            if (!_isActive) return;
            foreach (var team in _teams)
            {
                GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{team.Key}").StopShenShouJiangLin();
            }
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"神兽降临[{_serverId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"神兽降临[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"神兽降临[{_serverId}]:{msg}");
        }
    }
}