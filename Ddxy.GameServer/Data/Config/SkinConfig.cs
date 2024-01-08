using System.Collections.Generic;
namespace Ddxy.GameServer.Data.Config
{
    public class SkinConfig
    {
        public int id { get; set; }
        public string name { get; set; }
        public int shap { get; set; }
        public int itemId { get; set; }
        public double scale { get; set; }
        public Dictionary<string, float> pos { get; set; }
        public int index { get; set; }
        public Dictionary<string, int> attr { get; set; }
    }
}