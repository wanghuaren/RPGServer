using System.Threading.Tasks;
using Orleans;

namespace Ddxy.GrainInterfaces
{
    public interface IDiShaXingGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        ValueTask<bool> IsStar(uint onlyId);

        ValueTask<int> ApplyChallenge(uint onlyId, uint roleId, uint roleStar);

        Task ChallengeResult(uint onlyId, bool win);
    }
}