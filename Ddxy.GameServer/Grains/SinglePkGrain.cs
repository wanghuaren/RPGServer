using System;
using System.Collections.Generic;
using System.Linq;
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
    /// 单人PK，每天11:30到12:00报名
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class SinglePkGrain : Grain, ISinglePkGrain
    {
        private ILogger<SinglePkGrain> _logger;
        private AppOptions _options;

        private bool _isActive;
        private uint _serverId;
        private IServerGrain _serverGrain;

        private SinglePkEntity _entity;
        private List<uint> _listPkzs; //PK战神
        private Random _random;

        private SinglePkState _state;
        private IDisposable _tempTicker;
        private IDisposable _buildBattleTicker;
        private IDisposable _forceStopTicker;

        private uint _openTime;
        private uint _begineTime;

        private Dictionary<uint, TempRoleData> _players; //报名参赛的玩家
        private Dictionary<uint, uint> _scores; //报名参赛的玩家的积分(包括已离开的)

        private TimeSpan _signDuration = TimeSpan.FromMinutes(20); //报名阶段的时长
        private TimeSpan _fightDuration = TimeSpan.FromHours(2); //比赛时长
        private uint _fightInterval = 10; //每个队伍战斗完成后休息10秒钟继续匹配下一个战斗(s)

        private const string Name = "比武大会";

        public SinglePkGrain(ILogger<SinglePkGrain> logger, IOptions<AppOptions> options)
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

            _players = new Dictionary<uint, TempRoleData>(200);
            _scores = new Dictionary<uint, uint>(200);
            _state = SinglePkState.Close;

            _entity = await DbService.QuerySinglePk(_serverId);
            if (_entity == null)
            {
                _entity = new SinglePkEntity
                {
                    ServerId = _serverId,
                    Season = 0,
                    Pkzs = "",
                    LastTime = 0
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
            await Task.CompletedTask;
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _isActive = false;
            _buildBattleTicker?.Dispose();
            _buildBattleTicker = null;
            _tempTicker?.Dispose();
            _tempTicker = null;
            _forceStopTicker?.Dispose();
            _forceStopTicker = null;

            _serverGrain = null;
            _entity = null;
            _players.Clear();
            _players = null;
            _scores.Clear();
            _scores = null;

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
                if (_state == SinglePkState.Close)
                {
                    // 1s钟后开启
                    _tempTicker?.Dispose();
                    _tempTicker = RegisterTimer(OnActivityOpen, null, TimeSpan.FromSeconds(0.1),
                        TimeSpan.FromSeconds(1));
                    LogInfo($"后台用户{opUid}开启");
                }
            }
            else
            {
                if (_state != SinglePkState.Close)
                {
                    _tempTicker?.Dispose();
                    _tempTicker = RegisterTimer(OnActivityClose, null, TimeSpan.FromSeconds(0.1),
                        TimeSpan.FromSeconds(1));
                    LogInfo($"后台用户{opUid}关闭");
                }
            }

            return null;
        }

        public async ValueTask<bool> Online(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return false;
            var Signed = _players != null && _players.ContainsKey(roleId);
            var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            if (_state != SinglePkState.Close)
            {
                if (Signed || _state == SinglePkState.Sign)
                {
                    _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSinglePkState,
                        new S2C_SinglePkState { State = _state, Signed = Signed })));
                }
            }
            return Signed;
        }

        public ValueTask<bool> CheckRoleActive(uint roleId)
        {
            if (!_isActive) return ValueTask.FromResult(false);
            if (_state == SinglePkState.Close) return ValueTask.FromResult(false);
            if (_players == null) return ValueTask.FromResult(false);
            return ValueTask.FromResult(_players.ContainsKey(roleId));
        }

        public Task<Immutable<byte[]>> GetActivityInfo()
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            var resp = new SinglePkActivityInfo
            {
                Season = _entity.Season,
                State = _state,
                OpenTime = _openTime,
                BegineTime = _begineTime
            };
            if (resp.State == SinglePkState.Close)
            {
                resp.OpenTime = 0;
                resp.BegineTime = 0;
            }

            var bytes = new Immutable<byte[]>(Packet.Serialize(resp));
            return Task.FromResult(bytes);
        }

        public ValueTask<byte> GetState()
        {
            return new((byte) _state);
        }

        /// <summary>
        /// 报名参赛
        /// </summary>
        public Task<string> Sign(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return Task.FromResult("尚未激活");
            // 检查当前活动是否开启
            if (_state == SinglePkState.Close) return Task.FromResult("活动未开启");
            if (_state != SinglePkState.Sign) return Task.FromResult("报名已结束");

            var roleInfo = SinglePkRoleInfo.Parser.ParseFrom(reqBytes.Value);
            var rid = roleInfo.Role.Id;
            if (_players.ContainsKey(rid)) return Task.FromResult("已经报名了");

            _players[rid] = new TempRoleData(roleInfo, GrainFactory.GetGrain<IPlayerGrain>(rid));

            LogDebug($"玩家[{rid}]报名");
            return Task.FromResult(string.Empty);
        }

        /// <summary>
        /// 离场
        /// </summary>
        public async ValueTask<bool> UnSign(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return false;
            _players.Remove(roleId, out var trd);
            if (trd == null) return false;
            if (_state is SinglePkState.Sign or SinglePkState.Fight)
            {
                trd.Done();
            }

            LogDebug($"玩家[{roleId}]离场");
            return true;
        }

        public async Task UpdateRole(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return;
            // 这里让队伍持续更新信息, 这样在匹配战斗的时候就可以直接取数据
            if (_state != SinglePkState.Close)
            {
                var req = RoleInfo.Parser.ParseFrom(reqBytes.Value);
                if (_players.ContainsKey(req.Id))
                {
                    _players[req.Id].UpdateRoleInfo(req);
                }
            }

            await Task.CompletedTask;
        }

        public async Task<Immutable<byte[]>> GetInfo(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return new Immutable<byte[]>(null);
            if (_state == SinglePkState.Close) return new Immutable<byte[]>(null);
            if (!_players.TryGetValue(roleId, out var trd) || trd == null || trd.State == FightState.Done)
                return new Immutable<byte[]>(null);

            var resp = new S2C_SinglePkInfo
            {
                State = _state,
                Season = _entity.Season,
                IsSign = true,
                Score = trd.Score,
                Win = trd.Win,
                Lost = trd.Lost,
                Number = (uint) _players.Count
            };

            return new Immutable<byte[]>(Packet.Serialize(resp));
        }

        public async Task OnBattleEnd(uint roleId, bool win)
        {
            if (!_isActive) return;
            _players.TryGetValue(roleId, out var trd);
            if (trd == null) return;
            if (trd.State == FightState.Fighting)
            {
                trd.State = FightState.FightEnd;
                trd.FightEndTime = TimeUtil.TimeStamp;
                if (trd.BattleNum == trd.Records.Count)
                {
                    var record = trd.Records[trd.BattleNum - 1];
                    record.Win = win ? 1 : -1;
                    if (win)
                    {
                        trd.Win++;
                        trd.Score += 10;
                        _scores[roleId] = trd.Score;

                        _ = trd.Grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(
                            GameCmd.S2CSinglePkBattleResult, new S2C_SinglePkBattleResult
                            {
                                Win = true,
                                Exp = 100000U,
                                PetExp = 100000U,
                                Score = 10
                            })));
                    }
                    else
                    {
                        trd.Lost++;
                        trd.Score -= 10;
                        _scores[roleId] = trd.Score;

                        _ = trd.Grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(
                            GameCmd.S2CSinglePkBattleResult, new S2C_SinglePkBattleResult
                            {
                                Win = false,
                                Exp = 50000U,
                                PetExp = 50000,
                                Score = -10
                            })));

                        if (trd.Score <= 0)
                        {
                            _players.Remove(trd.Id);
                            trd.Done();
                        }
                    }

                    // 查找对手信息
                    _players.TryGetValue(record.RoleId, out var enemy);
                    if (enemy is {State: FightState.Fighting})
                    {
                        enemy.State = FightState.FightEnd;
                        enemy.FightEndTime = trd.FightEndTime;
                        if (enemy.BattleNum == enemy.Records.Count)
                        {
                            var enemyRecord = enemy.Records[enemy.BattleNum - 1];
                            enemyRecord.Win = win ? -1 : 1;
                            if (win)
                            {
                                enemy.Lost++;
                                enemy.Score -= 10;
                                _scores[enemy.Id] = enemy.Score;

                                _ = enemy.Grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(
                                    GameCmd.S2CSinglePkBattleResult, new S2C_SinglePkBattleResult
                                    {
                                        Win = false,
                                        Exp = 50000U,
                                        PetExp = 50000,
                                        Score = -10
                                    })));

                                if (enemy.Score <= 0)
                                {
                                    _players.Remove(enemy.Id);
                                    enemy.Done();
                                }
                            }
                            else
                            {
                                enemy.Win++;
                                enemy.Score += 10;
                                _scores[enemy.Id] = enemy.Score;

                                _ = enemy.Grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(
                                    GameCmd.S2CSinglePkBattleResult, new S2C_SinglePkBattleResult
                                    {
                                        Win = true,
                                        Exp = 100000U,
                                        PetExp = 100000U,
                                        Score = 10
                                    })));
                            }
                        }
                    }

                    // 用First记录，确保输入的日志能统一跟踪
                    if (record.First)
                    {
                        LogDebug($"玩家[{roleId}] VS 玩家[{record.RoleId}]结束战斗, 失败[{win}]");
                    }
                    else
                    {
                        LogDebug($"玩家[{record.RoleId}] VS 玩家[{roleId}]结束战斗, 失败[{win}]");
                    }
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 每天11:30到12:00报名
        /// </summary>
        private void CheckOpenTime(bool firstTime)
        {
            if (!_isActive) return;
            if (_options.FastSinglePk)
            {
                _fightInterval = 10;
                _signDuration = TimeSpan.FromSeconds(30);
                _fightDuration = TimeSpan.FromMinutes(15);

                var delayTsx = firstTime ? TimeSpan.FromSeconds(10) : TimeSpan.FromSeconds(30);
                _tempTicker?.Dispose();
                _tempTicker = RegisterTimer(OnActivityOpen, null, delayTsx, TimeSpan.FromSeconds(1));
                return;
            }

            // var now = DateTimeOffset.Now;
            // // 今天21:00开始报名
            // var nextOpenTime = new DateTimeOffset(now.Year, now.Month, now.Day, 21, 0, 0, TimeSpan.FromHours(8));
            // if (now.Hour >= 22 || now.Hour == 21 && now.Minute >= 30) nextOpenTime = nextOpenTime.AddDays(1);
            //
            // var delayTs = nextOpenTime.Subtract(now);
            // if (delayTs.TotalSeconds <= 0) delayTs = TimeSpan.FromSeconds(1);
            // 每周星期天晚上9点活动开始报名
            var now = DateTime.Now;
            var next = DateTimeUtil.GetWeekDayStartTime(DayOfWeek.Sunday, 21, 0, 0);
            if (now.DayOfWeek == next.DayOfWeek && (now.Hour >= 22 || (now.Hour == 21 && now.Minute >= _signDuration.Minutes)))
            {
                next = next.AddDays(7);
            }
            var delayTs = next.Subtract(now);
            if (delayTs.TotalSeconds <= 0) delayTs = TimeSpan.FromSeconds(1);
            LogInfo($"{delayTs.Days}日{delayTs.Hours}时{delayTs.Minutes}分{delayTs.Seconds}秒后开启");

            // 防止Timer等待太久而休眠, 超过1个小时的等待就用1个小时后再次来检查时间
            if (delayTs.TotalHours >= 1)
            {
                _tempTicker?.Dispose();
                _tempTicker = RegisterTimer(_ =>
                {
                    CheckOpenTime(false);
                    return Task.CompletedTask;
                }, null, TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(-1));
            }
            else
            {
                _tempTicker?.Dispose();
                _tempTicker = RegisterTimer(OnActivityOpen, null, delayTs, TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// 活动开始后的20分钟用来报名
        /// </summary>
        private async Task OnActivityOpen(object state)
        {
            _tempTicker?.Dispose();
            _tempTicker = null;
            if (!_isActive) return;
            if (_state == SinglePkState.Sign) return;
            _state = SinglePkState.Sign;
            
            _players.Clear();
            _scores.Clear();

            var now = DateTimeOffset.Now;
            _openTime = (uint) now.ToUnixTimeSeconds();
            _begineTime = (uint) now.Add(_signDuration).ToUnixTimeSeconds();

            // 重置单人PK排行榜 每个赛季重置单人PK排名
            await _serverGrain.ResetSinglePkRank();

            _entity.Season++;
            _entity.LastTime = TimeUtil.TimeStamp;
            await DbService.Sql.Update<SinglePkEntity>()
                .Where(it => it.Id == _entity.Id)
                .Set(it => it.Season, _entity.Season)
                .Set(it => it.LastTime, _entity.LastTime)
                .ExecuteAffrowsAsync();
            
            // 清理排行榜
            await RedisService.DelRoleSinglePkRank(_serverId);

            // 广播系统消息
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSinglePkState,
                new S2C_SinglePkState
                {
                    State = _state,
                    Signed = false
                }
            )));

            // 20分钟后开始
            _tempTicker?.Dispose();
            _tempTicker = RegisterTimer(OnActivityBegine, null, _signDuration, TimeSpan.FromSeconds(1));

            // 2个小时后强行停止
            _forceStopTicker?.Dispose();
            _forceStopTicker = RegisterTimer(OnActivityClose, null, _fightDuration, TimeSpan.FromSeconds(1));

            LogDebug("活动开启");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 活动正式开始
        /// </summary>
        private async Task OnActivityBegine(object args)
        {
            _tempTicker?.Dispose();
            _tempTicker = null;
            if (!_isActive) return;
            if (_state == SinglePkState.Fight) return;
            _state = SinglePkState.Fight;

            if (_players.Count == 0)
            {
                LogDebug("没有玩家报名参赛, 直接结束活动");
                await OnActivityClose(null);
                return;
            }

            BroadcastState();

            // 已报名的队伍跳入3003地图
            foreach (var trd in _players.Values)
            {
                _ = trd.Grain.OnEnterSinglePk();
            }

            // 10s后开始匹配，后面每1s更新
            _buildBattleTicker?.Dispose();
            _buildBattleTicker = RegisterTimer(BuildBattleWrap, null, TimeSpan.FromSeconds(_fightInterval),
                TimeSpan.FromSeconds(1));

            LogDebug($"开始比赛, 共计报名人数[{_players.Count}]");
            await Task.CompletedTask;
        }

        private async Task OnActivityClose(object o)
        {
            _buildBattleTicker?.Dispose();
            _buildBattleTicker = null;
            _tempTicker?.Dispose();
            _tempTicker = null;
            _forceStopTicker?.Dispose();
            _forceStopTicker = null;
            if (!_isActive) return;
            if (_state == SinglePkState.Close) return;
            _state = SinglePkState.Close;

            // 广播系统消息
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSinglePkState,
                new S2C_SinglePkState
                {
                    State = _state
                }
            )));

            // 通知所有的玩家活动已结束
            foreach (var trd in _players.Values)
            {
                if (trd == null) continue;
                trd.Done();
                _scores[trd.Id] = trd.Score;
            }

            // 根据积分来进行排名
            var rankList = _scores.Where(p => p.Value > 0).OrderByDescending(p => p.Value).ToList();
            for (var i = 0; i < rankList.Count; i++)
            {
                LogDebug($"第{(i + 1)}名{rankList[i].Key}得分[{rankList[i].Value}]");
            }

            try
            {
                // 清掉上赛季的PK战神的称号
                for (var i = 0; i < _listPkzs.Count; i++)
                {
                    var rid = _listPkzs[i];
                    if (rid == 0) continue;
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                    _ = grain.GmAddTitle((uint) GetTitleByIndex(i), false);
                }

                _listPkzs.Clear();

                // 统计本赛季的PK战神
                for (var i = 0; i < 3; i++)
                {
                    if (i >= rankList.Count) break;
                    _listPkzs.Add(rankList[i].Key);
                }

                for (var i = 0; i < rankList.Count; i++)
                {
                    var (rid, _) = rankList[i];
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                    _ = grain.OnSinglePkResult(i, (uint) GetTitleByIndex(i));
                }
            }
            catch (Exception ex)
            {
                LogError($"统计比武战神出错[{ex.Message}][{ex.StackTrace}]");
            }

            _entity.Pkzs = Json.Serialize(_listPkzs);

            _players.Clear();
            _scores.Clear();
            LogDebug("活动已关闭");

            // 等待下次开启
            CheckOpenTime(false);

            await Task.CompletedTask;
        }

        private async Task BuildBattleWrap(object _)
        {
            if (!_isActive) return;
            if (_state != SinglePkState.Fight)
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
                LogError($"构建战斗出错[{ex.Message}][{ex.StackTrace}]");
            }
        }

        // 检查战斗队伍是否都进入了战斗
        private async Task BuildBattle()
        {
            if (!_isActive) return;
            // 先检查一遍，所有的已经结束战斗的队伍是否完成冷却
            var now = TimeUtil.TimeStamp;
            // 已经被淘汰的--积分为0的
            var toRemove = new List<uint>();
            var wait2Match = new List<TempRoleData>();
            var playerIdList = new List<uint>();
            var hasFighting = false;
            foreach (var trd in _players.Values)
            {
                // 已经被淘汰的
                if (trd is {State: FightState.Done })
                {
                    toRemove.Add(trd.Id);
                    continue;
                }
                playerIdList.Add(trd.Id);
                // 刚才战斗结束的
                if (trd.State == FightState.FightEnd && now - trd.FightEndTime > _fightInterval)
                {
                    // 等待下一场战斗
                    trd.FightEndTime = 0;
                    trd.State = FightState.Wait;
                    wait2Match.Add(trd);
                    continue;
                }
                // 已经在等待的
                if (trd.State == FightState.Wait)
                {
                    wait2Match.Add(trd);
                    continue;
                }
                // 还有在战斗中的？
                if (trd.State == FightState.Fighting)
                {
                    hasFighting = true;
                }
            }
            // 清除已经被淘汰的
            foreach (var id in toRemove)
            {
                _players.Remove(id);
            }
            // 人数已经不足以再次匹配，则直接结算
            if (_players.Count <= 1)
            {
                LogDebug("全部战斗结束，可以结算了！");
                await OnActivityClose(null);
                return;
            }
            // 等待的人只有一个
            if (wait2Match.Count == 1)
            {
                var trd = wait2Match[0];
                trd.WaitSeconds += 1;
                if (trd.WaitSeconds >= 10)
                {
                    trd.WaitSeconds = 0;
                    _ = trd.Grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(
                        GameCmd.S2CNotice,
                        new S2C_Notice()
                        {
                            Text = "匹配中，请耐心等待对手..."
                        })));
                    // LogDebug($"玩家[{trd.Id}]轮空等待10秒");
                }
                // LogDebug($"玩家[{trd.Id}]轮空等待，只有一人");
                return;
            }
            // 已经全部对局过了？
            if (!hasFighting)
            {
                var allDone = true;
                foreach (var trd in _players.Values)
                {
                    foreach (var id in playerIdList)
                    {
                        if (id == trd.Id) continue;
                        if (trd.Records.Exists(p => p.RoleId == id)) continue;
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

            // 按等级进行排序
            var sortedList = wait2Match.OrderByDescending(CombineLevel).ToList();
            var tempList = new List<TempRoleData>();
            for (int i = 0, len = sortedList.Count; i < len; i++)
            {
                var trd = sortedList[i];

                // 下面的逻辑只是用来处理处于Wait状态的
                if (trd.State != FightState.Wait) continue;

#if false
                var clevel = CombineLevel(trd);
                // 寻找一个等级差在10以内且同样处于wait状态的敌方
                var sidx = i; // 开始索引
                var eidx = i; // 结束索引

                var levelGap = 0;
                while (sidx == i && eidx == i)
                {
                    // 每次递增5级的跨度
                    levelGap += 5;

                    // 第一步，先往前找等级比我高的
                    var cursor = i - 1;
                    while (cursor >= 0)
                    {
                        if (Math.Abs(CombineLevel(sortedList[cursor]) - clevel) <= levelGap)
                        {
                            sidx = cursor;
                        }
                        else if (sidx != i)
                        {
                            // 之前找到过就可以停止循环了
                            break;
                        }

                        cursor--;
                    }

                    // 第二步，往后找
                    cursor = i + 1;
                    while (cursor < len)
                    {
                        if (Math.Abs(clevel - CombineLevel(sortedList[cursor])) <= levelGap)
                        {
                            eidx = cursor;
                        }
                        else if (eidx != i)
                        {
                            // 之前找到过就可以停止循环了
                            break;
                        }

                        cursor++;
                    }

                    // 向前向后找到任何一个单位都可以终止
                    if (sidx != i || eidx != i) break;
                    // 最大不超过50级
                    if (levelGap >= 100) break;
                }

                // 如果上下都找不到合适的, 就启用全部
                if (sidx == i && eidx == i)
                {
                    sidx = 0;
                    eidx = len - 1;
                    // LogDebug($"玩家[{trd.Id}]找不到任何单位");
                }

                // 在sidx和eidx中筛选出当前同样wait的单位
                tempList.Clear();
                for (var xi = sidx; xi <= eidx; xi++)
                {
                    var xtrd = sortedList[xi];
                    // 过滤没有在等待状态的
                    if (xtrd.State != FightState.Wait) continue;
                    //过滤掉自己
                    if (trd.Id == xtrd.Id) continue;
                    // 已经打过的对手也不再重复
                    if (trd.Records.Exists(p => p.RoleId == xtrd.Id)) continue;
                    tempList.Add(xtrd);
                }
#else
                tempList.Clear();
                tempList.AddRange(sortedList.FindAll(t =>
                    // 不能是自己
                    t.Id != trd.Id &&
                    // 是等待状态
                    t.State == FightState.Wait &&
                    // 和我相差最多50级
                    Math.Abs(trd.Role.Level - t.Role.Level) <= 50 &&
                    // 没有和我战斗过
                    !t.Records.Exists(r => r.RoleId == trd.Id)));
                // 如果还是没有找到人，则在所有在场玩家中找对手
                if (tempList.Count == 0)
                {
                    for (var xi = 0; xi <= len - 1; xi++)
                    {
                        var xtrd = sortedList[xi];
                        // 过滤没有在等待状态的
                        if (xtrd.State != FightState.Wait) continue;
                        // 过滤掉自己
                        if (trd.Id == xtrd.Id) continue;
                        // 已经打过的对手也不再重复
                        if (trd.Records.Exists(p => p.RoleId == xtrd.Id)) continue;
                        tempList.Add(xtrd);
                    }
                }
#endif
                if (tempList.Count == 0)
                {
                    // 这种情况
                    trd.WaitSeconds += 1;
                    if (trd.WaitSeconds >= 10)
                    {
                        trd.WaitSeconds = 0;
                        _ = trd.Grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(
                            GameCmd.S2CNotice,
                            new S2C_Notice()
                            {
                                Text = "匹配中，请耐心等待对手..."
                            })));
                        // LogDebug($"玩家[{trd.Id}]轮空等待10秒");
                    }
                    // LogDebug($"玩家[{trd.Id}]轮空等待，匹配轮空");
                    continue;
                }
                var enemy = tempList[_random.Next(tempList.Count)];

                trd.WaitSeconds = 0;
                enemy.WaitSeconds = 0;
                // 标记为战斗状态
                trd.State = FightState.Fighting;
                enemy.State = FightState.Fighting;

                _ = trd.Grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSinglePkMatch,
                    new S2C_SinglePkMatch
                    {
                        My = {trd.Role},
                        Enemy = {enemy.Role}
                    })));

                _ = enemy.Grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSinglePkMatch,
                    new S2C_SinglePkMatch
                    {
                        My = {enemy.Role},
                        Enemy = {trd.Role}
                    })));

                var args = new UintPair() { Key = trd.Id, Value = enemy.Id };
                 //一次性Timer
                RegisterTimer(DoFight, args, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(-1));
                LogDebug($"玩家[{trd.Id}] VS 玩家[{enemy.Id}] 10s后战斗");
            }
        }

        private async Task DoFight(object args)
        {
            if (!_isActive) return;
            if (_state == SinglePkState.Close) return;
            var pair = (UintPair) args;
            _players.TryGetValue(pair.Key, out var trd);
            _players.TryGetValue(pair.Value, out var enemy);

            // 检测是否有在这倒计时内离开的
            if (trd == null || enemy == null)
            {
                if (trd != null)
                {
                    trd.RevertBattleState();
                }

                if (enemy != null)
                {
                    enemy.RevertBattleState();
                }

                return;
            }

            // 防止二次进入
            if (trd.State != FightState.Fighting || enemy.State != FightState.Fighting)
            {
                trd.RevertBattleState();
                enemy.RevertBattleState();
                return;
            }

            var ret = await trd.Grain.StartPvp(enemy.Role.Id, (byte) BattleType.SinglePk);
            if (ret != 0)
            {
                LogDebug($"玩家[{trd.Id}] VS 玩家[{enemy.Id}] 进入战斗失败[{ret}]");

                // 恢复状态, 重新进行匹配
                trd.RevertBattleState();
                enemy.RevertBattleState();
            } else {
                LogDebug($"玩家[{trd.Id}] VS 玩家[{enemy.Id}] 开始战斗");
                trd.Records.Add(new Record { RoleId = enemy.Id, Win = 0, First = true });
                trd.BattleNum++;
                enemy.Records.Add(new Record { RoleId = trd.Id, Win = 0, First = false });
                enemy.BattleNum++;
            }
        }

        private static int CombineLevel(TempRoleData trd)
        {
            return (int) (trd.Role.Relive * 20 + trd.Role.Level);
        }

        private static int GetTitleByIndex(int index)
        {
            var ret = index switch
            {
                0 => 59,
                1 => 60,
                2 => 58,
                _ => 0
            };

            return ret;
        }

        private void BroadcastState()
        {
            if (!_isActive) return;
            Broadcast(GameCmd.S2CSinglePkState, new S2C_SinglePkState {State = _state});
        }

        private void Broadcast(GameCmd cmd, IMessage msg)
        {
            if (!_isActive) return;
            var bytes = new Immutable<byte[]>(Packet.Serialize(cmd, msg));
            foreach (var trd in _players.Values)
            {
                if (trd != null) _ = trd.Grain.SendMessage(bytes);
            }
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"比武大会[{_serverId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"比武大会[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"比武大会[{_serverId}]:{msg}");
        }

        private class TempRoleData : IDisposable
        {
            public uint Id
            {
                get
                {
                    if (_info?.Role == null) return 0;
                    return _info.Role.Id;
                }
            }

            public RoleInfo Role => _info.Role;

            public uint Win { get; set; } //本次比赛的总赢次数
            public uint Lost { get; set; } //本次比赛的总输次数
            public uint Score { get; set; } //本次比赛的总积分

            public uint WaitSeconds { get; set; } //匹配等待时间

            public FightState State; //当前战斗状态
            public int BattleNum { get; set; } //已参战次数

            public List<Record> Records; //战斗记录

            public uint FightEndTime; //上次战斗结束的时间

            public IPlayerGrain Grain { get; private set; }

            private SinglePkRoleInfo _info;

            public TempRoleData(SinglePkRoleInfo info, IPlayerGrain grain)
            {
                _info = info;
                Grain = grain;
                State = FightState.Wait;
                Records = new List<Record>(5);
                Win = 0;
                Lost = 0;
                Score = 30; //默认30分
                WaitSeconds = 0;
            }

            public void UpdateRoleInfo(RoleInfo info)
            {
                if (_info != null) _info.Role = info;
            }

            public void Done()
            {
                if (State == FightState.Done) return;
                State = FightState.Done;
                _ = Grain.OnExitSinglePk(Win, Lost, Score);

                // 通知这个玩家，单人PK已结束
                _ = Grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSinglePkState,
                    new S2C_SinglePkState
                    {
                        State = SinglePkState.Close,
                        Signed = true
                    }
                )));
            }

            public void RevertBattleState()
            {
                State = FightState.Wait;
            }

            public void Dispose()
            {
                _info = null;
                Grain = null;
                Records?.Clear();
                Records = null;
            }
        }

        private class Record
        {
            public uint RoleId;
            public int Win; // 0未结果, 1赢，-1输
            public bool First;
        }

        private enum FightState
        {
            Wait = 0, //等待匹配
            Fighting = 1, //战斗中
            FightEnd = 2, //战斗结束
            Done = 3 //所有战斗已经完成
        }
    }
}