using System.Threading.Tasks;
using Orleans;

namespace Ddxy.GrainInterfaces
{
    public interface IKuLouWangGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        ValueTask<bool> IsKuLouWang(uint onlyId);

        ValueTask<int> ApplyChallenge(uint onlyId, uint roleId);

        Task ChallengeResult(uint onlyId, bool win);
    }
}