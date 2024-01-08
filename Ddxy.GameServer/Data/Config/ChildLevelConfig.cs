using System.Collections.Generic;
namespace Ddxy.GameServer.Data.Config
{
    public class ChildLevelConfig
    {
        public int level { get; set; }
        public int exp { get; set; }
        public Dictionary<int, List<int>> addon { get; set; }
    }
}