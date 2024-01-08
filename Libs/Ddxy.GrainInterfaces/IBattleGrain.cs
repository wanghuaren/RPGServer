using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces
{
    public interface IBattleGrain : IGrainWithIntegerKey
    {
        /// <summary>
        /// 开启一场战斗
        /// </summary>
        Task StartUp(Immutable<byte[]> reqBytes);
        
        Task ShutDown();

        ValueTask<bool> CheckActive();

        Task Exit(uint roleId);

        /// <summary>
        /// 战斗成员掉线
        /// </summary>
        Task Offline(uint roleId, bool pauseModel = false);

        /// <summary>
        /// 战斗成员上线
        /// </summary>
        Task Online(uint roleId, bool pauseModel = false);

        /// <summary>
        /// 玩家手动操作
        /// </summary>
        Task Attack(Immutable<byte[]> reqBytes);

        /// <summary>
        /// 进入观战
        /// </summary>
        ValueTask<bool> EnterWatchBattle(uint campId, uint roleId);

        /// <summary>
        /// 退出观战
        /// </summary>
        ValueTask<bool> ExitWatchBattle(uint roleId);

        /// <summary>
        /// 发弹幕
        /// </summary>
        Task SendDanMu(uint roleId, Immutable<byte[]> reqBytes);
    }
}