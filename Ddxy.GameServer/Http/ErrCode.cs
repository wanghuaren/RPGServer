namespace Ddxy.GameServer.Http
{
    public static class ErrCode
    {
        public const int InternalError = 1; //服务器内部出错
        public const int DbError = 2; //数据库出错
        public const int CacheError = 3; //缓存出错
        public const int BadRequest = 4; //错误请求, 通常表示参数不合法
        public const int BadOperation = 5; //错误操作
        public const int Unauthorized = 6; //未授权
        public const int NoPermission = 7; //权限不足

        public const int UserNameExists = 100000; //用户名已存在
        public const int UserNotExists = 100001; //用户不存在
        public const int UserPassError = 100002; //用户密码错误
        public const int UserFrozed = 100003; //用户被冻结
        public const int UserHasRoleInServer = 100004; //用户在该分区已有角色
        public const int ServerNotExists = 100005; //区服不存在
        public const int ServerNotValid = 100006; //区服不可用
        public const int RoleNickNameExists = 100007; //角色昵称已存在 
        public const int RoleFrozed = 100008; //角色被冻结
        public const int InviteCodeNotFound = 100009; //注册码不存在
        public const int VersionError = 100010; //版本错误，请更新版本
    }
}