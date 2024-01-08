using System.Threading.Tasks;
using System.Collections.Generic;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface ISectGrain : IGrainWithIntegerKey
    {
        Task StartUp();
        Task ShutDown();
        ValueTask<bool> CheckActive();
        Task Join(Immutable<byte[]> reqBytes);
        Task Exit(uint roleId);
        ValueTask<bool> Dismiss();
        ValueTask<bool> Online(uint roleId);
        Task Offline(uint roleId);
        Task SetPlayerName(uint roleId, string name);
        Task SetPlayerLevel(uint roleId, uint relive, uint level);
        Task SetPlayerCfgId(uint roleId, uint cfgId);
        Task SetPlayerSkin(uint roleId, List<int> skinUse);
        Task SetPlayerWeapon(uint roleId, uint cfgId, int category, uint gem, uint level);
        Task SetPlayerWing(uint roleId, uint cfgId, int category, uint gem, uint level);
        Task ApplyJoin(Immutable<byte[]> reqBytes);
        Task HandleJoinApply(uint roleId, uint applyId, bool agree);
        Task Kickout(uint roleId, uint mbRoleId);
        ValueTask<bool> Contrib(uint roleId, uint jade);
        Task<string> Appoint(uint roleId, uint targetRoleId, byte job);
        Task<string> Silent(uint roleId, uint targetRoleId);
        Task<string> ChangeDesc(uint roleId, string desc);


        Task GetMemberList(uint roleId, int pageIndex);
        Task GetJoinApplyList(uint roleId);

        Task Broadcast(Immutable<byte[]> reqBytes);

        Task SyncSectWaring(bool value);
    }
}