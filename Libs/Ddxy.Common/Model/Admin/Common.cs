namespace Ddxy.Common.Model.Admin
{
    public enum AdminCategory : byte
    {
        Unkown = 0,

        /// <summary>
        /// 超级管理员
        /// </summary>
        System = 1,

        /// <summary>
        /// 管理员
        /// </summary>
        Admin = 2,

        /// <summary>
        /// 代理
        /// </summary>
        Agency = 3
    }
}