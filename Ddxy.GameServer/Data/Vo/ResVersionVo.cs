using System;

namespace Ddxy.GameServer.Data.Vo
{
    [Serializable]
    public class ResVersionVo
    {
        public string Version { get; set; }
        
        public bool Force { get; set; }
    }
}