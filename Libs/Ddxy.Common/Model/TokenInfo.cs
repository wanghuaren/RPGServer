using Ddxy.Common.Model.Admin;

namespace Ddxy.Common.Model
{
    public readonly struct TokenInfo
    {
        /// <summary>
        /// 用户id
        /// </summary>
        public uint Id { get; }

        /// <summary>
        /// Admin Category, 如果是游戏前端的用户则为0
        /// </summary>
        public AdminCategory Category { get; }

        public TokenInfo(uint id, AdminCategory category)
        {
            Id = id;
            Category = category;
        }
    }
}