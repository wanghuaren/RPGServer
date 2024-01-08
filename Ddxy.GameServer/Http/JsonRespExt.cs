using Ddxy.GameServer.Util;
using Orleans.Concurrency;

namespace Ddxy.GameServer.Http
{
    public static class JsonRespExt
    {
        public static Immutable<byte[]> Serialize(this JsonResp resp)
        {
            byte[] bytes = null;
            if (resp != null)
                bytes = Json.SerializeToBytes(resp);
            return new Immutable<byte[]>(bytes);
        }
    }
}