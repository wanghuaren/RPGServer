using System.Threading.Tasks;
using Orleans;

namespace Ddxy.GrainInterfaces
{
    public interface IJinChanSongBaoGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        ValueTask<bool> IsJinChanSongBao(uint onlyId);

        ValueTask<int> ApplyChallenge(uint onlyId, uint roleId);

        Task ChallengeResult(uint onlyId, bool win);
    }
}