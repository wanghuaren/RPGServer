using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
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
    // 水路返回值
    class ShuiLuDaHuiCheckResult
    {
        public string error { get; set; }
        public int state { get; set; }
        public uint group { get; set; }
    }
    [CollectionAgeLimit(AlwaysActive = true)]
    public class TeamGrain : Grain, ITeamGrain
    {
        private ILogger<TeamGrain> _logger;
        private AppOptions _gameOptions;

        private uint _serverId; //区服id
        private uint _teamId; //队伍id
        private bool _isActive;
        private TeamTarget _lastTarget; //帮战的时候, 会自动转换队伍目标, 这里保留转换之前的目标
        private TeamTarget _target; //队伍目标
        private uint _createTime; //创建时间
        private uint _sectId; //所属帮派id

        private uint _mapId;
        private int _mapX;
        private int _mapY;
        private bool _needSyncImmediate;
        private IMapGrain _mapGrain;
        private List<Pos> _posList;
        private TeamMoveRequest _teamMoveReq;

        private List<XTaskData> _tasks;

        private List<TeamObjectData> _players; //队伍中所有的玩家(不包含队长)
        private List<TeamObjectData> _partners; //队长的伙伴
        private List<TeamObjectData> _queue; //当前队伍, 按照顺序
        private TeamObjectData _leader; //队长
        private Dictionary<uint, IPlayerGrain> _playerGrains;

        private Dictionary<uint, TeamApplyJoinData> _applyList;
        private uint _nextLeader; //只有队长邀请该玩家控制队伍时才临时记住

        private Dictionary<uint, byte> _invitList; //邀请记录
        private Dictionary<uint, TeamLeaderApply> _leaderApplyList; //申请队长记录

        private bool _dismiss; //是否已dismiss
        private IDisposable _autoDispose;
        private IShuiLuDaHuiGrain _shuiLuDaHuiGrain;
        private IDaLuanDouGrain _daLuanDouGrain;
        private IWangZheZhiZhanGrain _wangZheZhiZhanGrain;
        // 神兽降临
        private IShenShouJiangLinGrain _shenShouJiangLinGrain;

        private bool _needPersist; //标记OnDeactivateAsync时是否需要持久化

        private bool _sldhActive; //当前是否报名了水陆大会
        private int _sldhStateLastChecked = -1;

        // ReSharper disable once NotAccessedField.Local
        private uint _sldhGroup; //水路分组id

        private bool _dldActive; //当前是否报名了大乱斗
        private int _dldStateLastChecked = -1;
        private uint _dldGroup; //大乱斗分组id

        private bool _wzzzActive; //当前是否报名王者之战
        private int _wzzzStateLastChecked = -1;
        private uint _wzzzGroup; //王者之战分组id

        private bool _ssjlActive; //当前是否报名了神兽降临

        private const int MaxApplyCount = 100;

        public TeamGrain(ILogger<TeamGrain> logger, IOptions<AppOptions> options)
        {
            _logger = logger;
            _gameOptions = options.Value;
        }

        public override Task OnActivateAsync()
        {
            var keys = this.GetPrimaryKeyString().Split('_');
            _serverId = Convert.ToUInt32(keys[0]);
            _teamId = Convert.ToUInt32(keys[1]);
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

            _shuiLuDaHuiGrain = GrainFactory.GetGrain<IShuiLuDaHuiGrain>(_serverId);
            _daLuanDouGrain = GrainFactory.GetGrain<IDaLuanDouGrain>(_serverId);
            _wangZheZhiZhanGrain = GrainFactory.GetGrain<IWangZheZhiZhanGrain>(_serverId);
            // 神兽降临
            _shenShouJiangLinGrain = GrainFactory.GetGrain<IShenShouJiangLinGrain>(_serverId);

            _target = TeamTarget.Unkown;
            _lastTarget = TeamTarget.Unkown;
            _createTime = TimeUtil.TimeStamp;

            _players = new List<TeamObjectData>(4);
            _partners = new List<TeamObjectData>(10);
            _queue = new List<TeamObjectData>(5);
            _playerGrains = new Dictionary<uint, IPlayerGrain>(4);
            _leader = null;

            _applyList = new Dictionary<uint, TeamApplyJoinData>();
            _invitList = new Dictionary<uint, byte>();
            _leaderApplyList = new Dictionary<uint, TeamLeaderApply>(3);

            _mapId = 0;
            _mapX = 0;
            _mapY = 0;
            _posList = new List<Pos>(10);
            _tasks = new List<XTaskData>(5);

            _dismiss = false;
            _needPersist = true;

            // 激活后10s内要求TakeOver, 否则就自动释放掉
            _autoDispose = RegisterTimer(AutoDisposeTimeout, null, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));

            LogDebug("激活成功");
            return Task.CompletedTask;
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _isActive = false;
            _autoDispose?.Dispose();
            _autoDispose = null;

            // 通知Server，回收id, 清理组队大厅里的数据 ,清理队伍创建的Npc等
            _ = GrainFactory.GetGrain<IServerGrain>(_serverId).DeleteTeam(_teamId);

            // 通知所有玩家, 离开队伍
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    _playerGrains.TryGetValue(tod.DbId, out var grain);
                    if (grain != null) _ = grain.OnExitTeam();
                }
            }

            // 持久化, 方便停服之后能恢复队伍数据
            if (_needPersist)
            {
                // var redisData = new TeamRedisData
                // {
                //     Id = _teamId,
                //     Target = _target,
                //     CreateTime = _createTime,
                //     Leader = _leader,
                //     Players = {_players},
                //     Partners = {_partners},
                //     Applies = {_applyList.Values},
                //     NextLeader = _nextLeader,
                //     MapId = _mapId,
                //     MapX = _mapX,
                //     MapY = _mapY,
                //     PosList = {_posList},
                //     NeedSyncImmediate = _needSyncImmediate
                // };
                // foreach (var (k, v) in _invitList)
                // {
                //     redisData.InvitList.Add(new UintPair {Key = k, Value = v});
                // }
                //
                // await RedisService.SetTeamInfo(_serverId, _teamId, Packet.Serialize(redisData));
            }

            _players.Clear();
            _partners.Clear();
            _queue.Clear();

            _leader = null;
            _dismiss = true;
            _playerGrains.Clear();
            _playerGrains = null;
            _tasks.Clear();
            _tasks = null;
            _applyList.Clear();
            _applyList = null;
            _invitList.Clear();
            _invitList = null;
            _leaderApplyList.Clear();
            _leaderApplyList = null;
            _shuiLuDaHuiGrain = null;
            _daLuanDouGrain = null;
            _wangZheZhiZhanGrain = null;
            _shenShouJiangLinGrain = null;
            _mapGrain = null;

            LogDebug("注销成功");
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new ValueTask<bool>(_isActive);
        }

        /// <summary>
        /// 队长接管队伍
        /// </summary>
        public async ValueTask<bool> TakeOver(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return false;
            try
            {
                if (_leader != null) return false;
                var req = TakeOverTeamRequest.Parser.ParseFrom(reqBytes.Value);
                if (req.List.Count == 0 || req.List[0].Type != TeamObjectType.Player) return false;
                // 队伍目标
                _target = req.Target;
                _lastTarget = req.Target;
                _sectId = req.SectId;
                // 地图信息
                _mapId = req.MapId;
                _mapX = req.MapX;
                _mapY = req.MapY;
                _mapGrain = GrainFactory.GetGrain<IMapGrain>($"{_serverId}_{_mapId}");
                _posList.Clear();
                _posList.Add(new Pos {X = _mapX, Y = _mapY});

                // 定位队长
                _leader = req.List[0];
                // 皮肤
                _leader.Skins.Clear();
                _leader.Skins.AddRange(await RedisService.GetRoleSkin(_leader.DbId));
                // 武器
                _leader.Weapon = await RedisService.GetRoleWeapon(_leader.DbId);
                // 翅膀
                _leader.Wing = await RedisService.GetRoleWing(_leader.DbId);
                // 汇总伙伴
                for (var i = 1; i < req.List.Count; i++)
                {
                    _partners.Add(req.List[i]);
                }

                _playerGrains[_leader.DbId] = GrainFactory.GetGrain<IPlayerGrain>(_leader.DbId);

                // 构建队伍并下发给所有成员
                BuildTeamQueue();

                // 上报信息给ServerGrain
                UploadInfoToServer();

                if (_target == TeamTarget.SectWar)
                {
                    var grain = GrainFactory.GetGrain<ISectWarGrain>(_serverId);
                    _ = grain.CreateTeam(_leader.DbId, _teamId);
                }

                // 关闭倒计时
                _autoDispose?.Dispose();
                _autoDispose = null;
            }
            catch (Exception ex)
            {
                LogError($"接管出错[{ex.Message}][{ex.StackTrace}]");
                return false;
            }

            await Task.CompletedTask;
            return true;
        }

        /// <summary>
        /// 队伍成员主动退出队伍
        /// </summary>
        public async Task Exit(uint roleId)
        {
            if (!_isActive) return;
            // 检查是否在队伍中
            var idx = _queue.FindIndex(p => p.Type == TeamObjectType.Player && p.DbId == roleId);
            if (idx < 0) return;

            // 已经报名神兽降临不能私自离队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(roleId, "当前活动中，不允许私自离队");
                return;
            }
            // 已经报名大乱斗不能私自离队
            if ((_dldActive || await IsJoinedDaLuanDou()) && _mapId == 3004)
            {
                SendNotice(roleId, "当前活动中，不允许私自离队");
                return;
            }

            // 判断是否为队长
            var isLeader = _leader != null && _leader.DbId == roleId;
            if (isLeader)
            {
                // 队长离开后自动解散队伍
                await Dismiss();
                return;
            }

            // 从队伍中移除
            _queue.RemoveAt(idx);
            // 从players中移除
            var idx2 = _players.FindIndex(p => p.DbId == roleId);
            if (idx2 >= 0) _players.RemoveAt(idx2);
            // 移除grain
            _playerGrains.Remove(roleId, out var playerGrain);
            if (playerGrain != null)
            {
                _ = playerGrain.OnExitTeam();
                // 如果已报名了水陆大会, 则需要把自己踢出去
                if (_sldhGroup > 0)
                {
                    _ = playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhUnSign,
                        new S2C_SldhUnSign {IsAuto = true})));
                    _ = playerGrain.OnExitShuiLuDaHui(true);
                }
                // 如果已报名了王者之战, 则需要把自己踢出去
                if (_wzzzGroup > 0)
                {
                    _ = playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CWzzzUnSign,
                        new S2C_WzzzUnSign {IsAuto = true})));
                    _ = playerGrain.OnExitWangZheZhiZhan(true);
                }
            }


            // 重新构建队伍信息
            BuildTeamQueue();

            // 上报信息给ServerGrain
            UploadInfoToServer();

            // 报告给队长，成员数量变化
            SendUpdateForLeader();

            // 通知水陆大会，队伍信息变更
            UploadInfoToShuiLuDaHui();

            if (_target == TeamTarget.SectWar)
            {
                var grain = GrainFactory.GetGrain<ISectWarGrain>(_serverId);
                _ = grain.DelTeamMember(_leader.DbId, roleId);
            }

            await Task.CompletedTask;
        }


        /// <summary>
        /// 队伍成员上线
        /// </summary>
        public async Task Online(uint roleId)
        {
            if (!_isActive) return;
            // 检查是否在队伍中
            var tod = _queue.FirstOrDefault(p => p.Type == TeamObjectType.Player && p.DbId == roleId);
            if (tod == null) return;
            tod.Online = true;
            // 给该玩家下发队伍信息
            SendInfo(roleId);
            // 给其他玩家下发玩家上线通知
            SendForOther(roleId, GameCmd.S2CTeamPlayerOnline, new S2C_TeamPlayerOnline {RoleId = roleId});
            // 如果是队长, 下发全部的申请请求
            if (_leader != null && _leader.DbId == roleId)
            {
                SendPacket(roleId, GameCmd.S2CTeamJoinApplyList,
                    new S2C_TeamJoinApplyList {List = {_applyList.Values.Take(GameDefine.TeamApplyJoinListPageSize)}});
            }

            // 如果报名了水路大会, 玩家上线的时候要恢复状态
            if (await IsJoinedShuiLuDaHui())
            {
                if (_mapId is 1000 or 1206 or 3001)
                {
                    SendPacket(roleId, GameCmd.S2CSldhState, new S2C_SldhState {State = (SldhState) _sldhStateLastChecked});
                }
                // FIXME: 只要水路大会在进行中，当队伍是已经报名了的，则直接拉到水路大会地图
                if ((_sldhStateLastChecked is (int)SldhState.Allot or (int)SldhState.Fight))
                {
                   await UpdateMap(3001, 51, 29, true);
                }
            }
            // // 如果报名了王者之战, 玩家上线的时候要恢复状态
            // if (await IsJoinedWangZheZhiZhan())
            // {
            //     if (_mapId is 1000 or 1206 or 3001)
            //     {
            //         SendPacket(roleId, GameCmd.S2CWzzzState, new S2C_WzzzState {State = (WzzzState) _wzzzStateLastChecked});
            //     }
            //     // 只要王者之战在进行中，当队伍是已经报名了的，则直接拉到水路大会地图
            //     if ((_wzzzStateLastChecked is (int)WzzzState.Allot or (int)WzzzState.Fight))
            //     {
            //        await UpdateMap(3001, 51, 29, true);
            //     }
            // }
            // 如果报名了神兽降临, 玩家上线的时候要恢复状态
            var result = await _shenShouJiangLinGrain.CheckTeamActive(_teamId);
            if (string.IsNullOrWhiteSpace(result.Error))
            {
                _ssjlActive = true;
                var reward = ConfigService.CatchedMonstersForShenShouJiangLin.GetValueOrDefault(result.Reward, null);
                SendPacket(roleId, GameCmd.S2CSsjlState, new S2C_SsjlState
                {
                    State = (SsjlState)result.State,
                    Signed = true,
                    Reward = reward != null ? reward.Pet : 0,
                    StartTime = result.StartTime,
                    EndTime = result.EndTime,
                });
            }
            else
            {
                // 如果没有参加神兽降临或活动已经结束了，标志还在的话，则直接退神兽地图
                if (_ssjlActive)
                {
                    await StopShenShouJiangLin();
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 队伍成员掉线
        /// </summary>
        public async Task Offline(uint roleId)
        {
            if (!_isActive) return;
            // 检查是否在队伍中
            var tod = _queue.FirstOrDefault(p => p.Type == TeamObjectType.Player && p.DbId == roleId);
            if (tod == null) return;
            tod.Online = false;
            // 给其他玩家下发玩家下线通知
            SendForOther(roleId, GameCmd.S2CTeamPlayerOffline, new S2C_TeamPlayerOffline {RoleId = roleId});
            await Task.CompletedTask;
        }

        public ValueTask<byte> GetTarget()
        {
            return new((byte) _target);
        }

        /// <summary>
        /// 队伍成员更新自己的昵称
        /// </summary>
        public async Task SetPlayerName(uint roleId, string name)
        {
            if (!_isActive) return;
            // 检查是否在队伍中
            var tod = _queue.FirstOrDefault(p => p.Type == TeamObjectType.Player && p.DbId == roleId);
            if (tod == null || tod.Name.Equals(name)) return;
            tod.Name = name;
            SendForAll(GameCmd.S2CTeamPlayerUpdate, new S2C_TeamPlayerUpdate {Data = tod});
            await Task.CompletedTask;
        }

        /// <summary>
        /// 队伍成员更新自己的等级信息
        /// </summary>
        public async Task SetPlayerLevel(uint roleId, uint relive, uint level)
        {
            if (!_isActive) return;
            // 检查是否在队伍中
            var tod = _queue.FirstOrDefault(p => p.Type == TeamObjectType.Player && p.DbId == roleId);
            if (tod == null) return;
            if (tod.Relive == relive && tod.Level == level) return;
            tod.Relive = relive;
            tod.Level = level;
            SendForAll(GameCmd.S2CTeamPlayerUpdate, new S2C_TeamPlayerUpdate {Data = tod});
            await Task.CompletedTask;
        }

        public async Task SetPlayerCfgId(uint roleId, uint cfgId)
        {
            if (!_isActive) return;
            // 检查是否在队伍中
            var tod = _queue.FirstOrDefault(p => p.Type == TeamObjectType.Player && p.DbId == roleId);
            if (tod == null) return;
            if (tod.CfgId == cfgId) return;
            tod.CfgId = cfgId;
            SendForAll(GameCmd.S2CTeamPlayerUpdate, new S2C_TeamPlayerUpdate {Data = tod});
            await Task.CompletedTask;
        }

        public async Task SetPlayerSkin(uint roleId, List<int> skinUse)
        {
            if (!_isActive) return;
            // 检查是否在队伍中
            var tod = roleId == _leader.DbId ? _leader : _players.FirstOrDefault(p => p.DbId == roleId);
            if (tod == null) return;
            tod.Skins.Clear();
            tod.Skins.AddRange(skinUse);
            SendForAll(GameCmd.S2CTeamPlayerUpdate, new S2C_TeamPlayerUpdate {Data = tod});
            await Task.CompletedTask;
        }

        public async Task SetPlayerWeapon(uint roleId, uint cfgId, int category, uint gem, uint level)
        {
            if (!_isActive) return;
            // 检查是否在队伍中
            var tod = roleId == _leader.DbId ? _leader : _players.FirstOrDefault(p => p.DbId == roleId);
            if (tod == null) return;
            tod.Weapon.CfgId = cfgId;
            tod.Weapon.Category = (EquipCategory)category;
            tod.Weapon.Gem = gem;
            tod.Weapon.Level = level;
            SendForAll(GameCmd.S2CTeamPlayerUpdate, new S2C_TeamPlayerUpdate {Data = tod});
            await Task.CompletedTask;
        }

        public async Task SetPlayerWing(uint roleId, uint cfgId, int category, uint gem, uint level)
        {
            if (!_isActive) return;
            // 检查是否在队伍中
            var tod = roleId == _leader.DbId ? _leader : _players.FirstOrDefault(p => p.DbId == roleId);
            if (tod == null) return;
            tod.Wing.CfgId = cfgId;
            tod.Wing.Category = (EquipCategory)category;
            tod.Wing.Gem = gem;
            tod.Wing.Level = level;
            SendForAll(GameCmd.S2CTeamPlayerUpdate, new S2C_TeamPlayerUpdate {Data = tod});
            await Task.CompletedTask;
        }

        public async Task ChangeTarget(uint roleId, byte target)
        {
            if (!_isActive) return;
            await Task.CompletedTask;
            if (_leader == null || _leader.DbId != roleId) return;
            if (_target == TeamTarget.SectWar) return;
            var newTarget = (TeamTarget) target;
            if (newTarget == TeamTarget.SectWar) return;
            if (Equals(newTarget, _target)) return;

            _target = newTarget;
            UploadInfoToServer();

            SendPacket(roleId, GameCmd.S2CTeamTarget, new S2C_TeamTarget {Target = newTarget});
        }

        /// <summary>
        /// 其他玩家申请加入该队伍
        /// </summary>
        public async Task ApplyJoin(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return;
            var apply = TeamApplyJoinData.Parser.ParseFrom(reqBytes.Value);
            var applyGrain = GrainFactory.GetGrain<IPlayerGrain>(apply.RoleId);
            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(applyGrain, "当前活动，不允许新加入申请");
                return;
            }

            // 如果是帮战组队, 必须同一个帮派的人才能申请
            if (_target == TeamTarget.SectWar)
            {
                if (_sectId != apply.SectId)
                {
                    SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "你不属于该帮派"});
                    return;
                }

                if (apply.SectWarCamp == 0)
                {
                    SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "请先从长安城-帮派接引人处参加帮战"});
                    return;
                }
            }
            else
            {
                if (apply.SectWarCamp > 0)
                {
                    SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "您正在帮战中, 无法参加非帮战队伍"});
                    return;
                }
            }

            // 查找该角色是否已经申请过
            if (_applyList.ContainsKey(apply.RoleId))
            {
                SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "已经申请过，请等待队长的处理"});
                return;
            }

            // 检查申请队列长度
            if (_applyList.Count >= MaxApplyCount)
            {
                SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "申请名额已满，请选择其他队伍"});
                return;
            }

            // 缓存申请
            _applyList.Add(apply.RoleId, apply);
            SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "已申请，请等待队长确认"});

            // 如果队长在线就通知队长处理
            if (_leader is {Online: true})
            {
                SendPacket(_leader.DbId, GameCmd.S2CTeamJoinApplyAdd, new S2C_TeamJoinApplyAdd {Data = apply});
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 队长处理加入申请
        /// </summary>
        public async Task HandleJoinApply(uint applyId, bool agree)
        {
            if (!_isActive) return;
            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(_leader.DbId, "当前活动，不允许处理加入申请");
                return;
            }
            // 清理申请
            _applyList.TryGetValue(applyId, out var apply);
            if (apply == null) return;
            var applyGrain = GrainFactory.GetGrain<IPlayerGrain>(apply.RoleId);

            if (agree)
            {
                if (IsTeamFull)
                {
                    _applyList.Remove(applyId);
                    SendPacket(_leader.DbId, GameCmd.S2CNotice, new S2C_Notice {Text = "队伍已满"});
                    return;
                }

                // 检查此人是否还在监狱反省
                var inPrison = await applyGrain.CheckInPrison();
                if (inPrison)
                {
                    SendPacket(_leader.DbId, GameCmd.S2CNotice, new S2C_Notice {Text = "此人正在天牢反省，暂时不能入队！"});
                    return;
                }

                // 检查此人是否正在战斗中
                var inBattle = await applyGrain.CheckInBattle();
                if (inBattle)
                {
                    SendPacket(_leader.DbId, GameCmd.S2CNotice, new S2C_Notice {Text = "此人正在战斗中，暂时不能入队！"});
                    return;
                }

                // 如果是在水路大会地图中不允许同意申请
                // if (_mapId == 3001)
                // {
                //     SendPacket(_leader.DbId, GameCmd.S2CNotice, new S2C_Notice {Text = "当前正在水路大会中，暂时不能处理申请！"});
                //     return;
                // }
            }

            _applyList.Remove(applyId);
            SendPacket(_leader.DbId, GameCmd.S2CTeamJoinApplyDel, new S2C_TeamJoinApplyDel {RoleId = applyId});

            // 同意了, 让PlayerGrain主动加入进来，传递自己的信息
            if (agree)
            {
                _ = applyGrain.OnTeamJoinApplyAgree(_teamId, _leader.DbId, (byte) _target, _sectId);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 队员的加入申请被队长批准后，队员主动加入队伍
        /// </summary>
        public async Task<Immutable<byte[]>> Join(Immutable<byte[]> reqBytes)
        {
            await Task.CompletedTask;
            if (!_isActive) return new Immutable<byte[]>(null);

            var data = TeamObjectData.Parser.ParseFrom(reqBytes.Value);
            if (_target == TeamTarget.SectWar && data.SectId != _sectId) return new Immutable<byte[]>(null);

            if (_queue.Count(p => p.Type == TeamObjectType.Player) >= 5)
            {
                SendPacket(data.DbId, GameCmd.S2CNotice, new S2C_Notice {Text = "队伍已满"});
                return new Immutable<byte[]>(null);
            }

            if (data.Type != TeamObjectType.Player)
            {
                SendPacket(data.DbId, GameCmd.S2CNotice, new S2C_Notice {Text = "队伍成员只能是角色"});
                return new Immutable<byte[]>(null);
            }
            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(data.DbId, "当前队伍正在活动中，不允许新加入申请");
                return new Immutable<byte[]>(null);
            }
            // 皮肤
            data.Skins.Clear();
            data.Skins.AddRange(await RedisService.GetRoleSkin(data.DbId));
            // 武器
            data.Weapon = await RedisService.GetRoleWeapon(data.DbId);
            // 翅膀
            data.Wing = await RedisService.GetRoleWing(data.DbId);

            _players.Add(data);
            _playerGrains[data.DbId] = GrainFactory.GetGrain<IPlayerGrain>(data.DbId);

            // 如果在申请列表中，就删除申请
            _applyList.Remove(data.DbId);

            // 通知目标，队伍数据
            BuildTeamQueue();
            // 上报信息给ServerGrain
            UploadInfoToServer();
            // 报告给队长，成员数量变化
            SendUpdateForLeader();
            // 通知水陆大会，队伍信息变更
            UploadInfoToShuiLuDaHui();

            if (_target == TeamTarget.SectWar)
            {
                var grain = GrainFactory.GetGrain<ISectWarGrain>(_serverId);
                _ = grain.AddTeamMember(_leader.DbId, data.DbId);
            }

            // 给该成员找到一个合适的位置
            var resp = new JoinTeamResponse
            {
                MapId = _mapId, MapX = _mapX, MapY = _mapY, Tasks = {_tasks}
            };
            for (var i = 1; i < _queue.Count; i++)
            {
                var tod = _queue[i];
                if (tod.Type != TeamObjectType.Player) break;
                if (tod.DbId == data.DbId)
                {
                    if (_posList.Count > 0)
                    {
                        if (i >= _posList.Count)
                        {
                            resp.MapX = _posList[^1].X;
                            resp.MapY = _posList[^1].Y;
                        }
                        else
                        {
                            resp.MapX = _posList[i].X;
                            resp.MapY = _posList[i].Y;
                        }
                    }

                    break;
                }
            }

            // 推送任务
            SendTasks(data.DbId);

            // 等待一小会儿推送
            if (await IsJoinedShuiLuDaHui())
            {
                RegisterTimer(async _ =>
                {
                    if (_isActive && await IsJoinedShuiLuDaHui())
                    {
                        var state = (SldhState) await _shuiLuDaHuiGrain.GetState();
                        SendPacket(data.DbId, GameCmd.S2CSldhSign, new S2C_SldhSign {State = state});
                        SendPacket(data.DbId, GameCmd.S2CSldhState, new S2C_SldhState {State = state, Signed = true});
                    }
                }, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(-1));
            }

            //大乱斗 
            if (await IsJoinedDaLuanDou())
            {
                RegisterTimer(async _ =>
                {
                    if (_isActive && await IsJoinedDaLuanDou())
                    {
                        var state = (DaLuanDouState) await _daLuanDouGrain.GetState();
                        SendPacket(data.DbId, GameCmd.S2CDaLuanDouSign, new S2C_DaLuanDouSign {State = state});
                        SendPacket(data.DbId, GameCmd.S2CDaLuanDouState, new S2C_DaLuanDouState {State = state, Signed = true});
                    }
                }, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(-1));
            }

            // 王者之战
            if (await IsJoinedWangZheZhiZhan())
            {
                RegisterTimer(async _ =>
                {
                    if (_isActive && await IsJoinedWangZheZhiZhan())
                    {
                        var state = (WzzzState) await _wangZheZhiZhanGrain.GetState();
                        SendPacket(data.DbId, GameCmd.S2CWzzzSign, new S2C_WzzzSign {State = WzzzState.Sign});
                        SendPacket(data.DbId, GameCmd.S2CWzzzState, new S2C_WzzzState {State = state, Signed = true});
                    }
                }, null, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(-1));
            }

            return new Immutable<byte[]>(Packet.Serialize(resp));
        }

        /// <summary>
        /// 队长踢人
        /// </summary>
        public async Task Kickout(uint leaderRoleId, uint roleId)
        {
            if (!_isActive) return;
            // 检查是否是队长
            if (_leader == null || _leader.DbId != leaderRoleId || _leader.DbId == roleId) return;
            // 检查roleId是否存在
            if (!_queue.Exists(p => p.Type == TeamObjectType.Player && p.DbId == roleId)) return;

            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(_leader.DbId, "当前活动中，不允许踢人");
                return;
            }

            // 先从player中移除
            var idx = _players.FindIndex(p => p.DbId == roleId);
            if (idx >= 0) _players.RemoveAt(idx);
            // 通知成员被踢出队伍
            _playerGrains.Remove(roleId, out var grain);
            if (grain != null)
            {
                _ = grain.OnExitTeam();
                SendPacket(grain, GameCmd.S2CNotice, new S2C_Notice {Text = "你被队长踢出了队伍"});
                if (_sldhGroup > 0)
                {
                    _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhUnSign,
                        new S2C_SldhUnSign {IsAuto = true})));
                    _ = grain.OnExitShuiLuDaHui(true);
                }
                if (_wzzzGroup > 0)
                {
                    _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CWzzzUnSign,
                        new S2C_WzzzUnSign {IsAuto = true})));
                    _ = grain.OnExitWangZheZhiZhan(true);
                }
            }

            BuildTeamQueue();
            // 报告给队长，成员数量变化
            SendUpdateForLeader();
            UploadInfoToServer();
            // 通知水陆大会，队伍信息变更
            UploadInfoToShuiLuDaHui();

            if (_target == TeamTarget.SectWar)
            {
                var grainx = GrainFactory.GetGrain<ISectWarGrain>(_serverId);
                _ = grainx.DelTeamMember(leaderRoleId, roleId);
            }

            await Task.CompletedTask;
        }

        public async Task QueryApplyList(uint roleId)
        {
            if (!_isActive) return;
            if (_leader == null || _leader.DbId != roleId) return;
            var resp = new S2C_TeamJoinApplyList
            {
                Total = (uint) _applyList.Count,
                List = {_applyList.Values.Take(GameDefine.TeamApplyJoinListPageSize)}
            };
            SendPacket(roleId, GameCmd.S2CTeamJoinApplyList, resp);

            await Task.CompletedTask;
        }

        // 队长请求交接
        public async Task ReqHandOver(uint roleId, uint toRoleId)
        {
            if (!_isActive) return;
            if (_leader == null || _leader.DbId != roleId || roleId == toRoleId) return;
            // 确保roRole在队伍中
            var idx = _players.FindIndex(p => p != null && p.DbId == toRoleId);
            if (idx < 0) return;

            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(_leader.DbId, "当前活动中，不允许交接");
                return;
            }

            // 记录一下临时数据, 待新队长来接管的时候验证一下
            _nextLeader = toRoleId;
            // 通知目标用户, 让队员选择
            SendPacket(toRoleId, GameCmd.S2CTeamHandOver, new S2C_TeamHandOver());
            // 提示玩家
            await Task.CompletedTask;
        }

        // 队长请求交接
        public async ValueTask<bool> HandOver(Immutable<byte[]> reqBytes)
        {
            await Task.CompletedTask;
            if (!_isActive) return false;
            var req = HandOverTeamRequest.Parser.ParseFrom(reqBytes.Value);
            // 验证是否被邀请控制
            if (_nextLeader == 0 || req.RoleId != _nextLeader) return false;
            var idx = _players.FindIndex(p => p != null && p.DbId == req.RoleId);
            if (idx < 0) return false;

            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(_leader.DbId, "当前活动中，不允许交接");
                return false;
            }
            // 老队长在战场上，不允许交接
            var oldLeaderGrain = GrainFactory.GetGrain<IPlayerGrain>(_leader.DbId);
            if (oldLeaderGrain != null)
            {
                if (await oldLeaderGrain.IsInBattle())
                {
                    return false;
                }
            }
            else
            {
                LogError("申请队长失败");
                return false;
            }

            // 新领队来接管, 使用新的伙伴做领队，互相交换位置
            var oldLeader = _leader;
            var newLeader = _players[idx];
            _partners.Clear();
            _partners.AddRange(req.List);
            _players[idx] = _leader; //把原队长放回到Players中
            _leader = newLeader;
            _nextLeader = 0;

            BuildTeamQueue();
            UploadInfoToServer();
            UploadInfoToShuiLuDaHui();

            // 通知所有成员, 队长改变了
            var bits = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice,
                new S2C_Notice {Text = $"{_leader.Name}开始接管队伍"}));
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    _playerGrains.TryGetValue(tod.DbId, out var grain);
                    if (grain != null)
                    {
                        _ = grain.OnTeamChanged(_teamId, _leader.DbId, MemberCount);
                        _ = grain.SendMessage(bits);
                    }
                }
            }

            if (_target == TeamTarget.SectWar)
            {
                var grain = GrainFactory.GetGrain<ISectWarGrain>(_serverId);
                _ = grain.SwapTeamLeader(oldLeader.DbId, newLeader.DbId);
            }

            return true;
        }

        public async Task InvitePlayer(uint leaderRoleId, uint playerRoleId)
        {
            if (!_isActive) return;
            if (_leader == null) return;
            // 检查对方是否已经在队伍中了
            if (_queue.Exists(p => p.DbId == playerRoleId)) return;

            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(_leader.DbId, "当前活动中，不允许邀请");
                return;
            }

            // 记录邀请，对方处理邀请的时候要校验
            if (_invitList.ContainsKey(playerRoleId))
            {
                // 通知对方
                SendPacket(GrainFactory.GetGrain<IPlayerGrain>(playerRoleId), GameCmd.S2CTeamInvite, new S2C_TeamInvite
                {
                    TeamId = _teamId,
                    LeaderName = _leader.Name
                });
                SendPacket(leaderRoleId, GameCmd.S2CNotice, new S2C_Notice {Text = "已邀请, 请等待对方处理"});
                return;
            }

            var grain = GrainFactory.GetGrain<IPlayerGrain>(playerRoleId);
            if (_target == TeamTarget.SectWar)
            {
                // 检查对方是否和我在同一个帮派
                var ret = await grain.CheckCanInSectWarTeam(_sectId);
                if (!ret)
                {
                    SendPacket(leaderRoleId, GameCmd.S2CNotice, new S2C_Notice {Text = "对方不能参与本帮战队伍"});
                    return;
                }
            }
            else
            {
                // 检查对方是否在帮战中
                var ret = await grain.InSectWar();
                if (ret)
                {
                    SendPacket(leaderRoleId, GameCmd.S2CNotice, new S2C_Notice {Text = "对方正在帮战中，无法入队"});
                    return;
                }

                // 检查对方是否已报名比武大会
                ret = await grain.IsSignedSinglePk();
                if (ret)
                {
                    SendPacket(leaderRoleId, GameCmd.S2CNotice, new S2C_Notice {Text = "对方已报名比武大会，无法入队"});
                    return;
                }
            }

            _invitList[playerRoleId] = 0;

            // 通知对方
            SendPacket(grain, GameCmd.S2CTeamInvite, new S2C_TeamInvite
            {
                TeamId = _teamId,
                LeaderName = _leader.Name
            });

            SendPacket(leaderRoleId, GameCmd.S2CNotice, new S2C_Notice {Text = "邀请成功, 请等待对方处理"});
            await Task.CompletedTask;
        }

        public async Task HandleInvite(uint roleId, bool agree, uint sectId, int sectWarCamp)
        {
            if (!_isActive) return;
            if (!_invitList.Remove(roleId)) return;
            var inviteGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(inviteGrain, "当前活动中，不允许邀请");
                return;
            }

            // 检查是否已经入队
            if (_players.Exists(p => p.DbId == roleId))
            {
                SendPacket(inviteGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "已经在队伍中"});
                return;
            }

            // 如果有申请也删掉
            if (_applyList.Remove(roleId))
            {
                SendPacket(_leader.DbId, GameCmd.S2CTeamJoinApplyDel, new S2C_TeamJoinApplyDel {RoleId = roleId});
            }

            // 同意了, 让PlayerGrain主动加入进来，传递自己的信息
            if (agree)
            {
                if (_target == TeamTarget.SectWar)
                {
                    if (sectId != _sectId || sectWarCamp == 0)
                    {
                        SendPacket(inviteGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "请先从长安城-帮派接引人入帮"});
                        return;
                    }
                }
                else
                {
                    if (sectWarCamp > 0)
                    {
                        SendPacket(inviteGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "你正在帮派中，无法参与非帮战队伍"});
                        return;
                    }
                }

                // 检查此人是否还在监狱反省
                var inPrison = await inviteGrain.CheckInPrison();
                if (inPrison)
                {
                    SendPacket(inviteGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "你正在天牢反省，暂时不能入队！"});
                    return;
                }

                if (IsTeamFull)
                {
                    SendPacket(inviteGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "队伍已满"});
                    return;
                }

                _ = inviteGrain.OnTeamJoinApplyAgree(_teamId, _leader.DbId, (byte) _target, _sectId);
            }

            await Task.CompletedTask;
        }

        public async Task ApplyLeader(uint roleId)
        {
            await Task.CompletedTask;
            if (!_isActive) return;
            var applyData = _queue.FirstOrDefault(p => p.Type == TeamObjectType.Player && p.DbId == roleId);
            if (applyData == null || _leader == null || _leader.DbId == roleId) return;

            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(roleId, "当前活动中，不允许更换队长");
                return;
            }

            var applyGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            if (_leaderApplyList.ContainsKey(roleId))
            {
                SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "已经申请过, 请等待其他队员的确认"});
                return;
            }

            // 一次只能有1个申请
            if (_leaderApplyList.Count >= 1)
            {
                SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "当前队伍有其他人已申请队长, 请稍后再申请"});
                return;
            }

            // 缓存申请, 并且开启倒计时
            var apply = new TeamLeaderApply(roleId);
            _leaderApplyList[roleId] = apply;
            apply.Timer = RegisterTimer(OnLeaderApplyTiemout, apply.RoleId, TimeSpan.FromSeconds(10),
                TimeSpan.FromMilliseconds(-1));

            // 广播给其他队员
            SendForOther(roleId, GameCmd.S2CTeamApplyLeader, new S2C_TeamApplyLeader
            {
                RoleId = roleId,
                RoleName = applyData.Name
            });

            SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "申请成功, 请等待队友的处理"});
        }

        public async Task HandleApplyLeader(uint roleId, uint applyRoleId, bool agree)
        {
            if (!_isActive) return;
            if (agree) return;
            // 检查是否已经入队
            var oper = _queue.FirstOrDefault(p => p.DbId == roleId && p.Type == TeamObjectType.Player);
            if (oper == null) return;
            if (!_queue.Exists(p => p.DbId == applyRoleId && p.Type == TeamObjectType.Player)) return;
            if (_leader == null || _leader.DbId == applyRoleId) return;
            _leaderApplyList.TryGetValue(applyRoleId, out var apply);
            if (apply == null) return;
            // 有人拒绝就停止倒计时, 肯定不行
            var applyGrain = GrainFactory.GetGrain<IPlayerGrain>(apply.RoleId);
            SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = $"队友{oper.Name}拒绝了你的队长申请"});
            apply.Dispose();
            _leaderApplyList.Clear();
            await Task.CompletedTask;
        }

        private async Task OnLeaderApplyTiemout(object args)
        {
            if (!_isActive) return;
            if (args == null) return;
            uint.TryParse(args.ToString(), out var applyRoleId);
            _leaderApplyList.TryGetValue(applyRoleId, out var apply);
            if (apply == null) return;

            // 检查拒绝的人数
            var refuseNum = apply.Refuse.Count;
            if (refuseNum == 0)
            {
                // 检查申请者此时还在不在队伍
                if (_queue.Exists(p => p.Type == TeamObjectType.Player && p.DbId == applyRoleId))
                {
                    // 没有人拒绝, 就可以考虑进行队长转移
                    var applyGrain = GrainFactory.GetGrain<IPlayerGrain>(applyRoleId);
                    // 记录一下临时数据, 待新队长来接管的时候验证一下
                    _nextLeader = applyRoleId;
                    _ = applyGrain.OnTeamLeaderApplyPassed(_teamId);
                }
            }

            apply.Dispose();
            _leaderApplyList.Remove(applyRoleId);
            await Task.CompletedTask;
        }

        public async Task Leave(uint roleId)
        {
            if (!_isActive) return;
            // 检查是否在队伍中
            var tod = _players.FirstOrDefault(p => p.Type == TeamObjectType.Player && p.DbId == roleId && !p.Leave);
            if (tod == null) return;
            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(roleId, "当前活动中，不允许离队");
                return;
            }
            tod.Leave = true;

            // 通知所有人
            var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CTeamLeave,
                new S2C_TeamLeave {RoleId = roleId}));
            // 通知所有成员
            _ = Broadcast(bytes, false);

            _playerGrains.TryGetValue(tod.DbId, out var grain);
            if (grain != null)
            {
                _ = grain.OnTeamLeave();
            }

            await Task.CompletedTask;
        }

        public async Task Back(uint roleId)
        {
            if (!_isActive) return;
            var tod = _players.FirstOrDefault(p => p.Type == TeamObjectType.Player && p.DbId == roleId && p.Leave);
            if (tod == null) return;
            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(roleId, "当前活动中，不允许归队");
                return;
            }
            tod.Leave = false;

            // 通知所有人
            var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CTeamBack,
                new S2C_TeamBack {RoleId = roleId}));
            // 通知所有成员
            _ = Broadcast(bytes, false);

            // 立即同步队伍位置
            _playerGrains.TryGetValue(tod.DbId, out var grain);
            if (grain != null)
            {
                _ = grain.OnTeamBack(_mapId, _mapX, _mapY);
            }

            // 立即同步任务
            SendTasks(roleId);
            await Task.CompletedTask;
        }

        public async Task InviteBack(uint leaderRoleId, uint roleId)
        {
            if (!_isActive) return;
            if (_leader == null || _leader.DbId != leaderRoleId) return;
            var tod = _players.FirstOrDefault(p => p.Type == TeamObjectType.Player && p.DbId == roleId && p.Leave);
            if (tod == null) return;
            // 神兽降临不允许请申请入队
            if (_ssjlActive || await IsJoinedShuiLuDaHui())
            {
                SendNotice(leaderRoleId, "当前活动中，不允许归队邀请");
                return;
            }
            var grain = GrainFactory.GetGrain<IPlayerGrain>(tod.DbId);
            _ = grain.SendMessage(
                new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CTeamBackInvite, new S2C_TeamBackInvite())));
            await Task.CompletedTask;
        }

        /// <summary>
        /// 队长更新队伍的伙伴数据
        /// </summary>
        public Task UpdatePartner(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return Task.CompletedTask;
            var req = UpdateTeamPartnerRequest.Parser.ParseFrom(reqBytes.Value);
            _partners.Clear();
            _partners.AddRange(req.Member);

            BuildTeamQueue();
            UploadInfoToServer();

            return Task.CompletedTask;
        }

        /// <summary>
        /// 队长切换了地图
        /// </summary>
        public async Task UpdateMap(uint mapId, int mapX, int mapY, bool includeLeader = false)
        {
            if (!_isActive) return;
            _mapId = mapId;
            _mapX = mapX;
            _mapY = mapY;
            _mapGrain = GrainFactory.GetGrain<IMapGrain>($"{_serverId}_{_mapId}");
            _posList.Clear();
            _posList.Insert(0, new Pos {X = mapX, Y = mapY});
            TeamMove(true, includeLeader);

            UploadInfoToShuiLuDaHui();
            await Task.CompletedTask;
        }

        /// <summary>
        /// 队长移动了
        /// </summary>
        public Task UpdatePos(int mapX, int mapY, bool immediate)
        {
            if (!_isActive) return Task.CompletedTask;
            _mapX = mapX;
            _mapY = mapY;
            // 把这个位置放在最前面
            if (immediate) _posList.Clear();
            _posList.Insert(0, new Pos {X = mapX, Y = mapY});
            // 最多只保留5个点
            if (_posList.Count > 5)
                _posList.RemoveRange(5, _posList.Count - 5);
            // 通知队伍成员
            TeamMove(immediate);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 队长停止移动了
        /// </summary>
        public Task SetPathList(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return Task.CompletedTask;
            var req = C2S_MapStop.Parser.ParseFrom(reqBytes.Value);
            _posList.Clear();
            foreach (var pos in req.Path)
            {
                _posList.Add(pos);
            }

            TeamMove();

            return Task.CompletedTask;
        }

        /// <summary>
        /// 队长更新了任务
        /// </summary>
        public async Task UpdateTasks(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return;
            var req = UpdateTeamTasksRequest.Parser.ParseFrom(reqBytes.Value);
            _tasks.Clear();
            foreach (var xtd in req.List)
            {
                _tasks.Add(xtd);
            }

            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player && tod.DbId != _leader.DbId && !tod.Leave)
                {
                    _playerGrains.TryGetValue(tod.DbId, out var grain);
                    if (grain != null)
                    {
                        await grain.OnTeamTasksChanged(reqBytes);
                    }
                }
            }
        }

        public async Task FinishTaskEvent(uint taskId, uint step)
        {
            if (!_isActive) return;
            LogDebug($"完成任务事件[{taskId}]步骤[{step}]");
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player && tod.DbId != _leader.DbId && !tod.Leave)
                {
                    _playerGrains.TryGetValue(tod.DbId, out var grain);
                    if (grain != null)
                    {
                        await grain.OnTeamTaskEventFinish(taskId, step);
                    }
                }
            }
        }

        public async Task FinishTask(uint taskId, bool success)
        {
            if (!_isActive) return;
            LogDebug($"完成任务[{taskId}]成败[{success}]");
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player && tod.DbId != _leader.DbId && !tod.Leave)
                {
                    _playerGrains.TryGetValue(tod.DbId, out var grain);
                    if (grain != null)
                    {
                        await grain.OnTeamTaskFinish(taskId, success);
                    }
                }
            }
        }

        private void SendTasks(uint roleId)
        {
            if (!_isActive) return;
            if (_tasks.Count == 0) return;
            var player = _queue.FirstOrDefault(p => p.Type == TeamObjectType.Player && p.DbId == roleId);
            if (player == null) return;

            var req = new UpdateTeamTasksRequest
            {
                List = {_tasks}
            };
            var bytes = new Immutable<byte[]>(Packet.Serialize(req));

            var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            _ = grain.OnTeamTasksChanged(bytes);
        }

        private void TeamMove(bool immediate = false, bool includeLeader = false)
        {
            if (!_isActive) return;
            _teamMoveReq ??= new TeamMoveRequest();
            _teamMoveReq.List.Clear();
            if (!includeLeader)
            {
                _teamMoveReq.List.Add(new TeamMoveItem
                    {OnlyId = _leader.OnlyId, X = _mapX, Y = _mapY, Blink = immediate});
            }

            // 3002-帮派, 4001-家 队员没必要跟随进入, 在外面等队长出来后立即同步
            if (_mapId is 3002 or 4001)
            {
                // 标记一下，下次队长离开该地图，就立即同步位置
                _needSyncImmediate = true;
            }
            else
            {
                // 通知所有队伍成员
                var blink = _needSyncImmediate || immediate;
                for (var i = includeLeader ? 0 : 1; i < _queue.Count; i++)
                {
                    var tod = _queue[i];
                    if (tod.Type != TeamObjectType.Player) break;
                    // 暂离的队员不同步
                    if (tod.Leave) continue;
                    // 如果是瞬间同步, 就使用当前队长的位置
                    var pos = new Pos {X = _mapX, Y = _mapY};
                    if (!blink && _posList.Count > 0)
                    {
                        if (i >= _posList.Count)
                            pos = _posList[^1];
                        else
                            pos = _posList[i];
                    }

                    _teamMoveReq.List.Add(new TeamMoveItem {OnlyId = tod.OnlyId, Blink = blink, X = pos.X, Y = pos.Y});

                    // 通知队员更新自己的地图和位置信息, 注意它不需要再次提交到MapGrain
                    _playerGrains.TryGetValue(tod.DbId, out var grain);
                    if (grain != null)
                    {
                        _ = grain.OnTeamMapPosChanged(_mapId, pos.X, pos.Y, blink, includeLeader);
                    }
                }

                _needSyncImmediate = false;
            }

            // 统一打包提交给MapGrain
            if (_mapGrain != null)
                _ = _mapGrain.TeamMove(new Immutable<byte[]>(Packet.Serialize(_teamMoveReq)));
        }

        /// <summary>
        /// 队长发起战斗时，获得队员的角色id和有效参战的partner id集合
        /// </summary>
        public Task<Immutable<byte[]>> QueryTeamBattleMemebers()
        {
            var resp = new QueryTeamBattleMembersResponse();
            if (_isActive)
            {
                foreach (var tod in _queue)
                {
                    if (tod == null || tod.Leave) continue;
                    if (tod.Type == TeamObjectType.Player && tod.DbId != _leader.DbId)
                    {
                        resp.Players.Add(tod.DbId);
                    }
                    else if (tod.Type == TeamObjectType.Partner)
                    {
                        resp.Partners.Add(tod.CfgId);
                    }
                }
            }

            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(resp)));
        }

        public Task<Immutable<byte[]>> QueryRoleInfos(bool includeLeader = false)
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            var resp = new RoleInfoList();
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    if (includeLeader || tod.DbId != _leader.DbId)
                    {
                        resp.List.Add(new RoleInfo
                        {
                            Id = tod.DbId,
                            Name = tod.Name,
                            Relive = tod.Relive,
                            Level = tod.Level,
                            CfgId = tod.CfgId
                        });
                    }
                }
            }

            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(resp)));
        }

        public Task<uint[]> QueryTeamPlayers(bool includeLeader)
        {
            if (!_isActive) return Task.FromResult(Array.Empty<uint>());
            var list = new List<uint>();
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player && (includeLeader || tod.DbId != _leader.DbId))
                {
                    list.Add(tod.DbId);
                }
            }

            return Task.FromResult(list.ToArray());
        }

        public async Task SignShenShouJiangLin(uint roleId, bool sign)
        {
            if (!_isActive) return;
            // 必须是队长才可以操作
            if (_leader == null || _leader.DbId != roleId)
            {
                SendNotice(roleId, string.Format("只有队长才可以{0}", sign ? "报名活动" : "退出活动"));
                return;
            }
            if (sign)
            {
                if (MemberCount < 3)
                {
                    SendNotice(roleId, "队伍中玩家不能少于3个");
                    return;
                }
                if (_queue.Any(p => p.Type == TeamObjectType.Player && p.Relive < 1))
                {
                    SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice { Text = "队伍中玩家必须1转以上" });
                    return;
                }
                // 尝试去报名
                var result = await _shenShouJiangLinGrain.Sign(new Immutable<byte[]>(Packet.Serialize(BuildSsjlTeamData())));
                if (string.IsNullOrWhiteSpace(result.Error))
                {
                    var reward = ConfigService.CatchedMonstersForShenShouJiangLin.GetValueOrDefault(result.Reward, null);
                    // 报名成功, 通知所有队员
                    SendForAll(GameCmd.S2CSsjlSign, new S2C_SsjlSign
                    {
                        State = (SsjlState)result.State,
                        Reward = reward != null ? reward.Pet : 0,
                        StartTime = result.StartTime,
                        EndTime = result.EndTime,
                    });
                    await EnterSsjl();
                }
                else
                {
                    // 报名失败, 通知队长
                    SendNotice(roleId, result.Error);
                }
            }
            else
            {
                var ret = await _shenShouJiangLinGrain.UnSign(_teamId);
                if (ret)
                {
                    // 退出比赛，通知所有队员
                    SendForAll(GameCmd.S2CSsjlUnSign, new S2C_SsjlUnSign());
                }
                else
                {
                    SendPacket(roleId, GameCmd.S2CSsjlUnSign, new S2C_SsjlUnSign());
                }
                await ExitSsjl(true);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 神兽降临开始抓捕
        /// </summary>
        public async Task StartShenShouJiangLin(uint endTime, uint shenShouId, uint serverId)
        {
            if (!_isActive) return;
            // 通知所有玩家进入了神兽降临地图
            foreach (var tod in _queue)
            {
                if (tod is not { Type: TeamObjectType.Player }) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    _ = grain.OnStartShenShouJiangLin(endTime, shenShouId, serverId);
                }
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// 神兽降临停止抓捕
        /// </summary>
        public async Task StopShenShouJiangLin()
        {
            if (!_isActive) return;
            // 通知所有玩家离开了神兽降临地图
            foreach (var tod in _queue)
            {
                if (tod is not { Type: TeamObjectType.Player }) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    _ = grain.OnStopShenShouJiangLin();
                }
            }
            await ExitSsjl(true);
            await Task.CompletedTask;
        }

        public async Task SignShuiLuDaHui(uint roleId, bool sign)
        {
            if (!_isActive) return;
            // 必须是队长才可以操作
            if (_leader == null || _leader.DbId != roleId)
            {
                SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = "只有队长才可以报名"});
                return;
            }

            if (sign)
            {
                if (!_gameOptions.TeamUnLimit)
                {
                    if (MemberCount < 3)
                    {
                        SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = "队伍中玩家不能少于3个"});
                        return;
                    }

                    // 所有成员必须满50级
                    if (_queue.Any(p => p.Type == TeamObjectType.Player && p.Level < 50))
                    {
                        SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = "队伍中玩家不能低于50级"});
                        return;
                    }
                }

                // 尝试去报名
                var error = await _shuiLuDaHuiGrain.Sign(
                    new Immutable<byte[]>(Packet.Serialize(BuildSldhTeamData())));
                if (string.IsNullOrWhiteSpace(error))
                {
                    _sldhActive = true;
                    _sldhGroup = 0;
                    // 报名成功, 通知所有队员
                    SendForAll(GameCmd.S2CSldhSign, new S2C_SldhSign {State = SldhState.Sign});
                }
                else
                {
                    // 报名失败, 通知队长
                    SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = error});
                }
            }
            else
            {
                var ret = await _shuiLuDaHuiGrain.UnSign(_teamId);
                if (ret)
                {
                    SendForAll(GameCmd.S2CSldhUnSign, new S2C_SldhUnSign());

                    _ = ExitSldh(_mapId == 3001);
                }
                else
                {
                    SendPacket(roleId, GameCmd.S2CSldhUnSign, new S2C_SldhUnSign());
                }

                _sldhActive = false;
                _sldhGroup = 0;
            }

            await Task.CompletedTask;
        }

        public async Task SignDaLuanDou(uint roleId, bool sign) 
        {
            if (!_isActive) return;
            // 必须是队长才可以操作
            if (_leader == null || _leader.DbId != roleId)
            {
                SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = "只有队长才可以报名"});
                return;
            }

            if (sign)
            {
                if (!_gameOptions.TeamUnLimit)
                {
                    // if (MemberCount < 3)
                    // {
                    //     SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = "队伍中玩家不能少于3个"});
                    //     return;
                    // }

                    // // 所有成员必须满50级
                    // if (_queue.Any(p => p.Type == TeamObjectType.Player && p.Level < 50))
                    // {
                    //     SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = "队伍中玩家不能低于50级"});
                    //     return;
                    // }
                }

                // 尝试去报名
                var error = await _daLuanDouGrain.Sign(
                    new Immutable<byte[]>(Packet.Serialize(BuildDldTeamData())));
                if (string.IsNullOrWhiteSpace(error))
                {
                    _dldActive = true;
                    _dldGroup = 1;
                    // 报名成功, 通知所有队员
                    SendForAll(GameCmd.S2CDaLuanDouSign, new S2C_DaLuanDouSign {State = DaLuanDouState.Sign});
                }
                else
                {
                    // 报名失败, 通知队长
                    SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = error});
                }
            }
            else
            {
                // 已经报名大乱斗不能私自离队
                if ((_dldActive || await IsJoinedDaLuanDou()) && _mapId == 3004)
                {
                    SendNotice(roleId, "当前活动中，不允许离场");
                    return;
                }
                var ret = await _daLuanDouGrain.UnSign(_teamId);
                if (ret)
                {
                    SendForAll(GameCmd.S2CDaLuanDouUnSign, new S2C_DaLuanDouUnSign());

                    _ = ExitDld(_mapId == 3004);
                }
                else
                {
                    SendPacket(roleId, GameCmd.S2CDaLuanDouUnSign, new S2C_DaLuanDouUnSign());
                }

                _dldActive = false;
                _dldGroup = 0;
            }

            await Task.CompletedTask;
        }

        public async Task SignWangZheZhiZhan(uint roleId, bool sign)
        {
            if (!_isActive) return;
            // 必须是队长才可以操作
            if (_leader == null || _leader.DbId != roleId)
            {
                SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = "只有队长才可以报名"});
                return;
            }

            if (sign)
            {
                if (!_gameOptions.TeamUnLimit)
                {
                    if (MemberCount < 3)
                    {
                        SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = "队伍中玩家不能少于3个"});
                        return;
                    }

                    // 所有成员必须满50级
                    if (_queue.Any(p => p.Type == TeamObjectType.Player && p.Level < 1))
                    {
                        SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = "队伍中玩家不能低于1级"});
                        return;
                    }
                }

                // 尝试去报名
                var error = await _wangZheZhiZhanGrain.Sign(
                    new Immutable<byte[]>(Packet.Serialize(BuildWzzzTeamData())));
                if (string.IsNullOrWhiteSpace(error))
                {
                    _wzzzActive = true;
                    _wzzzGroup = 0;
                    // 报名成功, 通知所有队员
                    SendForAll(GameCmd.S2CWzzzSign, new S2C_WzzzSign {State = WzzzState.Sign});
                }
                else
                {
                    // 报名失败, 通知队长
                    SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice {Text = error});
                }
            }
            else
            {
                var ret = await _wangZheZhiZhanGrain.UnSign(_teamId);
                if (ret)
                {
                    SendForAll(GameCmd.S2CWzzzUnSign, new S2C_WzzzUnSign());

                    _ = ExitWzzz(_mapId == 3001);
                }
                else
                {
                    SendPacket(roleId, GameCmd.S2CWzzzUnSign, new S2C_WzzzUnSign());
                }

                _wzzzActive = false;
                _wzzzGroup = 0;
            }

            await Task.CompletedTask;
        }

        public async ValueTask<bool> CheckSldhSigned(uint roleId)
        {
            if (!_isActive) return false;
            if (!_queue.Exists(p => p.Type == TeamObjectType.Player && p.DbId == roleId)) return false;
            return await IsJoinedShuiLuDaHui();
        }
        public async ValueTask<bool> CheckDldSigned(uint roleId) 
        {
            if (!_isActive) return false;
            if (!_queue.Exists(p => p.Type == TeamObjectType.Player && p.DbId == roleId)) return false;
            return await IsJoinedDaLuanDou();
        }
        public async ValueTask<bool> CheckWzzzSigned(uint roleId)
        {
            if (!_isActive) return false;
            if (!_queue.Exists(p => p.Type == TeamObjectType.Player && p.DbId == roleId)) return false;
            return await IsJoinedWangZheZhiZhan();
        }

        public Task OnShuiLuDaHuiBattleResult(uint season, bool win)
        {
            if (!_isActive) return Task.CompletedTask;
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    _playerGrains.TryGetValue(tod.DbId, out var grain);
                    if (grain != null) _ = grain.OnShuiLuDaHuiBattleResult(season, win);
                }
            }

            return Task.CompletedTask;
        }

        public Task OnDaLuanDouBattleResult(uint season, bool win) 
        {
            if (!_isActive) return Task.CompletedTask;
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    _playerGrains.TryGetValue(tod.DbId, out var grain);
                    if (grain != null) _ = grain.OnDaLuanDouBattleResult(season, win);
                }
            }

            return Task.CompletedTask;
        }

        public Task OnWangZheZhiZhanBattleResult(uint season, bool win)
        {
            if (!_isActive) return Task.CompletedTask;
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    _playerGrains.TryGetValue(tod.DbId, out var grain);
                    if (grain != null) _ = grain.OnWangZheZhiZhanBattleResult(season, win);
                }
            }

            return Task.CompletedTask;
        }

        public Task Broadcast(Immutable<byte[]> reqBytes, bool ignoreLeave = true)
        {
            if (!_isActive) return Task.CompletedTask;
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player && tod.Online && (!ignoreLeave || !tod.Leave))
                {
                    _playerGrains.TryGetValue(tod.DbId, out var grain);
                    if (grain != null)
                    {
                        _ = grain.SendMessage(reqBytes);
                    }
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 解散队伍
        /// </summary>
        private async Task Dismiss()
        {
            if (!_isActive) return;
            if (_dismiss) return;
            _dismiss = true;
            _needPersist = false; //主动解散的, 不需要持久化

            if (await IsJoinedShuiLuDaHui())
            {
                _sldhActive = false;
                _sldhGroup = 0;
                var teamData = new SldhTeamData
                {
                    TeamId = _teamId,
                    PlayerNum = 0
                };
                if (_shuiLuDaHuiGrain != null)
                {
                    _ = _shuiLuDaHuiGrain.UpdateTeam(new Immutable<byte[]>(Packet.Serialize(teamData)));
                }
                var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CSldhUnSign,
                    new S2C_SldhUnSign {IsAuto = true}));
                _ = Broadcast(bytes, false);

                // 通知所有成员
                _ = ExitSldh();
            }
            if (await IsJoinedWangZheZhiZhan())
            {
                _wzzzActive = false;
                _wzzzGroup = 0;
                var teamData = new WzzzTeamData
                {
                    TeamId = _teamId,
                    PlayerNum = 0
                };
                if (_wangZheZhiZhanGrain != null)
                {
                    _ = _wangZheZhiZhanGrain.UpdateTeam(new Immutable<byte[]>(Packet.Serialize(teamData)));
                }
                var bytes = new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CWzzzUnSign,
                    new S2C_WzzzUnSign {IsAuto = true}));
                _ = Broadcast(bytes, false);

                // 通知所有成员
                _ = ExitWzzz();
            }

            if (_target == TeamTarget.SectWar)
            {
                var grain = GrainFactory.GetGrain<ISectWarGrain>(_serverId);
                _ = grain.DestroyTeam(_leader.DbId);
            }

            _autoDispose?.Dispose();
            _autoDispose = null;
            _ = ShutDown();
        }

        private async Task AutoDisposeTimeout(object _)
        {
            if (!_isActive) return;
            _autoDispose?.Dispose();
            _autoDispose = null;

            await Dismiss();
        }

        /// <summary>
        /// 重构队伍的成员信息, 需要智能的补充伙伴
        /// </summary>
        private void BuildTeamQueue()
        {
            if (!_isActive) return;
            _queue.Clear();
            _queue.Add(_leader);
            _queue.AddRange(_players);
            var needNum = 5 - _queue.Count;
            if (needNum > 0 && _partners.Count > 0)
            {
                for (var i = 0; i < needNum; i++)
                {
                    if (i >= _partners.Count) break;
                    _queue.Add(_partners[i]);
                }
            }

            SendInfo();
        }

        public bool IsTeamFull => MemberCount >= 5;

        public uint MemberCount => (uint) _queue.Count(p => p.Type == TeamObjectType.Player);

        private void SendUpdateForLeader()
        {
            if (!_isActive) return;
            _playerGrains.TryGetValue(_leader.DbId, out var grain);
            if (grain != null)
                _ = grain.OnTeamChanged(_teamId, _leader.DbId, MemberCount);
        }

        /// <summary>
        /// 队伍信息变更后，需要及时的同步到ServerGrain中
        /// </summary>
        private void UploadInfoToServer()
        {
            if (!_isActive) return;
            var grain = GrainFactory.GetGrain<IServerGrain>(_serverId);
            _ = grain.UpdateTeam(new Immutable<byte[]>(Packet.Serialize(BuildTeamData())));
        }

        private TeamData BuildTeamData()
        {
            if (!_isActive) return new();
            return new()
            {
                Id = _teamId,
                Leader = _leader.DbId,
                Target = _target,
                MapId = _mapId,
                MapX = _mapX,
                MapY = _mapY,
                CreateTime = _createTime,
                Members = {_queue},
                TeamMemberCount = MemberCount,
                SectId = _sectId
            };
        }

        private SldhTeamData BuildSldhTeamData()
        {
            if (!_isActive) return new();
            var teamData = new SldhTeamData
            {
                TeamId = _teamId,
                Name = $"{_leader.Name}的队伍",
                LeaderId = _leader.DbId,
                PlayerNum = 0,
                Score = 0,
                MapId = _mapId
            };
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    teamData.PlayerNum++;
                    // 积分修正 = 水路积分 + 等级 * 20 + 转生等级 * 400
                    teamData.Score += (tod.SldhScore + tod.Level * 20 + tod.Relive * 400);
                    teamData.Players.Add(new RoleInfo
                    {
                        Id = tod.DbId,
                        Name = tod.Name,
                        Relive = tod.Relive,
                        Level = tod.Level,
                        CfgId = tod.CfgId
                    });
                }
            }

            return teamData;
        }

        private DldTeamData BuildDldTeamData() 
        {
            if (!_isActive) return new();
            var teamData = new DldTeamData
            {
                TeamId = _teamId,
                Name = $"{_leader.Name}的队伍",
                LeaderId = _leader.DbId,
                PlayerNum = 0,
                Score = 0,
                MapId = _mapId
            };
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    teamData.PlayerNum++;
                    // 积分修正 = 水路积分 + 等级 * 20 + 转生等级 * 400
                    // teamData.Score += (tod.SldhScore + tod.Level * 20 + tod.Relive * 400);
                    teamData.Players.Add(new RoleInfo
                    {
                        Id = tod.DbId,
                        Name = tod.Name,
                        Relive = tod.Relive,
                        Level = tod.Level,
                        CfgId = tod.CfgId
                    });
                }
            }

            return teamData;
        }
        private WzzzTeamData BuildWzzzTeamData()
        {
            if (!_isActive) return new();
            var teamData = new WzzzTeamData
            {
                TeamId = _teamId,
                Name = $"{_leader.Name}的队伍",
                LeaderId = _leader.DbId,
                PlayerNum = 0,
                Score = 0,
                MapId = _mapId
            };
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    teamData.PlayerNum++;
                    // 积分修正 = 水路积分 + 等级 * 20 + 转生等级 * 400
                    teamData.Score += (tod.WzzzScore + tod.Level * 20 + tod.Relive * 400);
                    teamData.Players.Add(new RoleInfo
                    {
                        Id = tod.DbId,
                        Name = tod.Name,
                        Relive = tod.Relive,
                        Level = tod.Level,
                        CfgId = tod.CfgId
                    });
                }
            }

            return teamData;
        }

        private SsjlTeamData BuildSsjlTeamData()
        {
            if (!_isActive) return new();
            var teamData = new SsjlTeamData
            {
                TeamId = _teamId,
                Name = $"{_leader.Name}的队伍",
                LeaderId = _leader.DbId,
                PlayerNum = 0,
            };
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    teamData.PlayerNum++;
                    teamData.Players.Add(new RoleInfo
                    {
                        Id = tod.DbId,
                        Name = tod.Name,
                        Relive = tod.Relive,
                        Level = tod.Level,
                        CfgId = tod.CfgId
                    });
                }
            }

            return teamData;
        }

        private async void UploadInfoToShuiLuDaHui()
        {
#if false
            if (_isActive && _shuiLuDaHuiGrain != null && await IsJoinedShuiLuDaHui())
                _ = _shuiLuDaHuiGrain.UpdateTeam(new Immutable<byte[]>(Packet.Serialize(BuildSldhTeamData())));
#else
            await Task.CompletedTask;
#endif
        }

        private async void UploadInfoToWangZheZhiZhan()
        {
#if false
            // if (_isActive && _shuiLuDaHuiGrain != null && await IsJoinedShuiLuDaHui())
            //     _ = _shuiLuDaHuiGrain.UpdateTeam(new Immutable<byte[]>(Packet.Serialize(BuildSldhTeamData())));
#else
            await Task.CompletedTask;
#endif
        }

        public async Task EnterSldh(uint group)
        {
            if (!_isActive) return;
            _sldhGroup = group;
            // 通知所有玩家进入了水陆大会地图
            foreach (var tod in _queue)
            {
                if (tod is not {Type: TeamObjectType.Player}) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    _ = grain.OnEnterShuiLuDaHui(group);
                }
            }

            ConfigService.Maps.TryGetValue(3001, out var cfg);
            if (cfg == null) return;
            // 跳入水陆大会地图
            await UpdateMap(3001, cfg.StartPos.X, cfg.StartPos.Y, true);
        }

        public async Task EnterDld(uint group)
        {
            if (!_isActive) return;
            _dldGroup = group; 
            // LogDebug($"EnterDld---1--{group}");
            // 通知所有玩家进入了大乱斗地图
            foreach (var tod in _queue)
            {
                if (tod is not {Type: TeamObjectType.Player}) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    // LogDebug($"EnterDld---2--{group}");
                    _ = grain.OnEnterDaLuanDou();
                }
            }

            // LogDebug($"EnterDld---3--{group}");
            ConfigService.Maps.TryGetValue(3004, out var cfg);
            if (cfg == null) return;
            // LogDebug($"EnterDld---4--{group}");
            // 跳入大乱斗地图
            await UpdateMap(3004, cfg.StartPos.X, cfg.StartPos.Y, true);
            // LogDebug($"EnterDld---5--{group}");
        }

        public async Task EnterWzzz(uint group)
        {
            if (!_isActive) return;
            _wzzzGroup = group;
            // 通知所有玩家进入了水陆大会地图
            foreach (var tod in _queue)
            {
                if (tod is not {Type: TeamObjectType.Player}) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    _ = grain.OnEnterWangZheZhiZhan(group);
                }
            }

            await Task.CompletedTask;
            // ConfigService.Maps.TryGetValue(3001, out var cfg);
            // if (cfg == null) return;
            // // 跳入水陆大会地图
            // await UpdateMap(3001, cfg.StartPos.X, cfg.StartPos.Y, true);
        }

        public async Task ExitSldh(bool changeMap = true)
        {
            _sldhActive = false;
            _sldhGroup = 0;
            if (!_isActive) return;
            // 通知所有玩家退出了水陆大会地图
            foreach (var tod in _queue)
            {
                if (tod is not {Type: TeamObjectType.Player}) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    _ = grain.OnExitShuiLuDaHui();
                }
            }

            // 退出水陆大会，回到魏征处
            if (changeMap)
                await UpdateMap(1206, 25, 18, true);
        }

        public async Task ExitDld(bool changeMap = true)
        {
            _dldActive = false;
            _dldGroup = 0;
            if (!_isActive) return;
            // 通知所有玩家退出了大乱斗地图
            foreach (var tod in _queue)
            {
                if (tod is not {Type: TeamObjectType.Player}) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    _ = grain.OnExitDaLuanDou(0, 0, 0);
                }
            }

            // 退出大乱斗，回到长安城
            if (changeMap)
                await UpdateMap(1011, 999999, 999999, true);
        }

        public async Task ExitWzzz(bool changeMap = true)
        {
            _wzzzActive = false;
            _wzzzGroup = 0;
            if (!_isActive) return;
            // 通知所有玩家退出了水陆大会地图
            foreach (var tod in _queue)
            {
                if (tod is not {Type: TeamObjectType.Player}) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    _ = grain.OnExitWangZheZhiZhan();
                }
            }

            await Task.CompletedTask;
            // // 退出水陆大会，回到魏征处
            // if (changeMap)
            //     await UpdateMap(1206, 25, 18, true);
        }

        public async Task EnterSsjl()
        {
            if (!_isActive) return;
            _ssjlActive = true;
            // 通知所有玩家进入了神兽降临地图
            foreach (var tod in _queue)
            {
                if (tod is not { Type: TeamObjectType.Player }) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    _ = grain.OnEnterShenShouJiangLin();
                }
            }
            ConfigService.Maps.TryGetValue(6001, out var cfg);
            if (cfg == null) return;
            // 跳入神兽降临地图
            await UpdateMap(6001, cfg.StartPos.X, cfg.StartPos.Y, true);
        }

        public async Task ExitSsjl(bool changeMemberMap)
        {
            _ssjlActive = false;
            if (!_isActive) return;
            // 通知所有玩家退出了神兽降临地图
            foreach (var tod in _queue)
            {
                if (tod is not { Type: TeamObjectType.Player }) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    _ = grain.OnExitShenShouJiangLin(changeMemberMap);
                }
            }
            if (changeMemberMap) return;
            ConfigService.Npcs.TryGetValue(81004, out var npcCfg);
            if (npcCfg == null) return;
            ConfigService.Maps.TryGetValue(npcCfg.AutoCreate.Map, out var cfg);
            if (cfg == null) return;
            // 回到神兽大使附近
            var x = npcCfg.AutoCreate.X + new Random().Next(-50, 50);
            var y = npcCfg.AutoCreate.Y + new Random().Next(-50, 50);
            await UpdateMap(cfg.Id, x, y, true);
        }

        public async Task EnterSectWar(uint leaderRoleId, uint sectId)
        {
            if (!_isActive) return;
            // 检查是否是队长
            if (_leader == null || _leader.DbId != leaderRoleId) return;

            foreach (var tod in _queue)
            {
                if (tod.DbId == leaderRoleId) continue;
                if (tod.SectId != sectId)
                {
                    var idx = _players.FindIndex(p => p.DbId == tod.DbId);
                    if (idx >= 0) _players.RemoveAt(idx);
                    _playerGrains.Remove(tod.DbId, out var grain);
                    if (grain != null)
                    {
                        _ = grain.OnExitTeam();
                        SendPacket(grain, GameCmd.S2CNotice, new S2C_Notice {Text = "你被队长踢出了队伍"});
                    }
                }
            }

            // 自动转换目标
            _target = TeamTarget.SectWar;

            BuildTeamQueue();
            // 报告给队长，成员数量变化
            SendUpdateForLeader();
            UploadInfoToServer();

            await Task.CompletedTask;
        }

        public async Task ExitSectWar(uint leaderRoleId, uint sectId)
        {
            if (!_isActive) return;
            // 检查是否是队长
            if (_leader == null || _leader.DbId != leaderRoleId) return;

            // 自动恢复目标
            _target = _lastTarget;
            UploadInfoToServer();

            await Task.CompletedTask;
        }

        /// <summary>
        /// 发送通知
        /// </summary>
        private void SendNotice(uint roleId, string text)
        {
            if (!_isActive) return;
            SendPacket(roleId, GameCmd.S2CNotice, new S2C_Notice { Text = text });
        }
        /// <summary>
        /// 发送通知
        /// </summary>
        private void SendNotice(IPlayerGrain grain, string text)
        {
            if (!_isActive) return;
            SendPacket(grain, GameCmd.S2CNotice, new S2C_Notice { Text = text });
        }

        /// <summary>
        /// 给指定角色发送队伍详情
        /// </summary>
        private void SendInfo(uint roleId = 0)
        {
            if (!_isActive) return;
            var resp = new S2C_TeamData {Data = BuildTeamData()};
            if (roleId > 0)
            {
                SendPacket(roleId, GameCmd.S2CTeamData, resp);
            }
            else
            {
                SendForAll(GameCmd.S2CTeamData, resp);
            }
        }

        private void SendForOther(uint roleId, GameCmd command, IMessage msg)
        {
            if (!_isActive) return;
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player && roleId != tod.DbId)
                {
                    SendPacket(tod.DbId, command, msg);
                }
            }
        }

        private void SendForAll(GameCmd command, IMessage msg)
        {
            if (!_isActive) return;
            foreach (var tod in _queue)
            {
                if (tod.Type == TeamObjectType.Player)
                {
                    SendPacket(tod.DbId, command, msg);
                }
            }
        }

        private async ValueTask<bool> IsJoinedShuiLuDaHui()
        {
            if (!_isActive) return true;
            if (_sldhActive) return true;
            if (_shuiLuDaHuiGrain == null) return false;
            try
            {
                var result = Json.SafeDeserialize<ShuiLuDaHuiCheckResult>(await _shuiLuDaHuiGrain.CheckTeamActive(_teamId));
                if (string.IsNullOrEmpty(result.error))
                {
                    _sldhActive = true;
                    _sldhGroup = result.group;
                    _sldhStateLastChecked = result.state;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError($"检查水路状态[{ex.Message}][{ex.StackTrace}]");
                return false;
            }
            _sldhActive = false;
            _sldhGroup = 0;
            _sldhStateLastChecked = -1;
            return false;
        }
        private async ValueTask<bool> IsJoinedDaLuanDou()
        {
            if (!_isActive) return true;
            if (_dldActive) return true;
            if (_daLuanDouGrain == null) return false;
            try
            {
                var result = Json.SafeDeserialize<ShuiLuDaHuiCheckResult>(await _daLuanDouGrain.CheckTeamActive(_teamId));
                if (string.IsNullOrEmpty(result.error))
                {
                    _dldActive = true;
                    _dldGroup = result.group;
                    _dldStateLastChecked = result.state;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError($"检查大乱斗状态[{ex.Message}][{ex.StackTrace}]");
                return false;
            }
            _dldActive = false;
            _dldGroup = 0;
            _dldStateLastChecked = -1;
            return false;
        }
        private async ValueTask<bool> IsJoinedWangZheZhiZhan()
        {
            if (!_isActive) return true;
            if (_wzzzActive) return true;
            if (_wangZheZhiZhanGrain == null) return false;
            try
            {
                var result = Json.SafeDeserialize<ShuiLuDaHuiCheckResult>(await _wangZheZhiZhanGrain.CheckTeamActive(_teamId));
                if (string.IsNullOrEmpty(result.error))
                {
                    _wzzzActive = true;
                    _wzzzGroup = result.group;
                    _wzzzStateLastChecked = result.state;
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogError($"检查王者之战状态[{ex.Message}][{ex.StackTrace}]");
                return false;
            }
            _wzzzActive = false;
            _wzzzGroup = 0;
            _wzzzStateLastChecked = -1;
            return false;
        }

        private void SendPacket(uint roleId, GameCmd command, IMessage msg)
        {
            if (!_isActive) return;
            _playerGrains.TryGetValue(roleId, out var grain);
            SendPacket(grain, command, msg);
        }

        private void SendPacket(IPlayerGrain grain, GameCmd command, IMessage msg)
        {
            if (!_isActive) return;
            if (grain == null) return;
            _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(command, msg)));
        }

        // 检查多人日常任务是否有队员已完成
        public async ValueTask<string> CheckDailyTaskCompleted(uint group)
        {
            if (!_isActive) return "尚未激活";
            string slist = "";
            foreach (var tod in _queue)
            {
                if (tod is not { Type: TeamObjectType.Player }) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    if (await grain.IsDailyTaskCompleted(group))
                    {
                        slist += slist.Length == 0 ? tod.Name : "，" + tod.Name;
                    }
                }
            }
            return slist;
        }

        // 检查多人副本任务是否有队员已完成
        public async ValueTask<string> CheckInstanceTaskCompleted(uint taskId)
        {
            if (!_isActive) return "尚未激活";
            string slist = "";
            foreach (var tod in _queue)
            {
                if (tod is not { Type: TeamObjectType.Player }) continue;
                _playerGrains.TryGetValue(tod.DbId, out var grain);
                if (grain != null)
                {
                    if (await grain.IsInstanceTaskCompleted(taskId))
                    {
                        slist += slist.Length == 0 ? tod.Name : "，" + tod.Name;
                    }
                }
            }
            return slist;
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"队伍[{_serverId}][{_teamId}][{_sectId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"队伍[{_serverId}][{_teamId}][{_sectId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"队伍[{_serverId}][{_teamId}][{_sectId}]:{msg}");
        }
    }
}