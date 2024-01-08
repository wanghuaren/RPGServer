using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Newtonsoft.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Ddxy.GameServer.Util
{
    public static class Json
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            IgnoreNullValues = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };

        public static byte[] SerializeToBytes(object o)
        {
            byte[] bytes = null;
            if (o != null)
            {
                bytes = JsonSerializer.SerializeToUtf8Bytes(o, Options);
            }

            return bytes;
        }

        public static string Serialize(object o)
        {
            var res = string.Empty;
            if (o != null)
            {
                res = JsonSerializer.Serialize(o, Options);
            }

            return res;
        }

        public static T Deserialize<T>(string json)
        {
            return JsonSerializer.Deserialize<T>(json, Options);
        }

        public static T Deserialize<T>(byte[] bytes)
        {
            return JsonSerializer.Deserialize<T>(bytes, Options);
        }

        /// <summary>
        /// 使用Newtonsoft.Json序列化，便于支持key为值类型的字典
        /// </summary>
        public static string SafeSerialize(object o)
        {
            var res = string.Empty;
            if (o != null)
            {
                res = JsonConvert.SerializeObject(o);
            }

            return res;
        }

        /// <summary>
        /// 使用Newtonsoft.Json反序列化，便于支持key为值类型的字典
        /// </summary>
        public static T SafeDeserialize<T>(string json)
        {
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}