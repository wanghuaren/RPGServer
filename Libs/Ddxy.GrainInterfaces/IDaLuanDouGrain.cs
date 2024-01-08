using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface IDaLuanDouGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> CheckActive();

        Task Reload();

        Task<string> GmOpen(bool open, uint opUid);
        
        Task Online(uint roleId, uint teamId, uint season);

        Task<Immutable<byte[]>> GetActivityInfo();

        ValueTask<uint> GetSeason();

        ValueTask<byte> GetState();

        Task<string> Sign(Immutable<byte[]> reqBytes);

        ValueTask<bool> UnSign(uint teamId);

        Task DaLuanDouPk(uint roleId, uint teamId, uint targetRoleId);

        Task UpdateTeam(Immutable<byte[]> reqBytes);

        Task<Immutable<byte[]>> GetInfo(uint teamId);

        ValueTask<string> CheckTeamActive(uint teamId);

        Task OnBattleEnd(uint teamId, bool win, bool reward = true);

        ValueTask<bool> CheckPkzs(uint rid);

    }
}