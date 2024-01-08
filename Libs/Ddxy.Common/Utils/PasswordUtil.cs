using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace Ddxy.Common.Utils
{
    public static class PasswordUtil
    {
        /// <summary>
        /// 创建密码的salt和加盐处理后的密码
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="salt">生成的盐</param>
        /// <returns>加盐后的密码</returns>
        /// <exception cref="ArgumentException">密码不能为null或空白字符</exception>
        public static string Encode(string password, out string salt)
        {
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("password can not be null or empty");
            // 生成盐
            var saltBits = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBits);
            }

            salt = Convert.ToBase64String(saltBits);
            return Encode(password, saltBits);
        }

        /// <summary>
        /// 对指定的密码加指定的盐处理
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="salt">盐</param>
        /// <returns>加盐后的密码</returns>
        public static string Encode(string password, string salt)
        {
            return Encode(password, Convert.FromBase64String(salt));
        }

        /// <summary>
        /// 对指定的密码加指定的盐处理
        /// </summary>
        /// <param name="password">明文密码</param>
        /// <param name="saltBits">盐</param>
        /// <returns>加盐后的密码</returns>
        public static string Encode(string password, byte[] saltBits)
        {
            // derive a 256-bit subkey (use HMACSHA1 with 10,000 iterations)
            var subkey = KeyDerivation.Pbkdf2(password, saltBits, KeyDerivationPrf.HMACSHA256, 1000, 256 / 8);
            return Convert.ToBase64String(subkey);
        }
    }
}