using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface IGlobalGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task UpdateServer(uint serverId, int onlineNum);

        Task RemoveServer(uint serverId);

        ValueTask<int> CheckServer(uint serverId);

        Task UpdatePlayer(uint roleId);

        Task RemovePlayer(uint roleId);

        ValueTask<bool> CheckPlayer(uint roleId);

        ValueTask<uint> CreateBattle();

        Task RemoveBattle(uint battleId);

        ValueTask<bool> CheckBattle(uint battleId);

        Task<Immutable<byte[]>> GetResVersion();

        Task SetResVersion(string version, bool force);
    }
}