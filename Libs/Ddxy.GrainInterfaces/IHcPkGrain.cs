using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface IHcPkGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        ValueTask<bool> CheckPk(uint roleId);

        Task<Immutable<byte[]>> FindPk(uint roleId);

        ValueTask<bool> AddPk(Immutable<byte[]> reqBytes);

        Task DelPk(uint roleId);

        Task ReadyPk(uint roleId);

        Task PkWin(uint roleId, int win);

        Task SendRoleList(uint roleId);
    }
}