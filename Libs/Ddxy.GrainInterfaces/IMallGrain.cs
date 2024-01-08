using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface IMallGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        Task Reload();

        Task<Immutable<byte[]>> QueryItems(uint roleId, Immutable<byte[]> reqBytes);

        Task<Immutable<byte[]>> AddItem(uint roleId, Immutable<byte[]> reqBytes);

        Task<Immutable<byte[]>> DelItem(uint roleId, uint id);

        Task<bool> UpdateItem(uint roleId, uint id, uint price);

        Task<Immutable<byte[]>> BuyItem(uint roleId, uint id, uint num);

        Task<Immutable<byte[]>> GetItem(uint id);

        Task<Immutable<byte[]>> GetItemDetail(uint id);

        Task<Immutable<byte[]>> GetMyItems(uint id);
    }
}