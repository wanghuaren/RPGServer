using System.Threading.Tasks;
using System.Collections.Generic;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface ITeamGrain : IGrainWithStringKey
    {
        Task StartUp();
        Task ShutDown();
        ValueTask<bool> CheckActive();

        ValueTask<bool> TakeOver(Immutable<byte[]> reqBytes);

        Task Exit(uint roleId);

        Task Online(uint roleId);

        Task Offline(uint roleId);

        ValueTask<byte> GetTarget();

        Task SetPlayerName(uint roleId, string name);

        Task SetPlayerLevel(uint roleId, uint relive, uint level);

        Task SetPlayerCfgId(uint roleId, uint cfgId);

        Task SetPlayerSkin(uint roleId, List<int> skinUse);

        Task SetPlayerWeapon(uint roleId, uint cfgId, int category, uint gem, uint level);

        Task SetPlayerWing(uint roleId, uint cfgId, int category, uint gem, uint level);

        Task ChangeTarget(uint roleId, byte target);

        Task ApplyJoin(Immutable<byte[]> reqBytes);

        Task HandleJoinApply(uint applyId, bool agree);

        Task<Immutable<byte[]>> Join(Immutable<byte[]> reqBytes);

        Task Kickout(uint leaderRoleId, uint roleId);

        Task QueryApplyList(uint roleId);

        Task ReqHandOver(uint roleId, uint toRoleId);

        ValueTask<bool> HandOver(Immutable<byte[]> reqBytes);

        Task InvitePlayer(uint leaderRoleId, uint playerRoleId);

        Task HandleInvite(uint roleId, bool agree, uint sectId, int sectWarCamp);

        Task ApplyLeader(uint roleId);

        Task HandleApplyLeader(uint roleId, uint applyRoleId, bool agree);

        Task Leave(uint roleId);

        Task Back(uint roleId);

        Task InviteBack(uint leaderRoleId, uint roleId);

        Task UpdatePartner(Immutable<byte[]> reqBytes);

        Task UpdateMap(uint mapId, int mapX, int mapY, bool includeLeader = false);

        Task UpdatePos(int mapX, int mapY, bool immediate);

        Task SetPathList(Immutable<byte[]> reqBytes);

        Task UpdateTasks(Immutable<byte[]> reqBytes);

        Task FinishTaskEvent(uint taskId, uint step);

        Task FinishTask(uint taskId, bool success);

        Task<Immutable<byte[]>> QueryTeamBattleMemebers();

        Task<Immutable<byte[]>> QueryRoleInfos(bool includeLeader = false);

        Task<uint[]> QueryTeamPlayers(bool includeLeader);

        Task SignShuiLuDaHui(uint roleId, bool sign);
        Task SignDaLuanDou(uint roleId, bool sign);

        Task SignWangZheZhiZhan(uint roleId, bool sign);

        ValueTask<bool> CheckSldhSigned(uint roleId);
        ValueTask<bool> CheckDldSigned(uint roleId);
        
        ValueTask<bool> CheckWzzzSigned(uint roleId);

        Task OnShuiLuDaHuiBattleResult(uint season, bool win);
        Task OnDaLuanDouBattleResult(uint season, bool win);

        Task OnWangZheZhiZhanBattleResult(uint season, bool win);

        Task EnterSldh(uint group);
        Task EnterDld(uint group);

        Task EnterWzzz(uint group);

        Task ExitSldh(bool changeMap = true);
        Task ExitDld(bool changeMap = true);

        Task ExitWzzz(bool changeMap = true);

        Task EnterSectWar(uint leaderRoleId, uint sectId);

        Task ExitSectWar(uint leaderRoleId, uint sectId);

        Task SignShenShouJiangLin(uint roleId, bool sign);

        Task StartShenShouJiangLin(uint endTime, uint shenShouId, uint serverId);

        Task StopShenShouJiangLin();

        Task Broadcast(Immutable<byte[]> reqBytes, bool ignoreLeave = true);

        // 检查多人日常任务是否有队员已完成
        ValueTask<string> CheckDailyTaskCompleted(uint group);

        // 检查多人副本任务是否有队员已完成
        ValueTask<string> CheckInstanceTaskCompleted(uint taskId);
    }
}