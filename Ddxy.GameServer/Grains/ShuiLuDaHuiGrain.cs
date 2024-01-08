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
    /// 水陆大会, 每周1/3/5 19:30开始报名, 20:00开始分配队伍, 20：01正式打开, 21:00强制结束。
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class ShuiLuDaHuiGrain : Grain, IShuiLuDaHuiGrain
    {
        private ILogger<ShuiLuDaHuiGrain> _logger;
        private AppOptions _options;

        private uint _serverId;
        private IServerGrain _serverGrain;

        private bool _isActive;
        private SldhEntity _entity;
        private List<uint> _listSlZs; //水陆战神
        private Random _random;

        private SldhState _state;
        private IDisposable _tempTicker;
        private IDisposable _buildBattleTicker;
        private IDisposable _forceStopTicker;

        private uint _openTime;
        private uint _allotTime;
        private uint _fightTime;

        private Dictionary<uint, SldhTeamData> _teams; //报名参赛的队伍
        private Dictionary<uint, TempTeamData> _matchTeams; //参赛的队伍

        private uint _fightInterval = 60; //每个队伍战斗完成后休息1分钟继续匹配下一个战斗(s)

        // private uint _fightWait = 300; //每个队伍等待对手的最长时间(s)
        private TimeSpan _signDuration = TimeSpan.FromMinutes(30); //报名阶段的时长
        private TimeSpan _fightAfterAllot = TimeSpan.FromMinutes(1); //分配好队伍后,等待1分钟正式比赛
        private TimeSpan _fightDuration = TimeSpan.FromHours(1.5);

        private const string Name = "水陆大会";

        private const int TeamNumPerGroup = 4; //每组4个队伍

        public ShuiLuDaHuiGrain(ILogger<ShuiLuDaHuiGrain> logger, IOptions<AppOptions> options)
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
            _teams = new Dictionary<uint, SldhTeamData>(200);
            _matchTeams = new Dictionary<uint, TempTeamData>();
            _state = SldhState.Close;

            _entity = await DbService.QuerySldh(_serverId);
            if (_entity == null)
            {
                _entity = new SldhEntity
                {
                    ServerId = _serverId,
                    Season = 0,
                    Turn = 0,
                    LastTime = 0,
                    Slzs = ""
                };
                await DbService.InsertEntity(_entity);
            }

            if (string.IsNullOrWhiteSpace(_entity.Slzs))
            {
                _listSlZs = new List<uint>();
            }
            else
            {
                _listSlZs = Json.Deserialize<List<uint>>(_entity.Slzs);
            }
            _random = new Random();
            LogDebug("激活成功");

            CheckOpenTime(true);
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _buildBattleTicker?.Dispose();
            _buildBattleTicker = null;
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
            _entity = await DbService.QuerySldh(_serverId);
            if (string.IsNullOrWhiteSpace(_entity.Slzs))
            {
                _listSlZs = new List<uint>();
            }
            else
            {
                _listSlZs = Json.Deserialize<List<uint>>(_entity.Slzs);
            }
        }

        public async Task<string> GmOpen(bool open, uint opUid)
        {
            await Task.CompletedTask;
            if (!_isActive) return "尚未激活";

            if (open)
            {
                if (_state == SldhState.Close)
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
                if (_state != SldhState.Close)
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
                _ = grain.OnShuiLuDaHuiNewSeason(season);
            }

            if (_state == SldhState.Sign)
            {
                _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhState,
                    new S2C_SldhState { State = _state, Signed = _teams.ContainsKey(teamId) })));
            }

            await Task.CompletedTask;
        }

        public Task<Immutable<byte[]>> GetActivityInfo()
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            var resp = new SldhActivityInfo
            {
                Season = _entity.Season,
                Turn = _entity.Turn,
                State = _state,
                OpenTime = _openTime,
                AllotTime = _allotTime,
                FightTime = _fightTime
            };
            if (resp.State == SldhState.Close)
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
            if (!_isActive) return Task.FromResult("水陆大会未开启");
            // 检查当前活动是否开启
            if (_state == SldhState.Close) return Task.FromResult("水陆大会未开启");
            if (_state != SldhState.Sign) return Task.FromResult("报名已结束");

            var req = SldhTeamData.Parser.ParseFrom(reqBytes.Value);
            if (_teams.ContainsKey(req.TeamId)) return Task.FromResult("已经报名了");
            _teams.Add(req.TeamId, req);
            LogDebug($"玩家[{req.LeaderId}]带领着队伍[{req.TeamId}]报名");
            return Task.FromResult(string.Empty);
        }

        /// <summary>
        /// 离场
        /// </summary>
        public ValueTask<bool> UnSign(uint teamId)
        {
            if (!_isActive) return new ValueTask<bool>(false);
            _matchTeams.Remove(teamId);
            if (_teams.Remove(teamId, out var team))
            {
                LogDebug($"玩家[{team.LeaderId}]带领着队伍[{teamId}]离场");
                return new ValueTask<bool>(true);
            }

            return new ValueTask<bool>(false);
        }

        public async Task UpdateTeam(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return;
            // 这里让队伍持续更新信息, 这样在匹配战斗的时候就可以直接取数据
            if (_state != SldhState.Close)
            {
                var req = SldhTeamData.Parser.ParseFrom(reqBytes.Value);
                if (_teams.ContainsKey(req.TeamId))
                {
                    if (req.PlayerNum == 0)
                    {
                        _teams.Remove(req.TeamId, out var team);
                        _matchTeams.Remove(req.TeamId);
                        var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhUnSign,
                            new S2C_SldhUnSign()));
                        foreach (var ri in req.Players)
                        {
                            var grain = GrainFactory.GetGrain<IPlayerGrain>(ri.Id);
                            _ = grain.SendMessage(bytes);
                        }

                        var teamGrain = GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{req.TeamId}");
                        _ = teamGrain.ExitSldh();
                        LogDebug($"玩家[{team.LeaderId}]的队伍[{req.TeamId}]解散");
                    }
                    else if (_state == SldhState.Sign && req.MapId != 1206)
                    {
                        // 报名阶段不能离开皇宫和金銮殿
                        _teams.Remove(req.TeamId, out var team);
                        _matchTeams.Remove(req.TeamId);
                        var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhUnSign,
                            new S2C_SldhUnSign { IsAuto = true }));
                        foreach (var ri in req.Players)
                        {
                            var grain = GrainFactory.GetGrain<IPlayerGrain>(ri.Id);
                            _ = grain.SendMessage(bytes);
                        }

                        var teamGrain = GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{req.TeamId}");
                        _ = teamGrain.ExitSldh(false);

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
            if (_state == SldhState.Close) return new Immutable<byte[]>(null);
            if (!_teams.TryGetValue(teamId, out var teamInfo) || teamInfo == null)
                return new Immutable<byte[]>(null);

            var resp = new S2C_SldhInfo
            {
                State = _state,
                Turn = _entity.Turn,
                IsSign = true,
                MyTeam = { teamInfo.Players }
            };
            var sorted = _teams.Values.OrderByDescending(CombineLevel2).ToList();
            if (_state == SldhState.Sign)
            {
                foreach (var std in sorted.Take(10))
                {
                    resp.SignTeams.Add(new SldhTeamData
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
                        resp.FightTeams.Add(new SldhFightTeamData()
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
            if (_state == SldhState.Close)
            {
                return Json.SafeSerialize(new
                {
                    error = "活动已经结束",
                    state = (int)_state,
                    group = 0
                });
            }
            // 报名中
            else if (_state == SldhState.Sign)
            {
                _teams.TryGetValue(teamId, out var teamInfo);
                if (teamInfo == null)
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
                        group = 0
                    });
                }
            }
            // 分配中
            else if (_state == SldhState.Allot)
            {
                _teams.TryGetValue(teamId, out var teamInfo);
                if (teamInfo == null)
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
                    _matchTeams.TryGetValue(teamId, out var matchInfo);
                    if (matchInfo == null)
                    {
                        return Json.SafeSerialize(new
                        {
                            error = "",
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
                            group = matchInfo.Group
                        });
                    }
                }
            }
            // 战斗进行中
            else if (_state == SldhState.Fight)
            {
                _matchTeams.TryGetValue(teamId, out var matchInfo);
                // 不应该有这种情况？
                if (matchInfo == null)
                {
                    return Json.SafeSerialize(new
                    {
                        error = "系统错误",
                        state = (int)_state,
                        group = 0
                    });
                }
                else
                {
                    if (matchInfo.State == FightState.Done)
                    {
                        return Json.SafeSerialize(new
                        {
                            error = "已被淘汰",
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
                            group = matchInfo.Group
                        });
                    }
                }
            }
            // 结算阶段
            else
            {
                return Json.SafeSerialize(new
                {
                    error = "结算阶段",
                    state = (int)_state,
                    group = 0
                });
            }
        }

        public async Task OnBattleEnd(uint teamId, bool win, bool reward = true)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            var ttd = _matchTeams.GetValueOrDefault(teamId, null);
            if (ttd == null) return;
            // LogDebug($"玩家[{ttd.TeamData.Players[0].Id}][{ttd.TeamData.Players[0].Name}]的队伍 胜败[{win}] 状态[{ttd.State}] 战斗人数[{ttd.BattleNum}] 战斗记录[{ttd.Records.Count}]");
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
                        ttd.FightScore += 10;
                    }
                    else
                    {
                        ttd.Lost++;
                        ttd.FightScore -= 10;

                        if (ttd.FightScore <= 0 || ttd.Records.FindAll(p => p.Win == -1).Count() >= 5)
                        {
                            _teams.Remove(ttd.TeamId);
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
                                enemy.FightScore -= 10;

                                if (enemy.FightScore <= 0 || enemy.Records.FindAll(p => p.Win == -1).Count() >= 5)
                                {
                                    _teams.Remove(enemy.TeamId);
                                    enemy.Done();
                                }
                            }
                            else
                            {
                                enemy.Win++;
                                enemy.FightScore += 10;
                            }
                        }
                    }
                }
            }
        }

        public ValueTask<bool> CheckSlzs(uint rid)
        {
            if (!_isActive) return new ValueTask<bool>(false);
            return new(_listSlZs.Contains(rid));
        }

        /// <summary>
        /// 每周1、3、5的19:30到20:00报名
        /// </summary>
        private void CheckOpenTime(bool firstTime)
        {
            if (!_isActive) return;
            if (_options.FastSldh)
            {
                _fightInterval = 10;
                // _fightWait = 30;
                _signDuration = TimeSpan.FromSeconds(30);
                _fightAfterAllot = TimeSpan.FromSeconds(10);
                _fightDuration = TimeSpan.FromMinutes(15);

                var delayTsx = firstTime ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(30);
                _tempTicker = RegisterTimer(OnActivityOpen, null, delayTsx, TimeSpan.FromSeconds(1));
                return;
            }

            var now = DateTimeOffset.Now;
            var lastNow = DateTimeOffset.FromUnixTimeSeconds(_entity.LastTime).AddHours(8);
            var isLastSameDay = lastNow.Year == now.Year && lastNow.DayOfYear == now.DayOfYear;
            var is135 = now.DayOfWeek is DayOfWeek.Monday or DayOfWeek.Wednesday or DayOfWeek.Friday;
            // 今天晚上7点30
            var nextOpenTime = new DateTimeOffset(now.Year, now.Month, now.Day, 19, 30, 0, TimeSpan.FromHours(8));

            if (isLastSameDay || is135 && now.Hour >= 20)
            {
                if (now.DayOfWeek == DayOfWeek.Friday)
                {
                    // 星期五要等3天
                    nextOpenTime = nextOpenTime.AddDays(3);
                }
                else
                {
                    // 星期一、星期三等2天
                    nextOpenTime = nextOpenTime.AddDays(2);
                }
            }
            else if (!is135)
            {
                if (now.DayOfWeek == DayOfWeek.Saturday)
                {
                    // 星期六要等到星期一，要等2天
                    nextOpenTime = nextOpenTime.AddDays(2);
                }
                else
                {
                    // 星期二、星期四、星期天等1天
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

            if (_state == SldhState.Sign) return;
            _state = SldhState.Sign;

            var now = DateTimeOffset.Now;
            var lastNow = DateTimeOffset.FromUnixTimeSeconds(_entity.LastTime).AddHours(8);

            _openTime = (uint)now.ToUnixTimeSeconds();
            _allotTime = (uint)now.Add(_signDuration).ToUnixTimeSeconds();
            _fightTime = (uint)now.Add(_signDuration).Add(_fightAfterAllot).ToUnixTimeSeconds();

            bool nextSeason;
            if (_options.FastSldh)
            {
                nextSeason = _entity.Season == 0 || !TimeUtil.IsSameDay(lastNow, now);
            }
            else
            {
                nextSeason = _entity.Season == 0 || !TimeUtil.IsSameWeek(lastNow, now);
            }

            // 判断和上次开的是否是同一周
            if (nextSeason)
            {
                _entity.Season++;
                _entity.Turn = 1;
                _entity.LastTime = TimeUtil.TimeStamp;

                await DbService.Sql.Update<SldhEntity>()
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
                await DbService.Sql.Update<SldhEntity>()
                    .Where(it => it.Id == _entity.Id)
                    .Set(it => it.Turn, _entity.Turn)
                    .Set(it => it.LastTime, _entity.LastTime)
                    .ExecuteAffrowsAsync();
            }

            // 广播系统消息
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhState, new S2C_SldhState
            {
                State = _state,
                Signed = false
            }
            )));

            // 30分钟后开始分配队伍
            _tempTicker?.Dispose();
            _tempTicker = RegisterTimer(OnActivityAllotTeam, null, _signDuration, TimeSpan.FromSeconds(1));

            // 3个小时后强行停止
            _forceStopTicker?.Dispose();
            _forceStopTicker = RegisterTimer(OnActivityClose, null, _fightDuration, TimeSpan.FromSeconds(1));

            LogDebug("活动开启");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 报名时间结束, 开始分配队伍
        /// </summary>
        private async Task OnActivityAllotTeam(object args)
        {
            if (!_isActive) return;
            _tempTicker?.Dispose();
            _tempTicker = null;

            if (_state == SldhState.Allot) return;
            _state = SldhState.Allot;

            _matchTeams.Clear();
            foreach (var team in _teams.Values)
            {
                _matchTeams.Add(team.TeamId, new TempTeamData(team.TeamId, team.Score,
                    GrainFactory.GetGrain<ITeamGrain>($"{_serverId}_{team.TeamId}"), team));
            }

            if (_matchTeams.Count == 0)
            {
                LogDebug("没有队伍报名参赛, 直接结束活动");
                await OnActivityClose(null);
                return;
            }
            // 广播系统消息
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhState,
                new S2C_SldhState
                {
                    State = _state,
                    Signed = false
                }
            )));

            // 分区分组
            var sorted = _matchTeams.Values.OrderByDescending(CombineLevel).ToList();
            for (uint i = 0; i < sorted.Count; i++)
            {
                sorted[(int)i].Group = (uint)Math.Floor(i / 10f);
            }

            // 已报名的队伍跳入3001地图
            foreach (var ttd in sorted)
            {
                _ = ttd.Grain.EnterSldh(ttd.Group);
            }

            // 1分钟后正式比赛
            _tempTicker?.Dispose();
            _tempTicker = RegisterTimer(OnActivityFight, null, _fightAfterAllot, TimeSpan.FromSeconds(1));
        }

        private async Task OnActivityFight(object _)
        {
            if (!_isActive) return;
            _tempTicker?.Dispose();
            _tempTicker = null;

            if (_state == SldhState.Fight) return;
            _state = SldhState.Fight;
            BroadcastState();

            // 每1s更新
            _buildBattleTicker?.Dispose();
            _buildBattleTicker = RegisterTimer(BuildBattleWrap, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

            LogDebug("开始比赛");
            await Task.CompletedTask;
        }

        private async Task OnActivityClose(object o)
        {
            if (!_isActive) return;
            _buildBattleTicker?.Dispose();
            _buildBattleTicker = null;
            _tempTicker?.Dispose();
            _tempTicker = null;
            _forceStopTicker?.Dispose();
            _forceStopTicker = null;
            if (_state == SldhState.Close) return;
            _state = SldhState.Close;

            // 广播系统消息
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhState, new S2C_SldhState
            {
                State = _state,
            }
            )));

            // 通知所有的队伍水路大会已结束
            foreach (var td in _matchTeams.Values)
            {
                td?.Done();
            }

            _teams.Clear();
            _matchTeams.Clear();

            try
            {
                // 如果是星期6就统计水陆战神
                var now = DateTimeOffset.Now;
                if (now.DayOfWeek == DayOfWeek.Saturday)
                {
                    // 清掉上赛季的水陆战神的称号
                    foreach (var rid in _listSlZs)
                    {
                        if (rid == 0) continue;
                        var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                        _ = grain.GmAddTitle(86, false);
                    }

                    _listSlZs.Clear();

                    // 统计本赛季的水陆战神, 发放称号
                    var rankList = await RedisService.GetRoleSldhRank(_serverId, 1, 15);
                    if (rankList != null)
                    {
                        // 根据积分进行排名
                        var rank = 0;
                        var lastScore = 0U;
                        foreach (var mb in rankList)
                        {
                            if (mb == null || mb.Id == 0 || mb.Score == 0) continue;
                            if (mb.Score != lastScore)
                            {
                                lastScore = mb.Score;
                                rank++;
                                if (rank >= 4) break;
                            }

                            if (rank == 0) rank = 1;

                            _listSlZs.Add(mb.Id);
                            var grain = GrainFactory.GetGrain<IPlayerGrain>(mb.Id);
                            _ = grain.GmAddTitle(86, true, true);

                            if (rank == 1)
                            {
                                // 符咒女娲、紫云盖天翅膀
                                _ = grain.AddBagItem(90076, 1, tag: "水陆大会奖励");
                                _ = grain.GmAddWing(5017);
                            }
                            else if (rank == 2)
                            {
                                // 紫云盖天翅膀
                                _ = grain.GmAddWing(5017);
                            }
                            else if (rank == 3)
                            {
                                // 飞鸿幻翅
                                _ = grain.GmAddWing(5016);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"统计水路战神出错[{ex.StackTrace}]");
            }

            _entity.Slzs = Json.Serialize(_listSlZs);
            LogDebug("活动已关闭");
            // 等待下次开启
            CheckOpenTime(false);

            await Task.CompletedTask;
        }

        private async Task BuildBattleWrap(object _)
        {
            if (!_isActive) return;
            if (_state != SldhState.Fight)
            {
                _buildBattleTicker?.Dispose();
                _buildBattleTicker = null;
                await OnActivityClose(null);
                return;
            }

            try
            {
                await BuildBattle();
            }
            catch (Exception ex)
            {
                LogError($"构建战斗出错[{ex.StackTrace}]");
            }
        }

        private static int CombineLevel(TempTeamData ttd)
        {
            return (int) (ttd.Score);
        }
        private static int CombineLevel2(SldhTeamData ttd)
        {
            return (int) (ttd.Score);
        }

        // 检查战斗队伍是否都进入了战斗
        private async Task BuildBattle()
        {
            if (!_isActive) return;
            // 先检查一遍，所有的已经结束战斗的队伍是否完成冷却
            var now = TimeUtil.TimeStamp;
            // 已经被淘汰的--积分为0的
            var toRemove = new List<uint>();
            var wait2Match = new List<TempTeamData>();
            var teamIdList = new List<uint>();
            var hasFighting = false;
            foreach (var ttd in _matchTeams.Values)
            {
                // 已经被淘汰的
                if (ttd is {State: FightState.Done })
                {
                    toRemove.Add(ttd.TeamId);
                    continue;
                }
                // 检查队伍是否还存在
                if (!_teams.TryGetValue(ttd.TeamId, out var teamInfo) || teamInfo == null)
                {
                    toRemove.Add(ttd.TeamId);
                    continue;
                }
                teamIdList.Add(ttd.TeamId);
                // 刚才战斗结束的
                if (ttd is { State: FightState.FightEnd } && now - ttd.FightEndTime > _fightInterval)
                {
                    // 等待下一场战斗
                    ttd.FightEndTime = 0;
                    // ttd.FightWaitTime = now;
                    ttd.State = FightState.Wait;
                    wait2Match.Add(ttd);
                }
                // 已经在等待的
                if (ttd.State == FightState.Wait)
                {
                    wait2Match.Add(ttd);
                }
                // 还有在战斗中的？
                if (ttd.State == FightState.Fighting)
                {
                    hasFighting = true;
                }
            }
            // 人数已经不足以再次匹配，则直接结算
            if ((_matchTeams.Count - toRemove.Count())<= 1)
            {
                LogDebug("全部战斗结束，可以结算了！");
                await OnActivityClose(null);
                return;
            }
            // 等待的人只有一个
            if (wait2Match.Count == 1)
            {
                var ttd = wait2Match[0];
                ttd.WaitSeconds += 1;
                if (ttd.WaitSeconds >= 10)
                {
                    ttd.WaitSeconds = 0;
                    _ = ttd.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(
                        GameCmd.S2CNotice,
                        new S2C_Notice()
                        {
                            Text = "匹配中，请耐心等待对手..."
                        })));
                    // LogDebug($"队伍{ttd.TeamId} 轮空等待10秒");
                }
                // LogDebug($"队伍{ttd.TeamId} 轮空等待，只有一人");
                return;
            }
            // 已经全部对局过了？
            if (!hasFighting)
            {
                var allDone = true;
                foreach (var trd in _matchTeams.Values)
                {
                    foreach (var id in teamIdList)
                    {
                        if (id == trd.TeamId) continue;
                        if (trd.Records.Exists(p => p.TeamId == id)) continue;
                        allDone = false;
                        break;
                    }
                    if (allDone) continue;
                }
                if (allDone)
                {
                    LogDebug("全部战斗结束，可以结算了！");
                    await OnActivityClose(null);
                    return;
                }
            }

            // 按水路积分进行排序
            var sortedList = wait2Match.OrderByDescending(CombineLevel).ToList();
            var tempList = new List<TempTeamData>();
            for (int i = 0, len = sortedList.Count; i < len; i++)
            {
                var ttd = sortedList[i];
                // 下面的逻辑只是用来处理处于Wait状态的
                if (ttd.State != FightState.Wait) continue;

                // 自己当前队伍的前后5个队伍
                var sidx = Math.Max(0, i - 5); // 开始索引
                var eidx = Math.Min(i + 5, sortedList.Count - 1); // 结束索引

                // 在sidx和eidx中筛选出当前同样wait的单位
                tempList.Clear();
                for (var xi = sidx; xi <= eidx; xi++)
                {
                    var xttd = sortedList[xi];
                    // 过滤没有在等待状态的
                    if (xttd.State != FightState.Wait) continue;
                    //过滤掉自己
                    if (ttd.TeamId == xttd.TeamId) continue;
                    // 已经打过的对手也不再重复
                    if (ttd.Records.Exists(p => p.TeamId == xttd.TeamId)) continue;
                    tempList.Add(xttd);
                }
                if (tempList.Count == 0)
                {
                    // 这种情况
                    ttd.WaitSeconds += 1;
                    if (ttd.WaitSeconds >= 10)
                    {
                        ttd.WaitSeconds = 0;
                        _ = ttd.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(
                            GameCmd.S2CNotice,
                            new S2C_Notice()
                            {
                                Text = "匹配中，请耐心等待对手..."
                            })));
                        // LogDebug($"玩家{trd.Id} 轮空等待10秒");
                    }
                    // LogDebug($"玩家{trd.Id} 轮空等待，匹配轮空");
                    continue;
                }
                var enemy = tempList[_random.Next(tempList.Count)];
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
                            Text = "数据异常，已强制退出水路"
                        })));
                        enemy.Done();
                    }
                    if (!ttdIdle)
                    {
                        LogDebug($"队伍[{ttd.TeamId}]等待队伍[{enemy.TeamId}]，[{ttd.TeamId}]当前无法进入战斗");
                        _ = ttd.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice(){
                            Text = "数据异常，已强制退出水路"
                        })));
                        ttd.Done();
                    }
                    continue;
                }
                ttd.WaitSeconds = 0;
                enemy.WaitSeconds = 0;
                // 标记为战斗状态
                ttd.State = FightState.Fighting;
                enemy.State = FightState.Fighting;

                _ = ttd.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhMatch,
                    new S2C_SldhMatch
                    {
                        Team1 = { ttd.TeamData.Players },
                        Team2 = { enemy.TeamData.Players }
                    })));

                _ = enemy.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhMatch,
                    new S2C_SldhMatch
                    {
                        Team1 = { enemy.TeamData.Players },
                        Team2 = { ttd.TeamData.Players }
                    })));

                LogDebug($"队伍[{ttd.TeamId}]VS队伍[{enemy.TeamId}]10s后战斗");
                var args = new List<TempTeamData> { ttd, enemy };
                // 一次性Timer
                RegisterTimer(DoFight, args, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(-1));
            }
        }

        // 等待11s后正式开打
        private async Task DoFight(object args)
        {
            if (!_isActive) return;
            if (_state == SldhState.Close) return;
            var list = (List<TempTeamData>)args;
            if (list is not { Count: 2 }) return;
            var t1 = list[0];
            var t2 = list[1];

            // 防止二次进入
            if (t1.State != FightState.Fighting || t2.State != FightState.Fighting)
            {
                // 让队伍重新分派
                t1.State = FightState.Wait;
                t2.State = FightState.Wait;
                return;
            }

            // 检查team
            _teams.TryGetValue(t1.TeamId, out var team1);
            _teams.TryGetValue(t2.TeamId, out var team2);

            if (team1 == null || team2 == null)
            {
                if (team1 == null)
                    LogDebug($"队伍[{t1.TeamId}]跑了");
                if (team2 == null)
                    LogDebug($"队伍[{t2.TeamId}]跑了");

                // 让队伍重新分派
                t1.State = FightState.Wait;
                t2.State = FightState.Wait;
                return;
            }

            var leader1 = GrainFactory.GetGrain<IPlayerGrain>(team1.LeaderId);
            var ret = await leader1.StartPvp(team2.LeaderId, (byte)BattleType.ShuiLuDaHui);
            if (ret != 0)
            {
                LogDebug($"队伍[{t1.TeamId}]VS队伍[{t2.TeamId}]进入战斗失败[{ret}]");
                // 让队伍重新分派
                t1.State = FightState.Wait;
                t2.State = FightState.Wait;

                // 检查对方是否能进入战斗
                var leader1Grain = GrainFactory.GetGrain<IPlayerGrain>(team1.LeaderId);
                var leader2Grain = GrainFactory.GetGrain<IPlayerGrain>(team2.LeaderId);
                var leader1Idle = await leader1Grain.PreCheckPvp();
                var leader2Idle = await leader2Grain.PreCheckPvp();
                if (!leader1Idle)
                {
                    LogDebug($"队伍[{t1.TeamId}]VS队伍[{t2.TeamId}]进入战斗失败，[{t1.TeamId}]无法进入战斗");
                    _ = t1.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice()
                    {
                        Text = "数据异常，已强制退出水路"
                    })));
                    t1.Done();
                }
                if (!leader2Idle)
                {
                    LogDebug($"队伍[{t1.TeamId}]VS队伍[{t2.TeamId}]进入战斗失败，[{t2.TeamId}]无法进入战斗");
                    _ = t2.Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice()
                    {
                        Text = "数据异常，已强制退出水路"
                    })));
                    t2.Done();
                }
            }
            else
            {
                LogDebug($"队伍[{t1.TeamId}]VS队伍[{t2.TeamId}]开始战斗");
                t1.Records.Add(new Record { TeamId = t2.TeamId, Win = 0 });
                t1.BattleNum++;
                t2.Records.Add(new Record { TeamId = t1.TeamId, Win = 0 });
                t2.BattleNum++;
            }
        }

        private void SendTeamReward(TempTeamData team, bool win)
        {
            if (!_isActive) return;
            // LogDebug($"玩家[{team.TeamId}][{team.TeamData.Players[0].Name}]的队伍 胜败[{win}]");
            _ = team.Grain.OnShuiLuDaHuiBattleResult(_entity.Season, win);
        }

        private void NewSeason()
        {
            if (!_isActive) return;
            _ = _serverGrain.OnShuiLuDaHuiNewSeason(_entity.Season);
        }

        private void BroadcastState()
        {
            if (!_isActive) return;
            Broadcast(GameCmd.S2CSldhState, new S2C_SldhState { State = _state });
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
            _logger?.LogInformation($"水陆大会[{_serverId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"水陆大会[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"水陆大会[{_serverId}]:{msg}");
        }

        private class TempTeamData
        {
            public readonly uint TeamId; //队伍id
            public SldhTeamData TeamData { get; set; } //队伍数据
            public readonly uint Score; //队伍总水路积分
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

            public TempTeamData(uint teamId, uint score, ITeamGrain grain, SldhTeamData team)
            {
                TeamId = teamId;
                Score = score;
                Win = 0;
                Lost = 0;
                State = FightState.Wait;
                FightEndTime = 0;
                // FightWaitTime = 0;
                FightScore = 30;
                WaitSeconds = 0;
                Grain = grain;
                TeamData = team;

                Records = new List<Record>();
            }

            public void Done()
            {
                if (State == FightState.Done) return;
                State = FightState.Done;
                _ = Grain.ExitSldh();

                // 通知这个队伍，该水陆大会已结束
                _ = Grain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhState, new S2C_SldhState
                {
                    State = SldhState.Close,
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