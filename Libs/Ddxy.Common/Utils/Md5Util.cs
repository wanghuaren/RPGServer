using System;
using System.Security.Cryptography;
using System.Text;

namespace Ddxy.Common.Utils
{
    public static class Md5Util
    {
        public static string Encode(string text, bool upper = true)
        {
            using var md5 = MD5.Create();
            var result = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
            var sign = BitConverter.ToString(result).Replace("-", "");
            if (upper) sign = sign.ToUpper();
            else sign = sign.ToLower();
            return sign;
        }
    }
}