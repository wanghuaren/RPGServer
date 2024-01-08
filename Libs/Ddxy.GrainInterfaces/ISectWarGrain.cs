using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface ISectWarGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        Task<string> GmOpen(bool open, uint opUid);

        ValueTask<byte> State();

        Task<Immutable<byte[]>> GetActivityInfo();

        Task<Immutable<byte[]>> Enter(Immutable<byte[]> reqBytes);

        Task Exit(uint roleId);

        Task Offline(uint roleId, uint teamLeader);

        Task Online(uint roleId, uint teamLeader, uint sectId);

        Task CreateTeam(uint leaderRoleId, uint teamId);

        Task DestroyTeam(uint leaderRoleId);

        Task AddTeamMember(uint leaderRoleId, uint roleId);

        Task DelTeamMember(uint leaderRoleId, uint roleId);

        Task SwapTeamLeader(uint oldLeader, uint newLeader);

        ValueTask<bool> ChangePlace(uint roleId, byte placeValue);

        Task ReadyPk(uint roleId);

        Task CancelPk(uint roleId);

        Task OnPkWin(uint roleId);

        Task GrabCannon(uint roleId);

        Task OnCannonWin(uint roleId);

        Task LockDoor(uint roleId);

        Task CancelDoor(uint roleId);

        Task BreakDoor(uint roleId, uint targetRoleId);

        Task OnDoorWin(uint roleId);

        Task FreePk(uint roleId, uint targetRoleId);

        Task OnFreePkWin(uint roleId);

        Task<Immutable<byte[]>> QuerySectInfo(uint roleId, uint sectId);

        Task OnWarArenaEnter(uint battleId, uint roleId1, uint roleId2);
    }
}