using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Ddxy.Common.Model;
using Ddxy.Common.Orleans;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Config;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Data.Vo;
using Ddxy.GameServer.Logic;
using Ddxy.GameServer.Logic.Battle.Skill;
using Ddxy.GameServer.Logic.Equip;
using Ddxy.GameServer.Logic.Mail;
using Ddxy.GameServer.Logic.Mount;
using Ddxy.GameServer.Logic.Partner;
using Ddxy.GameServer.Logic.Pet;
using Ddxy.GameServer.Logic.Scheme;
using Ddxy.GameServer.Logic.Title;
using Ddxy.GameServer.Logic.XTask;
using Ddxy.GameServer.Option;
using Ddxy.GameServer.Util;
using Ddxy.GrainInterfaces;
using Ddxy.GrainInterfaces.Core;
using Ddxy.Protocol;
using FreeSql;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;
using Orleans.Runtime;

namespace Ddxy.GameServer.Grains
{
    public class BianshenObjectUsingData
    {
        // 变身卡ID
        public int id { get; set; }
        // 是否改变形象？
        public bool avatar { get; set; }
        // 失效截至时间戳
        public string timestamp { get; set; }
    }
    public class BianshenObject
    {
        // 拥有的变身卡ID及数量键值对，ID对应数量
        public Dictionary<int, int> cards { get; set; }
        // 五行修炼等级，金、木、水、火、土、五行，对应1、2、3、4、5、6
        public Dictionary<int, int> wuxing { get; set; }
        // 当前使用的变身卡信息
        public BianshenObjectUsingData current { get; set; }
    }
    public class XingzhenData
    {
        // 当前经验
        public int exp { get; set; }
        // 当前等级
        public int level { get; set; }
        // 洗炼属性
        public List<string> refine { get; set; }
        // 预览属性
        public List<string> preview { get; set; }
    }
    public class XingzhenObject
    {
        // 已经解锁的阵型
        public Dictionary<int, XingzhenData> unlocked { get; set; }
        // 使用中的阵型
        public int used { get; set; }
    }
    public class ChildObject
    {
        // 性别
        public Sex sex { get; set; }
        // 名字
        public string name { get; set; }
        // 技能
        public List<int> skill { get; set; }
        // 洗炼技能预览
        public List<int> preview { get; set; }
        // 经验
        public int exp { get; set; }
        // 等级
        public int level { get; set; }
        // 外观
        public int shape { get; set; }
    }
    public class TianceInfo
    {
        // 天演策等级
        public uint level { get; set; }
        // 天策符列表
        public List<TianceFu> list { get; set; }
        // 转为Json字符串
        public string ToJson()
        {
            return Json.Serialize(this);
        }
        // 从Json字符串转
        public void FromJson(string json)
        {
            this.level = 0;
            if (this.list != null)
            {
                this.list.Clear();
            }
            else
            {
                this.list = new();
            }
            if (json != null && json.Length > 0)
            {
                var i = Json.Deserialize<TianceInfo>(json);
                this.level = i.level;
                this.list.AddRange(i.list);
            }
        }
    }
    [CollectionAgeLimit(Hours = 5)]
    public partial class PlayerGrain : Grain, IPlayerGrain, IGrainWithIntegerKey, IGrain, IAddressable
    {
        private ILogger<PlayerGrain> _logger;
        private OrleansOptions _options;
        private XinPayOptions _xinPayOptions;

        public AppOptions AppOptions { get; }

        private Packet _packet;
        public Random Random;

        public bool IsActive { get; private set; } //是否已经激活
        public bool IsOnline { get; private set; } //表示当前是否已经进入游戏了, Online开启
        public bool IsEnterServer { get; private set; } //标记当前是否已经进入游戏, EnterServer开启

        public bool IsEnterBackGround { get; private set; } //标记当前是否已经进入后台
        private uint _enterBackGroundTime;

        private uint _lastHeartBeatTime;
        private IDisposable _updateTimer; //每s调用一次
        private uint _saveDataCnt; //用来控制保存数据的频率

        public uint RoleId; // 角色id
        public uint OnlyId; // 角色在serverr中的唯一id, EnterServer时获得

        public RoleEntity Entity;
        public RoleEntity LastEntity; //保存上次保存时的RoleEntity, Update中做增量更新
        public RoleExtEntity ExtEntity;
        public RoleExtEntity LastExtEntity;

        // 随身特效及足迹
        public List<int> SkinHas;
        public List<int> SkinUse;
        public List<int> SkinChat;
        private Attrs SkinAttrs = new Attrs();
        private bool IsSkinDirty = true;

        // 变身卡及五行修炼
        public BianshenObject Bianshen; // 变身卡信息
        private Attrs BianShenAttrs = new Attrs(); // 变身卡属性
        private Int64 _BianshenTimeout = 0; // 当前使用中变身卡失效倒计时，单位毫秒

        // 星阵
        public XingzhenObject Xingzhen; // 星阵信息
        private Attrs XingzhenAttrs = new Attrs();
        private bool IsXingzhengAttrsDirty = true;

        // 孩子
        public ChildObject Child; // 孩子信息
        private List<BattleChildSkillData> ChildSkills = new();
        private bool IsChildDirty = true;

        // 转盘--风雨值
        public uint LuckyDrawPoint = 0;

        private Dictionary<byte, byte> _rewards; //等级奖励, key是等级, value表示是否领取
        public List<ReliveRecord> Relives; //转生记录,race+sex
        private List<SkillData> _skills; //技能id及其熟练度

        private RoleSldhVo _sldh; //水陆大会数据
        private RoleWzzzVo _wzzz; //王者之战数据
        private RoleSsjlVo _ssjl; //神兽降临数据
        public Dictionary<uint, uint> Items { get; private set; } //道具及其数量
        public Dictionary<uint, uint> Repos { get; private set; } //道具及其数量
        public Dictionary<uint, byte> Mails { get; private set; } //我操作过的全局邮件
        public ulong ExpMax;

        public EquipManager EquipMgr; //装备管理器
        public SchemeManager SchemeMgr; //属性方案管理器
        public PetManager PetMgr; //宠物管理器
        public MountManager MountMgr; //坐骑管理器
        public PartnerManager PartnerMgr; //伙伴管理器
        public XTaskManager TaskMgr; // 任务管理器
        public MailManager MailMgr; //邮件管理器
        public TitleManager TitleMgr; //称号管理器

        private List<RoleInfo> _friendList; // 好友列表
        private List<RoleInfo> _friendApplyList; //好友申请列表

        private IMapGrain _mapGrain; //地图
        private MapConfig _mapCfg; //地图配置
        private TerrainConfig _terrainCfg; //地形配置

        // private bool _usingIncense; //当前是否正在使用引妖香
        private uint _incenseTime; //剩余的引妖香时间

        // private IDisposable _incenseTimer; //使用引妖香后60分钟自动撤销效果
        private bool _inPrison; //当前是否被监禁
        private uint _shane; //剩余监禁时间
        private uint _anleiCnt;
        private Flags _flags;
        private uint _lastWorldChatTime;
        private string _lastChatStr;        //最后一次聊天消息
        private uint _lastChatRepeatTimes;  //最后一条聊天消息重复次数
        private uint _lastChatTime;         //最后一次发聊天消息的时间 

        private uint _lastBellChatTime;
        private uint _lastSpreadsTime;

        private uint _lastFetchLevelRankTime; //上次获取等级排行榜的时间
        private S2C_RankLevel _lastRankLevelResp; //缓存的等级排行榜数据
        private uint _lastFetchJadeRankTime;
        private S2C_RankJade _lastRankJadeResp;
        private uint _lastFetchPayRankTime;
        private S2C_RankPay _lastRankPayResp;
        private uint _lastFetchLimitPayRankTime;
        private S2C_LimitChargeRankInfo _lastLimitRankInfoResp;
        private uint _lastFetchLimitLevelRankTime;
        private S2C_LimitLevelRankInfo _lastLimitLevelRankInfoResp;
        private uint _lastFetchSldhRankTime;
        private uint _lastFetchWzzzRankTime;
        private S2C_RankSldh _lastRankSldhResp;
        private S2C_RankWzzz _lastRankWzzzResp;
        private uint _lastFetchSectRankTime;
        private S2C_RankSect _lastRankSectResp;
        private uint _lastFetchSinglePkRankTime;
        private S2C_RankSinglePk _lastRankSinglePkResp;
        private uint _lastFetchDaLuanDouRankTime;
        private S2C_RankDaLuanDou _lastRankDaLuanDouResp;

        private uint _lastSearchRoleTime; //上次搜索角色的时间

        public IGlobalGrain GlobalGrain;
        public IServerGrain ServerGrain; // 区服
        public ISectGrain SectGrain; // 帮派
        private IBattleGrain _battleGrain; //战斗引用
        private uint _battleId = 0;
        private uint _campId = 0;
        // 观战
        private IBattleGrain _battleGrainWatched;
        private uint _battleIdWatched = 0;
        private uint _campIdWatched = 0;

        private IMallGrain _mallGrain; //摆摊

        private IZhenBuKuiGrain _zhenBuKuiGrain; //甄不亏

        //private ITianJiangLingHouGrain _tianJiangLingHouGrain; //天降灵猴
        private ISectWarGrain _sectWarGrain; //天降灵猴

        private ISinglePkGrain _singlePkGrain; // 比武大会

        private IDaLuanDouGrain _daLuanDouGrain; // 大乱斗

        // 邀请入队时间戳
        private Dictionary<uint, uint> C2S_TeamInviteDict = new();

        public uint TeamId { get; private set; }
        public uint TeamLeader { get; private set; }
        public uint TeamMemberCount { get; private set; }
        public ITeamGrain TeamGrain { get; private set; }
        public bool InTeam => TeamGrain != null && TeamId > 0 && TeamLeader > 0;
        public bool IsTeamLeader => TeamGrain != null && TeamId > 0 && TeamLeader == RoleId;

        public bool InSect => Entity.SectId > 0 && SectGrain != null;

        public bool InBattle => _battleGrain != null || _battleGrainWatched != null;
        public MapObjectEquipData MapWeapon { get; private set; }
        public MapObjectEquipData MapWing { get; private set; }

        public Attrs Attrs => SchemeMgr.Scheme.Attrs;

        public bool IsGm => Entity is {Type: UserType.Gm};

        // 帮战入场后的相关数据
        private uint _sectWarId;
        private int _sectWarCamp;
        private SectWarPlace _sectWarPlace = SectWarPlace.JiDi;

        // ReSharper disable once NotAccessedField.Local
        private SectWarRoleState _sectWarState = SectWarRoleState.Idle;

        // 当前是否处于暂离队伍的状态
        public bool _teamLeave;

        // 单人PK数据
        private RoleSinglePkVo _singlePkVo;

        // 大乱斗PK数据
        private RoleDaLuanDouVo _daLuanDouVo;

        private uint _deviceWidth;
        private uint _deviceHeight;

        private List<uint> _totalPayRewards = new();
        private List<uint> _ewaiPayRewards = new();
        private List<uint> _dailyPayRewards = new();

        // 当前VIP等级
        public uint VipLevel = 0;

        // 天策信息
        public TianceInfo Tiance = new();
        private Attrs TiancePlayerAttrs = new Attrs();
        private Attrs TiancePetAttrs = new Attrs();
        public List<TianceFu> TianceFuInBattle = new();
        private bool IsTianceInfoDirty = true;

        // 当前切割等级
        public uint QieGeLevel = 0;
        public uint QieGeExp = 0;
        private Attrs QieGeAttrs = new Attrs(); // 神器切割属性

        //神之力
        public uint ShenZhiLiHurtLv = 0;            //神之力真实伤害等级
        public uint ShenZhiLiHpLv = 0;              //神之力气血等级
        public uint ShenZhiLiSpeedLv = 0;           //神之力速度等级
        private Attrs ShenZhiLiAttrs = new Attrs(); // 神之力属性

        //成神之路副本
        public uint CszlLayer = 1;                          //成神之路
        private uint _lastFetchCszlLayerRankTime;           //上次获取成神之路副本排行榜的时间
        private S2C_RankCszlLayer _lastRankCszlLayerResp;   //缓存的成神之路副本排行榜数据


        // 金翅大鹏
        private IDisposable _eagleBroadCastTimer;

        // 上次发弹幕时间戳
        protected uint _lastDanMuTimestamp = 0;

        public PlayerGrain(ILogger<PlayerGrain> logger, IOptions<OrleansOptions> options,
            IOptions<AppOptions> gameOptions, IOptions<XinPayOptions> xinPayOptions)
        {
            _logger = logger;
            _options = options.Value;
            AppOptions = gameOptions.Value;
            _xinPayOptions = xinPayOptions.Value;
        }

        public override Task OnActivateAsync()
        {
            RoleId = (uint)this.GetPrimaryKeyLong();
            return Task.CompletedTask;
        }

        public override async Task OnDeactivateAsync()
        {
            await Shutdown();
        }

        public async Task<bool> StartUp()
        {
            if (IsActive) return true;
            IsActive = true;
            try
            {
                // 从数据库加载角色数据, 处理数据
                Entity = await DbService.QueryRole(RoleId);
                if (Entity == null)
                {
                    DeactivateOnIdle();
                    LogError("获取角色数据失败");
                    return false;
                }
                // 角色已被冻结
                if (Entity.Status != RoleStatus.Normal)
                {
                    DeactivateOnIdle();
                    LogError("角色已被冻结");
                    return false;
                }
                // 获取扩展数据
                ExtEntity = await DbService.QueryRoleExt(RoleId);
                if (ExtEntity == null)
                {
                    LogError("获取扩展数据失败");
                    DeactivateOnIdle();
                    return false;
                }
                // 检查区服是否已经激活
                ServerGrain = GrainFactory.GetGrain<IServerGrain>(Entity.ServerId);
                if (!await ServerGrain.CheckActive())
                {
                    LogError($"区服[{Entity.ServerId}]未激活/已停服");
                    DeactivateOnIdle();
                    return false;
                }
                // 前往Server中注册并获得OnlyId, 如果区服未能正常初始化, 会返回0
                OnlyId = await ServerGrain.Enter(RoleId);
                if (OnlyId == 0)
                {
                    DeactivateOnIdle();
                    LogError($"区服[{Entity.ServerId}]进入失败");
                    return false;
                }
                GlobalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);

                LastEntity = new RoleEntity();
                LastEntity.CopyFrom(Entity);

                LastExtEntity = new RoleExtEntity();
                LastExtEntity.CopyFrom(ExtEntity);

                _packet = new Packet(GetStreamProvider(_options.SmsProvider).GetStream<NotifyMessage>(Guid.Empty, _options.StreamNameSpace));
                Random = new Random();
                IsOnline = false;
                IsEnterServer = false;
                IsEnterBackGround = false;

                // 修正等级和经验
                if (Entity.Star < 1) Entity.Star = 1;
                if (Entity.Relive > 4) Entity.Relive = 4;
                var maxLevel = ConfigService.GetRoleMaxLevel(Entity.Relive);
                var minLevel = ConfigService.GetRoleMinLevel(Entity.Relive);
                Entity.Level = Math.Clamp(Entity.Level, minLevel, maxLevel);
                ExpMax = ConfigService.GetRoleUpgradeExp(Entity.Relive, Entity.Level);
                if (Entity.Exp > ExpMax) Entity.Exp = ExpMax;

                var maxXlLevel = RoleRefine.GetMaxRefineLevel(Entity.Relive);
                if (Entity.XlLevel > maxXlLevel) Entity.XlLevel = maxXlLevel;

                _flags = new Flags(Entity.Flags);

                if (Entity.SectId == 0)
                {
                    SetFlag(FlagType.SectSilent, false);
                }

                _mallGrain = GrainFactory.GetGrain<IMallGrain>(Entity.ServerId);
                _zhenBuKuiGrain = GrainFactory.GetGrain<IZhenBuKuiGrain>(Entity.ServerId);
                //_tianJiangLingHouGrain = GrainFactory.GetGrain<ITianJiangLingHouGrain>(Entity.ServerId);
                _sectWarGrain = GrainFactory.GetGrain<ISectWarGrain>(Entity.ServerId);
                _singlePkGrain = GrainFactory.GetGrain<ISinglePkGrain>(Entity.ServerId);
                _daLuanDouGrain = GrainFactory.GetGrain<IDaLuanDouGrain>(Entity.ServerId);

                C2S_TeamInviteDict = new();

                // 技能
                InitSkills();
                // 外观皮肤
                InitSkins();
                // 变身卡及五行修炼
                InitBianshen();
                // 星阵
                InitXingzhen();
                // 孩子
                InitChild();
                // 物品
                InitItems();
                // 仓库
                InitRepos();
                // 重生记录
                InitRelives();
                // 等级奖励
                InitRewards();
                // 已领取/已删除的邮件集合
                InitMails();
                // 水陆大会
                await InitSldh();
                // 王者之战
                await InitWzzz();
                // 单人PK
                InitSinglePk();
                // 大乱斗PK
                await InitDaLuanDou();
                // 神兽降临
                await InitSsjl();
                // 计算潜能兑换
                CalcExchangePotential();
                // 计算VIP等级
                await CalcVipLevel(true);
                // 初始化天策
                InitTiance();
                // 初始化切割
                await InitQieGe();
                // 初始化神之力
                await InitShenZhiLi();
                // 初始化成神之路
                await InitCszl();

                // 转盘--风雨值
                LuckyDrawPoint = await RedisService.GetRoleLuckyPoint(RoleId);

                // 更新缓存数据
                await RedisService.SetRoleInfo(Entity);

                // 称号
                TitleMgr = new TitleManager(this);
                await TitleMgr.Init();

                // 装备
                EquipMgr = new EquipManager(this);
                await EquipMgr.Init();

                // 属性方案
                SchemeMgr = new SchemeManager(this);
                await SchemeMgr.Init();

                // 宠物
                PetMgr = new PetManager(this);
                await PetMgr.Init();

                // 坐骑
                MountMgr = new MountManager(this);
                await MountMgr.Init();

                // 宠物计算管制坐骑带来的属性, 必须放到坐骑初始化之后
                await PetMgr.Start();

                // 伙伴
                PartnerMgr = new PartnerManager(this);
                await PartnerMgr.Init();

                // 地图
                if (Entity.MapId == 5001)
                {
                    // 从帮战地图中退到长安城帮派接引人面前
                    Entity.MapId = 1011;
                    Entity.MapX = 230;
                    Entity.MapY = 20;
                }

                if (Entity.MapId == 3001)
                {
                    Entity.MapId = 1206;
                    Entity.MapX = 25;
                    Entity.MapY = 18;
                }

                await EnterMap();

                // 帮派
                if (Entity.SectId > 0)
                {
                    var ret = await ServerGrain.ExistsSect(Entity.SectId);
                    if (ret)
                    {
                        SectGrain = GrainFactory.GetGrain<ISectGrain>(Entity.SectId);
                    }
                    else
                    {
                        Entity.SectId = 0;
                        Entity.SectContrib = 0;
                        Entity.SectJob = 0;
                    }
                }

                // 这里要构建当前的Weapon
                await RefreshWeapon(false);
                await RefreshWing(false);
                await FreshSkinsToRedis();

                // 任务, 一定要放在地图之后, 因为任务会自动创建Npc
                TaskMgr = new XTaskManager(this);
                await TaskMgr.Init();

                // 获取好友列表
                _friendList = new List<RoleInfo>(10);
                var flist = await RedisService.GetFriendList(RoleId);
                if (flist != null)
                {
                    foreach (var info in flist)
                    {
                        if (_friendList.Exists(p => p.Id == info.Id)) continue;
                        _friendList.Add(info);
                    }
                }
                // 获取好友申请列表
                _friendApplyList = new List<RoleInfo>(10);
                var falist = await RedisService.GetFriendApplyList(RoleId);
                if (falist != null) _friendApplyList.AddRange(falist);
                // 充值奖励领取情况
                if (!string.IsNullOrWhiteSpace(Entity.TotalPayRewards))
                {
                    _totalPayRewards = Json.Deserialize<List<uint>>(Entity.TotalPayRewards);
                }

                if (!string.IsNullOrWhiteSpace(Entity.EwaiPayRewards))
                {
                    _ewaiPayRewards = Json.Deserialize<List<uint>>(Entity.EwaiPayRewards);
                }

                if (!string.IsNullOrWhiteSpace(Entity.DailyPayRewards))
                {
                    _dailyPayRewards = Json.Deserialize<List<uint>>(Entity.DailyPayRewards);
                }

                await RefreshDailyPays();

                MailMgr = new MailManager(this);
                await MailMgr.Init();

                // 善恶，换算出剩余监禁时间
                _shane = 0;
                if (Entity.Shane > 0)
                {
                    var now = TimeUtil.TimeStamp;
                    if (now >= Entity.Shane)
                    {
                        Entity.Shane = 0;
                    }
                    else
                    {
                        _shane = Entity.Shane - now;
                        if (_shane > GameDefine.PrisionTime) _shane = GameDefine.PrisionTime;
                    }
                }

                // 更新在线状态
                _ = DbService.Sql.Update<RoleEntity>()
                    .Where(it => it.Id == RoleId)
                    .Set(it => it.Online, true)
                    .ExecuteAffrowsAsync();

                // 每秒tick一次
                _updateTimer = RegisterTimer(Update, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));

