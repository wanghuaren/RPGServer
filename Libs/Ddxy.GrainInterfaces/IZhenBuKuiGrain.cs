using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface IZhenBuKuiGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        Task Online(uint roleId);

        Task<Immutable<byte[]>> GetItems();

        Task<Immutable<byte[]>> GetItem(uint cfgId);

        Task<Immutable<byte[]>> BuyItem(uint cfgId, uint num);
    }
}