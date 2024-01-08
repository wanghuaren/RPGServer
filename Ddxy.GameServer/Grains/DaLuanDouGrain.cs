using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Option;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    /// <summary>
    /// 大乱斗, 每天下午17点进场开始，可以点匹配 设定一下17：10之后不可进场，防止刷钱   PK到18:30结束。
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class DaLuanDouGrain : Grain, IDaLuanDouGrain
    {
        private ILogger<DaLuanDouGrain> _logger;
        private AppOptions _options;

        private uint _serverId;
        private IServerGrain _serverGrain;

        private bool _isActive;
        private DaLuanDouEntity _entity;
        private List<uint> _listPkzs; //PK战神
        private Random _random;

        private DaLuanDouState _state;
        private IDisposable _tempTicker;
        private IDisposable _minuteTicker;
        private IDisposable _forceStopTicker;

        private uint _openTime;
        private uint _allotTime;
        private uint _fightTime;

        private Dictionary<uint, DldTeamData> _teams; //报名参赛的队伍
        private Dictionary<uint, TempTeamData> _matchTeams; //参赛的队伍

        private Dictionary<uint, bool> _outPlayers; //被踢出的玩家

        private uint _fightInterval = 60; //每个队伍战斗完成后休息1分钟继续匹配下一个战斗(s)

        // private uint _fightWait = 300; //每个队伍等待对手的最长时间(s)
        private TimeSpan _signDuration = TimeSpan.FromMinutes(10); //报名阶段的时长
        // private TimeSpan _fightAfterAllot = TimeSpan.FromMinutes(1); //分配好队伍后,等待1分钟正式比赛
        private TimeSpan _fightDuration = TimeSpan.FromHours(1.5);

        private const string Name = "大乱斗";

        private const int TeamNumPerGroup = 4; //每组4个队伍

        public DaLuanDouGrain(ILogger<DaLuanDouGrain> logger, IOptions<AppOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        public override Task OnActivateAsync()
        {
            _serverId = (uint)this.GetPrimaryKeyLong();
            _serverGrain = GrainFactory.GetGrain<IServerGrain>(_serverId);
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
            _teams = new Dictionary<uint, DldTeamData>(200);
            _matchTeams = new Dictionary<uint, TempTeamData>();
            _outPlayers = new Dictionary<uint, bool>();
            _state = DaLuanDouState.Close;

            _entity = await DbService.QueryDaLuanDou(_serverId);
            if (_entity == null)
            {
                _entity = new DaLuanDouEntity
                {
                    ServerId = _serverId,
                    Season = 0,
                    Turn = 0,
                    LastTime = 0,
                    Pkzs = ""
                };
                await DbService.InsertEntity(_entity);
            }

            if (string.IsNullOrWhiteSpace(_entity.Pkzs))
            {
                _listPkzs = new List<uint>();
            }
            else
            {
                _listPkzs = Json.Deserialize<List<uint>>(_entity.Pkzs);
            }
            _random = new Random();
            LogDebug("激活成功");

            CheckOpenTime(true);
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _minuteTicker?.Dispose();
            _minuteTicker = null;
            _tempTicker?.Dispose();
            _tempTicker = null;
            _forceStopTicker?.Dispose();
            _forceStopTicker = null;

            _serverGrain = null;
            _entity = null;
            _teams.Clear();
            _teams = null;
            _matchTeams.Clear();
            _matchTeams = null;
            _outPlayers.Clear();
            _outPlayers = null;
            _options = null;

            LogDebug("注销成功");
            _isActive = false;
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new ValueTask<bool>(_isActive);
        }

        public async Task Reload()
        {
            if (!_isActive) return;
            // 重新加载水陆战神
            _entity = await DbService.QueryDaLuanDou(_serverId);
            if (string.IsNullOrWhiteSpace(_entity.Pkzs))
            {
                _listPkzs = new List<uint>();
            }
            else
            {
                _listPkzs = Json.Deserialize<List<uint>>(_entity.Pkzs);
            }
        }

        public async Task<string> GmOpen(bool open, uint opUid)
        {
            await Task.CompletedTask;
            if (!_isActive) return "尚未激活";

            if (open)
            {
                if (_state == DaLuanDouState.Close)
                {
                    // 1s钟后开启
                    _tempTicker?.Dispose();
                    _tempTicker = RegisterTimer(OnActivityOpen, null, TimeSpan.FromSeconds(0.1),
                        TimeSpan.FromSeconds(1));
                    LogInfo($"后台用户[{opUid}]开启");
                }
            }
            else
            {
                if (_state != DaLuanDouState.Close)
                {
                    _tempTicker?.Dispose();
                    _tempTicker = RegisterTimer(OnActivityClose, null, TimeSpan.FromSeconds(0.1),
                        TimeSpan.FromSeconds(1));
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
                _ = grain.OnDaLuanDouNewSeason(season);
            }

            if (_state == DaLuanDouState.Sign)
            {
                _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CDaLuanDouState,
                    new S2C_DaLuanDouState { State = _state, Signed = _teams.ContainsKey(teamId) })));
            }

            await Task.CompletedTask;
        }

        public Task<Immutable<byte[]>> GetActivityInfo()
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            var resp = new DaLuanDouActivityInfo
            {
                Season = _entity.Season,
                Turn = _entity.Turn,
                State = _state,
                OpenTime = _openTime,
                AllotTime = _allotTime,
                FightTime = _fightTime
            };
            if (resp.State == DaLuanDouState.Close)
            {
                resp.OpenTime = 0;
                resp.AllotTime = 0;
                resp.FightTime = 0;
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

        /// <summary>
        /// 报名参赛
        /// </summary>
        public Task<string> Sign(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return Task.FromResult("大乱斗未开启");
            // 检查当前活动是否开启
            if (_state == DaLuanDouState.Close) return Task.FromResult("大乱斗未开启");
            if (_state != DaLuanDouState.Sign && _state != DaLuanDouState.Fight) return Task.FromResult("报名已结束");

            var req = DldTeamData.Parser.ParseFrom(reqBytes.Value);
            if (_teams.ContainsKey(req.TeamId)) return Task.FromResult("已经报名了");
            foreach (var mb in req.Players) {
                _outPlayers.TryGetValue(mb.Id, out var ok);
                if (ok == true) {
                    return Task.FromResult("失败次数达到上限，无法进入大乱斗");
                }
            }

            _teams.Add(req.TeamId, req);
            var ttd = new TempTeamData(req.TeamId, req.Score,
                GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{req.TeamId}"), req);
            ttd.Group = 1;
            _matchTeams.Add(req.TeamId, ttd);
            ttd.Grain.EnterDld(ttd.Group);

            LogDebug($"玩家[{req.LeaderId}]带领着队伍[{req.TeamId}]报名");
            return Task.FromResult(string.Empty);
        }

        /// <summary>
        /// 离场
        /// </summary>
        public ValueTask<bool> UnSign(uint teamId)
        {
            if (!_isActive) return new ValueTask<bool>(false);
            if (_matchTeams.Remove(teamId, out var ttd))
            {
                ttd.Grain.ExitDld();
            }
            if (_teams.Remove(teamId, out var team))
            {
                LogDebug($"玩家[{team.LeaderId}]带领着队伍[{teamId}]离场");
                return new ValueTask<bool>(true);
            }

            return new ValueTask<bool>(false);
        }

        public async Task DaLuanDouPk(uint roleId, uint myTeamId, uint targetRoleId)
        {
            if (!_isActive) return;
            var myGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            var targetGrain = GrainFactory.GetGrain<IPlayerGrain>(targetRoleId);
            if (myGrain == null || targetGrain == null) {
                LogDebug($"玩家[{roleId}]挑战[{targetRoleId}] 角色不存在");
                return;
            }

            _matchTeams.TryGetValue(myTeamId, out var ttd);
            if (ttd == null) {
                LogDebug($"玩家[{roleId}]队伍[{myTeamId}]没有报名");
                return;
            }

            TempTeamData enemy = null;
            var find = false;
            foreach (var team in _matchTeams.Values)
            {
                if (team != null) {
                    foreach (var v in team.TeamData.Players) {
                        if (v.Id == targetRoleId) {
                            enemy = team;
                            find = true;
                            break;
                        }
                    }
                }
                if (find) {
                    break;
                }
            }
            if (!find || enemy == null) {
                LogDebug($"被挑战玩家[{targetRoleId}]的队伍不存在");
                return;
            }

            if (ttd.State != FightState.Wait && ttd.State != FightState.FightEnd) {
                _ = ttd.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice(){
                    Text = "战斗中，请稍后。。。"
                })));
                return;
            }
            if (enemy.State != FightState.Wait && enemy.State != FightState.FightEnd) {
                _ = ttd.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice(){
                    Text = "对方战斗中，请稍后。。。"
                })));
                return;
            }

            // 检查对方是否能进入战斗
            var enemyLeader = GrainFactory.GetGrain<IPlayerGrain>(enemy.TeamData.LeaderId);
            var ttdLeader = GrainFactory.GetGrain<IPlayerGrain>(ttd.TeamData.LeaderId);
            var enemyIdle = await enemyLeader.PreCheckPvp();
            var ttdIdle = await enemyLeader.PreCheckPvp();
            if (!enemyIdle || !ttdIdle)
            {
                if (!enemyIdle)
                {
                    LogDebug($"队伍[{ttd.TeamId}]等待队伍[{enemy.TeamId}]，[{enemy.TeamId}]当前无法进入战斗");
                    _ = enemy.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice(){
                        Text = "数据异常，已强制退出战斗"
                    })));
                    // enemy.Done();
                }
                if (!ttdIdle)
                {
                    LogDebug($"队伍[{ttd.TeamId}]等待队伍[{enemy.TeamId}]，[{ttd.TeamId}]当前无法进入战斗");
                    _ = ttd.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice(){
                        Text = "数据异常，已强制退出战斗"
                    })));
                    // ttd.Done();
                }
                return;
            }

            ttd.WaitSeconds = 0;
            enemy.WaitSeconds = 0;
            // 标记为战斗状态
            ttd.State = FightState.Fighting;
            enemy.State = FightState.Fighting;

            _ = ttd.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CDaLuanDouMatch,
                new S2C_DaLuanDouMatch
                {
                    My = { ttd.TeamData.Players },
                    Enemy = { enemy.TeamData.Players }
                })));

            _ = enemy.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CDaLuanDouMatch,
                new S2C_DaLuanDouMatch
                {
                    My = { enemy.TeamData.Players },
                    Enemy = { ttd.TeamData.Players }
                })));

            LogDebug($"队伍[{ttd.TeamId}]VS队伍[{enemy.TeamId}]1大乱斗 开始战斗");

            var ret = await myGrain.StartPvp(enemy.TeamData.LeaderId, (byte)BattleType.DaLuanDou);
            if (ret != 0)
            {
                LogDebug($"队伍[{ttd.TeamId}]VS队伍[{enemy.TeamId}]进入战斗失败[{ret}]");
                // 让队伍重新分派
                ttd.State = FightState.Wait;
                enemy.State = FightState.Wait;

                // 检查对方是否能进入战斗
                var leader1Grain = GrainFactory.GetGrain<IPlayerGrain>(ttd.TeamData.LeaderId);
                var leader2Grain = GrainFactory.GetGrain<IPlayerGrain>(enemy.TeamData.LeaderId);
                var leader1Idle = await leader1Grain.PreCheckPvp();
                var leader2Idle = await leader2Grain.PreCheckPvp();
                if (!leader1Idle)
                {
                    LogDebug($"队伍[{ttd.TeamId}]VS队伍[{enemy.TeamId}]进入战斗失败，[{ttd.TeamId}]无法进入战斗");
                    _ = ttd.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice()
                    {
                        Text = "数据异常，已强制退出战斗"
                    })));
                    // ttd.Done();
                }
                if (!leader2Idle)
                {
                    LogDebug($"队伍[{ttd.TeamId}]VS队伍[{enemy.TeamId}]进入战斗失败，[{enemy.TeamId}]无法进入战斗");
                    _ = enemy.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice()
                    {
                        Text = "数据异常，已强制退出战斗"
                    })));
                    // enemy.Done();
                }
            }
            else
            {
                LogDebug($"队伍[{ttd.TeamId}]VS队伍[{enemy.TeamId}]开始战斗");
                ttd.Records.Add(new Record { TeamId = enemy.TeamId, Win = 0 });
                ttd.BattleNum++;
                enemy.Records.Add(new Record { TeamId = ttd.TeamId, Win = 0 });
                enemy.BattleNum++;
            }
            await Task.CompletedTask;
        }              

        public async Task UpdateTeam(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return;
            // 这里让队伍持续更新信息, 这样在匹配战斗的时候就可以直接取数据
            if (_state != DaLuanDouState.Close)
            {
                var req = DldTeamData.Parser.ParseFrom(reqBytes.Value);
                if (_teams.ContainsKey(req.TeamId))
                {
                    if (req.PlayerNum == 0)
                    {
                        _teams.Remove(req.TeamId, out var team);
                        _matchTeams.Remove(req.TeamId);
                        var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CDaLuanDouUnSign,
                            new S2C_DaLuanDouUnSign()));
                        foreach (var ri in req.Players)
                        {
                            var grain = GrainFactory.GetGrain<IPlayerGrain>(ri.Id);
                            _ = grain.SendMessage(bytes);
                        }

                        var teamGrain = GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{req.TeamId}");
                        _ = teamGrain.ExitDld();
                        LogDebug($"玩家[{team.LeaderId}]的队伍[{req.TeamId}]解散");
                    }
                    else if (_state == DaLuanDouState.Sign && req.MapId != 1206)
                    {
                        // 报名阶段不能离开皇宫和金銮殿
                        _teams.Remove(req.TeamId, out var team);
                        _matchTeams.Remove(req.TeamId);
                        var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CDaLuanDouUnSign,
                            new S2C_DaLuanDouUnSign { IsAuto = true }));
                        foreach (var ri in req.Players)
                        {
                            var grain = GrainFactory.GetGrain<IPlayerGrain>(ri.Id);
                            _ = grain.SendMessage(bytes);
                        }

                        var teamGrain = GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{req.TeamId}");
                        _ = teamGrain.ExitDld(false);

                        LogDebug($"玩家[{team.LeaderId}]带领着队伍[{req.TeamId}]离开了皇宫");
                    }
                    else
                    {
                        _teams[req.TeamId] = req;
                        // 可能还没有匹配？？
                        if (_matchTeams.ContainsKey(req.TeamId))
                        {
                            _matchTeams[req.TeamId].TeamData = req;
                        }
                    }
                }
            }

            await Task.CompletedTask;
        }

        public async Task<Immutable<byte[]>> GetInfo(uint teamId)
        {
            await Task.CompletedTask;
            if (!_isActive) return new Immutable<byte[]>(null);
            if (_state == DaLuanDouState.Close) return new Immutable<byte[]>(null);
            if (!_teams.TryGetValue(teamId, out var teamInfo) || teamInfo == null)
            {
                var TeamGrain = GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{teamId}");
                // 这里只包含了队员的数据
                var players = new RoleInfoList();
                var bits = await TeamGrain.QueryRoleInfos(true);
                if (bits.Value != null && bits.Value.Length > 0)
                {
                    var res = RoleInfoList.Parser.ParseFrom(bits.Value);
                    if (res.List.Count > 0)
                    {
                        players.List.AddRange(res.List);
                    }
                }
                var resp = new S2C_DaLuanDouInfo
                {
                    State = _state,
                    Turn = _entity.Turn,
                    IsSign = false,
                    MyTeam = { players.List }
                };
                // LogInfo($"MyTeamCount={resp.MyTeam.Count} MyTeam={resp.MyTeam}");
                return new Immutable<byte[]>(Packet.Serialize(resp));                
            } else {
                var resp = new S2C_DaLuanDouInfo
                {
                    State = _state,
                    Turn = _entity.Turn,
                    IsSign = true,
                    MyTeam = { teamInfo.Players }
                };
                LogInfo($"-------------IsSign={resp.IsSign}");
                var sorted = _teams.Values.OrderByDescending(CombineLevel2).ToList();
                if (_state == DaLuanDouState.Sign)
                {
                    foreach (var std in sorted.Take(10))
                    {
                        resp.SignTeams.Add(new DldTeamData
                        {
                            TeamId = std.TeamId,
                            Name = std.Name,
                            PlayerNum = std.PlayerNum,
                            Score = std.Score
                        });
                    }
                }
                else
                {
                    foreach (var std in sorted.Take(15))
                    {
                        var ttd = _matchTeams.GetValueOrDefault(std.TeamId, null);
                        if (ttd != null)
                        {
                            resp.FightTeams.Add(new DldFightTeamData()
                            {
                                Win = (int)ttd.Win,
                                Lose = (int)ttd.Lost,
                                FightScore = ttd.FightScore,
                                Members = { std.Players }
                            });
                        }
                    }
                }
                return new Immutable<byte[]>(Packet.Serialize(resp));
            }
        }

        /// <summary>
        /// 检查指定队伍当前是否必须守在皇宫/金銮殿内
        /// <returns>-1表示不需要, 其他情况返回当前的状态</returns>
        /// </summary>
        public async ValueTask<string> CheckTeamActive(uint teamId)
        {
            await Task.CompletedTask;
            if (!_isActive)
            {
                return Json.SafeSerialize(new
                {
                    error = "未激活",
                    state = (int)_state,
                    group = 0
                });
            }
            // 已关闭
            if (_state == DaLuanDouState.Close)
            {
                return Json.SafeSerialize(new
                {
                    error = "活动已经结束",
                    state = (int)_state,
                    group = 0
                });
            }
            else if (!_teams.ContainsKey(teamId))
            {
                return Json.SafeSerialize(new
                {
                    error = "没有报名",
                    state = (int)_state,
                    group = 0
                });
            }
            else
            {
                return Json.SafeSerialize(new
                {
                    error = "",
                    state = (int)_state,
                    group = 1
                });               
            }
        }

        public async Task OnBattleEnd(uint teamId, bool win, bool reward = true)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            var ttd = _matchTeams.GetValueOrDefault(teamId, null);
            if (ttd == null) return;
            //LogDebug($"玩家[{ttd.TeamData.Players[0].Id}][{ttd.TeamData.Players[0].Name}]的队伍 胜败[{win}] 状态[{ttd.State}] 战斗人数[{ttd.BattleNum}] 战斗记录[{ttd.Records.Count}]");
            if (ttd.State == FightState.Fighting)
            {
                ttd.State = FightState.FightEnd;
                ttd.FightEndTime = TimeUtil.TimeStamp;
                if (ttd.BattleNum == ttd.Records.Count)
                {
                    if (reward) SendTeamReward(ttd, win);
                    var record = ttd.Records[ttd.BattleNum - 1];
                    record.Win = win ? 1 : -1;
                    if (win)
                    {
                        ttd.Win++;
                        ttd.FightScore += 50;
                    }
                    else
                    {
                        ttd.Lost++;
                        ttd.FightScore -= 0;

                        //失败5次，直接离场
                        if (ttd.Lost >= 5)
                        {
                            // _teams.Remove(ttd.TeamId);
                            foreach (var mb in ttd.TeamData.Players) {
                                _outPlayers.Add(mb.Id, true);
                            }
                            ttd.Done();
                        }
                    }
                    // 查找对手信息
                    _matchTeams.TryGetValue(record.TeamId, out var enemy);
                    if (enemy is { State: FightState.Fighting })
                    {
                        if (reward) SendTeamReward(enemy, !win);
                        enemy.State = FightState.FightEnd;
                        enemy.FightEndTime = ttd.FightEndTime;
                        if (enemy.BattleNum == enemy.Records.Count)
                        {
                            var enemyRecord = enemy.Records[enemy.BattleNum - 1];
                            enemyRecord.Win = win ? -1 : 1;
                            if (win)
                            {
                                enemy.Lost++;
                                enemy.FightScore -= 0;

                                //失败5次，直接离场
                                if (enemy.Lost >= 5)
                                {
                                    // _teams.Remove(enemy.TeamId);
                                    foreach (var mb in enemy.TeamData.Players) {
                                        _outPlayers.Add(mb.Id, true);
                                    }
                                    enemy.Done();
                                }
                            }
                            else
                            {
                                enemy.Win++;
                                enemy.FightScore += 50;
                            }
                        }
                    }
                }
            }
        }

        public ValueTask<bool> CheckPkzs(uint rid)
        {
            if (!_isActive) return new ValueTask<bool>(false);
            return new(_listPkzs.Contains(rid));
        }

        /// <summary>
        /// 每周1、3、5的19:30到20:00报名
        /// </summary>
        private void CheckOpenTime(bool firstTime)
        {
            if (!_isActive) return;
            if (_options.FastDaLuanDou)
            {
                // _fightInterval = 10;
                // // _fightWait = 30;
                // _signDuration = TimeSpan.FromSeconds(30);
                // _fightAfterAllot = TimeSpan.FromSeconds(10);
                // _fightDuration = TimeSpan.FromMinutes(15);

                // var delayTsx = firstTime ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(30);
                _tempTicker = RegisterTimer(OnActivityOpen, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
                return;
            }

            var now = DateTimeOffset.Now;
            var lastNow = DateTimeOffset.FromUnixTimeSeconds(_entity.LastTime).AddHours(8);
            var isLastSameDay = lastNow.Year == now.Year && lastNow.DayOfYear == now.DayOfYear;
            // 今天晚上17点开始匹配，18:30结束
            var nextOpenTime = new DateTimeOffset(now.Year, now.Month, now.Day, 17, 0, 0, TimeSpan.FromHours(8));

            if (isLastSameDay || now.Hour > 18 || now.Hour == 18 && now.Minute >= 30)
            {
                // 等1天
                nextOpenTime = nextOpenTime.AddDays(1);
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
                _tempTicker = RegisterTimer(OnActivityOpen, null, delayTs, TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// 活动开始后的20分钟用来报名
        /// </summary>
        private async Task OnActivityOpen(object state)
        {
            if (!_isActive) return;
            _tempTicker?.Dispose();
            _tempTicker = null;

            if (_state == DaLuanDouState.Sign) return;
            _state = DaLuanDouState.Sign;

            var now = DateTimeOffset.Now;
            var lastNow = DateTimeOffset.FromUnixTimeSeconds(_entity.LastTime).AddHours(8);

            _openTime = (uint)now.ToUnixTimeSeconds();
            _allotTime = (uint)now.Add(_signDuration).ToUnixTimeSeconds();
            _fightTime = _allotTime;

            bool nextSeason;
            nextSeason = _entity.Season == 0 || !TimeUtil.IsSameDay(lastNow, now);
            // if (_options.FastDaLuanDou)
            // {
            //     nextSeason = _entity.Season == 0 || !TimeUtil.IsSameDay(lastNow, now);
            // }
            // else
            // {
            //     nextSeason = _entity.Season == 0 || !TimeUtil.IsSameWeek(lastNow, now);
            // }

            // 判断和上次开的是否是同一周
            if (nextSeason)
            {
                _entity.Season++;
                _entity.Turn = 1;
                _entity.LastTime = TimeUtil.TimeStamp;

                await DbService.Sql.Update<DaLuanDouEntity>()
                    .Where(it => it.Id == _entity.Id)
                    .Set(it => it.Season, _entity.Season)
                    .Set(it => it.Turn, _entity.Turn)
                    .Set(it => it.LastTime, _entity.LastTime)
                    .ExecuteAffrowsAsync();

                NewSeason();
            }
            else
            {
                _entity.Turn++;
                _entity.LastTime = TimeUtil.TimeStamp;
                await DbService.Sql.Update<DaLuanDouEntity>()
                    .Where(it => it.Id == _entity.Id)
                    .Set(it => it.Turn, _entity.Turn)
                    .Set(it => it.LastTime, _entity.LastTime)
                    .ExecuteAffrowsAsync();
            }

            // 广播系统消息
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CDaLuanDouState, new S2C_DaLuanDouState
            {
                State = _state,
                Signed = false
            }
            )));

            // 10分钟后 报名结束
            _tempTicker?.Dispose();
            _tempTicker = RegisterTimer(OnActivityAllotTeam, null, _signDuration, TimeSpan.FromSeconds(1));

            // 3个小时后强行停止
            _forceStopTicker?.Dispose();
            _forceStopTicker = RegisterTimer(OnActivityClose, null, _fightDuration, TimeSpan.FromSeconds(1));

            // 每分钟泡点加积分
            _minuteTicker?.Dispose();
            _minuteTicker = RegisterTimer(OnMinuteUpdate, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));

            LogDebug("活动开启");
            await Task.CompletedTask;
        }

        //每分钟泡点加积分
        private async Task OnMinuteUpdate(object args)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            foreach (var td in _matchTeams.Values)
            {
                if (td?.State == FightState.Done) {
                    continue;
                }
                foreach (var mb in td.TeamData.Players) {
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(mb.Id);
                    if (await grain.GetMapId() != 3004) {
                        continue;
                    }
                    await grain.AddMoney((byte) MoneyType.BindJade, 10, "大乱斗泡点");
                }

            }
        }

        /// <summary>
        /// 报名时间结束, 开始分配队伍
        /// </summary>
        private async Task OnActivityAllotTeam(object args)
        {
            if (!_isActive) return;
            _tempTicker?.Dispose();
            _tempTicker = null;

            if (_state == DaLuanDouState.Fight) return;
            _state = DaLuanDouState.Fight;

            // _matchTeams.Clear();
            // foreach (var team in _teams.Values)
            // {
            //     _matchTeams.Add(team.TeamId, new TempTeamData(team.TeamId, team.Score,
            //         GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{team.TeamId}"), team));
            // }

            // if (_matchTeams.Count == 0)
            // {
            //     LogDebug("没有队伍报名参赛, 直接结束活动");
            //     await OnActivityClose(null);
            //     return;
            // }
            // 广播系统消息
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CDaLuanDouState,
                new S2C_DaLuanDouState
                {
                    State = _state,
                    Signed = false
                }
            )));
            await Task.CompletedTask;

            // // 分区分组
            // var sorted = _matchTeams.Values.OrderByDescending(CombineLevel).ToList();
            // for (uint i = 0; i < sorted.Count; i++)
            // {
            //     sorted[(int)i].Group = (uint)Math.Floor(i / 10f);
            // }

            // // 已报名的队伍跳入3004地图
            // foreach (var ttd in sorted)
            // {
            //     _ = ttd.Grain.EnterDld(ttd.Group);
            // }

            // // 1分钟后正式比赛
            // _tempTicker?.Dispose();
            // _tempTicker = RegisterTimer(OnActivityFight, null, _fightAfterAllot, TimeSpan.FromSeconds(1));
        }

        private async Task OnActivityClose(object o)
        {
            if (!_isActive) return;
            _minuteTicker?.Dispose();
            _minuteTicker = null;
            _tempTicker?.Dispose();
            _tempTicker = null;
            _forceStopTicker?.Dispose();
            _forceStopTicker = null;
            if (_state == DaLuanDouState.Close) return;
            _state = DaLuanDouState.Close;

            // 广播系统消息
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CDaLuanDouState, new S2C_DaLuanDouState
            {
                State = _state,
            }
            )));

            // 通知所有的队伍大乱斗大会已结束
            foreach (var td in _matchTeams.Values)
            {
                td?.Done();
            }

            try
            {
                //第一名的队伍发公告 发称号
                var sortedList = _matchTeams.Values.OrderByDescending(CombineFightScore).ToList();
                if (sortedList.Count > 0) {
                    var ttd  = sortedList[0];
                    StringBuilder MyStringBuilder = new StringBuilder();
                    foreach (var mb in ttd.TeamData.Players) {

                        var grain = GrainFactory.GetGrain<IPlayerGrain>(mb.Id);
                        if (grain != null) {
                            _ = grain.GmAddTitle1(112, "", 7*24*60*60);
                            // _ = grain.GmAddTitle1(112, "大乱斗第一名", 7*24*60*60);
                        }

                        MyStringBuilder.Append(" ");
                        MyStringBuilder.Append(mb.Name);
                        MyStringBuilder.Append(" ");
                    }
                    string nameList = MyStringBuilder.ToString();
                    var text =
                        $"大乱斗结束，今日的最强队伍是<color=#00ff00>{nameList}</c><color=#ffffff>真是太幸运了</c><color=#ffffff>让我们祝贺他们!</c>";
                    for (var i=0; i<5; i++) {
                        var bytes = Packet.Serialize(GameCmd.S2CScreenNotice, new S2C_ScreenNotice
                        {
                            Text = text,
                            Front = 0
                        });
                        _ = _serverGrain.Broadcast(new Immutable<byte[]>(bytes));
                    }
                }

                // 如果是星期6就统计水陆战神
                var now = DateTimeOffset.Now;
                // if (now.DayOfWeek == DayOfWeek.Saturday)
                if (true)
                {
                    //// 清掉上赛季的水陆战神的称号
                    // foreach (var rid in _listPkzs)
                    // {
                    //     if (rid == 0) continue;
                    //     var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                    //     _ = grain.GmAddTitle(86, false);
                    // }

                    _listPkzs.Clear();

                    // // 统计本赛季的水陆战神, 发放称号
                    // var rankList = await RedisService.GetRoleDaLuanDouRank(_serverId, 1, 15);
                    // if (rankList != null)
                    // {
                    //     // 根据积分进行排名
                    //     var rank = 0;
                    //     var lastScore = 0U;
                    //     foreach (var mb in rankList)
                    //     {
                    //         if (mb == null || mb.Id == 0 || mb.Score == 0) continue;
                    //         if (mb.Score != lastScore)
                    //         {
                    //             lastScore = mb.Score;
                    //             rank++;
                    //             if (rank >= 4) break;
                    //         }

                    //         if (rank == 0) rank = 1;

                    //         // _listPkzs.Add(mb.Id);
                    //         // var grain = GrainFactory.GetGrain<IPlayerGrain>(mb.Id);
                    //         // // _ = grain.GmAddTitle(86, true, true);

                    //         // if (rank == 1)
                    //         // {
                    //         //     _ = grain.GmAddTitle1(112, "大乱斗第一名", 7*24*60*60);
                    //         //     // // 符咒女娲、紫云盖天翅膀
                    //         //     // _ = grain.AddBagItem(90076, 1, tag: "大乱斗奖励");
                    //         //     // _ = grain.GmAddWing(5017);
                    //         // }
                    //         // else if (rank == 2)
                    //         // {
                    //         //     // 紫云盖天翅膀
                    //         //     _ = grain.GmAddWing(5017);
                    //         // }
                    //         // else if (rank == 3)
                    //         // {
                    //         //     // 飞鸿幻翅
                    //         //     _ = grain.GmAddWing(5016);
                    //         // }
                    //     }
                    // }
                }
            }
            catch (Exception ex)
            {
                LogError($"统计大乱斗战神出错[{ex.StackTrace}]");
            }

            _teams.Clear();
            _matchTeams.Clear();
            _outPlayers.Clear();

            _entity.Pkzs = Json.Serialize(_listPkzs);
            LogDebug("活动已关闭");
            // 等待下次开启
            CheckOpenTime(false);

            await Task.CompletedTask;
        }

        private static int CombineLevel(TempTeamData ttd)
        {
            return (int) (ttd.Score);
        }
        private static int CombineLevel2(DldTeamData ttd)
        {
            return (int) (ttd.Score);
        }
        private static int CombineFightScore(TempTeamData ttd)
        {
            return (int) (ttd.FightScore);
        }


        private void SendTeamReward(TempTeamData team, bool win)
        {
            if (!_isActive) return;
            // LogDebug($"玩家[{team.TeamId}][{team.TeamData.Players[0].Name}]的队伍 胜败[{win}]");
            _ = team.Grain.OnDaLuanDouBattleResult(_entity.Season, win);
        }

        private void NewSeason()
        {
            if (!_isActive) return;
            _ = _serverGrain.OnDaLuanDouNewSeason(_entity.Season);
        }

        private void BroadcastState()
        {
            if (!_isActive) return;
            Broadcast(GameCmd.S2CDaLuanDouState, new S2C_DaLuanDouState { State = _state });
        }

        private void Broadcast(GameCmd cmd, IMessage msg)
        {
            if (!_isActive) return;
            var bytes = new Immutable<byte[]>(Packet.Serialize(cmd, msg));
            foreach (var team in _matchTeams.Values)
            {
                if (team != null) _ = team.Grain.Broadcast(bytes);
            }
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"大乱斗[{_serverId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"大乱斗[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"大乱斗[{_serverId}]:{msg}");
        }

        private class TempTeamData
        {
            public readonly uint TeamId; //队伍id
            public DldTeamData TeamData { get; set; } //队伍数据
            public readonly uint Score; //队伍总大乱斗积分
            public uint Win; //胜利次数
            public uint Lost; //失败次数
            public int FightScore { get; set; }  //本次比赛的总积分
            public FightState State; //当前战斗状态

            public uint FightEndTime; //上次战斗结束的时间
            // public uint FightWaitTime; //本场战斗等待的开始时间
            public uint WaitSeconds { get; set; } //匹配等待时间

            public int BattleNum { get; set; } //已参战次数

            public readonly List<Record> Records; //地方队伍和胜负情况, Item1为对方teamId，Item2为胜负
            public readonly ITeamGrain Grain;

            // 所属分组
            public uint Group;

            public TempTeamData(uint teamId, uint score, ITeamGrain grain, DldTeamData team)
            {
                TeamId = teamId;
                Score = score;
                Win = 0;
                Lost = 0;
                State = FightState.Wait;
                FightEndTime = 0;
                // FightWaitTime = 0;
                FightScore = 0;
                WaitSeconds = 0;
                Grain = grain;
                TeamData = team;

                Records = new List<Record>();
            }

            public void Done()
            {
                if (State == FightState.Done) return;
                State = FightState.Done;
                _ = Grain.ExitDld();

                // 通知这个队伍，该大乱斗已结束
                _ = Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CDaLuanDouState, new S2C_DaLuanDouState
                {
                    State = DaLuanDouState.Close,
                    Signed = true
                }
                )));
            }
        }

        private class Record
        {
            public uint TeamId;
            public int Win; // 0未开战, 1赢，-1输
        }

        private enum FightState
        {
            Wait = 1, //等待开始
            Fighting = 2, //战斗中
            FightEnd = 3, //战斗结束
            Done = 4 //所有战斗已经完成
        }
    }
}