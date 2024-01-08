using System.Text.Json;
namespace Ddxy.GameServer.Data.Config
{
    public class BianShenLevelConfig
    {
        public int level { get; set; }
        public string desc { get; set; }
        public JsonElement? attr { get; set; }
        public int exp { get; set; }
        public int itemid { get; set; }
        public int itemexp { get; set; }
    }
}