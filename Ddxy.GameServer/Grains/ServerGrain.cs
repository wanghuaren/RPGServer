using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Orleans;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GrainInterfaces.Core;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Util;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Logic.Mail;
using Ddxy.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    public class GmNoticeData
    {
        public string msg { get; set; }
        public uint times { get; set; }
    }
    [CollectionAgeLimit(AlwaysActive = true)]
    public class ServerGrain : Grain, IServerGrain
    {
        private ILogger<ServerGrain> _logger;
        private readonly OrleansOptions _options;
        private Packet _packet;

        private bool _isActive;
        private uint _serverId;
        private uint _createTime;

        private IDisposable _updateTicker;
        private uint _updateCnt;

        // onlyId生成器
        private IdGen<byte> _onlyIdGen;

        // teamId生成器
        private IdGen<TeamTarget> _teamIdGen;

        // 帮派根据contrib进行排序, key是contrib， value是id
        private Dictionary<uint, SectData> _sects;
        private List<SectData> _sectRank;
        private bool _sectRankDirty;

        // 存储该区当前激活的所有玩家, 注意key是roleId, value表示是否在线
        private Dictionary<uint, bool> _players;

        // 所有的队伍，按target分类
        private Dictionary<TeamTarget, List<TeamData>> _teams;

        // 所有MapGrain
        private Dictionary<uint, IMapGrain> _maps;

        // 存储所有的Npc, key是onlyId
        private Dictionary<uint, MapObjectData> _npcs;

        // 存储所有的, key是cfgId
        private Dictionary<uint, MapObjectData> _systemNpcs;

        // 存储每个玩家动态创建的Npc, 需要在玩家退出server的时候清空, key是RoleId, value是onlyId集合
        private Dictionary<uint, List<uint>> _playerNpcs;

        // 存储每个队伍动态创建的Npc, 队伍在解散的时候清理, key是teamId, value是onlyId集合
        private Dictionary<uint, List<uint>> _teamNpcs;

        // 所有全服邮件
        private List<Mail> _mails;

        private IGlobalGrain _globalGrain;
        private IMallGrain _mallGrain;

        private IShuiLuDaHuiGrain _shuiLuDaHuiGrain;

        private IWangZheZhiZhanGrain _wangZheZhiZhanGrain;

        private ISinglePkGrain _singlePkGrain;

        private IDaLuanDouGrain _daLuanDouGrain;

        private IZhenBuKuiGrain _zhenBuKuiGrain;

        // private ITianJiangLingHouGrain _tianJiangLingHouGrain;
        private IDiShaXingGrain _diShaXingGrain;
        private IKuLouWangGrain _kuLouWangGrain;
        private IJinChanSongBaoGrain _jinChanSongBaoGrain;
        private IEagleGrain _eagleGrain;
        private IHcPkGrain _hcPkGrain;
        private ISectWarGrain _sectWarGrain;
        // 神兽降临
        private IShenShouJiangLinGrain _shenShouJiangLinGrain;
        // 红包服务
        private IRedGrain _redGrain;
        // 转盘
        private LuckyDrawChest _luckyDrawChest;
        // 转盘--宝箱下次生成倒计时
        private int _luckyDrawChestNextGenTs;
        // 转盘--免费次数重置倒计时
        private int _luckyDrawFreeResetTs;
        // 1天秒数
        private int _oneDayDuration = 1 * 24 * 60 * 60;
        // 1周秒数
        private int _oneWeekDuration = 7 * 24 * 60 * 60;
        // GM广播消息
        private Queue<GmNoticeData> _gmNoticeQueue;
        private GmNoticeData _gmNoticeCurrent;
        private int _gmNoticeNextCD = 10;
        // 限时充值排行榜
        private int _limitChargeRankCountdown = -1;
        // 限时等级排行榜
        private int _limitLevelRankCountdown = -1;
        // 聊天记录
        private List<ChatMsgEntity> _chatMsgList;
        private uint _chatMsgLastExcuteTimeout = 60;
        // 假铃铛
        private uint _fakeBellDelay = 0;
        private int _fakeBellMsgIndex = 0;

        private bool _isShutDownReq;

        public ServerGrain(ILogger<ServerGrain> logger, IOptions<OrleansOptions> options)
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
            if (!_isActive) return Task.CompletedTask;
                _isActive = false;
            return Shutdown(false);
        }

        public async Task Startup()
        {
            if (_isShutDownReq) return;
            if (_isActive) return;
            _isActive = true;
            LogInfo("开始激活...");

            try
            {
                // 查询数据，检查状态
                var sentity = await DbService.Sql.Queryable<ServerEntity>().Where(it => it.Id == _serverId).FirstAsync();
                if (sentity == null)
                {
                    _isActive = false;
                    LogError("不存在");
                    DeactivateOnIdle();
                    return;
                }
                if (sentity.Status != ServerStatus.Normal)
                {
                    _isActive = false;
                    LogError("非正常状态");
                    DeactivateOnIdle();
                    return;
                }

                _packet = new Packet(GetStreamProvider(_options.SmsProvider).GetStream<NotifyMessage>(Guid.Empty, _options.StreamNameSpace));
                _createTime = sentity.CreateTime;

                _onlyIdGen = new IdGen<byte>(5000);
                _teamIdGen = new IdGen<TeamTarget>(1000);

                _players = new Dictionary<uint, bool>();

                _teams = new Dictionary<TeamTarget, List<TeamData>>(20);
                _sects = new Dictionary<uint, SectData>(500);
                _sectRank = new List<SectData>(500);

                _maps = new Dictionary<uint, IMapGrain>(ConfigService.Maps.Count);
                _npcs = new Dictionary<uint, MapObjectData>(2000);
                _systemNpcs = new Dictionary<uint, MapObjectData>(1000);
                _playerNpcs = new Dictionary<uint, List<uint>>(1000);
                _teamNpcs = new Dictionary<uint, List<uint>>(1000);

                _mails = new List<Mail>(10);

                _globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
                _mallGrain = GrainFactory.GetGrain<IMallGrain>(_serverId);
                _shuiLuDaHuiGrain = GrainFactory.GetGrain<IShuiLuDaHuiGrain>(_serverId);
                _wangZheZhiZhanGrain = GrainFactory.GetGrain<IWangZheZhiZhanGrain>(_serverId);
                _singlePkGrain = GrainFactory.GetGrain<ISinglePkGrain>(_serverId);
                _daLuanDouGrain = GrainFactory.GetGrain<IDaLuanDouGrain>(_serverId);
                _zhenBuKuiGrain = GrainFactory.GetGrain<IZhenBuKuiGrain>(_serverId);
                // _tianJiangLingHouGrain = GrainFactory.GetGrain<ITianJiangLingHouGrain>(_serverId);
                _diShaXingGrain = GrainFactory.GetGrain<IDiShaXingGrain>(_serverId);
                _kuLouWangGrain = GrainFactory.GetGrain<IKuLouWangGrain>(_serverId);
                _jinChanSongBaoGrain = GrainFactory.GetGrain<IJinChanSongBaoGrain>(_serverId);
                _eagleGrain = GrainFactory.GetGrain<IEagleGrain>(_serverId);
                _redGrain = GrainFactory.GetGrain<IRedGrain>(_serverId);
                _hcPkGrain = GrainFactory.GetGrain<IHcPkGrain>(_serverId);
                _sectWarGrain = GrainFactory.GetGrain<ISectWarGrain>(_serverId);
                // 神兽降临
                _shenShouJiangLinGrain = GrainFactory.GetGrain<IShenShouJiangLinGrain>(_serverId);
                // 转盘
                _luckyDrawChest = new LuckyDrawChest();
                RDLDChest chestCfg = null; // = await RedisService.GetLuckyDrawChest(_serverId);
                var now = TimeUtil.TimeStamp;
                var escaped = (int)(now - (chestCfg != null ? chestCfg.createTime : now));
                if (escaped <= 0 || escaped >= _oneWeekDuration)
                {
                    _luckyDrawChestNextGenTs = 0;
                    await CheckLuckyDrawChestNextGen();
                }
                else
                {
                    _luckyDrawChestNextGenTs = _oneWeekDuration - escaped;
                    LoadLuckyDrawChest(chestCfg);
                }
                var lastTs = await RedisService.GetLuckyDrawFreeLastReset();
                escaped = (int)(now - (lastTs == 0 ? now : lastTs));
                if (escaped <= 0 || escaped >= _oneDayDuration)
                {
                    _luckyDrawFreeResetTs = 0;
                    await CheckLuckyDrawFreeReset();
                }
                else
                {
                    _luckyDrawFreeResetTs = _oneDayDuration - escaped;
                }
                var ts = TimeSpan.FromSeconds(_luckyDrawChestNextGenTs);
                if (chestCfg != null)
                {
                    LogDebug($"转盘宝箱 上次重置时间:{chestCfg.createTime}");
                }
                else
                {
                    LogDebug($"转盘宝箱 新创建");
                }
                LogDebug($"转盘宝箱 重置还剩{_luckyDrawChestNextGenTs}秒，{ts.Days}天{ts.Hours}小时{ts.Minutes}分{ts.Seconds}秒");
                ts = TimeSpan.FromSeconds(_luckyDrawFreeResetTs);
                if (lastTs != 0)
                {
                    LogDebug($"转盘免费 上次重置时间:{lastTs}");
                }
                else
                {
                    LogDebug($"转盘免费 新创建");
                }
                LogDebug($"转盘免费 重置还剩{_luckyDrawFreeResetTs}秒，{ts.Days}天{ts.Hours}小时{ts.Minutes}分{ts.Seconds}秒");
                // GM广播消息
                _gmNoticeQueue = new();
                _gmNoticeCurrent = null;
                _gmNoticeNextCD = 10;
                // 限时充值排行榜
                var start = await RedisService.GetLimitChargeStartTimestamp(_serverId);
                if (start == 0)
                {
                    //如果限时排行榜没有开启，检查限时排行榜的开启条件
                    if (now > _createTime && now < _createTime + 7*24*60*60)
                    {
                        await GmSetLimitChargeRankTimestamp(_createTime, _createTime + 7 * 24 * 60 * 60, false);
                    }
                }
                var end = await RedisService.GetLimitChargeEndTimestamp(_serverId);
                if (end >= 0 && now > end)
                {
                    //如果限时充值榜到期，结算排行榜
                    var list = await RedisService.SettleRoleLimitChargeRank(_serverId);
                    foreach (var (roleId, mailId) in list) {
                        // 如果在线, 就推送
                        var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
                        var active = await globalGrain.CheckPlayer(roleId);
                        if (active)
                        {
                            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                            _ = playerGrain.OnRecvMail(mailId);
                        }
                    }
                }
                //if (end >= 0)
                //{
                //    _limitChargeRankCountdown = (int)(end + GameDefine.LimitChargeIconDelay - now);
                //    if (_limitChargeRankCountdown <= 0)
                //    {
                //        await CheckLimitChargeCount(true);
                //    }
                //}
                //else
                //{
                //    _limitChargeRankCountdown = -1;
                //}
                // 限时等级排行榜
                var start1 = await RedisService.GetLimitLevelStartTimestamp(_serverId);
                if (start1 == 0)
                {
                    //如果限时排行榜没有开启，检查限时排行榜的开启条件
                    if (now > _createTime && now < _createTime + 7 * 24 * 60 * 60)
                    {
                        await GmSetLimitLevelRankTimestamp(_createTime, _createTime + 7 * 24 * 60 * 60, true);
                    }
                }
                var end1 = await RedisService.GetLimitLevelEndTimestamp(_serverId);
                if (end1 >= 0 && now > end1)
                {
                    //如果限时等级榜到期，结算排行榜
                    var list = await RedisService.SettleRoleLimitLevelRank(_serverId);
                    foreach (var (roleId, mailId) in list) {
                        // 如果在线, 就推送
                        var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
                        var active = await globalGrain.CheckPlayer(roleId);
                        if (active)
                        {
                            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                            _ = playerGrain.OnRecvMail(mailId);
                        }
                    }
                }
                //if (end1 >= 0)
                //{
                //    _limitLevelRankCountdown = (int)(end1 + GameDefine.LimitLevelIconDelay - now);
                //    if (_limitLevelRankCountdown <= 0)
                //    {
                //        await CheckLimitLevelCount(true);
                //    }
                //}
                //else
                //{
                //    _limitLevelRankCountdown = -1;
                //}
                // 聊天记录
                await ExcuteChatMsgBulkInsert();

                // 更新服务
                await _globalGrain.UpdateServer(_serverId, _players.Count);

                LogDebug("初始化地图...");
                // 创建所有地图上的固定Npc
                foreach (var (mapId, mapCfg) in ConfigService.Maps)
                {
                    var mapGrain = GrainFactory.GetGrain<IMapGrain>($"{_serverId}_{mapId}");
                    _maps.Add(mapId, mapGrain);
                    await mapGrain.StartUp();

                    // 生成系统Npc
                    if (mapCfg.Npcs is { Count: > 0 })
                    {
                        foreach (var npcId in mapCfg.Npcs)
                        {
                            ConfigService.Npcs.TryGetValue(npcId, out var npcCfg);
                            if (npcCfg == null)
                            {
                                LogError($"地图[{mapId}]自动创建NPC[{npcId}]出错，NPC配置不存在！");
                                continue;
                            }

                            await CreateNpc(mapId, npcCfg.AutoCreate.X, npcCfg.AutoCreate.Y, npcId);
                        }
                    }
                }

                LogDebug("初始化帮派...");
                await ReloadSects();

                LogDebug("初始化帮战...");
                await _sectWarGrain.StartUp();

                LogDebug("初始化皇城PK...");
                await _hcPkGrain.StartUp();

                LogDebug("初始化水陆大会...");
                await _shuiLuDaHuiGrain.StartUp();

                LogDebug("初始化王者之战...");
                await _wangZheZhiZhanGrain.StartUp();

                LogDebug("初始化比武大会...");
                await _singlePkGrain.StartUp();

                LogDebug("初始化大乱斗...");
                await _daLuanDouGrain.StartUp();

                // LogDebug("初始化天降灵猴...");
                // await _tianJiangLingHouGrain.StartUp();

                LogDebug("初始化神兽降临...");
                await _shenShouJiangLinGrain.StartUp();

                LogDebug("初始化地煞星...");
                await _diShaXingGrain.StartUp();

                LogDebug("初始化骷髅王...");
                await _kuLouWangGrain.StartUp();

                LogDebug("初始化金蟾送宝...");
                await _jinChanSongBaoGrain.StartUp();

                LogDebug("初始化金翅大鹏...");
                await _eagleGrain.StartUp();

                LogDebug("初始化甄不亏...");
                await _zhenBuKuiGrain.StartUp();

                LogDebug("初始化摆摊...");
                await _mallGrain.StartUp();

                LogDebug("初始化邮件...");
                await ReloadMails();

                LogDebug("初始化红包服务...");
                await _redGrain.StartUp();

                _updateTicker?.Dispose();
                _updateTicker = RegisterTimer(OnUpdate, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

                _isActive = true;
                LogInfo("激活成功");
            }
            catch (Exception ex)
            {
                _isActive = false;
                LogError($"激活失败[{ex.Message}]");
                DeactivateOnIdle();
            }
        }

        /// <summary>
        /// 停服
        /// </summary>
        public async Task Shutdown(bool manually = true)
        {
            // 停计时器
            _updateTicker?.Dispose();
            _updateTicker = null;
            if (manually)
            {
                if (_isShutDownReq) return;
                _isShutDownReq = true;
                try
                {
                    LogInfo("开始注销...");
                    // 立即移除, 防止二次进入，尤其是断线的连接立即重连
                    if (_globalGrain != null)
                    {
                        await _globalGrain.RemoveServer(_serverId);
                    }

                    LogDebug("开始停服...");

                    // 先让该区服下的所有玩家离线
                    LogDebug("销毁玩家...");
                    var tasks = new List<Task>();
                    foreach (var rid in _players.Keys.ToList())
                    {
                        var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                        tasks.Add(grain.Shutdown());
                    }

                    await Task.WhenAll(tasks);

                    await CheckAndDeactiveOthers();
                }
                catch (Exception ex)
                {
                    LogError($"停服出错:[{ex.Message}]");
                }
            }
            else
            {
                LogInfo("开始注销...");
                 // 停计时器
                _updateTicker?.Dispose();
                _updateTicker = null;

                if (_globalGrain != null)
                {
                    _ = _globalGrain.RemoveServer(_serverId);
                }

                _onlyIdGen?.Dispose();
                _onlyIdGen = null;
                _teamIdGen?.Dispose();
                _teamIdGen = null;

                _players?.Clear();
                _players = null;

                _maps?.Clear();
                _maps = null;

                _npcs?.Clear();
                _npcs = null;

                _systemNpcs?.Clear();
                _systemNpcs = null;

                _playerNpcs?.Clear();
                _playerNpcs = null;

                _teamNpcs?.Clear();
                _teamNpcs = null;

                _sects?.Clear();
                _sects = null;
                _sectRank?.Clear();
                _sectRank = null;

                _mails?.Clear();
                _mails = null;

                _shuiLuDaHuiGrain = null;
                _wangZheZhiZhanGrain = null;
                _singlePkGrain = null;
                _daLuanDouGrain = null;
                _zhenBuKuiGrain = null;
                // _tianJiangLingHouGrain = null;
                _mallGrain = null;
                _diShaXingGrain = null;
                _kuLouWangGrain = null;
                _jinChanSongBaoGrain = null;
                _eagleGrain = null;
                _hcPkGrain = null;
                // 神兽降临
                _shenShouJiangLinGrain = null;
                // 红包服务
                _redGrain = null;
                // 转盘
                _luckyDrawChest = null;
                // GM广播消息
                _gmNoticeQueue?.Clear();
                _gmNoticeQueue = null;
                _gmNoticeCurrent = null;
                _gmNoticeNextCD = 10;
                // 聊天记录
                await ExcuteChatMsgBulkInsert(true);
                _chatMsgList?.Clear();
                _chatMsgList = null;

                _packet = null;
                LogDebug("注销成功");
            }
        }

        public ValueTask<bool> CheckActive()
        {
            return new(_isActive);
        }

        /// <summary>
        /// 玩家进入区服, 分配onlyId
        /// </summary>
        public ValueTask<uint> Enter(uint roleId)
        {
            if (!_isActive) return new((uint)0);
            if (_players == null) return new ValueTask<uint>(0);
            _players[roleId] = false;
            _ = _globalGrain.UpdateServer(_serverId, _players.Count);
            var onlyId = _onlyIdGen.Gain();
            return new ValueTask<uint>(onlyId);
        }

        /// <summary>
        /// 玩家离开区服, 回收onlyId，移除玩家创建的动态Npc
        /// </summary>
        public async Task Exit(uint onlyId, uint roleId)
        {
            _onlyIdGen?.Recycle(onlyId);
            _players?.Remove(roleId);
            _ = _globalGrain?.UpdateServer(_serverId, _players.Count);

            // 玩家销毁时候需要把他创建的Npc全部移除
            if (_playerNpcs != null)
            {
                _playerNpcs.Remove(roleId, out var list);
                if (list is { Count: > 0 })
                {
                    foreach (var x in list)
                    {
                        await DeleteNpc(x);
                    }
                }
            }

            await CheckAndDeactiveOthers();

            await Task.CompletedTask;
        }

        public async Task Online(uint roleId)
        {
            if (!_isActive) return;
            if (_players.ContainsKey(roleId))
                _players[roleId] = true;
            await Task.CompletedTask;
        }

        public async Task Offline(uint roleId)
        {
            if (!_isActive) return;
            if (_players.ContainsKey(roleId))
                _players[roleId] = false;
            await Task.CompletedTask;
        }

        public ValueTask<int> GetOnlineNum()
        {
            if (!_isActive) return new(0);
            var onlineNum = _players.Count(p => p.Value);
            return new ValueTask<int>(onlineNum);
        }

        public ValueTask<bool> CheckOnline(uint roleId)
        {
            if (!_isActive) return new (false);
            return new ValueTask<bool>(_players.ContainsKey(roleId));
        }

        public ValueTask<uint> CreateNpc(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return new(0);
            var req = CreateNpcRequest.Parser.ParseFrom(reqBytes.Value);
            return CreateNpc(req.MapId, req.MapX, req.MapY, req.CfgId, req.Owner);
        }

        public Task DeleteNpc(uint onlyId)
        {
            if (!_isActive) return Task.CompletedTask;
            _onlyIdGen.Recycle(onlyId);

            _npcs.Remove(onlyId, out var objData);
            if (objData != null)
            {
                _maps.TryGetValue(objData.MapId, out var mapGrain);
                if (mapGrain != null)
                {
                    // 记得从地图上移除
                    _ = mapGrain.Exit(onlyId);
                }
            }

            return Task.CompletedTask;
        }

        public async Task DeletePlayerNpc(uint onlyId, uint roleId)
        {
            if (!_isActive) return;
            _npcs.TryGetValue(onlyId, out var objData);
            if (objData == null) return;
            if (objData.Owner.Type == NpcOwnerType.Player && objData.Owner.Value == roleId)
            {
                await DeleteNpc(onlyId);

                // 从动态集合中删除
                _playerNpcs.TryGetValue(roleId, out var list);
                if (list != null)
                {
                    list.Remove(onlyId);
                    if (list.Count == 0) _playerNpcs.Remove(roleId);
                }
            }
        }

        public async Task DeleteTeamNpc(uint onlyId, uint teamId)
        {
            if (!_isActive) return;
            _npcs.TryGetValue(onlyId, out var objData);
            if (objData == null) return;
            if (objData.Owner.Type == NpcOwnerType.Team && objData.Owner.Value == teamId)
            {
                await DeleteNpc(onlyId);

                // 从动态集合中删除
                _teamNpcs.TryGetValue(teamId, out var list);
                if (list != null)
                {
                    list.Remove(onlyId);
                    if (list.Count == 0) _teamNpcs.Remove(teamId);
                }
            }
        }

        public Task<Immutable<byte[]>> FindNpc(uint onlyId)
        {
            if (!_isActive) return new(null);
            _npcs.TryGetValue(onlyId, out var objData);
            var res = new Immutable<byte[]>(Packet.Serialize(objData));
            return Task.FromResult(res);
        }

        public ValueTask<bool> ExistsNpc(uint onlyId)
        {
            if (!_isActive) return new(false);
            var ret = _npcs.ContainsKey(onlyId);
            return new ValueTask<bool>(ret);
        }

        public ValueTask<uint> FindCfgIdWithNpcOnlyId(uint onlyId)
        {
            if (!_isActive) return new(0);
            _npcs.TryGetValue(onlyId, out var objData);
            var cfgId = objData?.CfgId ?? 0;
            return new ValueTask<uint>(cfgId);
        }

        public ValueTask<uint> FindOnlyIdWithNpcCfgId(uint cfgId)
        {
            if (!_isActive) return new(0);
            _systemNpcs.TryGetValue(cfgId, out var mod);
            var onlyId = mod?.OnlyId ?? 0;
            return new ValueTask<uint>(onlyId);
        }

        // 发送PB消息给所有的在线玩家
        public async Task Broadcast(Immutable<byte[]> payload)
        {
            if (!_isActive) return;
            var bytes = payload.Value;
            foreach (var (k, v) in _players)
            {
                if (v)
                {
                    _ = _packet.SendPacket(k, bytes);
                }
            }

            await Task.CompletedTask;
        }

        public ValueTask<uint> CreateTeam(uint teamTarget)
        {
            if (!_isActive) return new(0);
            var teamId = _teamIdGen.Gain((TeamTarget) teamTarget);
            return new ValueTask<uint>(teamId);
        }

        public async Task DeleteTeam(uint teamId)
        {
            if (!_isActive) return;
            // 回收队伍id
            _teamIdGen.Recycle(teamId, out var target);
            // 移除组队大厅中的数据
            _teams.TryGetValue(target, out var list);
            if (list is {Count: > 0})
            {
                var idx = list.FindIndex(p => p != null && p.Id == teamId);
                if (idx >= 0) list[idx] = null;
            }

            // 移除队伍产生的所有Npc
            _teamNpcs.Remove(teamId, out var keyList);
            if (keyList is {Count: > 0})
            {
                foreach (var onlyId in keyList)
                {
                    if (onlyId > 0)
                    {
                        await DeleteNpc(onlyId);
                    }
                }
            }

            await Task.CompletedTask;
        }

        public async Task UpdateTeam(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return;
            var req = TeamData.Parser.ParseFrom(reqBytes.Value);
            // 检查id是否注册
            if (!_teamIdGen.Exists(req.Id)) return;
            // 分类的集合
            _teams.TryGetValue(req.Target, out var list);
            if (list == null)
            {
                list = new List<TeamData>(200);
                _teams[req.Target] = list;
            }

            var idx = list.FindIndex(p => p != null && p.Id == req.Id);
            if (idx >= 0)
            {
                list[idx] = req;
            }
            else
            {
                // 查找一个null值
                var idx2 = list.FindIndex(p => p == null);
                if (idx2 >= 0) list[idx2] = req;
                else list.Add(req);
            }

            await Task.CompletedTask;
        }

        public async Task<Immutable<byte[]>> QueryTeam(uint teamId)
        {
            if (!_isActive) return new(null);
            await Task.CompletedTask;

            TeamData td = null;
            foreach (var list in _teams.Values)
            {
                td = list.Find(p => p != null && p.Id == teamId);
                if (td != null) break;
            }

            return new Immutable<byte[]>(Packet.Serialize(td));
        }

        /// <summary>
        /// 查询指定目标的队伍, 如果自己在队伍中, 传入自己的队伍id, 会过滤掉该队伍
        /// </summary>
        public async Task<Immutable<byte[]>> QueryTeams(byte teamTarget, int pageIndex, uint teamId)
        {
            if (!_isActive) return new(null);
            // if (pageIndex < 1) pageIndex = 1;
            var resp = new S2C_TeamList {Target = (TeamTarget) teamTarget};

            _teams.TryGetValue(resp.Target, out var list);
            if (list != null)
            {
                // 先筛选条件, 帮战Team不能被搜索, 自己的队伍也查不到
                var query = list.Where(p =>
                    p != null && p.Target != TeamTarget.SectWar && (teamId == 0 || p.Id != teamId)).ToList();
                resp.Total = (uint) query.Count;

                if (query.Count > 0)
                {
                    var rnd = new Random();
                    while (resp.List.Count < GameDefine.TeamListPageSize)
                    {
                        if (query.Count == 0) break;
                        var idx = rnd.Next(0, query.Count);
                        resp.List.Add(query[idx]);
                        // 移除, 防止重复
                        query.RemoveAt(idx);
                    }
                }
            }

            await Task.CompletedTask;
            return new Immutable<byte[]>(Packet.Serialize(resp));
        }

        public ValueTask<bool> ExistsTeam(uint teamId)
        {
            if (!_isActive) return new(false);
            var ret = _teamIdGen.Exists(teamId);
            return new ValueTask<bool>(ret);
        }

        public async Task Reload()
        {
            if (!_isActive) return;
            // 重新加载帮派
            await ReloadSects();
            // 重新加载邮件
            await ReloadMails();
            // 水陆大会重载水陆战神
            await _shuiLuDaHuiGrain.Reload();
            // 大乱斗重载PK战神
            await _daLuanDouGrain.Reload();
            // 王者之战重载王者战神
            await _wangZheZhiZhanGrain.Reload();
        }

        public async Task ReloadSects()
        {
            if (!_isActive) return;
            var tasks = new List<Task>();
            var sects = await DbService.QuerySects(_serverId);
            foreach (var sid in sects)
            {
                tasks.Add(GrainFactory.GetGrain<ISectGrain>(sid).StartUp());
            }

            await Task.WhenAll(tasks);
        }

        public async Task ReloadMails()
        {
            if (!_isActive) return;
            var now = TimeUtil.TimeStamp;
            var entities = await DbService.QuerySystemMails(_serverId);

            _mails.Clear();
            foreach (var entity in entities)
            {
                if (now >= entity.ExpireTime)
                {
                    await DbService.DeleteEntity<MailEntity>(entity.Id);
                    continue;
                }

                _mails.Add(new Mail(entity));
            }
        }

        public ValueTask<bool> ExistsSect(uint sectId)
        {
            if (!_isActive) return new(false);
            var ret = _sects.ContainsKey(sectId);
            return new ValueTask<bool>(ret);
        }

        public Task UpdateSect(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return Task.CompletedTask;
            var req = SectData.Parser.ParseFrom(reqBytes.Value);
            _sects.TryGetValue(req.Id, out var oldSect);
            if (oldSect == null || oldSect.Contrib != req.Contrib)
            {
                // 需要更新排序
                _sects[req.Id] = req;
            }
            else
            {
                // 不需要更新排序
                _sects[req.Id] = req;
            }

            _sectRankDirty = true;
            return Task.CompletedTask;
        }

        public async Task DeleteSect(uint id)
        {
            if (!_isActive) return;
            _sects.Remove(id, out var oldSect);
            if (oldSect == null) return;
            _sectRankDirty = true;
            await Task.CompletedTask;
        }

        public Task<Immutable<byte[]>> QuerySects(string search, int pageIndex)
        {
            if (!_isActive) return new(null);
            if (pageIndex < 1) pageIndex = 1;
            SortSectList();

            var resp = new S2C_SectList {Search = search, PageIndex = (uint) pageIndex};
            if (string.IsNullOrWhiteSpace(search))
            {
                // 分页
                var res = _sectRank.Skip((pageIndex - 1) * GameDefine.SectListPageSize)
                    .Take(GameDefine.SectListPageSize);
                resp.Total = (uint) _sectRank.Count;
                resp.List.AddRange(res);
            }
            else
            {
                // 判断是否为数字
                search = search.Trim();
                uint.TryParse(search, out var id);
                // 考虑到sect的起始id
                if (id < 10000) id = 0;

                if (id == 0)
                {
                    var query = _sectRank.Where(p => p.Name.Contains(search)).ToList();
                    var res = query.Skip((pageIndex - 1) * GameDefine.SectListPageSize)
                        .Take(GameDefine.SectListPageSize);
                    resp.Total = (uint) query.Count;
                    resp.List.AddRange(res);
                }
                else
                {
                    // 根据id精准匹配
                    _sects.TryGetValue(id, out var sd);
                    if (sd != null)
                    {
                        resp.Total = 1;
                        resp.List.Add(sd);
                    }
                }
            }

            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(resp)));
        }

        public ValueTask<int> QuerySectNum()
        {
            if (!_isActive) return new(0);
            return new ValueTask<int>(_sects.Count);
        }

        public async ValueTask<uint> FindRandomSect()
        {
            if (!_isActive) return 0;
            // 前10名中随机选择
            await Task.CompletedTask;

            var keys = _sects.Keys.ToList();
            if (keys.Count == 0) return 0;
            return keys[new Random().Next(0, keys.Count)];
        }

        public Task<Immutable<byte[]>> GetSectRank(int pageIndex, int pageSize = 0)
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            if (pageIndex < 1) pageIndex = 1;
            if (pageSize == 0) pageSize = GameDefine.SectListPageSize;

            var resp = new S2C_RankSect {PageIndex = (uint) pageIndex};

            SortSectList();
            var query = _sectRank.Skip((pageIndex - 1) * pageSize)
                .Take(pageSize).Select(p => new SectRankMemberData
                {
                    Id = p.Id,
                    Name = p.Name,
                    OwnerName = p.OwnerName,
                    MemberNum = p.Total,
                    Contrib = p.Contrib
                });
            resp.List.AddRange(query);

            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(resp)));
        }

        public Task<Immutable<byte[]>> QuerySectsForSectWar()
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            // 帮派创建时间超过48小时
            var resp = new QuerySectsForSectWarResponse
            {
                List = {_sects.Values}
            };
            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(resp)));
        }

        public Task<Immutable<byte[]>> QueryMails()
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            var resp = new S2C_MailList();
            foreach (var mail in _mails)
            {
                resp.List.Add(mail.BuildPbData());
            }

            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(resp)));
        }

        public async ValueTask<bool> CheckMail(uint id)
        {
            if (!_isActive) return false;
            var idx = _mails.FindIndex(p => p.Id == id);
            if (idx < 0) return false;
            var mail = _mails[idx];
            if (mail == null) return false;
            if (mail.Expire)
            {
                _mails.RemoveAt(idx);
                await DbService.DeleteEntity<MailEntity>(mail.Id);
                mail.Dispose();
                return false;
            }

            return true;
        }

        public async Task OnMailAdd(uint id)
        {
            if (!_isActive) return;
            var entity = await DbService.Sql.Queryable<MailEntity>()
                .Where(it => it.Id == id)
                .FirstAsync();
            if (entity == null) return;
            var mail = new Mail(entity);
            _mails.Add(mail);

            // 通知所有在线的用户
            var bytes = new Immutable<byte[]>(Packet.Serialize(mail.BuildPbData()));
            foreach (var (k, v) in _players)
            {
                if (v)
                {
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(k);
                    _ = grain.OnRecvMail(bytes);
                }
            }
        }

        public async Task OnMailDel(uint id)
        {
            if (!_isActive) return;
            var idx = _mails.FindIndex(p => p.Id == id);
            if (idx < 0) return;
            var mail = _mails[idx];
            _mails.RemoveAt(idx);

            // 通知所有在线的用户
            var bytes = new Immutable<byte[]>(Packet.Serialize(mail.BuildPbData()));
            foreach (var (k, v) in _players)
            {
                if (v)
                {
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(k);
                    _ = grain.OnDelMail(bytes);
                }
            }

            await Task.CompletedTask;
        }

        // 记录聊天信息
        public async Task RecordChatMsg(uint fromRoleId, uint toRoleId, byte msgType, string msg, uint sendTime)
        {
            if (!_isActive) return;
            _chatMsgList.Add(new ChatMsgEntity()
            {
                FromRid = fromRoleId,
                ToRid = toRoleId,
                MsgType = msgType,
                Msg = msg,
                SendTime = sendTime,
            });
            if (_chatMsgList.Count >= 1000)
            {
                await ExcuteChatMsgBulkInsert();
            }
        }

        private async Task ExcuteChatMsgBulkInsert(bool shutdown = false)
        {
            if (_chatMsgList != null)
            {
                if (_chatMsgList.Count > 0)
                {
                    var countList = _chatMsgList.Count;
                    var countInsert = await DbService.InsertChatMsgBulk(_chatMsgList);
                    if (countInsert == countList)
                    {
                        LogDebug($"成功插入{countInsert}条聊天记录");
                    }
                    else
                    {
                        LogDebug($"成功插入{countInsert}条聊天记录, 失败{countList - countInsert}");
                    }
                    _chatMsgList.Clear();
                }
            }
            else
            {
                if (!shutdown)
                {
                    _chatMsgList = new();
                }
            }
            _chatMsgLastExcuteTimeout = 60;
        }

        public async Task OnShuiLuDaHuiNewSeason(uint season)
        {
            if (!_isActive) return;
            // 清空水路大会排行榜
            await RedisService.DelRoleSldhRank(_serverId);

            foreach (var rid in _players.Keys)
            {
                var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                _ = grain.OnShuiLuDaHuiNewSeason(season);
            }
        }
        public async Task OnDaLuanDouNewSeason(uint season)
        {
            if (!_isActive) return;
            // 清空大乱斗排行榜
            await RedisService.DelRoleDaLuanDouRank(_serverId);

            await Task.CompletedTask;
            foreach (var rid in _players.Keys)
            {
                var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                _ = grain.OnDaLuanDouNewSeason(season);
            }
        }
        public async Task OnWangZheZhiZhanNewSeason(uint season)
        {
            if (!_isActive) return;
            // 清空王者之战排行榜
            await RedisService.DelRoleWzzzRank(_serverId);

            foreach (var rid in _players.Keys)
            {
                var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                _ = grain.OnWangZheZhiZhanNewSeason(season);
            }
        }

        // 神兽降临
        public async Task OnShenShouJiangLinNewSeason(uint season)
        {
            if (!_isActive) return;
            foreach (var rid in _players.Keys)
            {
                var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                _ = grain.OnShenShouJiangLinNewSeason(season);
            }
            await Task.CompletedTask;
        }
        // GM广播消息
        public async Task GmBroadcastPalaceNotice(string msg, uint times)
        {
            if (!_isActive) return;
            _gmNoticeQueue.Enqueue(new GmNoticeData() { msg = msg, times = times });
            await Task.CompletedTask;
        }

        private async ValueTask<uint> CreateNpc(uint mapId, int mapX, int mapY, uint npcCfgId, NpcOwner owner = null)
        {
            if (!_isActive) return 0;
            _maps.TryGetValue(mapId, out var mapGrain);
            if (mapGrain == null) return 0;

            ConfigService.Npcs.TryGetValue(npcCfgId, out var npcCfg);
            if (npcCfg == null)
            {
                LogError($"地图[{mapId}]创建NPC[{npcCfgId}]出错，NPC配置不存在！");
                return 0;
            }

            owner ??= new NpcOwner {Type = NpcOwnerType.System};
            var objectData = new MapObjectData
            {
                OnlyId = _onlyIdGen.Gain(),
                Type = LivingThingType.Npc,
                CfgId = npcCfgId,
                Name = npcCfg.Name,
                MapId = mapId,
                MapX = new Int32Value {Value = mapX},
                MapY = new Int32Value {Value = mapY},
                Owner = owner
            };
            // 由于ServerGrain永远不会修改MapObjectData的数据, 而且Npc进入地图后也不会发生数据改变， 所以按Immutable的方式传入
            await mapGrain.Enter(new Immutable<byte[]>(Packet.Serialize(objectData)), 0, 0);

            // 存储到本地
            _npcs[objectData.OnlyId] = objectData;

            switch (owner.Type)
            {
                case NpcOwnerType.System:
                {
                    // 系统Npc需要存储, 任务系统需要查询OnlyId
                    // 用Add来确保不会重复, 如果有重复会引发异常
                    _systemNpcs.Add(objectData.CfgId, objectData);
                }
                    break;
                case NpcOwnerType.Player:
                {
                    // 玩家动态创建的npc需要记录, 玩家退出的时候需要删除这些临时npc
                    _playerNpcs.TryGetValue(owner.Value, out var list);
                    if (list == null)
                    {
                        list = new List<uint>(3);
                        _playerNpcs[owner.Value] = list;
                    }

                    list.Add(objectData.OnlyId);
                }
                    break;
                case NpcOwnerType.Team:
                {
                    // 队伍创建的npc需要记录
                    _teamNpcs.TryGetValue(owner.Value, out var list);
                    if (list == null)
                    {
                        list = new List<uint>(5);
                        _teamNpcs[owner.Value] = list;
                    }

                    list.Add(objectData.OnlyId);
                }
                    break;
            }

            return objectData.OnlyId;
        }

        // 转盘配置
        public Task<Immutable<byte[]>> QueryLuckyDrawChest()
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));

            var clist = ConfigService.LuckyDrawConfig.chestList;
            var rd = new Random();
            var chestHited = clist[rd.Next(clist.Count)];
            _luckyDrawChest.PetCfgId = chestHited.pet;
            _luckyDrawChest.ItemList.Clear();
            // 物品列表
            uint index = 0;
            foreach (var item in chestHited.items)
            {
                _luckyDrawChest.ItemList.Add(new LuckyDrawItem() { Index = index, CfgId = item.id, Num = item.num });
                index++;
            }
            _luckyDrawChest.FullPoint = ConfigService.LuckyDrawConfig.fullPoint;

            var res = new Immutable<byte[]>(Packet.Serialize(_luckyDrawChest));
            return Task.FromResult(res);
        }
        private async ValueTask<RDLDChest> GenLuckyDrawChest()
        {
            await Task.CompletedTask;
            return null;
            // var clist = ConfigService.LuckyDrawConfig.chestList;
            // var rd = new Random();
            // var cfg = new RDLDChest() { itemList = new() };
            // var chestHited = clist[DateTimeUtil.GetWeekNumber(DateTime.Now) % clist.Count];
            // // 宠物ID
            // cfg.pet = chestHited.pet;
            // var items = chestHited.items;
            // // 物品列表
            // for (int i = 0; i < 3 && i < items.Count; i++)
            // {
            //     var icfg = items[i];
            //     cfg.itemList.Add(new RDLDRewardItem()
            //     {
            //         id = icfg.id,
            //         num = icfg.num
            //     });
            // }
            // // 创建时间
            // var monday = DateTimeUtil.GetWeekDayStartTime(DayOfWeek.Monday, 0, 0, 0)
            //  - new DateTime(1970, 1, 1, 5, 0, 0, 0);
            // cfg.createTime = (uint)monday.TotalSeconds;
            // var ret = await RedisService.SetLuckyDrawChest(_serverId, cfg);
            // if (ret)
            // {
            //     LogDebug($"创建转盘宝箱成功:{Json.SafeSerialize(cfg)}");
            // }
            // else
            // {
            //     LogError($"创建转盘宝箱失败:{Json.SafeSerialize(cfg)}");
            // }
            // ret = await RedisService.DeleteLuckyDrawChestGot();
            // if (ret)
            // {
            //     LogDebug("清除转盘宝箱记录成功");
            // }
            // else
            // {
            //     LogError("清除转盘宝箱记录失败");
            // }
            // ret = await RedisService.DeleteRoleLuckyPointAll();
            // if (ret)
            // {
            //     LogDebug("清除转盘风雨值成功");
            // }
            // else
            // {
            //     LogError("清除转盘风雨值失败");
            // }
            // return cfg;
        }
        private void LoadLuckyDrawChest(RDLDChest cfg)
        {
            return;
            // // 满风雨值
            // _luckyDrawChest.FullPoint = ConfigService.LuckyDrawConfig.fullPoint;
            // // 宠物ID
            // _luckyDrawChest.PetCfgId = cfg.pet;
            // _luckyDrawChest.ItemList.Clear();
            // // 物品列表
            // uint index = 0;
            // foreach (var item in cfg.itemList)
            // {
            //     _luckyDrawChest.ItemList.Add(new LuckyDrawItem() { Index = index, CfgId = item.id, Num = item.num });
            //     index++;
            // }
            // var escapsed = (int)(TimeUtil.TimeStamp - cfg.createTime);
            // // 转盘--宝箱下次生成倒计时
            // _luckyDrawChestNextGenTs = _oneWeekDuration - escapsed;
        }
        private async Task CheckLuckyDrawChestNextGen()
        {
            await Task.CompletedTask;
            return;
            // // 检查是否应该重新生成转盘宝箱
            // _luckyDrawChestNextGenTs--;
            // if (_luckyDrawChestNextGenTs <= 0)
            // {
            //     LoadLuckyDrawChest(await GenLuckyDrawChest());
            // }
        }
        private async Task CheckLuckyDrawFreeReset()
        {
            // 检查是否应该重置玩家免费次数
            _luckyDrawFreeResetTs--;
            if (_luckyDrawFreeResetTs <= 0)
            {
                var now = DateTime.Now;
                var ts = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0) - new DateTime(1970, 1, 1, 5, 0, 0, 0);
                var ret = await RedisService.DeleteLuckyDrawChestFree();
                if (ret)
                {
                    LogDebug("清除转盘免费记录成功");
                }
                else
                {
                    LogError("清除转盘免费记录失败");
                }
                ret = await RedisService.SetLuckyDrawChestFreeLastReset((uint)ts.TotalSeconds);
                if (ret)
                {
                    LogDebug("设置转盘免费上次重置时间成功");
                }
                else
                {
                    LogError("设置转盘免费上次重置时间失败");
                }
                // FIXME: VIP领奖重置暂时做在免费转盘抽奖重置里
                ret = await RedisService.DeleteRoleVipGiftDaily();
                if (ret)
                {
                    LogDebug("清除VIP特权每日记录成功");
                }
                else
                {
                    LogError("清除VIP特权每日记录失败");
                }
                ret = await RedisService.DeleteCszlTimesDaily();
                if (ret)
                {
                    LogDebug("清除成神之路副本每日挑战次数成功");
                }
                else
                {
                    LogError("清除成神之路副本每日挑战次数失败");
                }
                // FIXME: 双倍经验重置暂时做在免费转盘抽奖重置里
                ret = await RedisService.DeleteRoleX2ExpLeft();
                if (ret)
                {
                    LogDebug("清除双倍经验剩余每日记录成功");
                }
                else
                {
                    LogError("清除双倍经验剩余每日记录失败");
                }
                // FIXME: 双倍经验重置暂时做在免费转盘抽奖重置里
                ret = await RedisService.DeleteRoleX2ExpCurrentGot();
                if (ret)
                {
                    LogDebug("清除已获双倍经验每日记录成功");
                }
                else
                {
                    LogError("清除已获双倍经验每日记录失败");
                }
                var escaped = (now - new DateTime(now.Year, now.Month, now.Day, 0, 0, 0)).TotalSeconds;
                _luckyDrawFreeResetTs = (int)(_oneDayDuration - escaped);
            }
        }

        // 对帮派进行排序
        private void SortSectList()
        {
            if (_sectRankDirty)
            {
                _sectRankDirty = false;
                _sectRank = _sects.Values
                    .OrderByDescending(p => p.Contrib)
                    .ThenByDescending(p => p.Total)
                    .ToList();
            }
        }

        private void CheckMails()
        {
            for (var i = _mails.Count - 1; i >= 0; i--)
            {
                if (_mails[i].Expire)
                {
                    // 邮件已过期
                    _ = DbService.DeleteEntity<MailEntity>(_mails[i].Id);
                    _mails[i].Dispose();
                    _mails.RemoveAt(i);
                }
            }
        }

        // 检查GM广播
        private async Task CheckGmNotice()
        {
            _gmNoticeNextCD++;
            if (_gmNoticeNextCD < 10) return;
            if (_gmNoticeCurrent == null)
            {
                if (_gmNoticeQueue.Count <= 0) return;
                _gmNoticeCurrent = _gmNoticeQueue.Dequeue();
            }
            // 还有内容广播
            if (_gmNoticeCurrent != null)
            {
                var notice = _gmNoticeCurrent;
                // 剩余次数
                _gmNoticeCurrent.times -= 1;
                // 广播完成
                if (_gmNoticeCurrent.times <= 0)
                {
                    _gmNoticeCurrent = null;
                    // 下次广播倒计时
                    _gmNoticeNextCD = (_gmNoticeQueue.Count > 0 ? 0 : 10);
                }
                else
                {
                    // 下次广播倒计时
                    _gmNoticeNextCD = 0;
                }
                // 广播1次
                await Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat
                {
                    Msg = new ChatMessage
                    {
                        Type = ChatMessageType.GmBell,
                        Msg = notice.msg,
                    }
                })));
            }
        }

        // 设置限时充值排行榜开始结束时间
        public async ValueTask<bool> GmSetLimitChargeRankTimestamp(uint start, uint end, bool clean)
        {
            if (!_isActive) return false;
            if (clean) await GmDelLimitChargeRank();
            var ret = await RedisService.SetLimitChargeStartEndTimestamp(_serverId, start, end);
            // 限时充值排行榜
            var now = TimeUtil.TimeStamp;
            if (end >= 0)
            {
                _limitChargeRankCountdown = (int)(end + GameDefine.LimitChargeIconDelay - now);
                if (_limitChargeRankCountdown <= 0)
                {
                    await CheckLimitChargeCount(true);
                }
            }
            else
            {
                _limitChargeRankCountdown = -1;
            }
            return ret;
        }

        // 清除当前限时充值排行榜
        public async ValueTask<bool> GmDelLimitChargeRank()
        {
            if (!_isActive) return false;
            var ret = await RedisService.DelLimitChargeRank(_serverId);
            for (int i = 0; i < ret.Count; i++)
            {
                if (i == 0)
                {
                    if (ret[i])
                    {
                        LogDebug("GM请求清除限时充值排行榜成功");
                    }
                    else
                    {
                        LogDebug("GM请求清除限时充值排行榜失败");
                    }
                }
                if (i == 1)
                {
                    if (ret[i])
                    {
                        LogDebug("GM请求清除限时充值排行榜开始时间成功");
                    }
                    else
                    {
                        LogDebug("GM请求清除限时充值排行榜开始时间失败");
                    }
                }
                if (i == 2)
                {
                    if (ret[i])
                    {
                        LogDebug("GM请求清除限时充值排行榜结束时间成功");
                    }
                    else
                    {
                        LogDebug("GM请求清除限时充值排行榜结束时间失败");
                    }
                }
            }
            return !ret.Contains(false);
        }

        // 检查限时充值排行榜
        private async Task CheckLimitChargeCount(bool force = false)
        {
            var end = await RedisService.GetLimitChargeEndTimestamp(_serverId);
            var now = TimeUtil.TimeStamp;
            if (end > 0 && now > end)
            {
                //如果限时充值榜到期，结算排行榜
                var list = await RedisService.SettleRoleLimitChargeRank(_serverId);
                foreach (var (roleId, mailId) in list) {
                    // 如果在线, 就推送
                    var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
                    var active = await globalGrain.CheckPlayer(roleId);
                    if (active)
                    {
                        var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                        _ = playerGrain.OnRecvMail(mailId);
                    }
                }
            }
            return;
            //if (_limitChargeRankCountdown > 0 || force)
            //{
            //    _limitChargeRankCountdown--;
            //    if (_limitChargeRankCountdown <= 0)
            //    {
            //        //在清理限时充值排行榜之前，需要确定前十名的奖励，并存储起来
            //        var ret = await RedisService.DelLimitChargeRank(_serverId);
            //        for (int i = 0; i < ret.Count; i++)
            //        {
            //            if (i == 0)
            //            {
            //                if (ret[i])
            //                {
            //                    LogDebug("清除限时充值排行榜成功");
            //                }
            //                else
            //                {
            //                    LogDebug("清除限时充值排行榜失败");
            //                }
            //            }
            //            if (i == 1)
            //            {
            //                if (ret[i])
            //                {
            //                    LogDebug("清除限时充值排行榜开始时间成功");
            //                }
            //                else
            //                {
            //                    LogDebug("清除限时充值排行榜开始时间失败");
            //                }
            //            }
            //            if (i == 2)
            //            {
            //                if (ret[i])
            //                {
            //                    LogDebug("清除限时充值排行榜结束时间成功");
            //                }
            //                else
            //                {
            //                    LogDebug("清除限时充值排行榜结束时间失败");
            //                }
            //            }
            //        }
            //        _limitChargeRankCountdown = -1;
            //    }
            //}
        }

        // 设置限时等级排行榜开始结束时间
        public async ValueTask<bool> GmSetLimitLevelRankTimestamp(uint start, uint end, bool clean)
        {
            if (!_isActive) return false;
            if (clean) await GmDelLimitLevelRank();
            var ret = await RedisService.SetLimitLevelStartEndTimestamp(_serverId, start, end);
            // 限时充值排行榜
            var now = TimeUtil.TimeStamp;
            if (end >= 0)
            {
                _limitLevelRankCountdown = (int)(end + GameDefine.LimitLevelIconDelay - now);
                if (_limitLevelRankCountdown <= 0)
                {
                    await CheckLimitLevelCount(true);
                }
            }
            else
            {
                _limitLevelRankCountdown = -1;
            }
            return ret;
        }

        // 清除当前限时等级排行榜
        public async ValueTask<bool> GmDelLimitLevelRank()
        {
            if (!_isActive) return false;
            var ret = await RedisService.DelLimitRoleLevelRank(_serverId);
            for (int i = 0; i < ret.Count; i++)
            {
                if (i == 0)
                {
                    if (ret[i])
                    {
                        LogDebug("GM请求清除限时充值排行榜成功");
                    }
                    else
                    {
                        LogDebug("GM请求清除限时充值排行榜失败");
                    }
                }
                if (i == 1)
                {
                    if (ret[i])
                    {
                        LogDebug("GM请求清除限时充值排行榜开始时间成功");
                    }
                    else
                    {
                        LogDebug("GM请求清除限时充值排行榜开始时间失败");
                    }
                }
                if (i == 2)
                {
                    if (ret[i])
                    {
                        LogDebug("GM请求清除限时充值排行榜结束时间成功");
                    }
                    else
                    {
                        LogDebug("GM请求清除限时充值排行榜结束时间失败");
                    }
                }
            }
            return !ret.Contains(false);
        }

        // 检查限时等级排行榜
        private async Task CheckLimitLevelCount(bool force = false)
        {
            var end = await RedisService.GetLimitLevelEndTimestamp(_serverId);
            var now = TimeUtil.TimeStamp;
            if (end > 0 && now > end)
            {
                //如果限时等级榜到期，结算排行榜
                var list = await RedisService.SettleRoleLimitLevelRank(_serverId);
                foreach (var (roleId, mailId) in list) {
                    // 如果在线, 就推送
                    var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
                    var active = await globalGrain.CheckPlayer(roleId);
                    if (active)
                    {
                        var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                        _ = playerGrain.OnRecvMail(mailId);
                    }
                }
            }
            return;
            //if (_limitLevelRankCountdown > 0 || force)
            //{
            //    _limitLevelRankCountdown--;
            //    if (_limitLevelRankCountdown <= 0)
            //    {
            //        //在清理限时等级排行榜之前，需要确定前十名的奖励，并存储起来
            //        var ret = await RedisService.DelLimitRoleLevelRank(_serverId);
            //        for (int i = 0; i < ret.Count; i++)
            //        {
            //            if (i == 0)
            //            {
            //                if (ret[i])
            //                {
            //                    LogDebug("清除限时充值排行榜成功");
            //                }
            //                else
            //                {
            //                    LogDebug("清除限时充值排行榜失败");
            //                }
            //            }
            //            if (i == 1)
            //            {
            //                if (ret[i])
            //                {
            //                    LogDebug("清除限时充值排行榜开始时间成功");
            //                }
            //                else
            //                {
            //                    LogDebug("清除限时充值排行榜开始时间失败");
            //                }
            //            }
            //            if (i == 2)
            //            {
            //                if (ret[i])
            //                {
            //                    LogDebug("清除限时充值排行榜结束时间成功");
            //                }
            //                else
            //                {
            //                    LogDebug("清除限时充值排行榜结束时间失败");
            //                }
            //            }
            //        }
            //        _limitLevelRankCountdown = -1;
            //    }
            //}
        }

        private async Task OnUpdate(object state)
        {
            if (_isShutDownReq)
            {
                _updateTicker?.Dispose();
                _updateTicker = null;
                return;
            }

            _updateCnt++;
            if (_updateCnt >= 3)
            {
                _updateCnt = 0;

                SortSectList();
                CheckMails();
                _ = _globalGrain.UpdateServer(_serverId, _players.Count);
            }
            if (_chatMsgLastExcuteTimeout > 0)
            {
                _chatMsgLastExcuteTimeout--;
                if (_chatMsgLastExcuteTimeout == 0)
                {
                    await ExcuteChatMsgBulkInsert();
                }
            }
            if (ConfigService.FakeBell.enabled)
            {
                _fakeBellDelay++;
                if (_fakeBellDelay >= ConfigService.FakeBell.delay)
                {
                    _fakeBellDelay = 0;
                    if (_fakeBellMsgIndex < ConfigService.FakeBell.msg.Count)
                    {
                        var roleInfo = new RoleInfo()
                        {
                            Id = 0,
                            Name = ConfigService.FakeBell.name,
                            Relive = ConfigService.FakeBell.relive,
                            Level = ConfigService.FakeBell.level,
                            CfgId = ConfigService.FakeBell.cfgId,
                            Type = 0,
                            VipLevel = ConfigService.FakeBell.vipLevel
                        };
                        roleInfo.Skins.AddRange(ConfigService.FakeBell.skins);
                        _ = Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat
                        {
                            Msg = new ChatMessage
                            {
                                Type = ChatMessageType.Bell,
                                Msg = ConfigService.FakeBell.msg[_fakeBellMsgIndex],
                                From = roleInfo,
                                BellTimes = 1,
                            }
                        })));
                    }
                    _fakeBellMsgIndex += 1;
                    if (_fakeBellMsgIndex >= ConfigService.FakeBell.msg.Count)
                    {
                        _fakeBellMsgIndex = 0;
                    }
                }
            }
            await CheckLuckyDrawChestNextGen();
            await CheckLuckyDrawFreeReset();
            await CheckGmNotice();
            await CheckLimitChargeCount(false);
            await CheckLimitLevelCount(false);
        }

        private async Task CheckAndDeactiveOthers()
        {
            if (_isShutDownReq && _players.Count == 0)
            {
                // 帮派销毁
                LogDebug("销毁帮派...");
                var tasks = new List<Task>();
                foreach (var sid in _sects.Keys.ToList())
                {
                    var grain = GrainFactory.GetGrain<ISectGrain>(sid);
                    tasks.Add(grain.ShutDown());
                }

                await Task.WhenAll(tasks);

                // 让活动注销
                LogDebug("销毁摆摊...");
                await _mallGrain.ShutDown();

                LogDebug("销毁甄不亏...");
                await _zhenBuKuiGrain.ShutDown();

                LogDebug("销毁地煞星...");
                await _diShaXingGrain.ShutDown();

                LogDebug("销毁骷髅王...");
                await _kuLouWangGrain.ShutDown();

                LogDebug("销毁金蟾送宝...");
                await _jinChanSongBaoGrain.ShutDown();

                LogDebug("销毁金翅大鹏...");
                await _eagleGrain.ShutDown();

                // await _tianJiangLingHouGrain.ShutDown();

                LogDebug("销毁水陆大会...");
                await _shuiLuDaHuiGrain.ShutDown();

                LogDebug("销毁王者之战...");
                await _wangZheZhiZhanGrain.ShutDown();

                LogDebug("销毁比武大会...");
                await _singlePkGrain.ShutDown();

                LogDebug("销毁大乱斗...");
                await _daLuanDouGrain.ShutDown();

                LogDebug("销毁神兽降临...");
                await _shenShouJiangLinGrain.ShutDown();

                LogDebug("销毁皇城PK...");
                await _hcPkGrain.ShutDown();

                LogDebug("销毁帮战...");
                await _sectWarGrain.ShutDown();

                LogDebug("销毁红包服务...");
                await _redGrain.ShutDown();

                LogDebug("写入聊天记录...");
                await ExcuteChatMsgBulkInsert();

                // 地图
                LogDebug("销毁地图...");
                tasks.Clear();
                foreach (var mapGrain in _maps.Values)
                {
                    tasks.Add(mapGrain.ShutDown());
                }

                await Task.WhenAll(tasks);

                DeactivateOnIdle();

                _isShutDownReq = false;
                _isActive = false;

                LogDebug("完成停服");
            }
        }

        // 重置单人PK排行榜
        public async Task ResetSinglePkRank()
        {
            List<uint> activedRoleList = new();
            foreach (var rid in _players.Keys.ToList())
            {
                var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                if (grain != null)
                {
                    // 激活的玩家，重置
                    if (await grain.ResetSinglePkInfo())
                    {
                        activedRoleList.Add(rid);
                    }
                }
            }
            // 数据库 清除单人PK信息
            await DbService.ResetAllRoleSinglePk(_serverId, activedRoleList);
            // REDIS 清除单人PK排行榜
            await RedisService.ResetAllRoleSinglePk(_serverId);
        }

        // 重置大乱斗PK排行榜
        public async Task ResetDaLuanDouRank()
        {
            await Task.CompletedTask;
            // List<uint> activedRoleList = new();
            // foreach (var rid in _players.Keys.ToList())
            // {
            //     var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
            //     if (grain != null)
            //     {
            //         // 激活的玩家，重置
            //         if (await grain.ResetDaLuanDouInfo())
            //         {
            //             activedRoleList.Add(rid);
            //         }
            //     }
            // }
            // // 数据库 清除大乱斗PK信息
            // await DbService.ResetAllRoleDaLuanDou(_serverId, activedRoleList);
            // // REDIS 清除大乱斗PK排行榜
            // await RedisService.ResetAllRoleDaLuanDou(_serverId);
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"区服[{_serverId}]:{msg}");
        }
        
        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"区服[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"区服[{_serverId}]:{msg}");
        }
    }
}