                // 注册Players
                await GlobalGrain.UpdatePlayer(RoleId);

                LogDebug($"激活成功");
                return true;
            }
            catch (Exception e)
            {
                DeactivateOnIdle();
                LogError($"激活出错{e.Message}");
                return false;
            }
        }

        public async Task Shutdown()
        {
            // 踢掉下线的行为, 要求用户重新登录
            if (Entity != null)
            {
                _ = RedisService.DelUserToken(Entity.UserId);
            }
            // 让网关切断socket连接
            if (_packet != null && RoleId > 0)
            {
                _ = _packet.SendStatus(RoleId, WebSocketCloseStatus.NormalClosure, false);
            }
            // 未激活忽略
            if (!IsActive)
            {
                return;
            }

            _updateTimer?.Dispose();
            _updateTimer = null;

            _eagleBroadCastTimer?.Dispose();
            _eagleBroadCastTimer = null;

            // 保存角色数据数据
            Entity.Online = false;
            LastEntity.Online = true;

            LogDebug("准备注销");

            // 优先，重点，就是保存数据，而且要考虑在线人数较多时，保存数据出错，用循环来不断尝试
            var tryCount = 10;
            while (tryCount-- > 0)
            {
                try
                {
                    await SaveAllData();
                    LogInformation("入库成功");
                    break;
                }
                catch (Exception ex)
                {
                    try
                    {
                        // 最后一次尝试失败，则写数据到日志
                        if (tryCount == 1)
                        {
                            // 防止数据丢失，这里输出到日志中来
                            var json = Json.SafeSerialize(Entity);
                            LogDebug(json);
                        }
                    }
                    catch
                    {
                        // ignore
                    }

                    LogError($"入库失败[{ex.Message}][{ex.StackTrace}]");
                    // 500ms后重试
                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }
            }

            if (InBattle)
            {
                try
                {
                    if (_battleGrain != null)
                    {
                        var battleId = (uint)_battleGrain.GetPrimaryKeyLong();
                        if (GlobalGrain != null)
                        {
                            var ret = await GlobalGrain.CheckBattle(battleId);
                            if (ret)
                            {
                                await _battleGrain.Offline(RoleId);
                            }
                        }
                    }
                    // 退出观战
                    await ReqExitWatchBattle();
                }
                catch (Exception)
                {
                    // ignore
                }

                _battleGrain = null;
                _battleId = 0;
                _campId = 0;
                // 观战
                _battleGrainWatched = null;
                _battleIdWatched = 0;
                _campIdWatched = 0;
            }
            // 往Players注销
            if (GlobalGrain != null)
            {
                await GlobalGrain.RemovePlayer(RoleId);
            }
            GlobalGrain = null;

            if (InTeam)
            {
                _ = TeamGrain.Exit(RoleId);
                TeamGrain = null;
                TeamId = 0;
                TeamLeader = 0;
            }

            SectGrain = null;

            // 退出地图
            await ExitMap();
            _mapGrain = null;

            // 退出Server
            if (ServerGrain != null)
            {
                _ = ServerGrain.Exit(OnlyId, RoleId);
            }
            ServerGrain = null;

            // 销毁各种Mgr
            if (TaskMgr != null) await TaskMgr.Destroy();
            TaskMgr = null;

            if (EquipMgr != null) await EquipMgr.Destroy();
            EquipMgr = null;

            if (SchemeMgr != null) await SchemeMgr.Destroy();
            SchemeMgr = null;

            if (PetMgr != null) await PetMgr.Destroy();
            PetMgr = null;

            if (MountMgr != null) await MountMgr.Destroy();
            MountMgr = null;

            if (PartnerMgr != null) await PartnerMgr.Destroy();
            PartnerMgr = null;

            if (TitleMgr != null) await TitleMgr.Destroy();
            TitleMgr = null;

            MailMgr?.Destroy();
            MailMgr = null;

            MapWeapon = null;
            MapWing = null;

            _mallGrain = null;
            _zhenBuKuiGrain = null;
            //_tianJiangLingHouGrain = null;
            _sectWarGrain = null;
            _singlePkGrain = null;
            _daLuanDouGrain = null;

            C2S_TeamInviteDict?.Clear();
            C2S_TeamInviteDict = null;

            Items?.Clear();
            Items = null;
            Repos?.Clear();
            Repos = null;
            Mails?.Clear();
            Mails = null;

            _lastRankLevelResp = null;
            _lastRankJadeResp = null;
            _lastRankPayResp = null;
            _lastRankSldhResp = null;
            _lastRankWzzzResp = null;
            _lastRankSectResp = null;
            _lastRankCszlLayerResp = null;

            _packet = null;
            // IsActive要最后才设置为false，否则中间很多方法调度都会因为IsActive为false而导致return
            IsActive = false;
            LogDebug($"注销成功");
        }

        public async Task Online()
        {
            if (!IsActive || IsOnline) return;
            // 启用心跳检测
            _lastHeartBeatTime = TimeUtil.TimeStamp;
            IsOnline = true;
            // 退出观战
            await ReqExitWatchBattle();

            LogDebug($"网络连接");
            await Task.CompletedTask;
        }

        public async Task Offline()
        {
            if (!IsActive || !IsOnline) return;
            IsOnline = false;
            IsEnterServer = false;

            // 通知战斗，我已离线
            if (InBattle)
            {
                if (_battleGrain != null)
                {
                    var battleId = (uint)_battleGrain.GetPrimaryKeyLong();
                    var ret = await GlobalGrain.CheckBattle(battleId);
                    if (ret)
                    {
                        await _battleGrain.Offline(RoleId);
                    }
                    else
                    {
                        _battleGrain = null;
                        _battleId = 0;
                        _campId = 0;
                    }
                }
                // 退出观战
                await ReqExitWatchBattle();
            }

            // 通知Server，该玩家已离线，后续没必要接收广播等通知
            await ServerGrain.Offline(RoleId);
            // 通知地图, 该玩家已离线, 后续不必刷新视野
            await _mapGrain.PlayerOffline(OnlyId);

            // 通知Team
            if (InTeam) _ = TeamGrain.Offline(RoleId);

            // 通知Sect
            if (InSect) _ = SectGrain.Offline(RoleId);

            // 通知帮战
            if (_sectWarCamp > 0) _ = _sectWarGrain.Offline(RoleId, TeamLeader);

            LogDebug($"网络断开");
        }

        public Task<Immutable<byte[]>> Dump()
        {
            if (!IsActive) return Task.FromResult(new Immutable<byte[]>(null));
            var bytes = Json.SerializeToBytes(Entity);
            return Task.FromResult(new Immutable<byte[]>(bytes));
        }

        public Task<Immutable<byte[]>> DumpPets()
        {
            if (!IsActive) return Task.FromResult(new Immutable<byte[]>(null));
            var list = new List<PetEntity>(PetMgr.All.Count);
            foreach (var pet in PetMgr.All)
            {
                list.Add(pet.Entity);
            }

            var bytes = Json.SerializeToBytes(list);
            return Task.FromResult(new Immutable<byte[]>(bytes));
        }

        public Task<Immutable<byte[]>> DumpMounts()
        {
            if (!IsActive) return Task.FromResult(new Immutable<byte[]>(null));
            var list = new List<MountEntity>(MountMgr.All.Count);
            foreach (var mount in MountMgr.All)
            {
                list.Add(mount.Entity);
            }

            var bytes = Json.SerializeToBytes(list);
            return Task.FromResult(new Immutable<byte[]>(bytes));
        }

        public Task<Immutable<byte[]>> DumpEquips()
        {
            if (!IsActive) return Task.FromResult(new Immutable<byte[]>(null));
            var list = new List<EquipEntity>(EquipMgr.Equips.Count);
            foreach (var equip in EquipMgr.Equips.Values)
            {
                list.Add(equip.Entity);
            }

            var bytes = Json.SerializeToBytes(list);
            return Task.FromResult(new Immutable<byte[]>(bytes));
        }

        public Task<Immutable<byte[]>> DumpOrnaments()
        {
            if (!IsActive) return Task.FromResult(new Immutable<byte[]>(null));
            var list = new List<OrnamentEntity>(EquipMgr.Ornaments.Count);
            foreach (var ornament in EquipMgr.Ornaments.Values)
            {
                list.Add(ornament.Entity);
            }

            var bytes = Json.SerializeToBytes(list);
            return Task.FromResult(new Immutable<byte[]>(bytes));
        }

        public async Task GmSetLevel(byte level)
        {
            if (!IsActive) return;
            var maxLevel = ConfigService.GetRoleMaxLevel(Entity.Relive);
            if (level > maxLevel) level = maxLevel;
            var oldLevel = Entity.Level;
            await SetLevel(level);
            if (Entity.Level != oldLevel)
            {
                // 重置等级段的经验值, 如果是当前转生等级段最高等级就调整到最大
                Entity.Exp = Entity.Level == maxLevel ? ExpMax : 0;
                await SendRoleExp(Entity.Level - oldLevel);

                SendNotice($"后台调整等级至:{Entity.Level}级");
            }
        }

        public Task GmAddStar(int value)
        {
            if (!IsActive) return Task.CompletedTask;
            value = (int) Entity.Star + value;
            if (value <= 0) value = 1;
            Entity.Star = (uint) value;

            DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.Star, Entity.Star)
                .ExecuteAffrowsAsync();

            SendNotice($"后台调整杀星至:{Entity.Star}级");
            return Task.CompletedTask;
        }

        public async Task GmAddTotalPay(int value) 
        {
            if (!IsActive) return;
            var now = DateTimeOffset.Now;
            var newTotalPayBS = Entity.TotalPayBS;
            var lastDailyPayTs = DateTimeOffset.FromUnixTimeSeconds(Entity.DailyPayTime).AddHours(8);
            var needResetDailyPay = now.Year != lastDailyPayTs.Year || now.DayOfYear != lastDailyPayTs.DayOfYear;

            var newTotalPay = Entity.TotalPay;
            var newEwaiPay = Entity.EwaiPay;
            var newDailyPay = Entity.DailyPay;
            var newDailyPayTime = Entity.DailyPayTime;
            var newDailyPayRewards = Entity.DailyPayRewards;

            newTotalPayBS = Entity.TotalPayBS + (uint)value;
            if (newTotalPayBS < 0) {
                newTotalPayBS = 0;
            }
            newTotalPay = Entity.TotalPay + (uint)value;
            if (newTotalPay < 0) {
                newTotalPay = 0;
            }
            newEwaiPay = Entity.EwaiPay + (uint)value;
            if (newEwaiPay < 0) {
                newEwaiPay = 0;
            }
            newDailyPay = Entity.DailyPay + (uint)value;
            if (newDailyPay < 0) {
                newDailyPay = 0;
            }
            if (needResetDailyPay)
            {
                newDailyPay = (uint)value;
                newDailyPayTime = TimeUtil.TimeStamp;
                newDailyPayRewards = string.Empty;
                _dailyPayRewards.Clear();
            }
            // 更新TotalPay
            _ = DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.TotalPayBS, newTotalPayBS)
                .Set(it => it.TotalPay, newTotalPay)
                .Set(it => it.EwaiPay, newEwaiPay)
                .Set(it => it.DailyPay, newDailyPay)
                .Set(it => it.DailyPayTime, newDailyPayTime)
                .Set(it => it.DailyPayRewards, newDailyPayRewards)
                .ExecuteAffrowsAsync();
            // if (er == 0) return Task.CompletedTask;

            Entity.TotalPayBS = newTotalPayBS;
            Entity.TotalPay = newTotalPay;
            Entity.EwaiPay = newEwaiPay;
            Entity.DailyPay = newDailyPay;
            Entity.DailyPayTime = newDailyPayTime;
            Entity.DailyPayRewards = newDailyPayRewards;

            LastEntity.TotalPayBS = Entity.TotalPayBS;
            LastEntity.TotalPay = Entity.TotalPay;
            LastEntity.EwaiPay = Entity.EwaiPay;
            LastEntity.DailyPay = Entity.DailyPay;
            LastEntity.DailyPayTime = Entity.DailyPayTime;
            LastEntity.DailyPayRewards = Entity.DailyPayRewards;
            // 更新缓存信息和充值排行榜
            await RedisService.SetRolePay(Entity);
            // 更新限时充值排行榜
            await RedisService.AddLimitPayRoleScore(Entity.ServerId, Entity.Id, (uint)Math.Abs(value));
            // 刷新数据
            await ReqRolePays();
            // 计算VIP等级
            await CalcVipLevel();
            SendNotice($"后台调整累计充值为:{Entity.TotalPay}");
        }

        public async Task GmAddSkillExp()
        {
            if (!IsActive) return;
            var changed = false;
            var maxSkillLExp = ConfigService.GetRoleSkillMaxExp(Entity.Relive);
            foreach (var sd in _skills)
            {
                if (sd.Exp >= maxSkillLExp) continue;
                sd.Exp = maxSkillLExp;
                UpdateSkillCost(sd);
                changed = true;

                // 下发给客户端
                await SendPacket(GameCmd.S2CSkillUpdate, new S2C_SkillUpdate {Data = sd});
            }

            if (changed)
            {
                SyncSkills();
            }
        }

        public async ValueTask<bool> GmAddEquip(uint cfgId, byte category, byte index, byte grade)
        {
            if (!IsActive) return false;
            // 新手装备和高级装备没有品阶
            if (category <= (byte) EquipCategory.High)
            {
                grade = 0;
            }

            if (cfgId != 0)
            {
                var equip = await EquipMgr.AddEquip(cfgId, false);
                if (equip == null) return false;
            }
            else
            {
                if (index == 0)
                {
                    for (var i = 1; i <= 5; i++)
                    {
                        var equip = await EquipMgr.AddEquip((EquipCategory) category, i, grade, false);
                        if (equip == null) return false;
                    }
                }
                else
                {
                    var equip = await EquipMgr.AddEquip((EquipCategory) category, index, grade, false);
                    if (equip == null) return false;
                }
            }

            return true;
        }

        public async ValueTask<bool> GmRefineEquip(uint id, List<Tuple<byte, float>> attrs)
        {
            if (!IsActive) return false;
            // 检查装备是否存在
            EquipMgr.Equips.TryGetValue(id, out var equip);
            if (equip == null) return false;
            var args = new List<AttrPair>(10);
            foreach (var (k, v) in attrs)
            {
                args.Add(new AttrPair {Key = (AttrType) k, Value = v});
            }

            var ret = await equip.Refine(args);
            return ret;
        }

        public async ValueTask<bool> GmAddOrnament(uint cfgId, uint suit, byte index, byte grade)
        {
            if (!IsActive) return false;
            if (cfgId != 0)
            {
                var ornament = await EquipMgr.AddOrnaments(cfgId, grade);
                if (ornament == null) return false;
            }
            else
            {
                if (index == 0)
                {
                    for (var i = 1; i <= 5; i++)
                    {
                        var ornament = await EquipMgr.AddOrnaments(Math.Min(i, 4), suit, grade);
                        if (ornament == null) return false;
                    }
                }
                else
                {
                    var ornament = await EquipMgr.AddOrnaments(index, suit, grade);
                    if (ornament == null) return false;
                }
            }

            return true;
        }

        public async ValueTask<bool> GmAddWing(uint cfgId)
        {
            if (!IsActive) return false;
            ConfigService.Wings.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return false;
            var equip = await EquipMgr.AddEquip(cfg, false);
            return equip != null;
        }

        public async ValueTask<bool> GmAddTitle(uint cfgId, bool add, bool use = false)
        {
            if (!IsActive) return false;
            ConfigService.Titles.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return false;
            if (add)
            {
                var title = await TitleMgr.AddTitle(cfgId);
                if (title != null && use)
                {
                    // 立即穿戴
                    await TitleMgr.ActiveTitle(title.Id);
                }
            }
            else
            {
                var title = TitleMgr.All.FirstOrDefault(p => p.CfgId == cfgId);
                if (title == null) return false;
                await TitleMgr.DelTitle(title.Id);
            }

            return true;
        }

        public async ValueTask<bool> GmAddTitle1(uint cfgId, string text = "", uint seconds = 0,
            bool send = true)
        {
            if (!IsActive) return false;
            ConfigService.Titles.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return false;
            var title = await TitleMgr.AddTitle(cfgId, text, seconds);
            if (title != null)
            {
                // 立即穿戴
                await TitleMgr.ActiveTitle(title.Id);
            }

            return true;
        }

        public async ValueTask<bool> GmDelShane(uint adminId)
        {
            if (!IsActive) return false;
            LogDebug($"管理员{adminId}解除监禁, 当前善恶{_shane}秒");
            if (_shane == 0) return true;
            _shane = 0;
            Entity.Shane = 0;
            await CheckShanEChange();
            return true;
        }

        public async ValueTask<bool> GmSetRoleType(byte type)
        {
            if (!IsActive) return false;
            var newType = (UserType) type;
            if (newType >= UserType.Robot) return false;
            var oldType = Entity.Type;
            if (oldType == newType) return true;

            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.Type, newType)
                .ExecuteAffrowsAsync();
            Entity.Type = newType;
            LastEntity.Type = newType;

            if (oldType == UserType.Gm)
            {
                // 移除游戏管理员称号,
                var title = TitleMgr.All.FirstOrDefault(p => p.CfgId == (uint) TitleId.Gm);
                if (title != null)
                {
                    if (title.Active)
                    {
                        // 先脱下
                        await TitleMgr.ActiveTitle(title.Id, false);
                    }

                    await TitleMgr.DelTitle(title.Id);
                }
            }

            if (newType == UserType.Gm)
            {
                // 增加游戏管理员称号
                var title = await TitleMgr.AddTitle((uint) TitleId.Gm);
                if (title != null)
                {
                    await TitleMgr.ActiveTitle(title.Id);
                }
            }

            return true;
        }

        public async ValueTask<bool> GmSetRoleFlag(byte type, bool value)
        {
            if (!IsActive) return false;
            var flagType = (FlagType) type;
            // 后台只允许修改禁言
            if (flagType != FlagType.WorldSilent && flagType != FlagType.SectSilent) return false;
            if (GetFlag(flagType) == value) return true;
            SetFlag(flagType, value);
            LastEntity.Flags = Entity.Flags;

            await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == RoleId)
                .Set(it => it.Flags, Entity.Flags)
                .ExecuteAffrowsAsync();

            // 通知
            if (IsEnterServer)
            {
                switch (flagType)
                {
                    case FlagType.WorldSilent:
                    {
                        await SendPacket(GameCmd.S2CChatSilent, new S2C_ChatSilent
                        {
                            RoleId = RoleId,
                            Silent = value
                        });
                    }
                        break;
                    case FlagType.SectSilent:
                    {
                        await SendPacket(GameCmd.S2CSectSilent, new S2C_SectSilent
                        {
                            RoleId = RoleId,
                            Silent = value
                        });
                    }
                        break;
                }
            }

            return true;
        }

        public async ValueTask<bool> GmSetMountSkill(uint mountId, int skIdx, uint skCfgId, byte skLevel, uint skExp)
        {
            if (!IsActive) return false;
            var mount = MountMgr.All.FirstOrDefault(p => p.Id == mountId);
            if (mount == null) return false;
            var ret = await mount.SetSkill(skIdx, skCfgId, skLevel, skExp);
            return ret;
        }

        // 检查当前是否已经安全锁定
        public bool CheckSafeLocked()
        {
            if (!IsActive) return true;
            if (Entity.SafeLocked)
            {
                SendNotice("安全锁已锁定, 无法进行当前的操作");
            }

            return Entity.SafeLocked;
        }

        // 获取等级奖励的总潜能点, 每一级奖励4个点
        public uint LevelPotential => 4 * (uint) Entity.Level + 100 * (uint) Entity.Relive;

        public uint ExchangedPotential = 0;

        // 设置等级，会引发潜能的变化
        public async Task SetLevel(byte level)
        {
            if (!IsActive) return;
            var maxLevel = ConfigService.GetRoleMaxLevel(Entity.Relive);
            if (level > maxLevel) return;
            // 记录修改之前的等级
            var oldLevel = Entity.Level;

            Entity.Level = level;
            // 计算当前等级的最大经验值
            ExpMax = ConfigService.GetRoleUpgradeExp(Entity.Relive, Entity.Level);

            if (level != oldLevel)
            {
                // 更新角色信息和排行榜
                await RedisService.SetRoleLevel(Entity);

                // 属性方案刷新潜能和属性
                await FreshAllSchemeAttrs();

                // 伙伴系统伴随角色升级、解锁新的伙伴
                await PartnerMgr.OnPlayerLevelUp();

                // 等级奖励
                {
                    var needSync = false;
                    foreach (var k in ConfigService.LevelRewards.Keys)
                    {
                        if (level < k) break;
                        // 新增，但是默认没领取
                        if (!_rewards.ContainsKey(k))
                        {
                            _rewards[k] = 0;
                            needSync = true;
                        }
                    }

                    if (needSync)
                    {
                        SyncRewards();
                        await SendLevelRewardList();
                    }
                }

                // 通知地图
                if (_mapGrain != null) _ = _mapGrain.SetPlayerLevel(OnlyId, Entity.Relive, Entity.Level);
                // 通知队伍
                if (InTeam) _ = TeamGrain.SetPlayerLevel(RoleId, Entity.Relive, Entity.Level);
                // 通知帮派
                if (InSect) _ = SectGrain.SetPlayerLevel(RoleId, Entity.Relive, Entity.Level);
            }
        }

        public async ValueTask<bool> StartPve(uint source, uint group = 10048, BattleType type = BattleType.Normal,
            bool yewai = false, byte starLevel = 0)
        {
            if (!IsActive) return false;
            if (InBattle) return false;
            // 队长才能发起战斗
            if (InTeam && !IsTeamLeader && !_teamLeave) return false;

            if (group == 0) group = 10048;
            ConfigService.MonsterGroups.TryGetValue(group, out var groupCfg);
            if (groupCfg?.Monsters == null || groupCfg.Monsters.Length == 0) return false;
            // 北俱芦洲 没有宝宝
            if (Entity.MapId == 1003) yewai = false;

            // 从Battles中创建一个id
            var battleId = await GlobalGrain.CreateBattle();
            var battleGrain = GrainFactory.GetGrain<IBattleGrain>(battleId);

            // 构建战斗发起请求
            var req = new StartBattleRequest
            {
                Type = type,
                Source = source,
                RoleId = RoleId,
                MonsterGroup = group,
                StarLevel = starLevel,
                ServerId = Entity.ServerId
            };

            // 构建我方阵营
            {
                var members = await BuildBattleTeamData(battleId, type, 1);
                if (members.Count <= 0)
                {
                    SendNotice("构建阵营失败，请稍候再试！");
                    return false;
                }
                req.Team1.AddRange(members);
            }

            // 构建怪物阵营
            {
                for (var i = 0; i < groupCfg.Monsters.Length; i++)
                {
                    if (groupCfg.Monsters[i] == 0) continue;
                    ConfigService.Monsters.TryGetValue(groupCfg.Monsters[i], out var cfg);
                    if (cfg == null) continue;
                    // if (type == BattleType.Cszl) {
                    //     req.Team2.Add(BuildBattleMemberDataFromMonsterForCszl(Entity.CszlLayer, cfg, i + 1));
                    // } else {
                        req.Team2.Add(BuildBattleMemberDataFromMonster(cfg, i + 1));
                    // }
                }

                // 野外补充宝宝怪物
                if (yewai && req.Team2.Count < 10)
                {
                    // 1~10，10个位置
                    var posMap = new Dictionary<int, byte>();
                    for (var i = 1; i <= 10; i++) posMap[i] = 0;
                    // 把已经有怪物的位置先移除
                    foreach (var mbData in req.Team2)
                    {
                        posMap.Remove(mbData.Pos);
                    }

                    var posArr = posMap.Keys.ToList();
                    if (posArr.Count > 0)
                    {
                        if (type == BattleType.ShenShouJiangLin)
                        {
                            var cfg = ConfigService.CatchedMonstersForShenShouJiangLin.GetValueOrDefault(_ssjl.ShenShouId, null);
                            // LogInformation($"神兽降临 神兽ID{_ssjl.ShenShouId}");
                            if (cfg != null)
                            {
                                var pos = posArr[Random.Next(0, posArr.Count)];
                                req.Team2.Add(BuildBattleMemberDataFromMonster(cfg, pos, true));
                            }
                        } else {
                        var cfg = ConfigService.GetRandomCatchedMonsterConfig();
                        if (cfg != null)
                        {
                            var pos = posArr[Random.Next(0, posArr.Count)];
                            req.Team2.Add(BuildBattleMemberDataFromMonster(cfg, pos, true));
                        }
                        }
                    }
                }
            }

            // 启动战斗，但不要await
            _battleGrain = battleGrain;
            _battleId = battleId;
            _campId = 1;
            _ = _battleGrain.StartUp(new Immutable<byte[]>(Packet.Serialize(req)));
            // 停止AOI同步
            if (_mapGrain != null)
            {
                _ = _mapGrain.PlayerEnterBattle(OnlyId, _battleId, _campId);
            }
            // 广播观战 金蟾送宝
            if (type == BattleType.JinChanSongBao)
            {
                // 构造消息
                var msg = new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Msg = Json.SafeSerialize(new { type = "JinChanSongBao" }),
                    From = BuildRoleInfo(),
                    To = 0,
                    BattleInfo = new InBattleInfo() { BattleId = _battleId, CampId = _campId }
                };
                _ = ServerGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
            }
            // // 广播观战 成神之路
            // if (type == BattleType.Cszl)
            // {
            //     // 构造消息
            //     var msg = new ChatMessage
            //     {
            //         Type = ChatMessageType.System,
            //         Msg = Json.SafeSerialize(new { type = "ChengShenZhiLu" }),
            //         From = BuildRoleInfo(),
            //         To = 0,
            //         BattleInfo = new InBattleInfo() { BattleId = _battleId, CampId = _campId }
            //     };
            //     _ = ServerGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
            // }
            // 广播观战 地煞星
            if (type == BattleType.DiShaXing && starLevel >= 18)
            {
                // 构造消息
                var msg = new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Msg = Json.SafeSerialize(new { type = "DiShaXing" }),
                    From = BuildRoleInfo(),
                    To = 0,
                    BattleInfo = new InBattleInfo() { BattleId = _battleId, CampId = _campId }
                };
                _ = ServerGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
            }
            // 广播观战 金翅大鹏
            if (type == BattleType.Eagle)
            {
                // 每30秒广播1次
                _eagleBroadCastTimer?.Dispose();
                _eagleBroadCastTimer = RegisterTimer(EagleBroadCast, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(30));
            }
            return true;
        }

        private Task EagleBroadCast(Object _)
        {
            // 战斗已经结束 则停掉广告
            if (_battleId == 0 || _campId == 0) {
                _eagleBroadCastTimer?.Dispose();
                _eagleBroadCastTimer = null;
                return Task.CompletedTask;
            }
            // 构造消息
            var msg = new ChatMessage
            {
                Type = ChatMessageType.System,
                Msg = Json.SafeSerialize(new { type = "Eagle" }),
                From = BuildRoleInfo(),
                To = 0,
                BattleInfo = new InBattleInfo() { BattleId = _battleId, CampId = _campId }
            };
            _ = ServerGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CChat, new S2C_Chat { Msg = msg })));
            return Task.CompletedTask;
        }

        private async Task EnterMap()
        {
            if (!IsActive) return;
            // 停掉杀人香的效果
            await StopIncense();

            // 退出之前的地图
            if (_mapGrain != null) await ExitMap();
            _mapGrain = null;

            // 检测mapId是否合法
            var mapId = Entity.MapId;
            ConfigService.Maps.TryGetValue(mapId, out _mapCfg);
            if (_mapCfg == null)
            {
                // 非法的mapId直接推到默认地图默认位置
                mapId = 1010;
                _mapCfg = ConfigService.Maps[mapId];
                _terrainCfg = ConfigService.Terrains[_mapCfg.Terrain];

                Entity.MapId = mapId;
                Entity.MapX = _mapCfg.StartPos.X;
                Entity.MapY = _mapCfg.StartPos.Y;
            }
            else
            {
                // 检测坐标是否合法, 非法的坐标也移动到地图的初始位置
                _terrainCfg = ConfigService.Terrains[_mapCfg.Terrain];
                if (Entity.MapX < 0 || Entity.MapX >= _terrainCfg.Cols) Entity.MapX = _mapCfg.StartPos.X;
                if (Entity.MapY < 0 || Entity.MapY >= _terrainCfg.Rows) Entity.MapY = _mapCfg.StartPos.Y;
            }

            // 获取地图引用
            _mapGrain = GrainFactory.GetGrain<IMapGrain>($"{Entity.ServerId}_{Entity.MapId}");
            // 进入地图, 这里将数据直接传入Map，避免后续频繁的管Player要, 不过后续如果修改了武器、坐骑、昵称、等级，需要去通知Map
            await _mapGrain.Enter(new Immutable<byte[]>(BuildMapObjectData()), _deviceWidth, _deviceHeight);
        }

        private async Task ExitMap()
        {
            if (!IsActive) return;
            try
            {
                // 停服的时候，有可能mapGrain已经被杀死了
                if (_mapGrain != null)
                {
                    await _mapGrain.Exit(OnlyId);
                }
            }
            catch (Exception)
            {
                // ignored
            }

            _mapGrain = null;

            _mapCfg = null;
            _terrainCfg = null;
        }

        private async Task ChangeMap(uint mapId, int x, int y)
        {
            if (!IsActive) return;
            // 在长安城就会被监禁
            if (!_inPrison && Entity.MapId == 1011 && _shane > 0)
            {
                await EnterPrison();
                await SendPacket(GameCmd.S2CPrisonTime, new S2C_PrisonTime
                {
                    Time = _shane
                });
                return;
            }

            Entity.MapId = mapId;
            Entity.MapX = x;
            Entity.MapY = y;

            // 进入地图
            await EnterMap();

            if (IsTeamLeader)
                _ = TeamGrain.UpdateMap(Entity.MapId, Entity.MapX, Entity.MapY);
        }

        // 触发暗雷怪
        private void TriggerAnlei()
        {
            if (!IsActive) return;
            ConfigService.Maps.TryGetValue(Entity.MapId, out var mapCfg);
            var array = mapCfg?.Anlei;
            if (array == null || array.Length == 0) return;

            // 随机一个怪物组
            var group = array[Random.Next(0, array.Length)];
            _ = StartPve(0, group, BattleType.Normal, true);
        }

        // 开始监禁
        private async Task EnterPrison()
        {
            if (!IsActive) return;
            if (_inPrison) return;
            _inPrison = true;
            if (InTeam)
            {
                // 主动离队
                _ = TeamGrain.Exit(RoleId);
            }

            // 去天牢待着
            await ChangeMap(1201, 59, 4);
            await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
            {
                Map = 1201,
                X = 59,
                Y = 4,
                Immediate = true
            });

            LogDebug($"开始监禁{_shane}秒");
        }

        // 停止监禁
        private async Task LeavePrison()
        {
            if (!IsActive) return;
            if (_shane > 0) return;
            _inPrison = false;

            await ChangeMap(1011, 112, 78);
            await SendPacket(GameCmd.S2CMapSyncPos, new S2C_MapSyncPos
            {
                Map = 1011,
                X = 112,
                Y = 78,
                Immediate = true
            });

            LogDebug("解除监禁");
        }

        private async ValueTask<bool> CheckShanEChange()
        {
            if (!IsActive) return false;
            // 检查坐牢的状态
            var change = false;
            if (Entity.MapId == 1201 && _shane <= 0)
            {
                await LeavePrison();
                change = true;
            }

            if (Entity.MapId == 1011 && _shane > 0)
            {
                await EnterPrison();
                change = true;
            }

            await SendPacket(GameCmd.S2CPrisonTime, new S2C_PrisonTime
            {
                Time = _inPrison ? _shane : 0
            });

            return change;
        }

        public async Task AddExp(ulong value, bool send = true)
        {
            if (!IsActive) return;
            if (value == 0) return;

            var finalExp = Entity.Exp + value; //最终的经验值
            var finalLevel = Entity.Level; //最终的等级
            var maxExp = ConfigService.GetRoleUpgradeExp(Entity.Relive, finalLevel);

            // 计算最终等级
            while (finalExp >= maxExp)
            {
                finalExp -= maxExp;
                // 递增1级等级
                finalLevel += 1;
                // 计算新等级的最大经验值
                maxExp = ConfigService.GetRoleUpgradeExp(Entity.Relive, finalLevel);
                if (maxExp == 0)
                {
                    // 说明已经到达该转生等级下的最大等级, 不能再升级了
                    finalLevel -= 1;
                    // maxExp = ConfigService.GetRoleUpgradeExp(Entity.Relive, finalLevel);
                    // finalExp = maxExp;
                    // 原来不保留溢出经验，这里保留溢出经验，用来兑换属性点
                    finalExp += maxExp;
                    break;
                }
            }

            var addLevel = Math.Max(0, finalLevel - Entity.Level);
            var addExp = value;

            Entity.Exp = finalExp;
            if (finalLevel != Entity.Level)
            {
                await SetLevel(finalLevel);
            }

            // 发送给客户端
            if (send && (addLevel > 0 || addExp > 0))
            {
                await SendRoleExp(addLevel, addExp);
            }
        }

        public async ValueTask<bool> AddItem(uint itemCfgId, int num, bool send = true, string tag = "")
        {
            if (!IsActive) return false;
            ConfigService.Items.TryGetValue(itemCfgId, out var itemCfg);
            if (itemCfg == null) return false;
            if (itemCfg.Type < 4)
            {
                await AddBagItem(itemCfgId, num, send, tag);
                return true;
            }

            switch (itemCfg.Type)
            {
                case 4:
                    await AddExp((ulong) num * (ulong) itemCfg.Num);
                    break;
                case 5:
                    if (itemCfgId == 91004)
                    {
                        // 帮派书简
                        await AddMoney(MoneyType.Contrib, num * itemCfg.Num, tag);
                    }
                    else
                    {
                        await AddMoney(MoneyType.Silver, num * itemCfg.Num, tag);
                    }

                    break;
                case 6:
                    await AddMoney(MoneyType.Jade, num * itemCfg.Num, tag);
                    break;
                case 7:
                case 8:
                {
                    // 装备
                    var category = -1;
                    var index = -1;
                    var grade = -1;
                    foreach (var property in itemCfg.Json.EnumerateObject())
                    {
                        if (property.NameEquals("type"))
                        {
                            category = property.Value.GetInt32();
                        }
                        else if (property.NameEquals("index"))
                        {
                            index = property.Value.GetInt32();
                        }
                        else if (property.NameEquals("grade"))
                        {
                            grade = property.Value.GetInt32();
                        }
                    }

                    if (category >= 0 && index >= 0 && grade >= 0)
                    {
                        // 原配置表的装备type是从0开始的, 所以加1, 道具中只能获取到一阶装备
                        await EquipMgr.AddEquip((EquipCategory) (category + 1), index, grade);
                    }
                }
                    break;
                case 9:
                {
                    // 宠物
                    foreach (var property in itemCfg.Json.EnumerateObject())
                    {
                        if (property.NameEquals("petid"))
                        {
                            await CreatePet(property.Value.GetUInt32());
                        }
                    }
                }
                    break;
                case 11:
                {
                    // json配置的数组中随机选择一个item
                    var len = itemCfg.Json.GetArrayLength();
                    if (len > 0)
                    {
                        var idx = Random.Next(0, len);
                        var itemId = itemCfg.Json[idx].GetUInt32();
                        await AddBagItem(itemId, 1, send, tag);
                    }
                }
                    break;
                case 12:
                {
                    await AddMoney(MoneyType.BindJade, num, tag);
                }
                    break;
                default:
                    await AddBagItem(itemCfgId, num, send, tag);
                    break;
            }

            return true;
        }

        public async Task UseItem(ItemConfig cfg, uint num, uint target)
        {
            if (!IsActive) return;
            switch (cfg.Type)
            {
                case 4: //经验
                case 5: //银两
                case 6: // 仙玉
                case 12: //绑定仙玉
                    await AddBagItem(cfg.Id, (int) -num, tag: "UseItem");
                    await AddItem(cfg.Id, (int) num, tag: "UseItem");
                    return;
                case 7:
                case 8:
                    // 装备
                    await AddBagItem(cfg.Id, (int) -num, tag: "UseItem");
                    await AddItem(cfg.Id, (int) num, tag: "UseItem");
                    return;
                case 9:
                {
                    await AddBagItem(cfg.Id, (int) -num, tag: "UseItem");
                    await AddItem(cfg.Id, (int) num, tag: "UseItem");
                }
                    break;
                case 11:
                {
                    // 元气丹, 使用后可以随机获得一个宠物元气丹
                    if (cfg.Json.ValueKind != JsonValueKind.Array)
                    {
                        SendNotice("配置出错");
                        return;
                    }

                    var list = new List<uint>();
                    foreach (var element in cfg.Json.EnumerateArray())
                    {
                        var itemId = element.GetUInt32();
                        if (itemId > 0) list.Add(itemId);
                    }

                    if (list.Count == 0)
                    {
                        SendNotice("配置出错");
                        return;
                    }

                    var gets = new Dictionary<uint, int>();
                    for (var i = 0; i < num; i++)
                    {
                        var getItemId = list[Random.Next(0, list.Count)];
                        if (gets.ContainsKey(getItemId))
                            gets[getItemId]++;
                        else
                            gets[getItemId] = 1;
                    }

                    // 扣除道具
                    await AddBagItem(cfg.Id, (int) -num, tag: "UseItem");
                    // 获得奖励
                    foreach (var (k, v) in gets)
                    {
                        await AddBagItem(k, v, tag: "使用元气丹获得");
                    }

                    return;
                }
            }

            switch (cfg.Id)
            {
                case 50001:
                case 50002:
                case 50003:
                {
                    // 藏宝图类型
                    var json = cfg.Json;
                    if (json.ValueKind != JsonValueKind.Object) return;

                    // 暂时不支持
                    if (json.TryGetProperty("monster", out var property))
                    {
                        SendNotice("这类藏宝图暂时不支持使用");
                        return;
                    }

                    // 扣除道具
                    await AddBagItem(cfg.Id, -(int) num, tag: "UseItem");

                    if (json.TryGetProperty("money", out property))
                    {
                        await AddMoney(MoneyType.Silver, property.GetInt32());
                    }

                    if (json.TryGetProperty("item", out property))
                    {
                        await AddItem(property.GetUInt32(), 1, true, "藏宝图");
                    }

                    // 50%的概率赠送一个神兽丹
                    if (Random.Next(0, 100) < 50)
                    {
                        await AddBagItem(10114, 1, true, "藏宝图赠送");
                    }

                    return;
                }
                case 80001:
                {
                    // 一箱银币, 一次只能使用一个, 主要是担心直接溢出
                    await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    await AddMoney(MoneyType.Silver, cfg.Num, "使用一箱银币");
                    break;
                }
                case 10101:
                case 10102:
                case 10103:
                {
                    // 技能书残卷
                    if (GetBagItemNum(cfg.Id) < cfg.Num) return;
                    await AddBagItem(cfg.Id, -cfg.Num, tag: "UseItem");
                    // 随机获得宠物技能
                    var sk = ConfigService.GetRandomPetSkill(cfg.Level);
                    if (sk > 0) await AddBagItem(sk, 1, tag: "UseItem");
                    return;
                }
                case 60200:
                case 60201:
                case 60202:
                case 60203:
                {
                    // 普通、高级、终极技能书
                    if (GetBagItemNum(cfg.Id) < num) return;
                    await AddBagItem(cfg.Id, -(int) num, tag: "UseItem");
                    // 随机获得宠物技能
                    for (var i = 0; i < num; i++)
                    {
                        var sk = ConfigService.GetRandomPetSkill(cfg.Level, 60202 == cfg.Id);
                        if (sk > 0) await AddBagItem(sk, 1, tag: "UseItem");
                    }
                }
                    break;
                case 10202:
                case 10204:
                {
                    // 伙伴修炼册
                    var ret = await PartnerMgr.AddPartnerExp(target, (ulong) cfg.Num);
                    if (ret) await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    return;
                }
                case 10203:
                {
                    // 超级星梦石, 重置角色加点
                    var ret = await SchemeMgr.Scheme.ResetApAttrs();
                    if (ret) await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    return;
                }
                case 10001:
                case 10004:
                {
                    // 战斗中禁止使用
                    if (InBattle)
                    {
                        SendNotice("战斗中禁止使用引妖香");
                        return;
                    }
                    // 神兽降临禁止使用
                    if (_ssjl.Signed)
                    {
                        SendNotice("神兽降临禁止使用引妖香");
                        return;
                    }

                    // 引妖香
                    await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    await UseIncense(cfg.Num);
                    return;
                }
                case 10116:
                {
                    // 凝魂丹, num是多少点经验换1点
                    var ret = await PetMgr.UseNingHunDan(target, (uint) cfg.Num);
                    if (ret) await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    return;
                }
                case 90001:
                {
                    // 经验转魂魄, num是 多少点经验换1点魂魄
                    if (GetBagItemNum(cfg.Id) == 0) return;
                    var ret = await PetMgr.UseExp2HunPo(target, cfg.Num);
                    if (ret) await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    return;
                }
                case 10117:
                {
                    // 龙骨
                    if (PetMgr.AddPetKeel(target))
                    {
                        await AddBagItem(cfg.Id, -1, tag: "UseItem");
                        return;
                    }

                    return;
                }
                case 10111:
                case 10120:
                case 10121:
                {
                    // 宠物亲密丹
                    if (GetBagItemNum(cfg.Id) == 0) return;
                    if (PetMgr.UseQinMiDan(target, cfg.Num))
                    {
                        await AddBagItem(cfg.Id, -1, tag: "UseItem");
                        return;
                    }

                    return;
                }
                case 100002:
                case 100003:
                {
                    // 坐骑修炼丹
                    if (GetBagItemNum(cfg.Id) == 0) return;
                    var ret = await MountMgr.AddMountExp(target, (uint) cfg.Num);
                    if (ret) await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    return;
                }
                case 90100:
                case 90101:
                case 90102:
                {
                    // 五常神兽、珍稀神兽、真*珍稀神兽
                    if (GetBagItemNum(cfg.Id) < num) return;
                    if (cfg.Json.ValueKind == JsonValueKind.Array)
                    {
                        var array = new List<uint>(10);
                        foreach (var element in cfg.Json.EnumerateArray())
                        {
                            element.TryGetUInt32(out var nextCfgId);
                            if (nextCfgId > 0) array.Add(nextCfgId);
                        }

                        if (array.Count > 0)
                        {
                            await AddBagItem(cfg.Id, -(int) num, tag: "UseItem");
                            for (var i = 0; i < num; i++)
                            {
                                var idx = Random.Next(0, array.Count);
                                await AddItem(array[idx], 1, tag: "UseItem");
                            }
                        }
                    }
                }
                    break;
                // 随机变身卡物品使用
                case 9904:
                    {
                        if (GetBagItemNum(cfg.Id) < 0) return;
                        await AddBagItem(cfg.Id, -1, tag: "UseItem");
                        AddBianShenCard();
                        var resp = new S2C_BianshenInfo() { Info = Entity.Bianshen };
                        await SendPacket(GameCmd.S2CBianshenInfo, resp);
                    }
                    break;
                // 还原丹
                case 9906:
                    {
                        if (GetBagItemNum(cfg.Id) < 0) return;
                        await ReqBianshenReset();
                    }
                    break;
                // 双倍经验点
                case 9918:
                {
                    if (GetBagItemNum(cfg.Id) < 0) return;
                    await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    var left = await RedisService.GetRoleX2ExpLeft(RoleId);
                    await RedisService.SetRoleX2ExpLeft(RoleId, (uint)(left + cfg.Num));
                    SendNotice($"+{cfg.Num}双倍经验点");
                }
                break;
                // 飞升券
                case 100327:
                {
                    if (GetBagItemNum(cfg.Id) < 0) return;
                    await AddBagItem(cfg.Id, -1, tag: "useItem");

                    Relives.Add(new ReliveRecord { Race = (Race)Entity.Race, Sex = (Sex)Entity.Sex });
                    SyncRelives();

                    Entity.Relive = 4;
                    Entity.Level = 230;
                    Entity.Exp = 0;
                    ExpMax = ConfigService.GetRoleUpgradeExp(Entity.Relive, Entity.Level);
                    await RedisService.SetRoleInfo(Entity);

                    // 技能换掉，但是技能经验保持
                    var skillExps = new Dictionary<uint, uint>();
                    foreach (var sk in _skills)
                    {
                        skillExps[sk.Idx] = sk.Exp;
                    }

                    Entity.Skills = string.Empty;
                    InitSkills();
                    foreach (var sk in _skills)
                    {
                        skillExps.TryGetValue(sk.Idx, out var exp);
                        sk.Exp = exp;
                    }

                    // 同步给Entity, 准备入库
                    SyncSkills();

                    // 脱下方案所有的装备
                    foreach (var scheme in SchemeMgr.All)
                    {
                        await scheme.Reset(true);
                    }

                    // 通知前端
                    await SendPacket(GameCmd.S2CRelive, new S2C_Relive
                    {
                        CfgId = Entity.CfgId,
                        Race = Entity.Race,
                        Sex = Entity.Sex,
                        Skills = { _skills },
                        Relive = Entity.Relive,
                        Level = Entity.Level,
                        Exp = Entity.Exp,
                        ExpMax = ExpMax
                    });

                    await SetLevel(Entity.Level);
                    await SendRoleExp(Entity.Level);

                    // 通知地图
                    if (_mapGrain != null)
                    {
                        _ = _mapGrain.SetPlayerLevel(OnlyId, Entity.Relive, Entity.Level);
                        _ = _mapGrain.SetPlayerCfgId(OnlyId, Entity.CfgId);
                    }

                    // 通知队伍
                    if (InTeam)
                    {
                        _ = TeamGrain.SetPlayerLevel(RoleId, Entity.Relive, Entity.Level);
                        _ = TeamGrain.SetPlayerCfgId(RoleId, Entity.CfgId);
                    }

                    // 通知帮派
                    if (InSect)
                    {
                        _ = SectGrain.SetPlayerLevel(RoleId, Entity.Relive, Entity.Level);
                        _ = SectGrain.SetPlayerCfgId(RoleId, Entity.CfgId);
                    }

                    SendNotice("恭喜飞升成功！");
                }
                break;
                // 随机天策符
                case 100328:
                {
                    if (GetBagItemNum(cfg.Id) < 0) return;
                    await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    List<uint> l = new (){100324, 100325, 100326};
                    var id = l[Random.Next(l.Count)];
                    await AddBagItem(id, 1, tag: "物品使用");
                    SendNotice($"恭喜获得一张{ConfigService.Items[id].Name}");
                }
                break;
                // 随机升阶石
                case 500046:
                {
                    if (GetBagItemNum(cfg.Id) < 0) return;
                    await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    List<uint> l = new() { 500041, 500044, 500042, 500043, 500045, };
                    var id = l[Random.Next(l.Count)];
                    await AddBagItem(id, 1, tag: "物品使用");
                    SendNotice($"恭喜获得一个{ConfigService.Items[id].Name}");
                }
                break;
                // 全服烟花
                case 500057:
                {
                    if (GetBagItemNum(cfg.Id) < 0) return;
                    await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    SendNotice($"燃放成功");
                    // 随机[1,100]积分
                    var count = Random.Next(1, 100) + 1;
                    await AddMoney(MoneyType.BindJade, count, tag: "全服烟花");
                    var resp = new S2C_FireWorks() { Role = BuildRoleInfo(), BindJade = (uint)count };
                    await ServerGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CFireWorks, resp)));
                }
                break;
                // 随机灵气果
                case 500051:
                {
                    if (GetBagItemNum(cfg.Id) < 0) return;
                    await AddBagItem(cfg.Id, (int)-num, tag: "UseItem");
                    List<uint> l = new() { 500047, 500048, 500049, };
                    var id = l[Random.Next(l.Count)];
                    await AddBagItem(id, (int)num, tag: "物品使用");
                    SendNotice($"恭喜获得一个{ConfigService.Items[id].Name}");
                }
                break;
                // 炼化锁--直接给1积分
                case 9902:
                {
                    if (GetBagItemNum(cfg.Id) < 0) return;
                    await AddBagItem(cfg.Id, -1, tag: "UseItem");
                    await AddMoney(MoneyType.Jade, 10000, tag: "炼化锁");
                }
                break;
                default:
                {
                    // 宠物经验丹
                    if (cfg.Id is >= 10112 and <= 10114 or 92000)
                    {
                        if (GetBagItemNum(cfg.Id) == 0) return;
                        var ret = await PetMgr.AddPetExp(target, (ulong) cfg.Num);
                        if (ret) await AddBagItem(cfg.Id, -1, tag: "UseItem");
                        return;
                    }

                    // 宠物元气丹
                    if ((int) MathF.Floor(cfg.Id / 100f) == 105)
                    {
                        if (cfg.Json.ValueKind != JsonValueKind.Object) return;
                        if (!cfg.Json.TryGetProperty("pet", out var petCfgIdElement)) return;
                        if (!cfg.Json.TryGetProperty("rate", out var petRateElement)) return;
                        var petCfgId = petCfgIdElement.GetUInt32();
                        var petRate = petRateElement.GetSingle();
                        if (PetMgr.UseYuanQiDan(target, petCfgId, petRate))
                            await AddBagItem(cfg.Id, -1, tag: "UseItem");
                        return;
                    }

                    return;
                }
            }
        }

        // 使用引妖香
        private async Task UseIncense(int seconds)
        {
            if (!IsActive) return;
            await StopIncense();

            _incenseTime = (uint) seconds;
            // _usingIncense = true;
            _anleiCnt = (uint) Random.Next(20, 41);
            await SendPacket(GameCmd.S2CIncenseTime, new S2C_IncenseTime {Time = _incenseTime});

            // _incenseTimer = RegisterTimer(IncenseTimeout, null, TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(1));
        }

        // 停止杀人香效果
        private Task StopIncense()
        {
            if (!IsActive) return Task.CompletedTask;
            // _usingIncense = false;
            // _incenseTimer?.Dispose();
            // _incenseTimer = null;

            _incenseTime = 0;
            return SendPacket(GameCmd.S2CIncenseTime, new S2C_IncenseTime {Time = _incenseTime});
        }

        // 杀人香有效时间消失
        // private Task IncenseTimeout(object _)
        // {
        //     return StopIncense();
        // }

        /// <summary>
        /// 获取包裹内的总数量, 包括装备、配饰和物品
        /// </summary>
        public int BagCount => EquipMgr.BagCount + Items.Count;

        /// <summary>
        /// 获取仓库内的总数量, 包括装备、配饰和物品
        /// </summary>
        public int RepoCount => EquipMgr.RepoCount + Repos.Count;

        /// <summary>
        /// 背包是否已经满了
        /// </summary>
        public bool IsBagFull => BagCount >= GameDefine.LimitBagItemKindNum;

        /// <summary>
        /// 仓库是否满了
        /// </summary>
        public bool IsRepoFull => RepoCount >= GameDefine.LimitRepoItemKindNum;

        /// <summary>
        /// 检测背包如果再装这么多项内容是否会溢出
        /// </summary>
        public bool CheckIsBagOverflow(uint num)
        {
            if (!IsActive) return true;
            return BagCount + num > GameDefine.LimitBagItemKindNum;
        }

        public bool CheckIsRepoOverflow(uint num)
        {
            if (!IsActive) return true;
            return Repos.Count + num > GameDefine.LimitRepoItemKindNum;
        }

        public void AddMp(int value)
        {
            if (!IsActive) return;
            if (value == 0) return;
            if (value > 0)
            {
            }
        }

        public uint GetBagItemNum(uint itemCfgId)
        {
            if (!IsActive) return 0;
            Items.TryGetValue(itemCfgId, out var num);
            return num;
        }

        /// <summary>
        /// BattleGrain中使用道具
        /// </summary>
        public ValueTask<uint> GetBagItemCount(uint cfgId)
        {
            if (!IsActive) return ValueTask.FromResult((uint)0);
            Items.TryGetValue(cfgId, out var num);
            return ValueTask.FromResult(num);
        }

        public bool GetFlag(FlagType ft)
        {
            if (!IsActive) return true;
            return _flags.GetFlag(ft);
        }

        public void SetFlag(FlagType ft, bool value)
        {
            if (!IsActive) return;
            _flags.SetFlag(ft, value);
            Entity.Flags = _flags.Value;
        }

        // 创建动态Npc
        public ValueTask<uint> CreateNpc(CreateNpcRequest req)
        {
            if (!IsActive) return new ValueTask<uint>(0);
            return ServerGrain.CreateNpc(
                new Immutable<byte[]>(Packet.Serialize(req)));
        }

        // 删除Npc
        public void DeleteNpc(uint onlyId)
        {
            if (!IsActive) return;
            // 不必等待
            _ = ServerGrain.DeletePlayerNpc(onlyId, RoleId);
        }

        public void DeleteTeamNpc(uint onlyId)
        {
            if (!IsActive) return;
            if (IsTeamLeader) _ = ServerGrain.DeleteTeamNpc(onlyId, TeamId);
        }

        public IPlayerGrain FindPlayer(uint roleId)
        {
            if (!IsActive) return null;
            return GrainFactory.GetGrain<IPlayerGrain>(roleId);
        }

        // 通知网关参数不合法
        public void BadRequest()
        {
            if (!IsActive) return;
            _ = _packet.SendStatus(RoleId, WebSocketCloseStatus.InvalidPayloadData, true);
        }

        // 发送数据包给网关, 这个函数可以使用await进行等待以确保发送的数据是按照预期的顺序
        public Task SendPacket(GameCmd command, IMessage msg = null)
        {
            if (!IsActive) return Task.CompletedTask;
            // 玩家进入游戏之前只能收发心跳
            if (IsEnterServer || command == GameCmd.S2CHeartBeat)
                return _packet.SendPacket(RoleId, command, msg);
            return Task.CompletedTask;
        }

        public Task SendPacket(GameCmd command, byte[] payload)
        {
            if (!IsActive) return Task.CompletedTask;
            // 玩家进入游戏之前只能收发心跳
            if (IsEnterServer || command == GameCmd.S2CHeartBeat)
                return _packet.SendPacket(RoleId, command, payload);
            return Task.CompletedTask;
        }

        // 发送通知
        public void SendNotice(string notice)
        {
            if (!IsActive) return;
            if (string.IsNullOrWhiteSpace(notice)) return;
            SendPacket(GameCmd.S2CNotice, new S2C_Notice {Text = notice});
        }

        // 发送通知
        public Task SendNpcNotice(uint npcCfgId, string notice)
        {
            if (!IsActive) return Task.CompletedTask;
            return SendPacket(GameCmd.S2CNpcNotice, new S2C_NpcNotice
            {
                CfgId = npcCfgId,
                Text = notice
            });
        }

        // 全服广播 跑马灯
        public void BroadcastScreenNotice(string text, int front)
        {
            if (!IsActive) return;
            if (string.IsNullOrWhiteSpace(text)) return;
            var bytes = Packet.Serialize(GameCmd.S2CScreenNotice, new S2C_ScreenNotice
            {
                Text = text,
                Front = 0
            });
            _ = ServerGrain.Broadcast(new Immutable<byte[]>(bytes));
        }

        private Task SendRoleInfo()
        {
            if (!IsActive) return Task.CompletedTask;
            var resp = new S2C_RoleInfo
            {
                OnlyId = OnlyId,
                RoleId = RoleId,
                Nickname = Entity.NickName,
                CfgId = Entity.CfgId,
                Sex = (Sex) Entity.Sex,
                Race = (Race) Entity.Race,
                Relive = Entity.Relive,
                Level = Entity.Level,
                Exp = Entity.Exp,
                ExpMax = ExpMax,
                Contrib = Entity.Contrib,
                Silver = Entity.Silver,
                Jade = Entity.Jade,
                BindJade = Entity.BindJade,
                SldhGongji = Entity.SldhGongJi,
                WzzzJiFen = Entity.WzzzJiFen,
                Skills = {_skills},
                Color1 = Entity.Color1,
                Color2 = Entity.Color2,
                XlLevel = Entity.XlLevel,
                RoleType = (uint) Entity.Type,
                WorldSilent = GetFlag(FlagType.WorldSilent),
                SectSilent = GetFlag(FlagType.SectSilent),
                ShanXianOrder = GetFlag(FlagType.ShanXianOrder),

                InBattle = InBattle,
                Locked = Entity.SafeLocked,
                LockSeted = !string.IsNullOrWhiteSpace(Entity.SafeCode),
                Spread = Entity.Spread,
                SpreadTime = Entity.SpreadTime,

                MapId = Entity.MapId,
                MapX = Entity.MapX,
                MapY = Entity.MapY,
                Weapon = MapWeapon,
                Wing = MapWing,
                Mount = MountMgr.ActiveMountCfgId,

                GuoShi = Entity.GuoShi,
                AutoSkill = Entity.AutoSkill,
                AutoSyncSkill = Entity.AutoSyncSkill,
                Skins = { SkinUse },
                Bianshen = Bianshen.current.id,
                VipLevel = VipLevel,
                QiegeLevel = QieGeLevel,
            };

            return SendPacket(GameCmd.S2CRoleInfo, resp);
        }

        public Task SendRoleExp(int addLevel = 0, ulong addExp = 0)
        {
            if (!IsActive) return Task.CompletedTask;
            var resp = new S2C_RoleExp
            {
                Level = Entity.Level,
                LevelAdd = addLevel,
                Exp = Entity.Exp,
                ExpMax = ExpMax,
                ExpAdd = addExp
            };
            return SendPacket(GameCmd.S2CRoleExp, resp);
        }

        // 刷新角色武器
        public async Task RefreshWeapon(bool send = true)
        {
            if (!IsActive) return;
            MapWeapon = null;
            var equip = EquipMgr.FindEquip(SchemeMgr.Scheme.WeaponId);
            if (equip != null)
            {
                MapWeapon = new MapObjectEquipData
                {
                    CfgId = equip.CfgId,
                    Category = equip.Category,
                    Gem = equip.Gem,
                    Level = equip.Grade
                };
                // 属性Redis
                var dict = new Dictionary<string, int>()
                {
                    ["cfgId"] = (int)equip.CfgId,
                    ["category"] = (int)equip.Category,
                    ["gem"] = (int)equip.Gem,
                    ["level"] = (int)equip.Grade,
                };
                _ = RedisService.SetRoleWeapon(Entity, Json.SafeSerialize(dict));
                // 通知帮派
                if (SectGrain != null)
                {
                    _ = SectGrain.SetPlayerWeapon(RoleId, equip.CfgId, (int)equip.Category, equip.Gem, equip.Grade);
                }
                // 通知队伍
                if (InTeam)
                {
                    _ = TeamGrain.SetPlayerWeapon(RoleId, equip.CfgId, (int)equip.Category, equip.Gem, equip.Grade);
                }
            }


            if (send)
            {
                await SendPacket(GameCmd.S2CRoleWeapon, new S2C_RoleWeapon {Data = MapWeapon});
            }

            // 通知地图
            if (_mapGrain != null)
            {
                _ = _mapGrain.SetPlayerWeapon(OnlyId, new Immutable<byte[]>(Packet.Serialize(MapWeapon)));
            }
        }

        public async Task RefreshWing(bool send = true)
        {
            if (!IsActive) return;
            MapWing = null;
            var equip = EquipMgr.FindEquip(SchemeMgr.Scheme.WingId);
            if (equip != null)
            {
                MapWing = new MapObjectEquipData
                {
                    Category = equip.Category,
                    CfgId = equip.CfgId,
                    Gem = equip.Gem,
                    Level = equip.Grade
                };
                // 属性Redis
                var dict = new Dictionary<string, int>()
                {
                    ["cfgId"] = (int)equip.CfgId,
                    ["category"] = (int)equip.Category,
                    ["gem"] = (int)equip.Gem,
                    ["level"] = (int)equip.Grade,
                };
                _ = RedisService.SetRoleWing(Entity, Json.SafeSerialize(dict));
                // 通知帮派
                if (SectGrain != null)
                {
                    _ = SectGrain.SetPlayerWing(RoleId, equip.CfgId, (int)equip.Category, equip.Gem, equip.Grade);
                }
                // 通知队伍
                if (InTeam)
                {
                    _ = TeamGrain.SetPlayerWing(RoleId, equip.CfgId, (int)equip.Category, equip.Gem, equip.Grade);
                }
            }

            if (send)
            {
                await SendPacket(GameCmd.S2CRoleWing, new S2C_RoleWing {Data = MapWing});
            }

            // 通知地图
            if (_mapGrain != null)
            {
                _ = _mapGrain.SetPlayerWing(OnlyId, new Immutable<byte[]>(Packet.Serialize(MapWing)));
            }
        }

        public async Task RefreshMount()
        {
            if (!IsActive) return;
            // 通知地图
            if (_mapGrain != null)
            {
                _ = _mapGrain.SetPlayerMount(OnlyId, MountMgr.ActiveMountCfgId);
            }
            await Task.CompletedTask;
        }

        public void RefreshTitle()
        {
            if (!IsActive) return;
            byte[] bits = null;
            if (TitleMgr.Title != null)
            {
                bits = Packet.Serialize(TitleMgr.Title.BuildPbData());
            }
            // 通知地图
            if (_mapGrain != null)
            {

                _ = _mapGrain.SetPlayerTitle(OnlyId, new Immutable<byte[]>(bits));
            }
        }

        // 下发所有物品
        private Task SendItemList()
        {
            if (!IsActive) return Task.CompletedTask;
            var resp = new S2C_ItemList();
            foreach (var (k, v) in Items)
            {
                if (v > 0) resp.BagList.Add(new ItemData {Id = k, Num = v});
            }

            foreach (var (k, v) in Repos)
            {
                if (v > 0) resp.RepoList.Add(new ItemData {Id = k, Num = v});
            }

            // LogDebug($"下发所有物品   userid={RoleId}");
            return SendPacket(GameCmd.S2CItemList, resp);
        }

        // 下发等级奖励
        private Task SendLevelRewardList()
        {
            if (!IsActive) return Task.CompletedTask;
            var resp = new S2C_LevelRewardList();
            foreach (var (k, v) in _rewards)
            {
                resp.List.Add(new UintPair {Key = k, Value = v});
            }

            return SendPacket(GameCmd.S2CLevelRewardList, resp);
        }

        // 解析relives
        private void InitRelives()
        {
            if (!IsActive) return;
            Relives = new List<ReliveRecord>(3);
            var str = Entity.Relives;
            if (!string.IsNullOrWhiteSpace(str))
            {
                var lines = str.Split(",");
                for (var i = 0; i < lines.Length; i++)
                {
                    if (i >= Entity.Relive) break;
                    var chars = lines[i];
                    byte.TryParse(chars[0].ToString(), out var race);
                    byte.TryParse(chars[1].ToString(), out var sex);
                    Relives.Add(new ReliveRecord
                    {
                        Race = (Race) race,
                        Sex = (Sex) sex
                    });
                }
            }
        }

        // 同步relives到Entity
        private void SyncRelives()
        {
            if (!IsActive) return;
            if (Relives == null || Relives.Count == 0)
            {
                Entity.Relives = string.Empty;
                return;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < Relives.Count; i++)
            {
                var record = Relives[i];
                sb.Append((byte) record.Race);
                sb.Append((byte) record.Sex);
                if (i < Relives.Count - 1)
                    sb.Append(",");
            }

            Entity.Relives = sb.ToString();
        }

        private void InitRewards()
        {
            if (!IsActive) return;
            _rewards = new Dictionary<byte, byte>();
            var str = Entity.Rewards;
            if (!string.IsNullOrWhiteSpace(str))
            {
                var dic = Json.SafeDeserialize<Dictionary<byte, byte>>(str);
                foreach (var (k, v) in dic)
                {
                    if (ConfigService.LevelRewards.ContainsKey(k))
                    {
                        _rewards[k] = v;
                    }
                }
            }
        }

        private void SyncRewards()
        {
            if (!IsActive) return;
            if (_rewards == null || _rewards.Count == 0) Entity.Rewards = string.Empty;
            else Entity.Rewards = Json.SafeSerialize(_rewards);
        }

        // 解析skills
        private void InitSkills()
        {
            if (!IsActive) return;
            _skills = new List<SkillData>((Race)Entity.Race == Race.Long ? 7 : 6);
            // 默认填充0
            var list = GameDefine.DefSkills[(Race) Entity.Race][
                (Sex) Entity.Sex];
            // 当前转生等级下的最大技能经验
            var expMax = ConfigService.GetRoleSkillMaxExp(Entity.Relive);
            for (var i = 0; i < list.Count && i < 6; i++)
            {
                var skData = new SkillData {Id = list[i], ExpMax = expMax, Exp = 0, Idx = (uint) i};
                _skills.Add(skData);
                UpdateSkillCost(skData);
            }

            // 从数据库中读取并覆盖数据
            var str = Entity.Skills;
            if (!string.IsNullOrWhiteSpace(str))
            {
                // 解析数据
                var exps = Json.Deserialize<List<uint>>(str);
                for (var i = 0; i < exps.Count && i < 6; i++)
                {
                    if (i >= _skills.Count) break;
                    _skills[i].Exp = exps[i];
                    UpdateSkillCost(_skills[i]);
                }
            }
            // 龙族--逆鳞
            if ((Race)Entity.Race == Race.Long) {
                var skData = new SkillData {Id = SkillId.NiLin, ExpMax = 0, Exp = 0, Idx = 6};
                _skills.Add(skData);
            }
        }

        // 解析skins
        private void InitSkins()
        {
            if (!IsActive) return;
            SkinHas = new List<int>();
            SkinUse = new List<int>();
            SkinChat = new List<int>();
            var _o = Json.Deserialize<Dictionary<string, List<int>>>(Entity.Skins);
            var _has = _o.GetValueOrDefault("has");
            var _use = _o.GetValueOrDefault("use");
            if (_has != null)
            {
                foreach (var i in _has)
                {
                    SkinHas.Add(i);
                }
            }
            if (_use != null)
            {
                foreach (var i in _use)
                {
                    SkinUse.Add(i);
                    var cfg = ConfigService.SkinConfigs.GetValueOrDefault(i, null);
                    if (cfg != null && (cfg.index == 6 || cfg.index == 8))
                    {
                        SkinChat.Add(i);
                    }
                }
            }
            IsSkinDirty = true;
        }
        // 同步skins到Entity
        public void SyncSkins()
        {
            if (!IsActive) return;
            var _o = new Dictionary<string, List<int>>
            {
                ["has"] = SkinHas,
                ["use"] = SkinUse
            };
            // 通知地图
            if (_mapGrain != null)
            {
                _ = _mapGrain.SetPlayerSkins(OnlyId, SkinUse);
            }
            SkinChat.Clear();
            foreach (var i in SkinUse)
            {
                var cfg = ConfigService.SkinConfigs.GetValueOrDefault(i, null);
                if (cfg != null && (cfg.index == 6 || cfg.index == 8))
                {
                    SkinChat.Add(i);
                }
            }
            _ = FreshSkinsToRedis();
            Entity.Skins = Json.SafeSerialize(_o);
            IsSkinDirty = true;
        }
        private async Task FreshSkinsToRedis()
        {
            if (!IsActive) return;
            // 通知帮派
            if (SectGrain != null)
            {
                _ = SectGrain.SetPlayerSkin(RoleId, SkinUse);
            }
            // 通知队伍
            if (InTeam)
            {
                _ = TeamGrain.SetPlayerSkin(RoleId, SkinUse);
            }
            // 属性Redis
            await RedisService.SetRoleSkin(Entity, Json.SafeSerialize(SkinUse));
        }
        // 获得皮肤属性加成
        public Attrs GetSkinAttrs()
        {
            if (!IsActive) return null;
            FreshSkinAttrs();
            return SkinAttrs;
        }
        // 刷新皮肤属性加成
        private void FreshSkinAttrs()
        {
            if (!IsActive) return;
            if (!IsSkinDirty) return;
            IsSkinDirty = false;
            SkinAttrs.Clear();
            foreach (var shap in SkinUse)
            {
                var config = ConfigService.SkinConfigs.GetValueOrDefault(shap, null);
                if (config == null)
                {
                    LogError($"没有找到皮肤配置({shap}),({Entity.Skins})");
                    continue;
                }
                foreach (KeyValuePair<string, int> kv in config.attr)
                {
                    var key = GameDefine.EquipAttrTypeMap[kv.Key];
                    var val = kv.Value;
                    // 非数值性，千分比
                    if (!GameDefine.EquipNumericalAttrType.ContainsKey(key))
                    {
                        val = val / 10;
                    }
                    this.SkinAttrs.Add(key, val);
                }
            }
        }
        // 解析变身
        private void InitBianshen()
        {
            if (!IsActive) return;
            Bianshen = JsonSerializer.Deserialize<BianshenObject>(Entity.Bianshen);
            CheckBianshen();
            SyncBianshen();
        }
        // 同步变身到Entity
        public void SyncBianshen()
        {
            if (!IsActive) return;
            // 通知地图
            if (_mapGrain != null)
            {
                _ = _mapGrain.SetPlayerBianshen(OnlyId, Bianshen.current.avatar ? Bianshen.current.id : 0);
            }
            // 更新实例
            Entity.Bianshen = Json.SafeSerialize(Bianshen);
            // 更新属性
            FreshBianshenAttrs();
        }
        // 检查变身卡是否还在有效期
        private bool CheckBianshen()
        {
            if (!IsActive) return false;
            if (Bianshen.current.id <= 0)
            {
                return false;
            }
            else
            {
                var nt = DateTimeUtil.GetTimestamp();
                var ct = Bianshen.current.timestamp.Length > 0 ? Convert.ToInt64(Bianshen.current.timestamp) : 0;
                // LogInformation(string.Format("检查变身卡是否失效: 当前{0} 过期{1} 剩余{2}秒", nt, ct, (ct - nt) / 1000));
                if (nt > ct)
                {
                    Bianshen.current.id = 0;
                    Bianshen.current.avatar = false;
                    Bianshen.current.timestamp = "0";
                    _BianshenTimeout = 0;
                    return true;
                }
                else
                {
                    _BianshenTimeout = ct - nt;
                }
            }
            return false;
        }
        public void AddBianShenCard()
        {
            if (!IsActive) return;
            var keys = ConfigService.BianShenCards.Keys.ToList();
            int index = Random.Next(keys.Count);
            int cardid = keys[index];
            Bianshen.cards[cardid] = Bianshen.cards.GetValueOrDefault(cardid, 0) + 1;
            SyncBianshen();
        }
        private void FreshBianshenAttrs()
        {
            if (!IsActive) return;
            this.BianShenAttrs.Clear();
            // 没有使用变身卡
            if (Bianshen.current.id <= 0)
            {
                return;
            }
            // 没有找到配置文件
            var cfg = ConfigService.BianShenCards.GetValueOrDefault(Bianshen.current.id, null);
            if (cfg == null)
            {
                LogError(string.Format("没有找到变身卡配置：roleid[{0}], id[{1}]", Entity.Id, Bianshen.current.id));
                return;
            }
            // 变身卡属性加成--基本属性
            foreach (var property in cfg.attrList.Value.EnumerateObject())
            {
                GameDefine.EquipAttrTypeMap.TryGetValue(property.Name, out var attrType);
                if (attrType != AttrType.Unkown)
                {
                    var value = property.Value.GetSingle();
                    // 非数值性，千分比
                    if (!GameDefine.EquipNumericalAttrType.ContainsKey(attrType))
                    {
                        value = value / 10;
                    }
                    this.BianShenAttrs.Add(attrType, value);
                }
            }
            // 五行修炼加成计算，以金木水火土的最低等级计算
            int minLevel = 50;
            int minType = 6;
            for (int type = 1; type <= 5; type++)
            {
                int level = Bianshen.wuxing.GetValueOrDefault(type, 0);
                if (level <= minLevel)
                {
                    minLevel = level;
                    minType = type;
                }
            }
            // LogInformation(string.Format("五行等级：{0}, {1}", minType, minLevel));
            if (minLevel > 0 && minLevel <= 50 && minType != 6)
            {
                var levels = ConfigService.BianShenLevels.GetValueOrDefault(minType, null);
                if (levels != null)
                {
                    var levelCfg = levels.GetValueOrDefault(minLevel, null);
                    if (levelCfg != null)
                    {
                        int addon = 0;
                        foreach (var property in levelCfg.attr.Value.EnumerateObject())
                        {
                            GameDefine.EquipAttrTypeMap.TryGetValue(property.Name, out var attrType);
                            if (attrType != AttrType.Unkown)
                            {
                                addon = property.Value.GetInt32();
                                if (addon > 0)
                                {
                                    break;
                                }
                            }
                        }
                        if (addon > 0)
                        {
                            foreach (var (k, v) in this.BianShenAttrs)
                            {
                                var value = v * (1 + addon / 1000.0f);
                                this.BianShenAttrs.Set(k, value);
                                //LogInformation(string.Format("老属性[{0}]{1}", k, v));
                                //LogInformation(string.Format("{0}新属性[{1}]{2}", addon / 1000.0f, k, value));
                            }
                        }
                    }
                }
            }
            // 变身卡属性加成--五行属性
            foreach (var property in cfg.wuxingAttr.Value.EnumerateObject())
            {
                GameDefine.EquipAttrTypeMap.TryGetValue(property.Name, out var attrType);
                if (attrType != AttrType.Unkown)
                {
                    var value = property.Value.GetSingle();
                    // 非数值性，千分比
                    if (!GameDefine.EquipNumericalAttrType.ContainsKey(attrType))
                    {
                        value = value / 10;
                    }
                    this.BianShenAttrs.Add(attrType, value);
                }
            }
            // // 金、木、水、火、土修炼加成
            // var levels = ConfigService.BianShenLevels.GetValueOrDefault(cfg.wuxing, null);
            // if (levels != null)
            // {
            //     var level = levels.GetValueOrDefault(Bianshen.wuxing.GetValueOrDefault(cfg.wuxing, 0), null);
            //     if (level != null)
            //     {
            //         foreach (var property in level.attr.Value.EnumerateObject())
            //         {
            //             GameDefine.EquipAttrTypeMap.TryGetValue(property.Name, out var attrType);
            //             if (attrType != AttrType.Unkown)
            //             {
            //                 if (this.BianShenAttrs.Has(attrType))
            //                 {
            //                     var value = this.BianShenAttrs.Get(attrType) * (1 + property.Value.GetSingle() / 1000);
            //                     this.BianShenAttrs.Set(attrType, value);
            //                 }
            //             }
            //         }
            //     }
            // }
        }
        // 获取变身卡属性加成
        public Attrs GetBianshenAttrs()
        {
            if (!IsActive) return null;
            // 变身卡过期
            if (CheckBianshen())
            {
                SyncBianshen();
            }
            return BianShenAttrs;
        }

        // 获取神器切割属性加成
        public Attrs GetQieGeAttrs()
        {
            if (!IsActive) return null;
            FreshQieGeAttrs();
            return QieGeAttrs;
        }

        // 刷新切割属性
        private void FreshQieGeAttrs()
        {
            if (!IsActive) return;
            this.QieGeAttrs.Clear();
            var levelConfig = ConfigService.QieGeLevelList.GetValueOrDefault(QieGeLevel, null);
            if (levelConfig == null) return; 
            //根据不同的切割等级配置属性 （真实属性要在面板上体现出来） 
            // 0-99级  增加10W血量  1级开始每升一级增加1000血量。99级效果是增加10w血量。
            {
                this.QieGeAttrs.Add(AttrType.HpMax, levelConfig.mhp);

                // var effectLv = this.QieGeLevel;
                // if (effectLv > 100) {
                //     effectLv = 100;
                // }
                // var key = AttrType.HpMax;
                // var val = effectLv*1000;
                // this.QieGeAttrs.Add(key, val);
            }
            // 100-199级  增加500点速度  100级开始每升一级增加5点速度，199级效果是增加500点人物速度（真实属性要在面板上体现出来）。
            {
                this.QieGeAttrs.Add(AttrType.Spd, levelConfig.speed);

                // var effectLv = this.QieGeLevel - 100;
                // if (effectLv < 0) {
                //     effectLv = 0;
                // } else if (effectLv > 100) {
                //     effectLv = 100;
                // }
                // var key = AttrType.Spd;
                // var val = effectLv*5;
                // this.QieGeAttrs.Add(key, val);
            }
            // 200-300级  原  切割属性不变 从200级开始每升一级增加0.3%，300级效果是增加30%切割伤害 等于说就是原来整体这件装备的属性 集中在了最后100级。
            //切割伤害在战斗里面提现，这里不用处理
        }
        public Attrs GetShenZhiLiAttrs()
        {
            if (!IsActive) return null;
            FreshShenZhiLiAttrs();
            return ShenZhiLiAttrs;
        }

        // 刷新切割属性
        private void FreshShenZhiLiAttrs()
        {
            if (!IsActive) return;
            this.ShenZhiLiAttrs.Clear();
            this.ShenZhiLiAttrs.Add(AttrType.HpMax, this.ShenZhiLiHpLv * 100000);
            this.ShenZhiLiAttrs.Add(AttrType.Spd, this.ShenZhiLiSpeedLv * 20);
        }

        // 解析星阵
        private void InitXingzhen() {
            if (!IsActive) return;
            Xingzhen = JsonSerializer.Deserialize<XingzhenObject>(Entity.Xingzhen);
        }
        // 同步星阵到Entity
        public void SyncXingzhen()
        {
            if (!IsActive) return;
            // // 通知地图
            // if (_mapGrain != null)
            // {
            //     _ = _mapGrain.SetPlayerBianshen(OnlyId, Bianshen.current.id);
            // }
            // 更新实例
            Entity.Xingzhen = Json.SafeSerialize(Xingzhen);
            IsXingzhengAttrsDirty = true;
        }
        // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
        public Attrs GetXingzhenAttrs()
        {
            if (!IsActive) return null;
            FreshXingzhenAttrs();
            return XingzhenAttrs;
        }
        // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
        private void FreshXingzhenAttrs()
        {
            if (!IsActive) return;
            if (!IsXingzhengAttrsDirty) return;
            IsXingzhengAttrsDirty = false;
            XingzhenAttrs.Clear();
            var id = Xingzhen.used;
            if (id == 0) return;
            var xz = Xingzhen.unlocked[id];
            if (xz == null)
            {
                LogError(string.Format("{0}|{1}--无法刷新星阵属性，星阵存储错误", Entity.Id, Entity.NickName));
                return;
            }
            var config = ConfigService.XingZhenItems[id];
            if (config == null)
            {
                LogError(string.Format("{0}|{1}--无法刷新星阵属性，星阵配置错误", Entity.Id, Entity.NickName));
                return;
            }
            var lconfig = ConfigService.XingZhenLevels[xz.level];
            if (lconfig == null)
            {
                LogError(string.Format("{0}|{1}--无法刷新星阵属性，星阵等级错误", Entity.Id, Entity.NickName));
                return;
            }
            float addon = lconfig.addon / (float)1000.0;
            // 洗炼属性
            if (xz.refine.Count > 0)
            {
                foreach (var s in xz.refine)
                {
                    var ss = s.Split('_');
                    if (ss.Length == 2)
                    {
                        var key = (AttrType)Convert.ToInt32(ss[0]);
                        var val = Convert.ToInt32(ss[1]);
                        if (key != AttrType.Unkown && val > 0)
                        {
                            // 非数值性，千分比
                            if (!GameDefine.EquipNumericalAttrType.ContainsKey(key))
                            {
                                val = val / 10;
                            }
                            this.XingzhenAttrs.Add(key, val * (1 + addon));
                        }
                    }
                    else
                    {
                        LogWarning(string.Format("{0}|{1}--无法刷新星阵属性，忽略错误的洗炼属性{2}", Entity.Id, Entity.NickName, s));
                    }
                }
            }
            // 基础属性
            foreach (KeyValuePair<string, int> kv in config.baseAttr)
            {
                var key = GameDefine.EquipAttrTypeMap[kv.Key];
                var val = kv.Value;
                // 非数值性，千分比
                if (!GameDefine.EquipNumericalAttrType.ContainsKey(key))
                {
                    val = val / 10;
                }
                this.XingzhenAttrs.Add(key, val);
            }
        }
        // 构建星阵属性
        private BattleXingzhenData BuildBattleXingzhenData()
        {
            if (!IsActive) return null;
            var id = Xingzhen.used;
            if (id == 0) return null;
            var xz = Xingzhen.unlocked[id];
            if (xz == null)
            {
                LogError(string.Format("{0}|{1}--无法带入星阵参战，星阵存储错误", Entity.Id, Entity.NickName));
                return null;
            }
            var config = ConfigService.XingZhenItems[id];
            if (config == null)
            {
                LogError(string.Format("{0}|{1}--无法带入星阵参战，星阵配置错误", Entity.Id, Entity.NickName));
                return null;
            }
            var lconfig = ConfigService.XingZhenLevels[xz.level];
            if (lconfig == null)
            {
                LogError(string.Format("{0}|{1}--无法带入星阵参战，星阵等级错误", Entity.Id, Entity.NickName));
                return null;
            }
            // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
            // float addon = lconfig.addon / (float)1000.0;
            // var attrs = new Attrs();
            // // 洗炼属性
            // if (xz.refine.Count > 0)
            // {
            //     foreach (var s in xz.refine)
            //     {
            //         var ss = s.Split('_');
            //         if (ss.Length == 2)
            //         {
            //             var key = (AttrType)Convert.ToInt32(ss[0]);
            //             var val = Convert.ToInt32(ss[1]);
            //             if (key != AttrType.Unkown && val > 0)
            //             {
            //                 // 非数值性，千分比
            //                 if (!GameDefine.EquipNumericalAttrType.ContainsKey(key))
            //                 {
            //                     val = val / 10;
            //                 }
            //                 attrs.Add(key, val * (1 + addon));
            //             }
            //         }
            //         else
            //         {
            //             LogWarning(string.Format("{0}|{1}--忽略错误的洗炼属性{2}", Entity.Id, Entity.NickName, s));
            //         }
            //     }
            // }
            // // 基础属性
            // foreach (KeyValuePair<string, int> kv in config.baseAttr)
            // {
            //     var key = GameDefine.EquipAttrTypeMap[kv.Key];
            //     var val = kv.Value;
            //     // 非数值性，千分比
            //     if (!GameDefine.EquipNumericalAttrType.ContainsKey(key))
            //     {
            //         val = val / 10;
            //     }
            //     attrs.Add(key, val);
            // }
            // return new BattleXingzhenData() { Id = id, Level = xz.level, Attrs = { attrs.ToList() } };
            return new BattleXingzhenData() { Id = id, Level = xz.level };
        }
        // 解析孩子信息
        private void InitChild() {
            if (!IsActive) return;
            if (Entity.Child.Length == 0) {
                Child = null;
            } else {
                Child = JsonSerializer.Deserialize<ChildObject>(Entity.Child);
            }
            IsChildDirty = true;
        }
        // 刷新孩子技能
        private void FreshChildSkills()
        {
            if (!IsActive) return;
            if (!IsChildDirty) return;
            IsChildDirty = false;
            ChildSkills.Clear();
            if (Child == null) return;
            var lconfig = ConfigService.ChildLevels.GetValueOrDefault(Child.level, null);
            if (lconfig == null)
            {
                LogError($"无效孩子技能配置level[{Child.level}]--[{Entity.Id}][{Entity.Child}]");
                return;
            }
            foreach (var id in Child.skill)
            {
                var config = ConfigService.ChildSkillItems.GetValueOrDefault(id, null);
                if (config == null)
                {
                    LogError($"无效孩子技能配置id[{id}]--[{Entity.Id}][{Entity.Child}]");
                    continue;
                }
                var skillId = GameDefine.childSkillString2SkillIds.GetValueOrDefault(config.sMark, SkillId.Unkown);
                if (skillId == SkillId.Unkown)
                {
                    LogError($"无效孩子技能配置sMark--[{config.sMark}]--[{Entity.Id}][{Entity.Child}]");
                    continue;
                }
                var addonList = lconfig.addon.GetValueOrDefault(config.quality, null);
                var addon = 0;
                if (addonList != null && config.index < addonList.Count)
                {
                    addon = addonList[config.index];
                }
                else
                {
                    if (addonList == null)
                    {
                        LogError($"无效孩子技能配置quality[{config.quality}]--[{Entity.Id}][{Entity.Child}]");
                    }
                    if (config.index >= addonList.Count)
                    {
                        LogError($"无效孩子技能配置index[{config.index}]--[{Entity.Id}][{Entity.Child}]");
                    }
                }
                ChildSkills.Add(new() { SkillId = skillId, Rate = config.baseRate + addon });
            }
        }
        // 同步孩子信息到Entity
        public void SyncChild()
        {
            if (!IsActive) return;
            // // 通知地图
            // if (_mapGrain != null)
            // {
            //     _ = _mapGrain.SetPlayerBianshen(OnlyId, Bianshen.current.id);
            // }
            // 更新实例
            if (Child == null) {
                Entity.Child = "";
            } else {
                Entity.Child = Json.SafeSerialize(Child);
            }
            IsChildDirty = true;
        }
        // 是否有孩子？
        public bool HasChild(bool showNotice = true)
        {
            if (!IsActive) return false;
            if (Child == null)
            {
                if (showNotice)
                {
                    SendNotice("没有找到孩子信息");
                }
                return false;
            }
            return true;
        }
        private static void UpdateSkillCost(SkillData skData)
        {
            // 绑定消耗, 每100点一个等级, 单体和群体不同
            var skLevel = (int) MathF.Floor(skData.Exp / 100f);
            if (skLevel < 0 || skLevel >= GameDefine.SkillUpgradeConsume.Count) return;
            var (cost1, cost2) = GameDefine.SkillUpgradeConsume[skLevel];
            skData.UpgradeCost = skData.Idx % 2 == 0 ? cost1 : cost2;
        }

        // 同步skills到Entity
        private void SyncSkills()
        {
            if (!IsActive) return;
            var list = _skills.Select(sk => sk.Exp).ToList();
            Entity.Skills = Json.Serialize(list);
        }

        // 解析items
        private void InitItems()
        {
            if (!IsActive) return;
            Items = new Dictionary<uint, uint>(100);
            if (!string.IsNullOrWhiteSpace(LastExtEntity.Items))
            {
                Items = Json.SafeDeserialize<Dictionary<uint, uint>>(LastExtEntity.Items);
            }
        }

        // 同步items到Entity
        private void SyncItems()
        {
            if (!IsActive) return;
            if (Items == null || Items.Count == 0)
            {
                ExtEntity.Items = string.Empty;
            }
            else
            {
                ExtEntity.Items = Json.SafeSerialize(Items);
            }
        }

        // 解析仓库
        private void InitRepos()
        {
            if (!IsActive) return;
            Repos = new Dictionary<uint, uint>(100);
            if (!string.IsNullOrWhiteSpace(ExtEntity.Repos))
            {
                Repos = Json.SafeDeserialize<Dictionary<uint, uint>>(ExtEntity.Repos);
            }
        }

        // 同步仓库到Entity
        private void SyncRepos()
        {
            if (!IsActive) return;
            if (Repos == null || Repos.Count == 0)
            {
                ExtEntity.Repos = string.Empty;
            }
            else
            {
                ExtEntity.Repos = Json.SafeSerialize(Repos);
            }
        }

        // 解析mails
        private void InitMails()
        {
            if (!IsActive) return;
            Mails = new Dictionary<uint, byte>(20);
            if (!string.IsNullOrWhiteSpace(ExtEntity.Mails))
            {
                Mails = Json.SafeDeserialize<Dictionary<uint, byte>>(ExtEntity.Mails);
            }
        }

        // 同步mails到Entity
        public void SyncMails()
        {
            if (!IsActive) return;
            if (Mails == null || Mails.Count == 0)
            {
                ExtEntity.Mails = string.Empty;
                return;
            }

            ExtEntity.Mails = Json.SafeSerialize(Mails);
        }

        private async Task InitSsjl()
        {
            if (!IsActive) return;
            var ssjlGrain = GrainFactory.GetGrain<IShenShouJiangLinGrain>(Entity.ServerId);
            var season = await ssjlGrain.GetSeason();
            _ssjl = new RoleSsjlVo() { Season = season };
        }

        // 计算潜能兑换
        private void CalcExchangePotential()
        {
            if (!IsActive) return;
            int times = (int)Entity.ExpExchangeTimes;
            if (times > 0)
            {
                var list = ConfigService.Exp2PotentialList.Take(times);
                ExchangedPotential = (uint)list.Sum(p => p.potential);
            }
            else
            {
                ExchangedPotential = 0;
            }
        }

        // 计算VIP等级
        private async Task CalcVipLevel(bool init = false)
        {
            if (!IsActive) return;
            uint NewVipLevel = 0;
            var vipLevelCount = ConfigService.VipConfigList.Count;
            for (int i = vipLevelCount - 1; i >= 1; i--)
            {
                if (Entity.TotalPayBS >= ConfigService.VipConfigList[i - 1].next && Entity.TotalPayBS < ConfigService.VipConfigList[i].next)
                {
                    NewVipLevel = (uint)i;
                    break;
                }
            }
            if (NewVipLevel == 0)
            {
                if (Entity.TotalPayBS >= ConfigService.VipConfigList[vipLevelCount - 2].next)
                {
                    NewVipLevel = (uint)(vipLevelCount - 1);
                }
            }
            if (init)
            {
                VipLevel = NewVipLevel;
                return;
            }
            else
            {
                if (VipLevel != NewVipLevel)
                {
                    VipLevel = NewVipLevel;
                    // 通知地图
                    if (_mapGrain != null)
                    {
                        _ = _mapGrain.SetPlayerVipLevel(OnlyId, NewVipLevel);
                    }
                    await ReqVipInfo();
                }
                else
                {
                    VipLevel = NewVipLevel;
                }
            }
        }

        // 初始化天策
        private void InitTiance()
        {
            if (!IsActive) return;
            Tiance.FromJson(ExtEntity.Tiance);
        }
        public async Task SyncTiance(bool recalc = false)
        {
            if (!IsActive) return;
            // 更新实例
            if (Tiance == null)
            {
                ExtEntity.Tiance = "";
            }
            else
            {
                ExtEntity.Tiance = Tiance.ToJson();
            }
            if (recalc)
            {
                IsTianceInfoDirty = true;

                // 属性方案刷新潜能和属性
                await FreshAllSchemeAttrs();
                // 重新计算宠物属性并下发列表
                await PetMgr.RecalculateAttrsAndSendList();
            }
        }
        public Attrs GetTianceAttrPlayer()
        {
            if (!IsActive) return null;
            FreshTianceAttr();
            return TiancePlayerAttrs;
        }
        public Attrs GetTianceAttrPet()
        {
            if (!IsActive) return null;
            FreshTianceAttr();
            return TiancePetAttrs;
        }
        // 天演策等级加成
        public float GetTianyanceLvAddition(uint grade)
        {
            return (float)(0.1 * grade * Tiance.level / 100.0);
        }
        private void FreshTianceAttr()
        {
            if (!IsActive) return;
            if (!IsTianceInfoDirty) return;
            IsTianceInfoDirty = false;
            TiancePlayerAttrs.Clear();
            TiancePetAttrs.Clear();
            TianceFuInBattle.Clear();
            foreach (var f in Tiance.list)
            {
                if (f.State != TianceFuState.Unknown)
                {
                    var skCfg = ConfigService.TianceSkillList.GetValueOrDefault(f.SkillId, null);
                    if (skCfg == null)
                    {
                        LogError($"没有找到天策符技能配置（角宠基本加成）RoleId:{RoleId}, fid:{f.Id}, skillId:{f.SkillId}, name:{f.Name}");
                        continue;
                    }
                    var skillId = f.SkillId;
                    // 跳过诛神符后面处理
                    if (skillId >= SkillId.ZhuShen1 && skillId <= SkillId.ZhuShen3)
                    {
                        continue;
                    }
                    // 跳过枯荣符后面处理
                    if (skillId >= SkillId.KuRong1 && skillId <= SkillId.KuRong3)
                    {
                        continue;
                    }
                    // FIXME: 没有填写属性，这里暂时任务是战斗中作用的天策符
                    if (skCfg.attr.Count == 0)
                    {
                        TianceFuInBattle.Add(f);
                    }
                    // 天演策等级加成
                    float additionTlv = GetTianyanceLvAddition(f.Grade);
                    // 千钧符、载物符
                    if (f.Type != TianceFuType.YueShou)
                    {
                        foreach (var a in skCfg.attr)
                        {
                            var ak = GameDefine.EquipAttrTypeMap.GetValueOrDefault(a.Key, AttrType.Unkown);
                            if (ak != AttrType.Unkown)
                            {
                                float val = (float)(a.Value.increase ? a.Value.baseAddition * f.Addition : a.Value.baseAddition);
                                // 非数值性，千分比
                                if (!GameDefine.EquipNumericalAttrType.ContainsKey(ak))
                                {
                                    val = val / 10;
                                }
                                // 牺牲类不涉及天演策加成
                                if (val < 0)
                                {
                                    TiancePlayerAttrs.Add(ak, val);
                                }
                                else
                                {
                                    TiancePlayerAttrs.Add(ak, val * (1 + additionTlv));
                                }
                            }
                        }
                        continue;
                    }
                    // 御兽符
                    foreach (var a in skCfg.attr)
                    {
                        var ak = GameDefine.EquipAttrTypeMap.GetValueOrDefault(a.Key, AttrType.Unkown);
                        if (ak != AttrType.Unkown)
                        {
                            float val = (float)(a.Value.increase ? a.Value.baseAddition * f.Addition : a.Value.baseAddition);
                            // 非数值性，千分比
                            if (!GameDefine.EquipNumericalAttrType.ContainsKey(ak))
                            {
                                val = val / 10;
                            }
                            TiancePetAttrs.Add(ak, val * (1 + additionTlv));
                        }
                    }
                }
            }
        }
        // 获得天策符
        public TianceFu GetTianceFuBySkillId(SkillId min, SkillId max)
        {
            if (!IsActive) return null;
            var list = Tiance.list.FindAll(f =>
            f.SkillId >= min && f.SkillId <= max
            && f.State != TianceFuState.Unknown
            && null != ConfigService.TianceSkillList.GetValueOrDefault(f.SkillId, null)).OrderBy(f => f.SkillId).ToList();
            if (list.Count > 0)
            {
                return list[0];
            }
            return null;
        }
        // 检查御兽符里面的每100点加成属性
        public float checkPetTianceUseState(Pet pet, AttrType attr)
        {
            if (!IsActive) return 0;
            var tlevel = Tiance.level;
            foreach (var f in Tiance.list)
            {
                // 跳过没有装备的
                if (f.State == TianceFuState.Unknown) continue;
                // 跳过没有找到技能配置的
                var skCfg = ConfigService.TianceSkillList.GetValueOrDefault(f.SkillId, null);
                if (skCfg == null) continue;
                var skillId = f.SkillId;
                var addition = f.Addition;
                // 破防
                if (attr == AttrType.PpoFang)
                {
                    // 猛攻符
                    // 召唤兽每100点力量提高一定破防程度
                    if (skillId >= SkillId.MengGong1 && skillId <= SkillId.MengGong3)
                    {
                        return pet.checkTianceSkill(skillId, addition, tlevel);
                    }
                    continue;
                }
                // 破防率
                else if (attr == AttrType.PpoFangLv)
                {
                    // 看破符
                    // 召唤兽每100点力量提高一定破防概率
                    if (skillId >= SkillId.KanPo1 && skillId <= SkillId.KanPo3)
                    {
                        return pet.checkTianceSkill(skillId, addition, tlevel);
                    }
                    continue;
                }
                // 命中率
                else if (attr == AttrType.PmingZhong)
                {
                    // 精准符
                    // 召唤兽每100点力量提高一定命中率
                    if (skillId >= SkillId.JingZhun1 && skillId <= SkillId.JingZhun3)
                    {
                        return pet.checkTianceSkill(skillId, addition, tlevel);
                    }
                    continue;
                }
                // 抗封印、抗混乱、抗昏睡、抗遗忘
                else if (attr == AttrType.DfengYin || attr == AttrType.DhunLuan || attr == AttrType.DhunShui || attr == AttrType.DyiWang)
                {
                    // 慧根符
                    // 召唤兽每100点根骨或灵性，增加一定抗冰混睡忘
                    if (skillId >= SkillId.HuiGen1 && skillId <= SkillId.HuiGen3)
                    {
                        return pet.checkTianceSkill(skillId, addition, tlevel);
                    }
                    // 青岚符
                    // 召唤兽3转120级开始，每10级提高一定抗控制（180级达到最大值）
                    if (skillId >= SkillId.QingLan1 && skillId <= SkillId.QingLan3)
                    {
                        // 3转120级，180级达到最大值
                        if ((pet.Entity.Relive == 3 && pet.Entity.Level >= 120) || pet.Entity.Relive >= 4)
                        {
                            var add = (Math.Min(pet.Entity.Relive >= 4 ? 180 : pet.Entity.Level, (byte)180) - 120) / 10;
                            return add * (1 + f.Grade * 1f * Tiance.level / GameDefine.TianYanCeMaxLevel + f.Addition * 0.1f) / 2;
                        }
                    }
                    continue;
                }
            }
            return 0;
        }
        // 检查天策符--龙魂萦体符 对宠物成长率加成
        // 3转开始，召唤兽每10级提高一定成长率（180级达到最大值）
        public uint checkPetTianceLonghyt(Pet pet)
        {
            if (!IsActive) return 0;
            // 3转开始，召唤兽每10级提高一定成长率（180级达到最大值）
            if (pet.Entity.Relive >= 3)
            {
                foreach (var f in Tiance.list)
                {
                    // 跳过没有装备的
                    if (f.State == TianceFuState.Unknown) continue;
                    var skillId = f.SkillId;
                    // 跳过不是龙魂萦体符的
                    if (skillId < SkillId.LongHunYingTi1 || skillId > SkillId.LongHunYingTi3) continue;
                    // 跳过没有找到技能配置的
                    var skCfg = ConfigService.TianceSkillList.GetValueOrDefault(skillId, null);
                    if (skCfg == null) continue;
                    uint add = (uint)(Math.Min(pet.Entity.Relive >= 4 ? 180 : (int)pet.Entity.Level, 180) / 10);
                    if (skillId == SkillId.LongHunYingTi1)
                    {
                        return add * (1 * f.Addition + Tiance.level);
                    }
                    if (skillId == SkillId.LongHunYingTi2)
                    {
                        return add * (2 * f.Addition + Tiance.level);
                    }
                    if (skillId == SkillId.LongHunYingTi3)
                    {
                        return add * (3 * f.Addition + Tiance.level);
                    }
                }
            }
            return 0;
        }

        private async Task InitQieGe()
        {
            if (!IsActive) return;
            QieGeLevel = ExtEntity.QieGeLevel;
            QieGeExp = ExtEntity.QieGeExp;
            await SyncQieGe(true);
        }

        public async Task SyncQieGe(bool needMapSync)
        {
            if (!IsActive) return;
            // 通知地图
            if (needMapSync && _mapGrain != null)
            {
                await _mapGrain.SetPlayerQieGeLevel(OnlyId, QieGeLevel);
            }
            ExtEntity.QieGeLevel = QieGeLevel;
            ExtEntity.QieGeExp = QieGeExp;
        }

        private async Task InitShenZhiLi()
        {
            await Task.CompletedTask;
            if (!IsActive) return;
            ShenZhiLiHurtLv = ExtEntity.ShenZhiLiHurtLv;
            ShenZhiLiHpLv = ExtEntity.ShenZhiLiHpLv;
            ShenZhiLiSpeedLv = ExtEntity.ShenZhiLiSpeedLv;
        }

        public async Task SyncShenZhiLi()
        {
            await Task.CompletedTask;
            if (!IsActive) return;
            // 
            ExtEntity.ShenZhiLiHurtLv = ShenZhiLiHurtLv;
            ExtEntity.ShenZhiLiHpLv = ShenZhiLiHpLv;
            ExtEntity.ShenZhiLiSpeedLv = ShenZhiLiSpeedLv;
        }

        private async Task InitCszl()
        {
            await Task.CompletedTask;
            if (!IsActive) return;
            CszlLayer = Entity.CszlLayer;
        }

        public async Task SyncCszl()
        {
            await Task.CompletedTask;
            if (!IsActive) return;
            Entity.CszlLayer = CszlLayer;
            await RedisService.SetRoleCszlLayer(Entity);
        }

        private async Task InitSldh()
        {
            if (!IsActive) return;
            _sldh = new RoleSldhVo {Season = 1};
            if (!string.IsNullOrWhiteSpace(Entity.Sldh))
            {
                _sldh = Json.SafeDeserialize<RoleSldhVo>(Entity.Sldh);
            }

            var sldhGrain = GrainFactory.GetGrain<IShuiLuDaHuiGrain>(Entity.ServerId);
            var season = await sldhGrain.GetSeason();
            if (_sldh.Season != season)
            {
                _sldh = new RoleSldhVo {Season = 1};
                await SyncSldh();
            }

            Entity.SldhScore = _sldh.Score;
            Entity.SldhWin = _sldh.Win;
        }

        private async Task SyncSldh()
        {
            if (!IsActive) return;
            if (_sldh == null)
            {
                Entity.Sldh = string.Empty;
            }
            else
            {
                Entity.Sldh = Json.SafeSerialize(_sldh);
                Entity.SldhScore = _sldh.Score;
                Entity.SldhWin = _sldh.Win;
                await RedisService.SetRoleSldh(Entity);
            }
        }
        private async Task InitWzzz()
        {
            if (!IsActive) return;
            _wzzz = new RoleWzzzVo {Season = 1};
            if (!string.IsNullOrWhiteSpace(Entity.Wzzz))
            {
                _wzzz = Json.SafeDeserialize<RoleWzzzVo>(Entity.Wzzz);
            }

            var wzzzGrain = GrainFactory.GetGrain<IWangZheZhiZhanGrain>(Entity.ServerId);
            var season = await wzzzGrain.GetSeason();
            if (_wzzz.Season != season)
            {
                _wzzz = new RoleWzzzVo {Season = 1};
                await SyncWzzz();
            }

            Entity.WzzzScore = _wzzz.Score;
            Entity.WzzzWin = _wzzz.Win;
        }

        private async Task SyncWzzz()
        {
            if (!IsActive) return;
            if (_wzzz == null)
            {
                Entity.Wzzz = string.Empty;
            }
            else
            {
                Entity.Wzzz = Json.SafeSerialize(_wzzz);
                Entity.WzzzScore = _wzzz.Score;
                Entity.WzzzWin = _wzzz.Win;
                await RedisService.SetRoleWzzz(Entity);
            }
        }

        private void InitSinglePk()
        {
            if (!IsActive) return;
            _singlePkVo = new RoleSinglePkVo();
            if (!string.IsNullOrWhiteSpace(Entity.SinglePk))
            {
                _singlePkVo = Json.SafeDeserialize<RoleSinglePkVo>(Entity.SinglePk);
            }

            Entity.SinglePkWin = _singlePkVo.Win;
            Entity.SinglePkLost = _singlePkVo.Lost;
            Entity.SinglePkScore = _singlePkVo.Score;
        }

        private async Task SyncSinglePk()
        {
            if (!IsActive) return;
            if (_singlePkVo == null)
            {
                Entity.SinglePk = string.Empty;
            }
            else
            {
                Entity.SinglePk = Json.SafeSerialize(_singlePkVo);
                Entity.SinglePkScore = _singlePkVo.Score;
                Entity.SinglePkWin = _singlePkVo.Win;
                Entity.SinglePkLost = _singlePkVo.Lost;
                await RedisService.SetRoleSinglePk(Entity);
            }
        }

        private  async Task InitDaLuanDou()
        {
            if (!IsActive) return;
            _daLuanDouVo = new RoleDaLuanDouVo {Season = 1};
            if (!string.IsNullOrWhiteSpace(Entity.DaLuanDou))
            {
                _daLuanDouVo = Json.SafeDeserialize<RoleDaLuanDouVo>(Entity.DaLuanDou);
            }

            var dldGrain = GrainFactory.GetGrain<IDaLuanDouGrain>(Entity.ServerId);
            var season = await dldGrain.GetSeason();
            if (_daLuanDouVo.Season != season)
            {
                _daLuanDouVo = new RoleDaLuanDouVo {Season = 1};
                await SyncDaLuanDou();
            }

            Entity.DaLuanDouWin = _daLuanDouVo.Win;
            Entity.DaLuanDouLost = _daLuanDouVo.Lost;
            Entity.DaLuanDouScore = _daLuanDouVo.Score;
        }

        private async Task SyncDaLuanDou()
        {
            if (!IsActive) return;
            if (_daLuanDouVo == null)
            {
                Entity.DaLuanDou = string.Empty;
            }
            else
            {
                Entity.DaLuanDou = Json.SafeSerialize(_daLuanDouVo);
                Entity.DaLuanDouScore = _daLuanDouVo.Score;
                Entity.DaLuanDouWin = _daLuanDouVo.Win;
                Entity.DaLuanDouLost = _daLuanDouVo.Lost;
                await RedisService.SetRoleDaLuanDou(Entity);
            }
        }

        // 构建在地图上的数据
        private byte[] BuildMapObjectData()
        {
            if (!IsActive) return null;
            var data = new MapObjectData
            {
                OnlyId = OnlyId,
                Type = LivingThingType.Player,
                CfgId = Entity.CfgId,
                Name = Entity.NickName,
                MapId = Entity.MapId,
                MapX = new Int32Value {Value = Entity.MapX},
                MapY = new Int32Value {Value = Entity.MapY},

                RoleId = RoleId,
                Relive = new UInt32Value {Value = Entity.Relive},
                Level = new UInt32Value {Value = Entity.Level},
                Color1 = new UInt32Value {Value = Entity.Color1},
                Color2 = new UInt32Value {Value = Entity.Color2},
                TeamId = new UInt32Value(),
                TeamLeader = new UInt32Value(),
                TeamMemberCount = new UInt32Value(),
                SectId = new UInt32Value(),

                Weapon = MapWeapon,
                Wing = MapWing,
                Mount = new UInt32Value {Value = MountMgr.ActiveMountCfgId},
                Title = TitleMgr.Title?.BuildPbData(),

                Online = new BoolValue {Value = IsEnterServer},
                Battle = new BoolValue {Value = InBattle},

                SectWarId = _sectWarId,
                SldhGroup = _sldh.Group,
                WzzzGroup = _wzzz.Group,
                TeamLeave = _teamLeave,
                Skins = { SkinUse },
                Bianshen = Bianshen.current.avatar ? Bianshen.current.id : 0,
                VipLevel = VipLevel,
                QiegeLevel = QieGeLevel,
            };
            if (_battleGrain != null)
            {
                data.BattleInfo = new InBattleInfo() { BattleId = _battleId, CampId = _campId };
            }
            if (_battleGrainWatched != null)
            {
                data.BattleInfo = new InBattleInfo() { BattleId = _battleIdWatched, CampId = _campIdWatched };
            }

            if (InTeam)
            {
                data.TeamId.Value = TeamId;
                data.TeamLeader.Value = TeamLeader;
                if (IsTeamLeader) data.TeamMemberCount.Value = TeamMemberCount;
            }

            if (InSect)
            {
                data.SectId.Value = Entity.SectId;
            }

            return Packet.Serialize(data);
        }

        private TeamObjectData BuildTeamObjectData()
        {
            if (!IsActive) return new ();
            return new TeamObjectData
            {
                Type = TeamObjectType.Player,
                OnlyId = OnlyId,
                DbId = RoleId,
                Name = Entity.NickName,
                CfgId = Entity.CfgId,
                Relive = Entity.Relive,
                Level = Entity.Level,
                Online = IsEnterServer,
                Skins = { SkinUse },
                Weapon = MapWeapon,
                Wing = MapWing,
            };
        }

        public IEnumerable<TeamObjectData> BuildTeamMembers(bool includePartner)
        {
            if (!IsActive) return new List<TeamObjectData>(0);
            var list = new List<TeamObjectData> {BuildTeamObjectData()};
            // 上报自己所有出战的伙伴
            if (includePartner)
            {
                list.AddRange(PartnerMgr.BuildTeamMembers());
            }

            return list;
        }

        public SectMemberData BuildSectMemberData()
        {
            if (!IsActive) return new();
            return new()
            {
                Id = Entity.Id,
                Name = Entity.NickName,
                Relive = Entity.Relive,
                Level = Entity.Level,
                CfgId = Entity.CfgId,
                Online = IsEnterServer,
                Contrib = Entity.SectContrib,
                Type = (SectMemberType) Entity.SectJob,
                JoinTime = Entity.SectJoinTime,
                Skins = { SkinUse },
                Weapon = MapWeapon,
                Wing = MapWing,
            };
        }

        private async Task<List<BattleMemberData>> BuildBattleTeamData(uint battleId, BattleType type, uint campId)
        {
            if (!IsActive) return new(0);
            var self = BuildBattleMemberData();
            // 构建战斗单元失败
            if (self == null)
            {
                return new(0);
            }
            // 角色
            var list = new List<BattleMemberData> {self};
            // 宠物
            var pets = PetMgr.BuildBattleTeamData(6);
            list.AddRange(pets);

            if (IsTeamLeader)
            {
                // 获取队伍成员信息, 不包括队长, 因为我就是队长
                var reqBytes = new Immutable<byte[]>(Packet.Serialize(new GetBattleMembersRequest
                {
                    BattleId = battleId,
                    BattleType = type
                }));
                var respBytes = await TeamGrain.QueryTeamBattleMemebers();
                var teamBattleMembers = QueryTeamBattleMembersResponse.Parser.ParseFrom(respBytes.Value);

                // 队长已经占领了1号位, 队员从2号开始
                var pos = 2;

                // 管所有的成员要战斗信息
                foreach (var rid in teamBattleMembers.Players)
                {
                    var grain = GrainFactory.GetGrain<IPlayerGrain>(rid);
                    var bytes = (await grain.GetBattleMembers(reqBytes, campId)).Value;
                    if (bytes == null || bytes.Length == 0) continue;
                    var resp = GetBattleMembersResponse.Parser.ParseFrom(bytes);
                    foreach (var bmd in resp.List)
                    {
                        if (bmd.Type == LivingThingType.Player)
                        {
                            // player 位置确定位置
                            bmd.Pos = pos++;
                            list.Add(bmd);
                        }
                        else if (bmd.Type == LivingThingType.Pet)
                        {
                            bmd.OwnerId = rid;
                            // 上阵的宠物，定位, 这里要注意pos被前面player递增了
                            if (bmd.Pos > 0) bmd.Pos = pos - 1 + 5;
                            list.Add(bmd);
                        }
                    }
                }

                // 补充队伍的参战partner
                if (type is BattleType.Normal
                         or BattleType.DiShaXing
                         or BattleType.KuLouWang
                         or BattleType.JinChanSongBao
                         or BattleType.Cszl
                         or BattleType.Eagle
                         or BattleType.HuangChengPk
                         or BattleType.ShenShouJiangLin)
                {
                    // 这里要算上第一个队长
                    pos = teamBattleMembers.Players.Count + 2;
                    foreach (var pid in teamBattleMembers.Partners)
                    {
                        // 注意Partners是cfgId
                        var partner = PartnerMgr.Actives.FirstOrDefault(p => p.CfgId == pid);
                        if (partner != null)
                        {
                            list.Add(partner.BuildBattleMemberData(pos++));
                        }
                    }
                }
            }
            else
            {
                // partner
                if (type is BattleType.Normal
                         or BattleType.DiShaXing
                         or BattleType.KuLouWang
                         or BattleType.JinChanSongBao
                         or BattleType.Cszl
                         or BattleType.Eagle
                         or BattleType.HuangChengPk
                         or BattleType.ShenShouJiangLin)
                {
                    var pos = 2;
                    list.AddRange(PartnerMgr.Actives.Select(partner => partner.BuildBattleMemberData(pos++)));
                }
            }

            return list;
        }

        // 构建战斗参战数据
        private BattleMemberData BuildBattleMemberData()
        {
            if (!IsActive) return null;
            var roleCfg = ConfigService.Roles[Entity.CfgId];
            // 刷新孩子技能
            FreshChildSkills();
            var data = new BattleMemberData
            {
                Type = LivingThingType.Player,
                Pos = 1,
                Online = IsEnterServer && !IsEnterBackGround,

                Id = RoleId,
                CfgId = Entity.CfgId,
                Name = Entity.NickName,
                Res = roleCfg.Res,
                Relive = Entity.Relive,
                Level = Entity.Level,
                Color1 = Entity.Color1,
                Color2 = Entity.Color2,
                Money = Entity.Jade,
                Weapon = MapWeapon,
                DefSkillId = (SkillId) Entity.AutoSkill,
                Attrs = {SchemeMgr.Scheme.Attrs.ToList()},
                OrnamentSkills = {SchemeMgr.Scheme.OrnamentSkills}, //配饰套装技能,
                Race = (Race) Entity.Race,
                Sex = (Sex) Entity.Sex,
                RoleType = (uint) Entity.Type,
                ShanXianOrdered = GetFlag(FlagType.ShanXianOrder),
                Skins = { SkinUse },
                Wing = MapWing,
                Bianshen = Bianshen.current.avatar ? Bianshen.current.id : 0,
                VipLevel = VipLevel,
                QiegeLevel = QieGeLevel,
                ShenzhiliHurtLevel = ShenZhiLiHurtLv,
            };
            // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
            // // 只需要队长带星阵属性
            // if (IsTeamLeader)
            // {
            //     var xingzhen = BuildBattleXingzhenData();
            //     if (xingzhen != null)
            //     {
            //         data.Xinzhen = xingzhen;
            //     }
            // }
            var xingzhen = BuildBattleXingzhenData();
            if (xingzhen != null)
            {
                data.Xinzhen = xingzhen;
            }

            foreach (var skData in _skills)
            {
                data.Skills.Add((uint) skData.Id, skData.Exp);
            }
            // 孩子信息
            if (Child != null)
            {
                // 形象、名字、初始动作
                data.Child = new BattleChildInfoData()
                {
                    Shape = Child.shape,
                    Name = Child.name,
                    AniName = GameDefine.ChildAniNameList[Random.Next(GameDefine.ChildAniNameList.Count)]
                };
                // 技能
                foreach (var cs in ChildSkills)
                {
                    data.ChildSkills.Add(new BattleChildSkillData()
                    {
                        SkillId = cs.SkillId,
                        Rate = cs.Rate,
                    });
                }
            }
            // 战斗中天策符
            var level = Tiance.level;
            foreach (var f in TianceFuInBattle)
            {
                data.TianceSkillList.Add(new BattleTianceSkill() { SkillId = f.SkillId, Addition = f.Addition, TianYanCeLevel = level });
            }

            return data;
        }

        // 属性方案刷新潜能和属性
        public async Task FreshAllSchemeAttrs()
        {
            if (!IsActive) return;
            foreach (var scheme in SchemeMgr.All)
            {
                await scheme.RefreshAttrs();
            }
        }

        private static BattleMemberData BuildBattleMemberDataFromMonster(MonsterConfig cfg, int pos,
            bool catched = false)
        {
            // 构建参战数据
            var mbData = new BattleMemberData
            {
                Type = LivingThingType.Monster,
                Pos = pos,
                Catched = catched,
                Id = cfg.Id,
                CfgId = cfg.Id,
                Name = cfg.Name,
                Res = cfg.Res,
                Level = cfg.Level,
                SkillProfic = (uint)cfg.Profic
            };
            // 技能, 怪物技能的熟练度都是0
            if (cfg.Skills != null)
            {
                foreach (var id in cfg.Skills)
                {
                    mbData.Skills[id] = 0;
                }
            }

            // 基础属性
            mbData.Attrs.Add(new AttrPair {Key = AttrType.Hp, Value = cfg.Hp});
            mbData.Attrs.Add(new AttrPair {Key = AttrType.HpMax, Value = cfg.Hp});
            mbData.Attrs.Add(new AttrPair {Key = AttrType.Mp, Value = 9999999});
            mbData.Attrs.Add(new AttrPair {Key = AttrType.MpMax, Value = 9999999});
            mbData.Attrs.Add(new AttrPair {Key = AttrType.Atk, Value = cfg.Atk});
            mbData.Attrs.Add(new AttrPair {Key = AttrType.Spd, Value = cfg.Spd});
            // 附加属性
            if (cfg.Attrs is {ValueKind: JsonValueKind.Object})
            {
                foreach (var property in cfg.Attrs.Value.EnumerateObject())
                {
                    if (Enum.TryParse(typeof(AttrType), property.Name, true, out var val) && val != null)
                    {
                        var attrType = (AttrType) val;
                        mbData.Attrs.Add(new AttrPair {Key = attrType, Value = property.Value.GetSingle()});
                    }
                }
            }

            return mbData;
        }

        private static BattleMemberData BuildBattleMemberDataFromMonsterForCszl(uint layer, MonsterConfig cfg, int pos,
            bool catched = false)
        {
            // 构建参战数据
            var mbData = new BattleMemberData
            {
                Type = LivingThingType.Monster,
                Pos = pos,
                Catched = catched,
                Id = cfg.Id,
                CfgId = cfg.Id,
                Name = cfg.Name,
                Res = cfg.Res,
                Level = cfg.Level,
                SkillProfic = (uint)cfg.Profic
            };
            // 技能, 怪物技能的熟练度都是0
            if (cfg.Skills != null)
            {
                foreach (var id in cfg.Skills)
                {
                    mbData.Skills[id] = 0;
                }
            }

            // 基础属性
            mbData.Attrs.Add(new AttrPair {Key = AttrType.Hp, Value = cfg.Hp*(1.0f + 0.05f*layer)});
            mbData.Attrs.Add(new AttrPair {Key = AttrType.HpMax, Value = cfg.Hp*(1.0f + 0.05f*layer)});
            mbData.Attrs.Add(new AttrPair {Key = AttrType.Mp, Value = 9999999});
            mbData.Attrs.Add(new AttrPair {Key = AttrType.MpMax, Value = 9999999});
            mbData.Attrs.Add(new AttrPair {Key = AttrType.Atk, Value = cfg.Atk*(1.0f + 0.05f*layer)});
            mbData.Attrs.Add(new AttrPair {Key = AttrType.Spd, Value = cfg.Spd});
            // 附加属性
            if (cfg.Attrs is {ValueKind: JsonValueKind.Object})
            {
                foreach (var property in cfg.Attrs.Value.EnumerateObject())
                {
                    if (Enum.TryParse(typeof(AttrType), property.Name, true, out var val) && val != null)
                    {
                        var attrType = (AttrType) val;
                        mbData.Attrs.Add(new AttrPair {Key = attrType, Value = property.Value.GetSingle()});
                    }
                }
            }

            return mbData;
        }

        public RoleInfo BuildRoleInfo()
        {
            if (!IsActive) return new();
            var info = new RoleInfo
            {
                Id = Entity.Id,
                Name = Entity.NickName,
                Relive = Entity.Relive,
                Level = Entity.Level,
                CfgId = Entity.CfgId,
                Type = (uint) Entity.Type,
                VipLevel = VipLevel,
                QiegeLevel = QieGeLevel,
            };
            // 添加皮肤信息
            foreach (var i in SkinChat)
            {
                info.Skins.Add(i);
            }
            return info;
        }

        // 保存所有数据
        private async Task SaveAllData()
        {
            if (!IsActive) return;
            await SaveRoleData();

            if (TaskMgr != null) await TaskMgr.SaveData();
            if (EquipMgr != null) await EquipMgr.SaveData();
            if (SchemeMgr != null) await SchemeMgr.SaveData();
            if (PetMgr != null) await PetMgr.SaveData();
            if (MountMgr != null) await MountMgr.SaveData();
            if (PartnerMgr != null) await PartnerMgr.SaveData();
            if (TitleMgr != null) await TitleMgr.SaveData();
        }

        private async Task SaveRoleData()
        {
            if (!IsActive) return;
            if (Entity != null && LastEntity != null && !Entity.Equals(LastEntity))
            {
                var ret = await DbService.UpdateEntity(LastEntity, Entity);
                if (ret) LastEntity.CopyFrom(Entity);
            }

            if (ExtEntity != null && LastExtEntity != null && !ExtEntity.Equals(LastExtEntity))
            {
                var ret = await DbService.UpdateEntity(LastExtEntity, ExtEntity);
                if (ret) LastExtEntity.CopyFrom(ExtEntity);
            }
        }

        // 每s调用一次
        private async Task Update(object _)
        {
            if (!IsActive)
            {
                _updateTimer?.Dispose();
                _updateTimer = null;
                return;
            }
            var now = TimeUtil.TimeStamp;
            // 清理超过30s的
            if (C2S_TeamInviteDict != null && C2S_TeamInviteDict.Count > 0)
            {
                foreach (var (key, value) in C2S_TeamInviteDict)
                {
                    if ((value - now) > 30)
                    {
                        C2S_TeamInviteDict.Remove(key);
                    }
                }
            }

            // 引妖香的时间减少1s
            if (_incenseTime > 0)
            {
                _incenseTime--;
                if (_incenseTime == 0)
                    await StopIncense();
            }
            // 神兽降临
            if (IsTeamLeader && _ssjl.Signed && _ssjl.Started && _ssjl.EndTime > 0)
            {
                _ssjl.EndTime--;
            }

            // 变身卡失效检查
            if (_BianshenTimeout > 0)
            {
                _BianshenTimeout -= 1000;
                if (_BianshenTimeout <= 0)
                {
                    if (CheckBianshen())
                    {
                        SyncBianshen();
                        // 还在线？
                        if (IsOnline)
                        {
                            // 属性方案刷新潜能和属性
                            await FreshAllSchemeAttrs();
                            var resp = new S2C_BianshenInfo() { Info = Entity.Bianshen };
                            await SendPacket(GameCmd.S2CBianshenInfo, resp);
                        }
                    }
                }
            }

            // 检查心跳时间，判断是否已掉线
            if (IsOnline)
            {
                if (!IsEnterBackGround)
                {
                    if (now - _lastHeartBeatTime >= GameDefine.HeartBeatOfflineTime)
                    {
                        LogDebug("心跳超时");
                        await _packet.SendStatus(RoleId, WebSocketCloseStatus.NormalClosure, false);
                        await Offline();
                        return;
                    }
                }

                if (IsEnterBackGround && IsEnterServer)
                {
                    if (now - _enterBackGroundTime > GameDefine.BackgroundTime)
                    {
                        // 判断为断线
                        LogDebug("后台超时");
                        await _packet.SendStatus(RoleId, WebSocketCloseStatus.NormalClosure, false);
                        await Offline();
                        return;
                    }
                }
            }

            if (_shane > 0)
            {
                _shane--;
                if (_shane <= 0)
                {
                    _shane = 0;
                    Entity.Shane = 0;

                    await CheckShanEChange();
                }
            }

            // 系统模块需要tick
            await TaskMgr.Tick(now);

            // 到点保存数据
            _saveDataCnt++;
            if (_saveDataCnt >= GameDefine.SaveDataInterval)
            {
                _saveDataCnt = 0;
                await SaveAllData();
            }
        }

        public void LogDebug(string msg)
        {
            _logger?.LogDebug($"玩家[{RoleId}]:{msg}");
        }

        public void LogInformation(string msg)
        {
            _logger?.LogInformation($"玩家[{RoleId}]:{msg}");
        }

        public void LogWarning(string msg)
        {
            _logger?.LogWarning($"玩家[{RoleId}]:{msg}");
        }

        public void LogError(string msg)
        {
            _logger?.LogError($"玩家[{RoleId}]:{msg}");
        }
    }
}