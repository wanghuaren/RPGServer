using Ddxy.Protocol;
namespace Ddxy.GameServer.Data.Config
{
    public class TianceConfig
    {
        public uint id { get; set; }
        public SkillId skillId { get; set; }
        public string name { get; set; }
        public TianceFuType type { get; set; }
        public uint tier { get; set; }
    }
}