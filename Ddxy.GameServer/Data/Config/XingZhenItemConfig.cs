using System.Collections.Generic;
namespace Ddxy.GameServer.Data.Config
{
    public class XingZhenItemConfig
    {
        public int id { get; set; }
        public string name { get; set; }
        public string desc { get; set; }
        public Dictionary<string, int> baseAttr { get; set; }
        public Dictionary<string, int[]> refineAttr { get; set; }
        public int unlockItemId { get; set; }
        public int unlockItemNum { get; set; }
        public List<int> upgradeItemId { get; set; }
        public int refineItemId { get; set; }
        public int refineItemNum { get; set; }
    }
}