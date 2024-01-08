namespace Ddxy.GameServer.Data.Config
{
    public class TianceLevelItem
    {
        public uint id { get; set; }
        public uint num { get; set; }
    }
    public class TianceLevelupConfig
    {
        public uint level { get; set; }
        public uint addition { get; set; }
        public uint jade { get; set; }
        public uint bindJade { get; set; }
        public TianceLevelItem item { get; set; }
    }
}