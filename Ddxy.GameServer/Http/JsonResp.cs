namespace Ddxy.GameServer.Http
{
    public class JsonResp
    {
        /// <summary>
        /// 错误码, 0表示正常, 非0表示出错 
        /// </summary>
        public int? ErrCode { get; set; }

        /// <summary>
        /// 错误提示信息
        /// </summary>
        public string ErrMsg { get; set; }

        /// <summary>
        /// 数据
        /// </summary>
        public object Data { get; set; }

        /// <summary>
        /// 重新下发新的token
        /// </summary>
        public string Token { get; set; }

        public static JsonResp Ok(object data = null, string token = null)
        {
            return new JsonResp {Data = data, Token = token};
        }

        public static JsonResp InternalError(string errMsg = null, object data = default)
        {
            return Error(Http.ErrCode.InternalError, errMsg, data);
        }

        public static JsonResp DbError(string errMsg = null, object data = default)
        {
            return Error(Http.ErrCode.DbError, errMsg, data);
        }

        public static JsonResp CacheError(string errMsg = null, object data = default)
        {
            return Error(Http.ErrCode.CacheError, errMsg, data);
        }

        public static JsonResp BadRequest(string errMsg = null, object data = default)
        {
            return Error(Http.ErrCode.BadRequest, errMsg, data);
        }

        public static JsonResp BadOperation(string errMsg = null, object data = default)
        {
            return Error(Http.ErrCode.BadOperation, errMsg, data);
        }

        public static JsonResp Unauthorized(string errMsg = null, object data = default)
        {
            return Error(Http.ErrCode.Unauthorized, errMsg, data);
        }

        public static JsonResp NoPermission(string errMsg = null, object data = default)
        {
            return Error(Http.ErrCode.NoPermission, errMsg, data);
        }

        public static JsonResp Error(int errCode, string errMsg = null, object data = default)
        {
            return new JsonResp {ErrCode = errCode, ErrMsg = errMsg, Data = data};
        }

        public static JsonResp Error(string errMsg, object data = default)
        {
            return new JsonResp {ErrCode = -1, ErrMsg = errMsg, Data = data};
        }
    }
}