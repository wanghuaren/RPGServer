using System.Collections.Generic;
namespace Ddxy.GameServer.Data.Config
{
    public class FakeBellConfig
    {
        public string name { get; set; }
        public uint relive { get; set; }
        public uint level { get; set; }
        public uint cfgId { get; set; }
        public List<int> skins { get; set; }
        public uint vipLevel { get; set; }
        public List<string> msg { get; set; }
        public uint delay { get; set; }
        public bool enabled { get; set; }
    }
}