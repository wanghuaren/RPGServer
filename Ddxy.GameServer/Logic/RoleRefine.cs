using System.Collections.Generic;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic
{
    public class RoleRefine
    {
        /// <summary>
        /// 不同转生等级对应各种属性的最大抗性点
        /// </summary>
        public static readonly Dictionary<AttrType, int[]> MaxAttrValues = new Dictionary<AttrType, int[]>
        {
            [AttrType.KbingFengMax] = new[] {5, 10, 15, 20, 20},

            [AttrType.DfengYin] = new[] {16, 20, 24, 26, 26},
            [AttrType.DhunLuan] = new[] {16, 20, 24, 26, 26},
            [AttrType.DhunShui] = new[] {16, 20, 24, 26, 26},
            [AttrType.DyiWang] = new[] {16, 20, 24, 26, 26},

            [AttrType.Dfeng] = new[] {10, 12, 14, 16, 16},
            [AttrType.Dhuo] = new[] {10, 12, 14, 16, 16},
            [AttrType.Dshui] = new[] {10, 12, 14, 16, 16},
            [AttrType.Dlei] = new[] {10, 12, 14, 16, 16},
            [AttrType.Ddu] = new[] {10, 12, 14, 16, 16},
            [AttrType.DguiHuo] = new[] {10, 12, 14, 16, 16},
            [AttrType.DsanShi] = new[] {10, 12, 14, 16, 16},

            [AttrType.PxiShou] = new[] {10, 15, 20, 25, 25},
            // [AttrType.PmingZhong] = new[] {10, 15, 20, 25},
            // [AttrType.PshanBi] = new[] {10, 15, 20, 25},
            // [AttrType.PlianJi] = new[] {3, 3, 3, 3},
            // [AttrType.PlianJiLv] = new[] {10, 15, 20, 25},
            // [AttrType.PkuangBao] = new[] {10, 15, 20, 25},
            // [AttrType.PpoFang] = new[] {10, 15, 20, 25},
            // [AttrType.PpoFangLv] = new[] {10, 15, 20, 25},
            [AttrType.PfanZhenLv] = new[] {10, 13, 16, 16, 16},
            [AttrType.PfanZhen] = new[] {10, 13, 16, 19, 19}
        };

        /// <summary>
        /// 等级对应的帮贡价格
        /// </summary>
        public static readonly Dictionary<uint, uint> RefineContribPrices = new Dictionary<uint, uint>
        {
            [0] = 825,
            [1] = 2112,
            [2] = 3717,
            [3] = 7416,
            [4] = 10188,
            [5] = 16600,
            [6] = 25062,
            [7] = 30738,
            [8] = 36482,
            [9] = 43517,
            [10] = 48900,
            [11] = 53966,
            [12] = 54851,
            [13] = 55801,
            [14] = 56815,
            [15] = 57893,
            [16] = 59033,
            [17] = 60236,
            [18] = 61501,
            [19] = 62826,
            [20] = 64212,
            [21] = 65659,
            [22] = 67165,
            [23] = 68731,
            [24] = 70355,
            [25] = 76079,
            [26] = 78181,
            [27] = 80358,
            [28] = 82609,
            [29] = 84933,
            [30] = 87330,
            [31] = 89801,
            [32] = 92344,
            [33] = 94960,
            [34] = 97648,
            [35] = 100408,
            [36] = 103240,
            [37] = 107595,
            [38] = 114602,
            [39] = 121909,
            [40] = 129524,
            [41] = 137452,
            [42] = 145700,
            [43] = 154274,
            [44] = 163181,
            [45] = 172426,
            [46] = 182015,
            [47] = 191956,
            [48] = 202254,
            [49] = 212915,
            [50] = 286043,
            [51] = 301089,
            [52] = 326641,
            [53] = 342707,
            [54] = 359295,
            [55] = 376411,
            [56] = 394314,
            [57] = 418064,
            [58] = 447245,
            [59] = 466989,
            [60] = 487304,
            [61] = 508197,
            [62] = 529676,
            [63] = 551748,
            [64] = 574421,
            [65] = 594158,
            [66] = 621514,
            [67] = 646266,
            [68] = 671650,
            [69] = 697673,
            [70] = 754342,
            [71] = 781665,
            [72] = 829648,
            [73] = 838300,
            [74] = 907627,
            [75] = 1082194,
            [76] = 1283276,
            [77] = 1317827,
            [78] = 1334797,
            [79] = 1351800,
            [80] = 1368836,
            [81] = 1385907,
            [82] = 1403010,
            [83] = 1420148,
            [84] = 1437320,
            [85] = 1454526,
            [86] = 1471766,
            [87] = 1489040,
            [88] = 1506348,
            [89] = 1523691,
            [90] = 1541069,
            [91] = 1558481,
            [92] = 1575928,
            [93] = 1593410,
            [94] = 1610926,
            [95] = 1628478,
            [96] = 1646065,
            [97] = 1663687,
            [98] = 1681345,
            [99] = 1699037,
            [100] = 1719180,
            [101] = 1734290,
            [102] = 1749386,
            [103] = 1764470,
            [104] = 1779542,
            [105] = 1794601,
            [106] = 1809649,
            [107] = 1824685,
            [108] = 1839709,
            [109] = 1854722,
            [110] = 1869725,
            [111] = 1884716,
            [112] = 1899697,
            [113] = 1914667,
            [114] = 1929627,
            [115] = 1944577,
            [116] = 1959517,
            [117] = 1974447,
            [118] = 1989368,
            [119] = 2004279,
            [120] = 2019181,
            [121] = 2034074,
            [122] = 2048959,
            [123] = 2063834,
            [124] = 2078701,
            [125] = 0
        };

        /// <summary>
        /// 转生等级对应的最大修炼等级
        /// </summary>
        public static readonly Dictionary<uint, uint> RefineMaxlevels = new Dictionary<uint, uint>
        {
            [0] = 25,
            [1] = 50,
            [2] = 75,
            [3] = 100,
            [4] = 100
        };

        /// <summary>
        /// 获取指定转生等级下指定属性值的最大值
        /// </summary>
        public static int GetMaxAttrValue(byte relive, AttrType attrType)
        {
            MaxAttrValues.TryGetValue(attrType, out var val);
            if (val == null || val.Length <= relive) return 0;
            return val[relive];
        }

        /// <summary>
        /// 获取指定转生等级下最大的修炼等级
        /// </summary>
        public static uint GetMaxRefineLevel(byte relive)
        {
            RefineMaxlevels.TryGetValue(relive, out var val);
            return val;
        }

        /// <summary>
        /// 获取指定修炼等级往上升一级所需的帮贡
        /// </summary>
        public static uint GetContribPrice(uint refineLevel)
        {
            RefineContribPrices.TryGetValue(refineLevel, out var val);
            return val;
        }
    }
}