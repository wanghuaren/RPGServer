using Ddxy.Protocol;
using System.Collections.Generic;
namespace Ddxy.GameServer.Data.Config
{
    public class TianceSkillAttrConfig
    {
        public double baseAddition {get; set;}
        public bool increase {get; set;}
    }
    public class TianceSkillConfig
    {
        public SkillId id { get; set; }
        public string icon { get; set; }
        public Dictionary<string, TianceSkillAttrConfig> attr { get; set; }
        public string name { get; set; }
        public string type { get; set; }
        public string desc1 { get; set; }
        public string desc2 { get; set; }
    }
}