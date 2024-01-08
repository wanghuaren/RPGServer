using System.Text.Json.Serialization;

namespace Ddxy.GameServer.Data.Vo
{
    public class RoleSldhVo
    {
        public uint Season { get; set; }
        
        public uint Score { get; set; }
        
        public uint Win { get; set; }
        
        public uint Lost { get; set; }
        
        // /// <summary>
        // /// 表示当前是否报名
        // /// </summary>
        // [JsonIgnore]
        // public bool Sign { get; set; }
        
        /// <summary>
        /// 跳入水陆大会地图后的分组id
        /// </summary>
        [JsonIgnore]
        public uint Group { get; set; }
    }
}