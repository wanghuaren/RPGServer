using System.Threading.Tasks;
using Ddxy.GrainInterfaces.Core;
using Orleans;
using Orleans.Concurrency;

namespace Ddxy.GrainInterfaces.Gate
{
    /// <summary>
    /// 执行网关的Http业务, 实现的Grain要求标注[StatelessWorker]
    /// </summary>
    public interface IApiGateGrain : IGrainWithIntegerKey
    {
        /// <summary>
        /// 检测指定userId缓存的token是否和参数中指定的token匹配
        /// <param name="addExpireIfEquals">如果匹配，是否增加过期时间</param>
        /// </summary>
        Task<bool> CheckUserToken(uint userId, string token, bool addExpireIfEquals = false);

        /// <summary>
        /// 查找指定用户下的所有角色id和上次使用的角色id
        /// </summary>
        Task<Roles> QueryRoles(uint userId);

        Task ReportError(Immutable<byte[]> payload);

        /// <summary>
        /// 注册
        /// </summary>
        Task<Immutable<byte[]>> SignUp(string username, string password, string inviteCode, bool isRobot, string version);

        /// <summary>
        /// 登入
        /// </summary>
        Task<Immutable<byte[]>> SignIn(string ip, string username, string password, string version);

        /// <summary>
        /// 获取公告
        /// </summary>
        Task<Immutable<byte[]>> GetNotice();

        /// <summary>
        /// 列举服务器和角色简要信息
        /// </summary>
        Task<Immutable<byte[]>> ListServer(uint userId);

        /// <summary>
        /// 创建角色
        /// </summary>
        Task<Immutable<byte[]>> CreateRole(uint userId, uint serverId, uint cfgId, string nickname);

        /// <summary>
        /// 进入区服
        /// </summary>
        Task<Immutable<byte[]>> EnterServer(uint userId, uint roleId);
        Task<string> MYXinNotify(string json);

        Task<string> MYXinNotify2(string json);

        Task<string> MYXinNotifyBindJade(string json);
    }
}