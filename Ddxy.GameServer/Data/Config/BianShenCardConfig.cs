using System.Text.Json;
namespace Ddxy.GameServer.Data.Config
{
    public class BianShenCardConfig
    {
        public int id { get; set; }
        public int itemid { get; set; }
        public int category { get; set; }
        public string name { get; set; }
        public int shap { get; set; }
        public string desc { get; set; }
        public JsonElement? attrList { get; set; }
        public JsonElement? wuxingAttr { get; set; }
        public int resid { get; set; }
        public int time { get; set; }
        public int wuxing { get; set; }
        public int grade { get; set; }
        public int needWuxing { get; set; }
    }
}