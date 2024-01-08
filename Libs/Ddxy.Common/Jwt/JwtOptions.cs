namespace Ddxy.Common.Jwt
{
    public class JwtOptions
    {
        /// <summary>
        /// token颁发者
        /// </summary>
        public string Issuer { get; set; }

        /// <summary>
        /// token使用者
        /// </summary>
        public string Audience { get; set; }

        /// <summary>
        /// token加密的key, 要大于16个字符
        /// </summary>
        public string SecretKey { get; set; }

        /// <summary>
        /// Token过期的分钟数
        /// </summary>
        public long ExpiresMinutes { get; set; }
    }
}