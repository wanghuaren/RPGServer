using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface IPlayerGrain : IGrainWithIntegerKey
    {
        /// <summary>
        /// 玩家网络连接上, 留空，仅仅是为了唤醒Grain, 逻辑上的"上线"必须要等EnterServer协议
        /// </summary>
        Task Online();

        /// <summary>
        /// 玩家网络掉线
        /// </summary>
        Task Offline();

        /// <summary>
        /// 激活
        /// </summary>
        Task<bool> StartUp();

        /// <summary>
        /// 让Grain保存数据, 销毁Grain
        /// </summary>
        Task Shutdown();

        /// <summary>
        /// 获取最新的Entity数据
        /// </summary>
        Task<Immutable<byte[]>> Dump();

        Task<Immutable<byte[]>> DumpPets();
        
        Task<Immutable<byte[]>> DumpMounts();

        Task<Immutable<byte[]>> DumpEquips();

        Task<Immutable<byte[]>> DumpOrnaments();

        Task GmSetLevel(byte level);

        Task GmAddStar(int value);
		
        Task GmAddTotalPay(int value);
		
        Task GmAddSkillExp();

        ValueTask<bool> GmAddEquip(uint cfgId, byte category, byte index, byte grade);

        ValueTask<bool> GmRefineEquip(uint id, List<Tuple<byte, float>> attrs);

        ValueTask<bool> GmAddOrnament(uint cfgId, uint suit, byte index, byte grade);

        ValueTask<bool> GmAddWing(uint cfgId);

        ValueTask<bool> GmAddTitle(uint cfgId, bool add, bool use = false);

        ValueTask<bool> GmAddTitle1(uint cfgId, string text = "", uint seconds = 0,
            bool send = true);

        ValueTask<bool> GmDelShane(uint adminId);

        ValueTask<bool> GmSetRoleType(byte type);

        ValueTask<bool> GmSetRoleFlag(byte type, bool value);
        
        ValueTask<bool> GmSetMountSkill(uint mountId, int skIdx, uint skCfgId, byte skLevel, uint skExp);

        /// <summary>
        /// 处理来自客户端的Protobuf数据包, 如果解析PB出错则返回false
        /// </summary>
        Task HandlePacket(ushort command, byte[] payload);

        /// <summary>
        /// 其他Grain让PlayerGrain转发消息
        /// </summary>
        Task SendMessage(Immutable<byte[]> bytes);

        /// <summary>
        /// 向Server广播
        /// </summary>
        Task BroadcastMessage(Immutable<byte[]> bytes);

        /// <summary>
        /// BattleGrain告诉PlayerGrain 在战斗中捕获到了宠物
        /// </summary>
        Task CreatePet(uint cfgId, string from);

        /// <summary>
        /// BattleGrain中使用道具
        /// </summary>
        ValueTask<bool> AddBagItem(uint cfgId, int num, bool send = true, string tag = "");

        /// <summary>
        /// BattleGrain中使用道具
        /// </summary>
        ValueTask<uint> GetBagItemCount(uint cfgId);

        /// <summary>
        /// 添加Money
        /// </summary>
        ValueTask<long> AddMoney(byte type, int value, string tag = "", bool noNotice = false);

        Task AddShanE(uint value);

        ValueTask<bool> CheckInPrison();
        ValueTask<bool> CheckInBattle();
        ValueTask<bool> CheckCanHcPk();

        /// <summary>
        /// BattleGrain告诉PlayerGrain 战斗结束
        /// </summary>
        Task ExitBattle(Immutable<byte[]> reqBytes);

        /// <summary>
        /// 队长拉队员进入观战
        /// </summary>
        Task EnterBattleWatch(uint battleId, uint campId);

        /// <summary>
        /// 队长拉队员退出观战
        /// </summary>
        Task ExitBattleWatch();

        /// <summary>
        /// 判断玩家是否在战场上
        /// </summary>
        ValueTask<bool> IsInBattle();


        /// <summary>
        /// 获取角色信息
        /// </summary>
        Task<Immutable<byte[]>> GetRoleInfo();

        /// <summary>
        /// 队长管队员要参战数据
        /// </summary>
        Task<Immutable<byte[]>> GetBattleMembers(Immutable<byte[]> reqBytes, uint campId);

        Task<uint> GetMapId();

        Task OnTeamLeave();

        Task OnTeamBack(uint mapId, int mapX, int mapY);

        /// <summary>
        /// TeamGrain通知player已经离开了队伍
        /// </summary>
        Task OnExitTeam();

        /// <summary>
        /// 队长的人数变化或者队长更换了
        /// </summary>
        Task OnTeamChanged(uint teamId, uint teamLeader, uint teamMemberCount);

        /// <summary>
        /// 发起的申请被同意了, 由TeamGrain调用
        /// </summary>
        Task OnTeamJoinApplyAgree(uint teamId, uint teamLeader, byte teamTarget, uint teamSect);

        /// <summary>
        /// 队长带着成员走
        /// </summary>
        Task OnTeamMapPosChanged(uint mapId, int mapX, int mapY, bool immediate = false, bool includeLeader = false,
            bool synced = true);

        /// <summary>
        /// 队长更新了任务
        /// </summary>
        Task OnTeamTasksChanged(Immutable<byte[]> reqBytes);

        /// <summary>
        /// 组队任务, 队长完成了某个步骤, 同步给所有队员, 下发奖励
        /// </summary>
        Task OnTeamTaskEventFinish(uint taskId, uint step);

        /// <summary>
        /// 队伍任务完成
        /// </summary>
        Task OnTeamTaskFinish(uint cfgId, bool success);

        Task OnTeamLeaderApplyPassed(uint teamId);

        Task OnEnterSect(uint sectId, string sectName, uint ownerId, byte job);

        Task OnExitSect(uint sectId, string sectName, uint ownerId);

        Task OnSectJob(uint sectId, string sectName, uint targetRoleId, byte job);

        Task OnSectSilent(uint sectId, string sectName, uint opRoleId, string opName, byte opJob);

        Task OnSectJoinApplyAgree(uint sectId, uint ownerId);

        /// <summary>
        /// 有新的好友申请
        /// </summary>
        Task OnFriendApply(Immutable<byte[]> reqBytes);

        /// <summary>
        /// 申请加他人为好友，他人同意了，成立好友关系
        /// </summary>
        Task OnFriendAdd(Immutable<byte[]> reqBytes);

        /// <summary>
        /// 好友删除了我，我也需要删除他
        /// </summary>
        Task OnFriendDel(uint roleId);

        /// <summary>
        /// 好友发了消息给我
        /// </summary>
        Task OnRecvChat(Immutable<byte[]> reqBytes);

        ValueTask<bool> OnChatSilent();

        /// <summary>
        /// 商品在摆摊中卖掉的时候回调
        /// </summary>
        Task OnMallItemSelled(Immutable<byte[]> reqBytes);

        /// <summary>
        /// 商品没卖完超过24小时下架通知
        /// </summary>
        ValueTask<bool> OnMallItemUnShelf(Immutable<byte[]> reqBytes);

        /// <summary>
        /// 预检查能否进行相应PVP
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        ValueTask<bool> PreCheckPvp(byte type = 0);

        /// <summary>
        /// 主动发起PVP战斗
        /// </summary>
        ValueTask<int> StartPvp(uint targetRoleId, byte type = 0);

        /// <summary>
        /// 有人尝试和我PK，我检查是否符合满足PK的条件, 满足条件就返回参战队员信息
        /// </summary>
        Task<Immutable<byte[]>> OnPvp(Immutable<byte[]> reqBytes);

        /// <summary>
        /// 进入水路大会地图
        /// </summary>
        Task OnEnterShuiLuDaHui(uint group);

        Task OnExitShuiLuDaHui(bool changeMap = false);

        /// <summary>
        /// 进入王者之战地图
        /// </summary>
        Task OnEnterWangZheZhiZhan(uint group);

        Task OnExitWangZheZhiZhan(bool changeMap = false);

        /// <summary>
        /// 水陆大会奖励
        /// </summary>
        Task OnShuiLuDaHuiBattleResult(uint season, bool win);

        /// <summary>
        /// 大乱斗奖励
        /// </summary>
        Task OnDaLuanDouBattleResult(uint season, bool win);

        /// <summary>
        /// 王者之战奖励
        /// </summary>
        Task OnWangZheZhiZhanBattleResult(uint season, bool win);

        /// <summary>
        /// 水路大会新赛季
        /// </summary>
        /// <returns></returns>
        Task OnShuiLuDaHuiNewSeason(uint season);

        /// <summary>
        /// 大乱斗
        /// </summary>
        Task OnDaLuanDouNewSeason(uint season);

        /// <summary>
        /// 王者之战新赛季
        /// </summary>
        /// <returns></returns>
        Task OnWangZheZhiZhanNewSeason(uint season);

        /// <summary>
        /// 神兽降临新赛季
        /// </summary>
        /// <returns></returns>
        Task OnShenShouJiangLinNewSeason(uint season);

        /// <summary>
        /// 进入神兽降临
        /// </summary>
        Task OnEnterShenShouJiangLin();

        /// <summary>
        /// 退出神兽降临
        /// </summary>
        Task OnExitShenShouJiangLin(bool changeMap);

        /// <summary>
        /// 神兽降临开始抓捕
        /// </summary>
        Task OnStartShenShouJiangLin(uint endTime, uint shenShouId, uint serverId);

        /// <summary>
        /// 神兽降临停止抓捕
        /// </summary>
        Task OnStopShenShouJiangLin();

        /// <summary>
        /// 进入单人PK地图
        /// </summary>
        Task OnEnterSinglePk();

        /// <summary>
        /// 退出单人Pk地图
        /// </summary>
        /// <returns></returns>
        Task OnExitSinglePk(uint win, uint lost, uint score);


        /// <summary>
        /// 进入大乱斗PK地图
        /// </summary>
        Task OnEnterDaLuanDou();

        /// <summary>
        /// 退出大乱斗Pk地图
        /// </summary>
        /// <returns></returns>
        Task OnExitDaLuanDou(uint win, uint lost, uint score);

        /// <summary>
        /// 触发杀星战斗
        /// </summary>
        ValueTask<bool> OnStarBattle(uint npcOnlyId, byte level);

        /// <summary>
        /// 触发骷髅王战斗
        /// </summary>
        ValueTask<bool> OnKuLouWangBattle(uint npcOnlyId);
        
        /// <summary>
        /// 触发金蟾送宝战斗
        /// </summary>
        ValueTask<bool> OnJinChanSongBaoBattle(uint npcOnlyId);

        /// <summary>
        /// 触发金翅大鹏战斗
        /// </summary>
        ValueTask<bool> OnEagleBattle(uint npcOnlyId);

        ValueTask<Immutable<byte[]>> PreCheckHcPk();

        Task OnHcPkResult(bool win);

        Task<Immutable<byte[]>> QueryRoleInfo();

        Task<Immutable<byte[]>> QueryRoleList();

        Task OnExitSectWar();

        Task OnSectWarState(byte state);
        Task OnSectWarPlace(byte place);

        Task OnSectWarResult(bool win);

        ValueTask<bool> CheckCanInSectWarTeam(uint teamSectId);

        ValueTask<bool> InSectWar();

        ValueTask<bool> IsTeamMember();
        
        ValueTask<bool> IsSignedSinglePk();

        Task OnSinglePkResult(int rank, uint title);

        ValueTask<bool> IsSignedDaLuanDou();

        Task OnDaLuanDouResult(int rank, uint title);

        Task OnRecvMail(Immutable<byte[]> bytes);
        Task OnDelMail(Immutable<byte[]> bytes);
        Task OnRecvMail(uint id);
        Task OnDelMail(uint id);

        ValueTask<int> OnPayed(int money, int jade);

        ValueTask<int> OnPayedBindJade(int money, int jade, int bindJade, bool multi);

        ValueTask<int> OnPayedItem(uint item, uint num, int money);

        ValueTask<int> OnPayedGift(uint id, int money);

        ValueTask<string> GmSendRoleGift();

        // 检查多人日常任务是否有队员已完成
        ValueTask<string> CheckTeamDailyTaskCompleted(uint group);
        ValueTask<bool> IsDailyTaskCompleted(uint group);

        // 检查多人副本任务是否有队员已完成
        ValueTask<string> CheckTeamInstanceTaskCompleted(uint taskId);
        ValueTask<bool> IsInstanceTaskCompleted(uint taskId);
        // 重置单人PK排行榜
        ValueTask<bool> ResetSinglePkInfo();
        // 重置大乱斗PK排行榜
        ValueTask<bool> ResetDaLuanDouInfo();
    }
}