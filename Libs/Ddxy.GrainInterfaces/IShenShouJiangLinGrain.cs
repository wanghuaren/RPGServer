using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public class ShenShouJiangLinSignResult
    {
        // 错误
        public string Error { get; set;}
        // 活动状态
        public byte State { get; set; }
        // 活动奖励
        public uint Reward { get; set; }
        // 开始时间
        public uint StartTime { get; set; }
        // 结束时间
        public uint EndTime { get; set; }
    }
    public interface IShenShouJiangLinGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        Task<string> GmOpen(bool open, uint opUid);

        Task Online(uint roleId, uint teamId, uint season);

        Task<Immutable<byte[]>> GetActivityInfo();

        ValueTask<uint> GetSeason();

        ValueTask<byte> GetState();

        Task<ShenShouJiangLinSignResult> Sign(Immutable<byte[]> reqBytes);

        ValueTask<bool> UnSign(uint teamId);

        Task<ShenShouJiangLinSignResult> CheckTeamActive(uint teamId);

        Task OnBattleStart(uint teamId);

        Task OnBattleEnd(uint teamId);
    }
}