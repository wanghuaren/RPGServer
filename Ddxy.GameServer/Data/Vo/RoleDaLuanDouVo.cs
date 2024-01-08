using System.Text.Json.Serialization;

namespace Ddxy.GameServer.Data.Vo
{
    public class RoleDaLuanDouVo
    {
        public uint Season { get; set; }
        public uint Score { get; set; }

        public uint Win { get; set; }

        public uint Lost { get; set; }
        
        /// <summary>
        /// 表示当前是否已报名
        /// </summary>
        [JsonIgnore]
        public bool Sign { get; set; }
    }
}