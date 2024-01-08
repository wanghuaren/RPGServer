using System.Threading.Tasks;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces.Gate
{
    /// <summary>
    /// 执行网关的后台Http业务, 实现的Grain要求标注[StatelessWorker]
    /// </summary>
    public interface IAdminGateGrain : IGrainWithIntegerKey
    {
        /// <summary>
        /// 登入
        /// </summary>
        Task<Immutable<byte[]>> SignIn(string ip, string username, string password);

        /// <summary>
        /// 登录之后的操作
        /// </summary>
        Task<Immutable<byte[]>> Handle(uint opUid, string ip, string method, Immutable<byte[]> payload);
    }
}