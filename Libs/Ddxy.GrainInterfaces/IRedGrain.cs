using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface IRedGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        Task Enter(uint roleId, uint sectId);

        Task Detail(uint roleId, uint redId);

        Task History(uint roleId, byte redType, bool recived);

        ValueTask<uint> Send(uint roleId, uint sectId, byte redType, string wish, uint jade, uint total);

        Task Get(uint roleId, uint redId);
    }
}