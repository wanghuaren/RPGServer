using System.Collections.Generic;
namespace Ddxy.GameServer.Data.Config
{
    // 转盘奖项
    public class DrawItem
    {
        // 物品配置ID
        public uint id { get; set; }
        // 物品数量
        public uint num { get; set; }
        // 概率--千分比
        public uint rate { get; set; }
    }
    // 宝箱奖项
    public class ChestItem
    {
        // 物品配置ID
        public uint id { get; set; }
        // 物品数量
        public uint num { get; set; }
    }
    // 宝箱配置
    public class ChestConfig
    {
        // 宠物配置ID
        public uint pet { get; set; }
        // 物品列表
        public List<ChestItem> items { get; set; }
    }
    // 转盘奖励配置
    public class LuckyDrawConfig
    {
        // 转盘奖项
        public List<DrawItem> drawItems { get; set; }
        // 宝箱--宝箱列表
        public List<ChestConfig> chestList { get; set; }
        // 开宝箱需要的风雨值
        public uint fullPoint { get; set; }
        // 每次转盘获得的风雨值
        public uint drawPoint { get; set; }
        // 每天免费次数
        public uint freeTimesADay { get; set; }
    }
}