using System.Threading.Tasks;
using Orleans;

namespace Ddxy.GrainInterfaces
{
    public interface IEagleGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        ValueTask<bool> IsEagle(uint onlyId);

        ValueTask<int> ApplyChallenge(uint onlyId, uint roleId);

        Task ChallengeResult(uint onlyId, bool win);
    }
}