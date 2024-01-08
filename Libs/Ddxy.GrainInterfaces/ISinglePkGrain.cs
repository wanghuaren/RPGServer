using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface ISinglePkGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        Task<string> GmOpen(bool open, uint opUid);
        
        ValueTask<bool> Online(uint roleId);

        ValueTask<bool> CheckRoleActive(uint roleId);

        Task<Immutable<byte[]>> GetActivityInfo();

        ValueTask<byte> GetState();

        Task<string> Sign(Immutable<byte[]> reqBytes);

        ValueTask<bool> UnSign(uint roleId);

        Task UpdateRole(Immutable<byte[]> reqBytes);

        Task<Immutable<byte[]>> GetInfo(uint roleId);

        Task OnBattleEnd(uint roleId, bool win);
    }
}