using System.Threading.Tasks;
using Orleans;

namespace Ddxy.GrainInterfaces
{
    public interface ITianJiangLingHouGrain : IGrainWithIntegerKey
    {
        Task StartUp();

        Task ShutDown();

        ValueTask<bool> IsOpen();

        /// <summary>
        /// 发起天降灵猴战斗
        /// </summary>
        /// <returns>0-成功，1-灵猴被打跑了, 2-玩家每日超过50次</returns>
        ValueTask<int> Fight(uint roleId, uint npcOnlyId);

        Task OnBattleResult(uint npcOnlyId, long win);
    }
}