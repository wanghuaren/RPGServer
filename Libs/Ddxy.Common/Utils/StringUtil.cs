using System;
using System.Text;

namespace Ddxy.Common.Utils
{
    public static class StringUtil
    {
        // abcdefghijklmnopqrstuvwxyz
        private const string Chars1 = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        private const string Chars2 =
            "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~";

        public static string Random(int length, bool useSpecial = false)
        {
            if (length <= 0) return string.Empty;

            var chars = useSpecial ? Chars2 : Chars1;

            var rnd = new Random();
            var sb = new StringBuilder(length);
            for (var i = 0; i < length; i++)
            {
                var c = chars[rnd.Next(0, chars.Length)];
                sb.Append(c);
            }

            return sb.ToString();
        }
    }
}