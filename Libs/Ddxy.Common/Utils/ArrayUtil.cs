using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Ddxy.Common.Utils
{
    public static class ArrayUtil
    {
        /// <summary>
        /// string内容按","分割后形成List
        /// </summary>
        public static List<T> String2Array<T>(string str, int capacity = 10)
        {
            if (string.IsNullOrWhiteSpace(str)) return new List<T>(capacity);
            var arr = str.Split(",");
            if (capacity < arr.Length)
                capacity = arr.Length;
            var res = new List<T>(capacity);
            res.AddRange(from s in arr where !string.IsNullOrWhiteSpace(s) select (T) Convert.ChangeType(s, typeof(T)));
            return res;
        }

        /// <summary>
        /// List的内容按","连接
        /// </summary>
        public static string Array2String(IList list)
        {
            if (list == null) return string.Empty;
            var x = string.Join(",", list);
            return x;
        }

        /// <summary>
        /// string按","分割后填充为Dictionary的key，value恒定为0
        /// </summary>
        public static Dictionary<TKey, TValue> String2Dic<TKey, TValue>(string str, int capacity = 10)
        {
            if (string.IsNullOrWhiteSpace(str)) return new Dictionary<TKey, TValue>(capacity);
            var arr = str.Split(",");
            if (capacity < arr.Length)
                capacity = arr.Length;
            var res = new Dictionary<TKey, TValue>(capacity);
            foreach (var s in arr)
            {
                var tmparr = s.Split(":");
                var key = (TKey) Convert.ChangeType(tmparr[0], typeof(TKey));
                var value = (TValue) Convert.ChangeType(tmparr[1], typeof(TValue));
                res.TryAdd(key, value);
            }

            return res;
        }

        /// <summary>
        /// Dictionary的key按","进行拼接
        /// </summary>
        public static string Dic2String<TKey, TValue>(IDictionary<TKey, TValue> dic)
        {
            if (dic == null) return string.Empty;
            var list = new List<string>();
            foreach (var (k, v) in dic)
            {
                list.Add($"{k}:{v}");
            }

            return string.Join(",", list);
        }

        /// <summary>
        /// string按","分割后填充为Dictionary的key，value恒定为0
        /// </summary>
        public static Dictionary<T, byte> String2KeyDic<T>(string str, int capacity = 10)
        {
            if (string.IsNullOrWhiteSpace(str)) return new Dictionary<T, byte>(capacity);
            var arr = str.Split(",");
            if (capacity < arr.Length)
                capacity = arr.Length;
            var res = new Dictionary<T, byte>(capacity);
            foreach (var s in arr)
            {
                var key = (T) Convert.ChangeType(s, typeof(T));
                res.TryAdd(key, 0);
            }

            return res;
        }

        /// <summary>
        /// Dictionary的key按","进行拼接
        /// </summary>
        public static string KeyDic2String<T>(IDictionary<T, byte> dic)
        {
            if (dic == null) return string.Empty;
            return string.Join(",", dic.Keys);
        }
    }
}