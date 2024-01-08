using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    /// <summary>
    /// 表示一个区服
    /// </summary>
    public interface IServerGrain : IGrainWithIntegerKey
    {
        Task Startup();

        Task Shutdown(bool manually = true);

        ValueTask<bool> CheckActive();

        ValueTask<uint> Enter(uint roleId);

        Task Exit(uint onlyId, uint roleId);

        Task Online(uint roleId);

        Task Offline(uint roleId);

        ValueTask<int> GetOnlineNum(); 

        ValueTask<bool> CheckOnline(uint roleId);

        ValueTask<uint> CreateNpc(Immutable<byte[]> reqBytes);

        Task DeleteNpc(uint onlyId);

        Task DeletePlayerNpc(uint onlyId, uint roleId);

        Task DeleteTeamNpc(uint onlyId, uint teamId);

        Task<Immutable<byte[]>> FindNpc(uint onlyId);

        ValueTask<bool> ExistsNpc(uint onlyId);

        ValueTask<uint> FindCfgIdWithNpcOnlyId(uint onlyId);

        ValueTask<uint> FindOnlyIdWithNpcCfgId(uint cfgId);

        Task Broadcast(Immutable<byte[]> payload);

        ValueTask<uint> CreateTeam(uint teamTarget);

        Task DeleteTeam(uint teamId);

        Task UpdateTeam(Immutable<byte[]> reqBytes);

        Task<Immutable<byte[]>> QueryTeam(uint teamId);

        Task<Immutable<byte[]>> QueryTeams(byte teamTarget, int pageIndex, uint teamId);

        ValueTask<bool> ExistsTeam(uint teamId);

        // 合服之后, 刷新数据
        Task Reload();

        Task ReloadSects();
        
        Task ReloadMails();
        
        ValueTask<bool> ExistsSect(uint teamId);

        Task UpdateSect(Immutable<byte[]> reqBytes);

        Task DeleteSect(uint id);

        Task<Immutable<byte[]>> QuerySects(string search, int pageIndex);

        ValueTask<int> QuerySectNum();

        ValueTask<uint> FindRandomSect();

        Task<Immutable<byte[]>> GetSectRank(int pageIndex, int pageSize = 10);

        Task<Immutable<byte[]>> QuerySectsForSectWar();

        Task<Immutable<byte[]>> QueryMails();

        ValueTask<bool> CheckMail(uint id);

        Task OnMailAdd(uint id);
        
        Task OnMailDel(uint id);

        Task OnShuiLuDaHuiNewSeason(uint season);

        Task OnDaLuanDouNewSeason(uint season);

        Task OnWangZheZhiZhanNewSeason(uint season);

        Task OnShenShouJiangLinNewSeason(uint season);

        Task<Immutable<byte[]>> QueryLuckyDrawChest();

        // GM广播消息
        Task GmBroadcastPalaceNotice(string msg, uint times);

        // 设置限时充值排行榜开始结束时间
        ValueTask<bool> GmSetLimitChargeRankTimestamp(uint start, uint end, bool clean);
        // 清除当前限时充值排行榜
        ValueTask<bool> GmDelLimitChargeRank();
        
        // 设置限时等级排行榜开始结束时间
        ValueTask<bool> GmSetLimitLevelRankTimestamp(uint start, uint end, bool clean);
        // 清除当前限时等级排行榜
        ValueTask<bool> GmDelLimitLevelRank();

        // 记录聊天信息
        Task RecordChatMsg(uint fromRoleId, uint toRoleId, byte msgType, string msg, uint sendTime);

        // 重置单人PK排行榜
        Task ResetSinglePkRank();

        // 重置大乱斗PK排行榜
        Task ResetDaLuanDouRank();
    }
}