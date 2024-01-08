using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Ddxy.GameServer.Data.Config;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Data
{
    public static class ConfigService
    {
        private static string _lastLoadDir = "";

        public static readonly Dictionary<byte, List<uint>> ReliveRoles = new Dictionary<byte, List<uint>>(4);

        public static readonly Dictionary<byte, ExpConfig> RoleExps = new Dictionary<byte, ExpConfig>(300);
        public static readonly Dictionary<byte, ExpConfig> PetExps = new Dictionary<byte, ExpConfig>(300);
        public static readonly Dictionary<uint, PetConfig> Pets = new Dictionary<uint, PetConfig>(80);
        public static readonly Dictionary<uint, PetColorConfig> PetColors = new Dictionary<uint, PetColorConfig>(50);

        public static readonly Dictionary<uint, MountConfig> Mounts = new Dictionary<uint, MountConfig>(40);

        // 根据race分类
        public static readonly Dictionary<byte, List<MountConfig>> MountGroups =
            new Dictionary<byte, List<MountConfig>>(40);

        // 所有坐骑技能, key是id
        public static readonly Dictionary<uint, MountSkillConfig> MountSkills =
            new Dictionary<uint, MountSkillConfig>(100);

        public static readonly Dictionary<byte, uint> MountExps = new Dictionary<byte, uint>(100);

        // 根据攻血法敏分类, 然后根据技能1,2,3分类
        public static readonly Dictionary<int, Dictionary<int, List<MountSkillConfig>>> MountGroupedSkills =
            new Dictionary<int, Dictionary<int, List<MountSkillConfig>>>(100);
        public static readonly Dictionary<int, List<MountSkillConfig>> MountGroupedPSkills =
            new Dictionary<int, List<MountSkillConfig>>(100);


        public static readonly Dictionary<byte, RoleLevelConfig> RoleLevels = new Dictionary<byte, RoleLevelConfig>(4);
        public static readonly Dictionary<byte, PetLevelConfig> PetLevels = new Dictionary<byte, PetLevelConfig>(4);
        public static readonly Dictionary<uint, RoleConfig> Roles = new Dictionary<uint, RoleConfig>(30);

        public static readonly Dictionary<uint, Dictionary<string, RoleColorConfig>> RoleColors =
            new Dictionary<uint, Dictionary<string, RoleColorConfig>>(20);

        public static readonly Dictionary<uint, ItemConfig> Items = new Dictionary<uint, ItemConfig>(200);
        public static readonly Dictionary<uint, float> ItemPetRates = new Dictionary<uint, float>();
        public static readonly Dictionary<uint, List<uint>> ItemPetSkills = new Dictionary<uint, List<uint>>();

        public static readonly Dictionary<byte, LevelRewardConfig> LevelRewards =
            new Dictionary<byte, LevelRewardConfig>(100);


        public static readonly Dictionary<uint, MapConfig> Maps = new Dictionary<uint, MapConfig>(50);
        public static readonly Dictionary<uint, TerrainConfig> Terrains = new Dictionary<uint, TerrainConfig>(50);
        public static readonly Dictionary<uint, NpcConfig> Npcs = new Dictionary<uint, NpcConfig>(500);

        public static readonly Dictionary<uint, MonsterConfig> Monsters = new Dictionary<uint, MonsterConfig>(500);
        public static readonly List<MonsterConfig> CatchedMonsters = new List<MonsterConfig>(100);
        // 神兽降临--神兽配置及ID列表
        public static readonly Dictionary<uint, MonsterConfig> CatchedMonstersForShenShouJiangLin = new();
        public static readonly List<uint> CatchedMonstersIdForShenShouJiangLin = new();

        public static readonly Dictionary<uint, MonsterGroupConfig> MonsterGroups =
            new Dictionary<uint, MonsterGroupConfig>(200);

        public static readonly Dictionary<uint, CszlConfig> Cszl =
            new Dictionary<uint, CszlConfig>(99);

        public static readonly Dictionary<uint, PartnerConfig> Partners = new Dictionary<uint, PartnerConfig>(30);
        public static readonly Dictionary<byte, ExpConfig> PartnerExps = new Dictionary<byte, ExpConfig>(300);

        public static readonly Dictionary<uint, PartnerPowerConfig> PartnetPowers =
            new Dictionary<uint, PartnerPowerConfig>(30);

        // task id为主键的map
        public static readonly Dictionary<uint, TaskConfig> Tasks = new Dictionary<uint, TaskConfig>(200);

        // task kind为主键的map
        public static readonly Dictionary<byte, List<TaskConfig>> TypedTasks =
            new Dictionary<byte, List<TaskConfig>>(10);

        // 日常任务按group进行分类
        public static readonly Dictionary<uint, List<TaskConfig>> GroupedDailyTasks =
            new Dictionary<uint, List<TaskConfig>>(10);

        // 装备
        public static readonly Dictionary<uint, EquipConfig> Equips1 = new Dictionary<uint, EquipConfig>(500);
        public static readonly Dictionary<uint, EquipConfig> Equips2 = new Dictionary<uint, EquipConfig>(500);
        public static readonly Dictionary<uint, EquipConfig> Equips3 = new Dictionary<uint, EquipConfig>(500);
        public static readonly Dictionary<uint, EquipConfig> Equips4 = new Dictionary<uint, EquipConfig>(500);
        public static readonly Dictionary<uint, EquipConfig> Equips = new Dictionary<uint, EquipConfig>(2000);
        public static readonly Dictionary<uint, EquipConfig> Wings = new Dictionary<uint, EquipConfig>(30);
        public static readonly Dictionary<uint, OrnamentConfig> Ornaments = new Dictionary<uint, OrnamentConfig>(1000);

        public static readonly Dictionary<uint, OrnamentSuitConfig> OrnamentSuits =
            new Dictionary<uint, OrnamentSuitConfig>(100);

        public static readonly Dictionary<uint, OrnamentSkillConfig> OrnamentSkill =
            new Dictionary<uint, OrnamentSkillConfig>(100);

        public static readonly Dictionary<string, EquipAttrConfig> EquipAttrs =
            new Dictionary<string, EquipAttrConfig>(500);

        public static readonly Dictionary<uint, List<EquipRefinConfig>> EquipRefins =
            new Dictionary<uint, List<EquipRefinConfig>>(5);

        public static readonly Dictionary<uint, List<EquipRefinConfig>> OrnamentAttrs =
            new Dictionary<uint, List<EquipRefinConfig>>(5);

        public static readonly Dictionary<uint, Dictionary<string,PetOrnamentAttrConfig>> PetOrnamentAttrs =
            new Dictionary<uint, Dictionary<string, PetOrnamentAttrConfig>>();

        public static readonly Dictionary<uint, Dictionary<AttrType, int>> OrnamentDingZhiAttrs = new();

        public static readonly Dictionary<uint, TitleConfig> Titles = new Dictionary<uint, TitleConfig>(30);

        public static Dictionary<uint, ShopItemConfig> ShopItems =
            new Dictionary<uint, ShopItemConfig>(100);

        public static Dictionary<uint, List<NpcShopItemConfig>> NpcShopItems =
            new Dictionary<uint, List<NpcShopItemConfig>>(10);

        public static Dictionary<uint, ZhenBuKuiShopItemConfig> ZhenBuKuiShopItems =
            new Dictionary<uint, ZhenBuKuiShopItemConfig>(100);

        public static readonly List<LotteryConfig> Lotterys =
            new List<LotteryConfig>(100);

        public static readonly List<LotteryConfig> ShanHeTus =
            new List<LotteryConfig>(100);

        public static readonly List<LotteryConfig> BlindBoxs =
            new List<LotteryConfig>(100);

        public static readonly List<LotteryConfig> BlindBoxsPet =
            new List<LotteryConfig>(100);

        public static readonly List<TotalPayConfig> TotalPays =
            new List<TotalPayConfig>(100);

        public static readonly List<TotalPayConfig> EwaiPays =
            new List<TotalPayConfig>(100);

        public static readonly List<TotalPayConfig> DailyPays =
            new List<TotalPayConfig>(100);

        private class Exp2Potential
        {
            public string exp { get; set; }
            public string potential { get; set; }
            public uint cost { get; set; }
        }
        public static readonly List<Exp2PotentialConfig> Exp2PotentialList = new List<Exp2PotentialConfig>();
        public static uint Exp2PotentialTotalTimes = 0;
        public static uint Exp2PotentialTotalPotential = 0;

        // VIP 配置
        public static readonly List<VipConfig> VipConfigList = new List<VipConfig>();

        // 限时排行奖励配置
        public static readonly List<LimitRankPrizeConfig> LimitRankPrizeConfigList = new List<LimitRankPrizeConfig>();

        // 双倍经验
        public static readonly List<X2ExpConfig> X2ExpConfigList = new List<X2ExpConfig>();

        // 皮肤配置
        public static readonly Dictionary<int, SkinConfig> SkinConfigs = new();

        // 装备定制需要的物品
        public static readonly Dictionary<int, uint> DingZhiNeedItems = new()
        {
            { 1, 500030 },
            { 2, 500033 },
            { 3, 500031 },
            { 4, 500032 },
            { 5, 500034 },
            { 6, 500035 },
        };
        // 装备定制属性配置
        public static readonly Dictionary<int, Dictionary<AttrType, int>> DingZhiAttrConfig = new()
        {
            //1武器
            {
                1,
                new()
                {
                    { AttrType.PkuangBao, 40 },// '致命（狂暴） 4%',
                    { AttrType.PmingZhong, 40 },// '命中率 4%',
                    { AttrType.Aatk, 50 },// '增加攻击百分比 5%',
                    { AttrType.Spd, 30 },// '速度 30',
                    { AttrType.Hdfeng, 75 },// '忽视抗风 7.5%',
                    { AttrType.Hdhuo, 75 },// '忽视抗火 7.5%',
                    { AttrType.Hdshui, 75 },// '忽视抗水 7.5%',
                    { AttrType.Hdlei, 75 },// '忽视抗雷 7.5%',
                    { AttrType.HdhunLuan, 50 },// '忽视抗混乱 5%',
                    { AttrType.HdfengYin, 50 },// '忽视抗封印 5%',
                    { AttrType.HdhunShui, 50 },// '忽视抗昏睡 5%',
                    { AttrType.Hddu, 50 },// '忽视抗毒 5%',
                    { AttrType.HdguiHuo, 75 },// '忽视抗鬼火 7.5%',
                    { AttrType.HdyiWang, 50 },// '忽视抗遗忘 5%',
                    { AttrType.HdsanShi, 75 },// '忽视抗三尸 7.5%',
                    { AttrType.HdzhenShe, 20 },// '忽视抗震慑 2%',
                    { AttrType.JqMeiHuo, 50 },// '加强魅惑 5%',
                    { AttrType.GenGu, 25 },// '根骨 25',
                    { AttrType.LingXing, 25 },// '灵性 25',
                    { AttrType.LiLiang, 25 },// '力量 25',
                    { AttrType.MinJie, 25 },// '敏捷 25',
                    { AttrType.JqDefend, 50 },// '加强加防 5%',
                    { AttrType.DzhenShe, 20 },// '抗震慑 2%',
                    { AttrType.PlianJi, 2 },// '连击次数 2',
                    { AttrType.KshuiLv, 80 },// '水系狂暴率 8%',
                    { AttrType.KleiLv, 80 },// '雷系狂暴率 8%',
                    { AttrType.KhuoLv, 80 },// '火系狂暴率 8%',
                    { AttrType.KfengLv, 80 },// '风系狂暴率 8%',
                    { AttrType.KsanShiLv, 80 },// '三尸狂暴率 8%',
                    { AttrType.KguiHuoLv, 80 },// '鬼火狂暴率 8%',
                    { AttrType.Qjin, 200 },// '强力克金 20%',
                    { AttrType.Qmu, 200 },// '强力克木 20%',
                    { AttrType.Qshui, 200 },// '强力克水 20%',
                    { AttrType.Qhuo, 200 },// '强力克火 20%',
                    { AttrType.Qtu, 200 },// '强力克土 20%',
                }
            },
            //2项链
            {
                2,
                new()
                {
                    { AttrType.PkuangBao, 40 },// '致命（狂暴） 4%',
                    { AttrType.PmingZhong, 40 },// '命中率 4%',
                    { AttrType.Aatk, 30 },// '增加攻击百分比 3%',
                    { AttrType.Spd, 30 },// '速度 30',
                    { AttrType.Dhuo, 80 },// '抗火 8%',
                    { AttrType.Dfeng, 80 },// '抗风 8%',
                    { AttrType.Dshui, 80 },// '抗水 8%',
                    { AttrType.Dlei, 80 },// '抗雷 8%',
                    { AttrType.Hdfeng, 50 },// '忽视抗风 5%',
                    { AttrType.Hdhuo, 50 },// '忽视抗火 5%',
                    { AttrType.Hdshui, 50 },// '忽视抗水 5%',
                    { AttrType.Hdlei, 50 },// '忽视抗雷 5%',
                    { AttrType.DhunLuan, 80 },// '抗混乱 8%',
                    { AttrType.DfengYin, 80 },// '抗封印 8%',
                    { AttrType.DhunShui, 80 },// '抗昏睡 8%',
                    { AttrType.Ddu, 80 },// '抗毒 8%',
                    { AttrType.HdhunLuan, 30 },// '忽视抗混乱 3%',
                    { AttrType.HdfengYin, 30 },// '忽视抗封印 3%',
                    { AttrType.HdhunShui, 30 },// '忽视抗昏睡 3%',
                    { AttrType.Hddu, 30 },// '忽视抗毒 3%',
                    { AttrType.DguiHuo, 80 },// '抗鬼火 8%',
                    { AttrType.DyiWang, 80 },// '抗遗忘 8%',
                    { AttrType.DsanShi, 80 },// '抗三尸 8%',
                    { AttrType.HdguiHuo, 50 },// '忽视抗鬼火 5%',
                    { AttrType.HdyiWang, 30 },// '忽视抗遗忘 3%',
                    { AttrType.HdsanShi, 50 },// '忽视抗三尸 5%',
                    { AttrType.HdzhenShe, 20 },// '忽视抗震慑 2%',
                    { AttrType.JqMeiHuo, 30 },// '加强魅惑 3%',
                    { AttrType.DwuLi, 80 },// '抗物理 8%',
                    { AttrType.GenGu, 30 },// '根骨 30',
                    { AttrType.LingXing, 30 },// '灵性 30',
                    { AttrType.LiLiang, 30 },// '力量 30',
                    { AttrType.MinJie, 30 },// '敏捷 30',
                    { AttrType.JqDefend, 30 },// '加强加防 3%',
                    { AttrType.PshanBi, 40 },// '闪躲率 4%',
                    { AttrType.KshuiLv, 50 },// '水系狂暴率 5%',
                    { AttrType.KleiLv, 50 },// '雷系狂暴率 5%',
                    { AttrType.KhuoLv, 50 },// '火系狂暴率 5%',
                    { AttrType.KfengLv, 50 },// '风系狂暴率 5%',
                    { AttrType.KsanShiLv, 50 },// '三尸狂暴率 5%',
                    { AttrType.KguiHuoLv, 50 },// '鬼火狂暴率 5%',
                    { AttrType.Qjin, 100 },// '强力克金 10%',
                    { AttrType.Qmu, 100 },// '强力克木 10%',
                    { AttrType.Qshui, 100 },// '强力克水 10%',
                    { AttrType.Qhuo, 100 },// '强力克火 10%',
                    { AttrType.Qtu, 100 },// '强力克土 10%',
                }
            },
            //3衣服
            {
                3,
                new()
                {
                    { AttrType.Spd, 30 },// '速度 30',
                    { AttrType.Dhuo, 80 },// '抗火 8%',
                    { AttrType.Dfeng, 80 },// '抗风 8%',
                    { AttrType.Dshui, 80 },// '抗水 8%',
                    { AttrType.Dlei, 80 },// '抗雷 8%',
                    { AttrType.DhunLuan, 80 },// '抗混乱 8%',
                    { AttrType.DfengYin, 80 },// '抗封印 8%',
                    { AttrType.DhunShui, 80 },// '抗昏睡 8%',
                    { AttrType.Ddu, 80 },// '抗毒 8%',
                    { AttrType.DguiHuo, 80 },// '抗鬼火 8%',
                    { AttrType.DyiWang, 80 },// '抗遗忘 8%',
                    { AttrType.DsanShi, 80 },// '抗三尸 8%',
                    { AttrType.DwuLi, 60 },// '抗物理 6%',
                    { AttrType.GenGu, 25 },// '根骨 25',
                    { AttrType.LingXing, 25 },// '灵性 25',
                    { AttrType.LiLiang, 25 },// '力量 25',
                    { AttrType.MinJie, 25 },// '敏捷 25',
                    { AttrType.PshanBi, 40 },// '闪躲率 4%',
                    { AttrType.Qjin, 100 },// '强力克金 10%',
                    { AttrType.Qmu, 100 },// '强力克木 10%',
                    { AttrType.Qshui, 100 },// '强力克水 10%',
                    { AttrType.Qhuo, 100 },// '强力克火 10%',
                    { AttrType.Qtu, 100 },// '强力克土 10%',
                }
            },
            //4头盔
            {
                4,
                new()
                {
                    { AttrType.Spd, 30 },// '速度 30',
                    { AttrType.Dhuo, 80 },// '抗火 8%',
                    { AttrType.Dfeng, 80 },// '抗风 8%',
                    { AttrType.Dshui, 80 },// '抗水 8%',
                    { AttrType.Dlei, 80 },// '抗雷 8%',
                    { AttrType.DhunLuan, 80 },// '抗混乱 8%',
                    { AttrType.DfengYin, 80 },// '抗封印 8%',
                    { AttrType.DhunShui, 80 },// '抗昏睡 8%',
                    { AttrType.Ddu, 80 },// '抗毒 8%',
                    { AttrType.DguiHuo, 80 },// '抗鬼火 8%',
                    { AttrType.DyiWang, 80 },// '抗遗忘 8%',
                    { AttrType.DsanShi, 80 },// '抗三尸 8%',
                    { AttrType.DwuLi, 60 },// '抗物理 6%',
                    { AttrType.GenGu, 25 },// '根骨 25',
                    { AttrType.LingXing, 25 },// '灵性 25',
                    { AttrType.LiLiang, 25 },// '力量 25',
                    { AttrType.MinJie, 25 },// '敏捷 25',
                    { AttrType.PshanBi, 40 },// '闪躲率 4%',
                    { AttrType.Qjin, 100 },// '强力克金 10%',
                    { AttrType.Qmu, 100 },// '强力克木 10%',
                    { AttrType.Qshui, 100 },// '强力克水 10%',
                    { AttrType.Qhuo, 100 },// '强力克火 10%',
                    { AttrType.Qtu, 100 },// '强力克土 10%',
                }
            },
            //5鞋子
            {
                5,
                new()
                {
                    { AttrType.PkuangBao, 20 },// '致命（狂暴） 2%',
                    { AttrType.PmingZhong, 20 },// '命中率 2%',
                    { AttrType.Spd, 30 },// '速度 30',
                    { AttrType.Dhuo, 50 },// '抗火 5%',
                    { AttrType.Dfeng, 50 },// '抗风 5%',
                    { AttrType.Dshui, 50 },// '抗水 5%',
                    { AttrType.Dlei, 50 },// '抗雷 5%',
                    { AttrType.DhunLuan, 50 },// '抗混乱 5%',
                    { AttrType.DfengYin, 50 },// '抗封印 5%',
                    { AttrType.DhunShui, 50 },// '抗昏睡 5%',
                    { AttrType.Ddu, 50 },// '抗毒 5%',
                    { AttrType.DguiHuo, 50 },// '抗鬼火 5%',
                    { AttrType.DyiWang, 50 },// '抗遗忘 5%',
                    { AttrType.DsanShi, 50 },// '抗三尸 5%',
                    { AttrType.DwuLi, 50 },// '抗物理 5%',
                    { AttrType.GenGu, 35 },// '根骨 35',
                    { AttrType.LingXing, 35 },// '灵性 35',
                    { AttrType.LiLiang, 35 },// '力量 35',
                    { AttrType.MinJie, 35 },// '敏捷 35',
                    { AttrType.PshanBi, 20 },// '闪躲率 2%',
                    { AttrType.Qjin, 100 },// '强力克金 10%',
                    { AttrType.Qmu, 100 },// '强力克木 10%',
                    { AttrType.Qshui, 100 },// '强力克水 10%',
                    { AttrType.Qhuo, 100 },// '强力克火 10%',
                    { AttrType.Qtu, 100 },// '强力克土 10%',
                }
            },
            //6翅膀
            {
                6,
                new()
                {
                    { AttrType.PkuangBao, 40 },// '致命（狂暴） 4%',
                    { AttrType.PmingZhong, 40 },// '命中率 4%',
                    { AttrType.Aatk, 50 },// '增加攻击百分比 5%',
                    { AttrType.Spd, 30 },// '速度 30',
                    { AttrType.Hdfeng, 75 },// '忽视抗风 7.5%',
                    { AttrType.Hdhuo, 75 },// '忽视抗火 7.5%',
                    { AttrType.Hdshui, 75 },// '忽视抗水 7.5%',
                    { AttrType.Hdlei, 75 },// '忽视抗雷 7.5%',
                    { AttrType.HdhunLuan, 50 },// '忽视抗混乱 5%',
                    { AttrType.HdfengYin, 50 },// '忽视抗封印 5%',
                    { AttrType.HdhunShui, 50 },// '忽视抗昏睡 5%',
                    { AttrType.Hddu, 50 },// '忽视抗毒 5%',
                    { AttrType.HdguiHuo, 75 },// '忽视抗鬼火 7.5%',
                    { AttrType.HdyiWang, 50 },// '忽视抗遗忘 5%',
                    { AttrType.HdsanShi, 75 },// '忽视抗三尸 7.5%',
                    { AttrType.HdzhenShe, 20 },// '忽视抗震慑 2%',
                    { AttrType.JqMeiHuo, 50 },// '加强魅惑 5%',
                    { AttrType.GenGu, 25 },// '根骨 25',
                    { AttrType.LingXing, 25 },// '灵性 25',
                    { AttrType.LiLiang, 25 },// '力量 25',
                    { AttrType.MinJie, 25 },// '敏捷 25',
                    { AttrType.JqDefend, 50 },// '加强加防 5%',
                    { AttrType.DzhenShe, 20 },// '抗震慑 2%',
                    { AttrType.PlianJi, 2 },// '连击次数 2',
                    { AttrType.KshuiLv, 80 },// '水系狂暴率 8%',
                    { AttrType.KleiLv, 80 },// '雷系狂暴率 8%',
                    { AttrType.KhuoLv, 80 },// '火系狂暴率 8%',
                    { AttrType.KfengLv, 80 },// '风系狂暴率 8%',
                    { AttrType.KsanShiLv, 80 },// '三尸狂暴率 8%',
                    { AttrType.KguiHuoLv, 80 },// '鬼火狂暴率 8%',
                    { AttrType.Qjin, 200 },// '强力克金 20%',
                    { AttrType.Qmu, 200 },// '强力克木 20%',
                    { AttrType.Qshui, 200 },// '强力克水 20%',
                    { AttrType.Qhuo, 200 },// '强力克火 20%',
                    { AttrType.Qtu, 200 },// '强力克土 20%',
                }
            },
        };

        // 变身卡配置
        public static readonly Dictionary<int, BianShenCardConfig> BianShenCards = new();
        // 五行升级配置
        public static readonly Dictionary<int, Dictionary<int, BianShenLevelConfig>> BianShenLevels = new();
        // 金、木、水、火、土、五行 名称配置
        public static readonly Dictionary<int, string> BianShenNameConfig = new()
        {
            { (int)WuXingType.Jin, "金" },
            { (int)WuXingType.Mu, "木" },
            { (int)WuXingType.Shui, "水" },
            { (int)WuXingType.Huo, "火" },
            { (int)WuXingType.Tu, "土" },
            { (int)WuXingType.Wuxing, "五行" },
        };

        // 星阵配置
        public static readonly Dictionary<int, XingZhenItemConfig> XingZhenItems = new();
        public static readonly Dictionary<int, XingZhenLevelConfig> XingZhenLevels = new();
        // 孩子技能配置
        public static readonly Dictionary<int, ChildSkillConfig> ChildSkillItems = new();
        public static readonly Dictionary<int, List<int>> ChildSkillQualityList = new();
        public static readonly Dictionary<int, ChildLevelConfig> ChildLevels = new();
        // 转盘配置
        public static LuckyDrawConfig LuckyDrawConfig = new LuckyDrawConfig()
        {
            drawItems = new(),
            chestList = new(),
            fullPoint = 200,
            drawPoint = 1,
            freeTimesADay = 1,
        };
        // 天策符配置
        public static Dictionary<uint, TianceConfig> TianceFuListAll = new();
        // 天策符技能配置
        public static Dictionary<SkillId, TianceSkillConfig> TianceSkillList = new();
        // 类型对天策符配置
        public static Dictionary<TianceFuType, List<TianceConfig>> TianceFuListByType = new();
        // 天演策等级配置
        public static Dictionary<uint, TianceLevelupConfig> TianceLevelups = new();
        // 天策符装备等级限制配置
        // type:tier:state:limit
        public static readonly Dictionary<TianceFuType, Dictionary<uint, Dictionary<TianceFuState, uint>>> tianceLevelLimit = new()
        {
            [TianceFuType.QianQun] = new()
            {
                [1] = new() { [TianceFuState.Pos1] = 4,  [TianceFuState.Pos2] = 64  },
                [2] = new() { [TianceFuState.Pos1] = 16, [TianceFuState.Pos2] = 76  },
                [3] = new() { [TianceFuState.Pos1] = 28, [TianceFuState.Pos2] = 88  },
                [4] = new() { [TianceFuState.Pos1] = 40, [TianceFuState.Pos2] = 100 },
                [5] = new() { [TianceFuState.Pos1] = 52, [TianceFuState.Pos2] = 0   }
            },
            [TianceFuType.ZaiWu] = new()
            {
                [1] = new() { [TianceFuState.Pos1] = 8,  [TianceFuState.Pos2] = 68  },
                [2] = new() { [TianceFuState.Pos1] = 20, [TianceFuState.Pos2] = 80  },
                [3] = new() { [TianceFuState.Pos1] = 32, [TianceFuState.Pos2] = 92  },
                [4] = new() { [TianceFuState.Pos1] = 44, [TianceFuState.Pos2] = 104 },
                [5] = new() { [TianceFuState.Pos1] = 56, [TianceFuState.Pos2] = 0   }
            },
            [TianceFuType.YueShou] = new()
            {
                [1] = new() { [TianceFuState.Pos1] = 12, [TianceFuState.Pos2] = 72  },
                [2] = new() { [TianceFuState.Pos1] = 24, [TianceFuState.Pos2] = 84  },
                [3] = new() { [TianceFuState.Pos1] = 36, [TianceFuState.Pos2] = 96  },
                [4] = new() { [TianceFuState.Pos1] = 48, [TianceFuState.Pos2] = 108 },
                [5] = new() { [TianceFuState.Pos1] = 60, [TianceFuState.Pos2] = 0   }
            },
        };

        // 聊天内容检测封号配置
        public static BanChatConfig BanChat = new() { numberLimit = 3, numberList = new(), wordList = new() };

        // 切割等级配置
        public static Dictionary<uint, QieGeLevelConfig> QieGeLevelList = new ();
        // 装备升星配置
        public static Dictionary<uint, JingLianConfig> JingLianList = new ();
        // 充值项目配置
        public static ChargeConfig ChargeItemConfig = new ChargeConfig() { jade = new(), bindJade = new() };
        // 假铃铛
        public static FakeBellConfig FakeBell = new()
        {
            name = "",
            relive = 0,
            level = 0,
            cfgId = 0,
            skins = new(),
            vipLevel = 10,
            msg = new(),
            delay = uint.MaxValue,
            enabled = false
        };
        // 物品商店
        public static Dictionary<uint, ItemShopGood> ItemShopGoods = new();
        public static Dictionary<uint, GiftShopGood> GiftShopGoods = new Dictionary<uint, GiftShopGood>(10);
        public static TotalPayConfig FirstPay;

        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            // 忽略大小写
            PropertyNameCaseInsensitive = true
        };

        /// <summary>
        /// 获取指定转生等级能用的角色id
        /// </summary>
        public static List<uint> GetRolesCanBeUsed(byte relive)
        {
            var dic = new Dictionary<uint, bool>();
            foreach (var (k, v) in ReliveRoles)
            {
                if (k <= relive)
                {
                    foreach (var rid in v)
                    {
                        dic[rid] = true;
                    }
                }
            }

            return dic.Keys.ToList();
        }

        public static TaskEventConfig GetEventStep(uint taskId, uint taskStep)
        {
            Tasks.TryGetValue(taskId, out var taskCfg);
            if (taskCfg?.Events == null || taskStep >= taskCfg.Events.Length) return null;
            return taskCfg.Events[taskStep];
        }

        public static ulong GetRoleUpgradeExp(byte relive, byte level)
        {
            RoleExps.TryGetValue(level, out var cfg);
            if (cfg == null) return 0;
            if (relive == 0) return cfg.Exp0 ?? 0;
            if (relive == 1) return cfg.Exp1 ?? 0;
            if (relive == 2) return cfg.Exp2 ?? 0;
            if (relive == 3) return cfg.Exp3 ?? 0;
            return cfg.Exp4 ?? 0;
        }

        public static ulong GetPetUpgradeExp(byte relive, byte level)
        {
            PetExps.TryGetValue(level, out var cfg);
            if (cfg == null) return 0;
            if (relive == 0) return cfg.Exp0 ?? 0;
            if (relive == 1) return cfg.Exp1 ?? 0;
            if (relive == 2) return cfg.Exp2 ?? 0;
            if (relive == 3) return cfg.Exp3 ?? 0;
            return cfg.Exp4 ?? 0;
        }

        public static ulong GetPartnerUpgradeExp(byte relive, byte level)
        {
            PartnerExps.TryGetValue(level, out var cfg);
            if (cfg == null) return 0;
            if (relive == 0) return cfg.Exp0 ?? 0;
            if (relive == 1) return cfg.Exp1 ?? 0;
            if (relive == 2) return cfg.Exp2 ?? 0;
            return cfg.Exp3 ?? 0;
        }

        public static byte GetRoleMinLevel(byte relive)
        {
            return RoleLevels[relive].MinLv;
        }

        public static byte GetRoleMaxLevel(byte relive)
        {
            return RoleLevels[relive].MaxLv;
        }

        public static uint GetRoleSkillMaxExp(byte relive)
        {
            return RoleLevels[relive].MaxSkillExp;
        }

        public static byte GetPetMinLevel(byte relive)
        {
            return PetLevels[relive].MinLv;
        }

        public static byte GetPetMaxLevel(byte relive)
        {
            return PetLevels[relive].MaxLv;
        }

        public static uint GetMountUpgradeExp(byte level)
        {
            MountExps.TryGetValue(level, out var exp);
            return exp;
        }

        // 随机一个宝宝
        public static MonsterConfig GetRandomCatchedMonsterConfig()
        {
            if (CatchedMonsters.Count == 0) return null;
            var idx = new Random().Next(0, CatchedMonsters.Count);
            return CatchedMonsters[idx];
        }

        public static TaskFailEventConfig GetTaskFailEventConfig(uint taskId, int step)
        {
            Tasks.TryGetValue(taskId, out var cfg);
            if (cfg == null || step >= cfg.FailEvents.Length) return null;
            return cfg.FailEvents[step];
        }

        public static MedicineEffect GetMedicineEffect(uint itemId)
        {
            var effect = new MedicineEffect();
            Items.TryGetValue(itemId, out var cfg);
            if (cfg == null) return effect;
            if (itemId == 40017)
            {
                // 破隐身
                effect.DYinShen = true;
                return effect;
            }

            if (itemId == 40018)
            {
                // 隐身
                effect.YinShen = true;
                return effect;
            }

            var json = cfg.Json;
            if (json.ValueKind != JsonValueKind.Object || !json.TryGetProperty("hm", out var hmElement)) return effect;
            var hm = hmElement.GetString();
            if (string.IsNullOrWhiteSpace(hm)) return effect;

            var hpIdx = hm.IndexOf('h');
            var mpIdx = hm.IndexOf('m');
            if (hpIdx >= 0)
            {
                if (json.TryGetProperty("jc", out var jcElement))
                {
                    var jc = jcElement.GetString();
                    if ("j".Equals(jc)) effect.AddHp = cfg.Num;
                    else if ("c".Equals(jc)) effect.MulHp = cfg.Num;
                }
            }

            if (mpIdx >= 0)
            {
                if (json.TryGetProperty("jc", out var jcElement))
                {
                    var jc = jcElement.GetString();
                    if ("j".Equals(jc)) effect.AddMp = cfg.Num;
                    else if ("c".Equals(jc)) effect.MulMp = cfg.Num;
                }
            }

            return effect;
        }

        /// <summary>
        /// 随机获得一本宠物技能书
        /// </summary>
        /// <param name="skillLevel">技能等级</param>
        /// <param name="strict">严格模式下，终极技能书没法开出 化无 子虚 绝境</param>
        public static uint GetRandomPetSkill(uint skillLevel, bool strict = false)
        {
            ItemPetSkills.TryGetValue(skillLevel, out var list);
            if (list == null || list.Count == 0) return 0;
            {
                // 排除技能书 永远开不出来
                list = new List<uint>(list);
                list.Remove(60017);
                list.Remove(60046);
                list.Remove(60047);
                list.Remove(60048);
                list.Remove(60049);
                list.Remove(60050);
                list.Remove(60051);
                list.Remove(60052);
            }
            if (strict && skillLevel == 3)
            {
                // 拷贝1份出来, 并移除化无、子虚、绝境
                list = new List<uint>(list);
                list.Remove(60023);
                list.Remove(60024);
                list.Remove(60025);
            }

            var rnd = new Random();
            return list[rnd.Next(0, list.Count)];
        }


        public static async Task Load(string dir, bool reload = false)
        {
            _lastLoadDir = dir;

            var files = new List<string>(200);
            SearchFiles(dir, files);
            // 遍历文件进行解析
            foreach (var file in files)
            {
                try
                {
                    await ParseFile(file, reload);
                }
                catch (Exception e)
                {
                    throw new Exception($"load file {Path.GetFileName(file)} error", e);
                }
            }

            // 二次构造
            Build(reload);
        }

        public static async Task Reload()
        {
            if (string.IsNullOrWhiteSpace(_lastLoadDir)) return;
            await Load(_lastLoadDir, true);
        }

        private static void SearchFiles(string dir, List<string> files)
        {
            // 忽略client目录
            if (string.Equals("client", Path.GetFileNameWithoutExtension(dir),
                StringComparison.OrdinalIgnoreCase)) return;

            // 本目录下的json文件
            files.AddRange(Directory.GetFiles(dir, "*.json", SearchOption.TopDirectoryOnly));

            // 继续查找子目录
            foreach (var tmpDir in Directory.GetDirectories(dir))
            {
                SearchFiles(tmpDir, files);
            }
        }

        private static async Task ParseFile(string path, bool reload = false)
        {
            var shortName = Path.GetFileNameWithoutExtension(path);
            switch (shortName)
            {
                case "relive_role":
                {
                    ReliveRoles.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, List<uint>>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        ReliveRoles[Convert.ToByte(key)] = value;
                    }

                    break;
                }
                case "role_exp":
                {
                    RoleExps.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, ExpConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        RoleExps[Convert.ToByte(key)] = value;
                    }

                    break;
                }
                case "pet_exp":
                {
                    PetExps.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, ExpConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        PetExps[Convert.ToByte(key)] = value;
                    }

                    break;
                }
                case "role_level":
                {
                    RoleLevels.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, RoleLevelConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        RoleLevels[Convert.ToByte(key)] = value;
                    }

                    break;
                }
                case "role_color":
                {
                    RoleColors.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, Dictionary<string, RoleColorConfig>>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        RoleColors[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "level_reward":
                {
                    LevelRewards.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, LevelRewardConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        LevelRewards[Convert.ToByte(key)] = value;
                    }

                    break;
                }
                case "pet_level":
                {
                    PetLevels.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, PetLevelConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        PetLevels[Convert.ToByte(key)] = value;
                    }

                    break;
                }
                case "pet_color":
                {
                    PetColors.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, PetColorConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        PetColors[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "partner":
                {
                    Partners.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, PartnerConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Partners[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "partner_power":
                {
                    PartnetPowers.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, PartnerPowerConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        PartnetPowers[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "partner_exp":
                {
                    PartnerExps.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, ExpConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        PartnerExps[Convert.ToByte(key)] = value;
                    }

                    break;
                }
                case "map":
                {
                    if (reload) return;
                    Maps.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, MapConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Maps[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "npc":
                {
                    Npcs.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, NpcConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Npcs[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "role":
                {
                    Roles.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, RoleConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Roles[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "task":
                {
                    Tasks.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, TaskConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Tasks[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "monster":
                {
                    Monsters.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, MonsterConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Monsters[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "monster_group":
                {
                    MonsterGroups.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, MonsterGroupConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        MonsterGroups[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "cszl":
                {
                    Cszl.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, CszlConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Cszl[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "pet":
                {
                    Pets.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, PetConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Pets[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "mount":
                {
                    Mounts.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, MountConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Mounts[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "mount_exp":
                {
                    MountExps.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, MountExpConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        MountExps[Convert.ToByte(key)] = value.Exp;
                    }

                    break;
                }
                case "mount_skill":
                {
                    MountSkills.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, MountSkillConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        value.Attrs2 = new Dictionary<byte, float>[value.Attrs.Length];
                        for (var i = 0; i < value.Attrs.Length; i++)
                        {
                            value.Attrs2[i] = new Dictionary<byte, float>(5);

                            var element = value.Attrs[i];
                            foreach (var property in element.EnumerateObject())
                            {
                                byte.TryParse(property.Name, out var attrType);
                                if (attrType > 0)
                                {
                                    value.Attrs2[i].Add(attrType, property.Value.GetSingle());
                                }
                            }
                        }

                        MountSkills[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "item":
                {
                    Items.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, ItemConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Items[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "equip_1":
                {
                    Equips1.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, EquipConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Equips1[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "equip_2":
                {
                    Equips2.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, EquipConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Equips2[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "equip_3":
                {
                    Equips3.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, EquipConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Equips3[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "equip_4":
                {
                    Equips4.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, EquipConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Equips4[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "wing":
                {
                    Wings.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, EquipConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Wings[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "title":
                {
                    Titles.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, TitleConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Titles[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "equip_attrs":
                {
                    EquipAttrs.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, EquipAttrConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        EquipAttrs[key] = value;
                    }

                    break;
                }
                case "equip_refine":
                {
                    EquipRefins.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, EquipRefinConfig>>(stream, Options);
                    for (uint i = 1; i <= 5; i++)
                    {
                        EquipRefins.Add(i, new List<EquipRefinConfig>(dic.Count));
                    }

                    foreach (var cfg in dic.Values)
                    {
                        if (!string.IsNullOrWhiteSpace(cfg.Pos1))
                        {
                            EquipRefins.TryGetValue(1, out var list);
                            if (list == null) continue;
                            list.Add(cfg);
                        }

                        if (!string.IsNullOrWhiteSpace(cfg.Pos2))
                        {
                            EquipRefins.TryGetValue(2, out var list);
                            if (list == null) continue;
                            list.Add(cfg);
                        }

                        if (!string.IsNullOrWhiteSpace(cfg.Pos3))
                        {
                            EquipRefins.TryGetValue(3, out var list);
                            if (list == null) continue;
                            list.Add(cfg);
                        }

                        if (!string.IsNullOrWhiteSpace(cfg.Pos4))
                        {
                            EquipRefins.TryGetValue(4, out var list);
                            if (list == null) continue;
                            list.Add(cfg);
                        }

                        if (!string.IsNullOrWhiteSpace(cfg.Pos5))
                        {
                            EquipRefins.TryGetValue(5, out var list);
                            if (list == null) continue;
                            list.Add(cfg);
                        }
                    }

                    break;
                }
                case "ornament":
                {
                    Ornaments.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, OrnamentConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        Ornaments[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "ornament_suit":
                {
                    OrnamentSuits.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, OrnamentSuitConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        OrnamentSuits[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "ornament_skill":
                {
                    OrnamentSkill.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, OrnamentSkillConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        value.Attrs2 = new Dictionary<AttrType, float>();
                        if (value.Attrs.ValueKind == JsonValueKind.Object)
                        {
                            foreach (var property in value.Attrs.EnumerateObject())
                            {
                                byte.TryParse(property.Name, out var attrType);
                                if (attrType > 0)
                                {
                                    value.Attrs2[(AttrType) attrType] = property.Value.GetSingle();
                                }
                            }
                        }

                        OrnamentSkill[Convert.ToUInt32(key)] = value;
                    }

                    break;
                }
                case "ornament_attrs":
                {
                    OrnamentAttrs.Clear();
                    OrnamentDingZhiAttrs.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, EquipRefinConfig>>(stream, Options);
                    for (uint i = 1; i <= 5; i++)
                    {
                        OrnamentAttrs.Add(i, new List<EquipRefinConfig>(10));
                        OrnamentDingZhiAttrs.Add(i, new());
                    }

                    foreach (var cfg in dic.Values)
                    {
                        if (!string.IsNullOrWhiteSpace(cfg.Pos1))
                        {
                            var arr = cfg.Pos1.Split(",");
                            int.TryParse(arr[1], out var max);
                            if (max > 0)
                            {
                                if (GameDefine.EquipAttrTypeMap.TryGetValue(cfg.Attr, out var attrType))
                                {
                                    OrnamentDingZhiAttrs[1][attrType] = max;
                                }
                            }
                            OrnamentAttrs.TryGetValue(1, out var list);
                            if (list == null) continue;
                            list.Add(cfg);
                        }

                        if (!string.IsNullOrWhiteSpace(cfg.Pos2))
                        {
                            var arr = cfg.Pos2.Split(",");
                            int.TryParse(arr[1], out var max);
                            if (max > 0)
                            {
                                if (GameDefine.EquipAttrTypeMap.TryGetValue(cfg.Attr, out var attrType))
                                {
                                    OrnamentDingZhiAttrs[2][attrType] = max;
                                }
                            }
                            OrnamentAttrs.TryGetValue(2, out var list);
                            if (list == null) continue;
                            list.Add(cfg);
                        }

                        if (!string.IsNullOrWhiteSpace(cfg.Pos3))
                        {
                            var arr = cfg.Pos3.Split(",");
                            int.TryParse(arr[1], out var max);
                            if (max > 0)
                            {
                                if (GameDefine.EquipAttrTypeMap.TryGetValue(cfg.Attr, out var attrType))
                                {
                                    OrnamentDingZhiAttrs[3][attrType] = max;
                                }
                            }
                            OrnamentAttrs.TryGetValue(3, out var list);
                            if (list == null) continue;
                            list.Add(cfg);
                        }

                        if (!string.IsNullOrWhiteSpace(cfg.Pos4))
                        {
                            var arr = cfg.Pos4.Split(",");
                            int.TryParse(arr[1], out var max);
                            if (max > 0)
                            {
                                if (GameDefine.EquipAttrTypeMap.TryGetValue(cfg.Attr, out var attrType))
                                {
                                    OrnamentDingZhiAttrs[4][attrType] = max;
                                }
                            }
                            OrnamentAttrs.TryGetValue(4, out var list);
                            if (list == null) continue;
                            list.Add(cfg);
                        }

                        if (!string.IsNullOrWhiteSpace(cfg.Pos5))
                        {
                            var arr = cfg.Pos5.Split(",");
                            int.TryParse(arr[1], out var max);
                            if (max > 0)
                            {
                                if (GameDefine.EquipAttrTypeMap.TryGetValue(cfg.Attr, out var attrType))
                                {
                                    OrnamentDingZhiAttrs[5][attrType] = max;
                                }
                            }
                            OrnamentAttrs.TryGetValue(5, out var list);
                            if (list == null) continue;
                            list.Add(cfg);
                        }
                    }

                    break;
                }
                case "pet_ornament_attrs":
                {
                    PetOrnamentAttrs.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<uint, Dictionary<string, PetOrnamentAttrConfig>>>(stream, Options);
                    foreach (var (type, value) in dic)
                    {
                        PetOrnamentAttrs.Add(type, value);
                    }
                    break;
                }
                case "shop":
                {
                    ShopItems.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, ShopItemConfig>>(stream, Options);
                    foreach (var value in dic.Values)
                    {
                        ShopItems[value.ItemId] = value;
                    }

                    break;
                }
                case "npc_shop":
                {
                    NpcShopItems.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, NpcShopItemConfig>>(stream, Options);
                    foreach (var value in dic.Values)
                    {
                        var npcId = value.NpcId;
                        NpcShopItems.TryGetValue(npcId, out var list);
                        if (list == null)
                        {
                            list = new List<NpcShopItemConfig>(100);
                            NpcShopItems[npcId] = list;
                        }

                        list.Add(value);
                    }

                    break;
                }
                case "zhenbukui_shop":
                {
                    ZhenBuKuiShopItems.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, ZhenBuKuiShopItemConfig>>(stream, Options);
                    foreach (var value in dic.Values)
                    {
                        ZhenBuKuiShopItems[value.ItemId] = value;
                    }

                    break;
                }
                case "lottery":
                {
                    Lotterys.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, LotteryConfig>>(stream, Options);
                    foreach (var value in dic.Values)
                    {
                        Lotterys.Add(value);
                    }
                    
                    break;
                }
                case "blind_box":
                {
                    BlindBoxs.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, LotteryConfig>>(stream, Options);
                    foreach (var value in dic.Values)
                    {
                        BlindBoxs.Add(value);
                    }
                    
                    break;
                }
                case "blind_box_pet":
                {
                    BlindBoxsPet.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, LotteryConfig>>(stream, Options);
                    foreach (var value in dic.Values)
                    {
                        BlindBoxsPet.Add(value);
                    }
                    
                    break;
                }
                case "shanhetu":
                {
                    ShanHeTus.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, LotteryConfig>>(stream, Options);
                    foreach (var value in dic.Values)
                    {
                        ShanHeTus.Add(value);
                    }

                    break;
                }
                case "total_pay":
                {
                    TotalPays.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, TotalPayConfig>>(stream, Options);
                    var xx = dic.Values.OrderBy(it => it.Pay);
                    TotalPays.AddRange(xx);
                    break;
                }
                case "ewai_pay":
                {
                    EwaiPays.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, TotalPayConfig>>(stream, Options);
                    var xx = dic.Values.OrderBy(it => it.Pay);
                    EwaiPays.AddRange(xx);
                    break;
                }
                case "daily_pay":
                {
                    DailyPays.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer
                        .DeserializeAsync<Dictionary<string, TotalPayConfig>>(stream, Options);
                    var xx = dic.Values.OrderBy(it => it.Pay);
                    DailyPays.AddRange(xx);
                    break;
                }
                case "first_pay":
                {
                    await using var stream = File.OpenRead(path);
                    FirstPay = await JsonSerializer.DeserializeAsync<TotalPayConfig>(stream, Options);
                    break;
                }
                // 皮肤
                case "skin":
                {
                    SkinConfigs.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<int, SkinConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        SkinConfigs[value.shap] = value;
                    }
                    break;
                }
                // 变身卡配置
                case "bianshen_card":
                {
                    BianShenCards.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<int, BianShenCardConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        BianShenCards[key] = value;
                    }
                    break;
                }
                // 五行升级配置
                case "bianshen_level":
                {
                    BianShenLevels.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<int, Dictionary<int, BianShenLevelConfig>>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        BianShenLevels[key] = value;
                    }
                    break;
                }
                // 星阵阵型配置
                case "xingzhen_item":
                {
                    XingZhenItems.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<int, XingZhenItemConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        XingZhenItems[key] = value;
                    }
                    break;
                }
                // 星阵等级配置
                case "xingzhen_level":
                {
                    XingZhenLevels.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<int, XingZhenLevelConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        XingZhenLevels[key] = value;
                    }
                    break;
                }
                // 孩子技能配置
                case "child_skill":
                {
                    ChildSkillItems.Clear();
                    ChildSkillQualityList.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<int, ChildSkillConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        ChildSkillItems[key] = value;
                        var quality = value.quality;
                        if (!ChildSkillQualityList.ContainsKey(quality))
                        {
                            ChildSkillQualityList.Add(quality, new());
                        }
                        ChildSkillQualityList[quality].Add(value.id);
                    }
                    break;
                }
                // 孩子等级配置
                case "child_level":
                {
                    ChildLevels.Clear();
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<int, ChildLevelConfig>>(stream, Options);
                    foreach (var (key, value) in dic)
                    {
                        ChildLevels[key] = value;
                    }
                    break;
                }
                // 转盘配置
                case "luckydraw":
                {
                    await using var stream = File.OpenRead(path);
                    var c = await JsonSerializer.DeserializeAsync<LuckyDrawConfig>(stream, Options);
                    LuckyDrawConfig.drawItems.Clear();
                    LuckyDrawConfig.drawItems.AddRange(c.drawItems);

                    LuckyDrawConfig.chestList.Clear();
                    LuckyDrawConfig.chestList.AddRange(c.chestList);

                    LuckyDrawConfig.fullPoint = c.fullPoint;

                    LuckyDrawConfig.drawPoint = c.drawPoint;

                    LuckyDrawConfig.freeTimesADay = c.freeTimesADay;
                    break;
                }
                // 经验对属性点
                case "exp2potential":
                {
                    await using var stream = File.OpenRead(path);
                    var list = await JsonSerializer.DeserializeAsync<List<Exp2Potential>>(stream, Options);
                    Exp2PotentialList.Clear();
                    Exp2PotentialTotalTimes = 0;
                    Exp2PotentialTotalPotential = 0;
                    foreach (var i in list)
                    {
                        var potential = Convert.ToUInt32(i.potential);
                        Exp2PotentialList.Add(new Exp2PotentialConfig()
                        {
                            exp = Convert.ToUInt64(i.exp),
                            potential = potential,
                            cost = i.cost,
                        });
                        Exp2PotentialTotalTimes += 1;
                        Exp2PotentialTotalPotential += potential;
                    }
                    break;
                }
                // VIP配置
                case "vip":
                {
                    await using var stream = File.OpenRead(path);
                    var list = await JsonSerializer.DeserializeAsync<List<VipConfig>>(stream, Options);
                    VipConfigList.Clear();
                    foreach (var i in list)
                    {
                        VipConfigList.Add(i);
                    }
                    break;
                }
                // 限时排行奖励配置
                case "limit_rank_prize":
                {
                    await using var stream = File.OpenRead(path);
                    var list = await JsonSerializer.DeserializeAsync<List<LimitRankPrizeConfig>>(stream, Options);
                    LimitRankPrizeConfigList.Clear();
                    foreach (var i in list)
                    {
                            LimitRankPrizeConfigList.Add(i);
                    }
                    break;
                }
                // 双倍经验
                case "x2exp":
                {
                    await using var stream = File.OpenRead(path);
                    var list = await JsonSerializer.DeserializeAsync<List<X2ExpConfig>>(stream, Options);
                    X2ExpConfigList.Clear();
                    foreach (var i in list)
                    {
                        X2ExpConfigList.Add(i);
                    }
                    break;
                }
                // 天策符配置
                case "tiance":
                {
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<uint, TianceConfig>>(stream, Options);
                    TianceFuListAll.Clear();
                    TianceFuListByType.Clear();
                    foreach (var (key, value) in dic)
                    {
                        // 全部
                        TianceFuListAll[value.id] = value;
                        // 按照类型分类
                        if (!TianceFuListByType.ContainsKey(value.type))
                        {
                            TianceFuListByType[value.type] = new();
                        }
                        TianceFuListByType[value.type].Add(value);
                    }
                    break;
                }
                // 天策符技能配置
                case "tiance_skill":
                {
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<uint, TianceSkillConfig>>(stream, Options);
                    TianceSkillList.Clear();
                    foreach (var (key, value) in dic)
                    {
                        TianceSkillList[value.id] = value;
                    }
                    break;
                }
                // 天演策等级配置
                case "tiance_levelup":
                {
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<uint, TianceLevelupConfig>>(stream, Options);
                    TianceLevelups.Clear();
                    foreach (var (key, value) in dic)
                    {
                        TianceLevelups[Convert.ToUInt32(key)] = value;
                    }
                    break;
                }
                // 聊天内容检测封号配置
                case "banchat":
                {
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<BanChatConfig>(stream, Options);
                    BanChat.numberLimit = dic.numberLimit > 0 ? dic.numberLimit : 1;
                    BanChat.numberList.Clear();
                    BanChat.numberList.AddRange(dic.numberList);
                    BanChat.wordList.Clear();
                    BanChat.wordList.AddRange(dic.wordList);
                    break;
                }
                // 切割等级配置
                case "qiege":
                {
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<uint, QieGeLevelConfig>>(stream, Options);
                    QieGeLevelList.Clear();
                    foreach (var (key, value) in dic)
                    {
                        QieGeLevelList[Convert.ToUInt32(key)] = value;
                    }
                    break;
                }
                // 装备升星配置
                case "jinglian":
                {
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<uint, JingLianConfig>>(stream, Options);
                    JingLianList.Clear();
                    foreach (var (key, value) in dic)
                    {
                        JingLianList[Convert.ToUInt32(key)] = value;
                    }
                    break;
                }
                // 充值项目配置
                case "charge":
                {
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<ChargeConfig>(stream, Options);
                    ChargeItemConfig.jade.Clear();
                    ChargeItemConfig.jade.AddRange(dic.jade);
                    ChargeItemConfig.bindJade.Clear();
                    ChargeItemConfig.bindJade.AddRange(dic.bindJade);
                    break;
                }
                // 物品商店
                case "item_shop":
                {
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<uint, JItemShopGood>>(stream, Options);
                    ItemShopGoods.Clear();
                    foreach (var (key, value) in dic)
                    {
                        ItemShopGoods.Add(value.id, new ItemShopGood() { Id = value.id, Item = value.item, Num = value.num, Price = value.price });
                    }
                    break;
                }
                // 直购礼包
                case "gift_shop":
                {
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<Dictionary<uint, GiftShopGood>>(stream, Options);
                    GiftShopGoods.Clear();
                    foreach (var (key, value) in dic)
                    {
                        GiftShopGoods[value.id] = value;
                    }
                    break;
                }
                // 假铃铛
                case "fake_bell":
                {
                    await using var stream = File.OpenRead(path);
                    var dic = await JsonSerializer.DeserializeAsync<FakeBellConfig>(stream, Options);
                    FakeBell.name = dic.name;
                    FakeBell.relive = dic.relive;
                    FakeBell.level = dic.level;
                    FakeBell.cfgId = dic.cfgId;
                    FakeBell.skins.Clear();
                    FakeBell.skins.AddRange(dic.skins);
                    FakeBell.vipLevel = dic.vipLevel;
                    FakeBell.msg.Clear();
                    FakeBell.msg.AddRange(dic.msg);
                    FakeBell.delay = dic.delay;
                    FakeBell.enabled = dic.enabled;
                    break;
                }
                default:
                {
                    if (reload) return;
                    if (shortName.StartsWith("terrain_"))
                    {
                        await using var stream = File.OpenRead(path);
                        var config = await JsonSerializer.DeserializeAsync<TerrainConfig>(stream, Options);
                        Terrains[config.Id] = config;
                    }
                }
                    break;
            }
        }

        private static void Build(bool reload = false)
        {
            // 读取npc的autoCreate，反向填充到map的npcs中, 这样MapGrain初始化的时候就能读取到要初始化的Npc
            if (!reload)
            {
                foreach (var (key, npcCfg) in Npcs)
                {
                    if (npcCfg.AutoCreate == null) continue;
                    Maps.TryGetValue(npcCfg.AutoCreate.Map, out var mapCfg);
                    if (mapCfg == null) continue;
                    mapCfg.Npcs ??= new List<uint>(5);
                    mapCfg.Npcs.Add(key);
                }
            }

            foreach (var (_, value) in Tasks)
            {
                // 基于kind进行分类
                TypedTasks.TryGetValue(value.Kind, out var list);
                if (list == null)
                {
                    list = new List<TaskConfig>();
                    TypedTasks[value.Kind] = list;
                }

                list.Add(value);

                // 基于group进行分类, 只选取日常任务
                if (value.Kind == 2)
                {
                    GroupedDailyTasks.TryGetValue(value.Group, out list);
                    if (list == null)
                    {
                        list = new List<TaskConfig>();
                        GroupedDailyTasks[value.Group] = list;
                    }

                    list.Add(value);
                }
            }

            // monster对catch进行筛选，过滤掉神兽降临中的神兽
            CatchedMonsters.AddRange(Monsters.Values.Where(p => p.Catch && !(p.Id >= 11001 && p.Id <= 12006)));
            // 神兽降临--神兽配置及ID列表
            CatchedMonstersForShenShouJiangLin.Clear();
            CatchedMonstersIdForShenShouJiangLin.Clear();
            var ssjl = Monsters.Values.Where(p => p.Catch && p.Id >= 11001 && p.Id <= 12006).ToList();
            foreach (var c in ssjl)
            {
                CatchedMonstersForShenShouJiangLin[c.Id] = c;
                CatchedMonstersIdForShenShouJiangLin.Add(c.Id);
            }

            // 和宠物相关的道具
            ItemPetRates.Clear();
            foreach (var v in Items.Values)
            {
                // 宠物技能, 记录每等级对应的技能书id集合
                if (v.Type == 10)
                {
                    ItemPetSkills.TryGetValue(v.Level, out var skills);
                    if (skills == null)
                    {
                        skills = new List<uint>();
                        ItemPetSkills[v.Level] = skills;
                    }

                    skills.Add(v.Id);
                }

                if (v.Json.ValueKind != JsonValueKind.Object) continue;
                // 宠物吃元气丹成长率, 注意超级元气丹配置的pet是0
                if (v.Json.TryGetProperty("pet", out var petElement))
                {
                    var pet = petElement.GetUInt32();
                    if (pet > 0 && v.Json.TryGetProperty("rate", out var rateElement))
                    {
                        var rate = rateElement.GetSingle();
                        if (rate > 0) ItemPetRates.Add(pet, rate);
                    }
                }
            }

            // 神兵、骷髅王、赢鱼、雨师、永、符咒女娲 加上超级元气丹
            if (Items.TryGetValue(10584, out var cjyqd) && cjyqd != null)
            {
                if (cjyqd.Json.ValueKind == JsonValueKind.Object)
                {
                    if (cjyqd.Json.TryGetProperty("rate", out var rateElement))
                    {
                        var rate = rateElement.GetSingle();
                        if (rate > 0)
                        {
                            foreach (var petId in Pets.Keys)
                            {
                                // 没有专属元气丹的宠物使用超级元气丹
                                if (!ItemPetRates.ContainsKey(petId))
                                {
                                    ItemPetRates.Add(petId, rate);
                                }
                            }
                        }
                    }
                }
            }

            // 坐骑根据race进行分类
            MountGroups.Clear();
            foreach (var v in Mounts.Values)
            {
                if (!MountGroups.TryGetValue(v.Race, out var list))
                {
                    list = new List<MountConfig>();
                    MountGroups[v.Race] = list;
                }

                list.Add(v);
            }

            // 坐骑技能分类
            MountGroupedSkills.Clear();
            foreach (var mskill in MountSkills.Values)
            {
                // 根据type来进行攻血法敏分类
                foreach (var mst in mskill.Type)
                {
                    MountGroupedSkills.TryGetValue(mst, out var alls);
                    if (alls == null)
                    {
                        alls = new Dictionary<int, List<MountSkillConfig>>(3);
                        MountGroupedSkills[mst] = alls;
                    }

                    // 根据Grids来进行1,2,3技能分类
                    foreach (var msg in mskill.Grids)
                    {
                        alls.TryGetValue(msg, out var list);
                        if (list == null)
                        {
                            list = new List<MountSkillConfig>(10);
                            alls[msg] = list;
                        }

                        list.Add(mskill);
                    }
                }
            }
            MountGroupedPSkills.Clear();
            foreach (var mskill in MountSkills.Values)
            {
                // 根据Grids来进行1,2,3技能分类
                foreach (var msg in mskill.Grids)
                {
                    MountGroupedPSkills.TryGetValue(msg, out var list);
                    if (list == null)
                    {
                        list = new List<MountSkillConfig>(100);
                        MountGroupedPSkills[msg] = list;
                    }
                    list.Add(mskill);
                }
            }

            Equips.Clear();
            // 所有装备
            foreach (var (k, v) in Equips1)
            {
                Equips[k] = v;
            }

            foreach (var (k, v) in Equips2)
            {
                Equips[k] = v;
            }

            foreach (var (k, v) in Equips3)
            {
                Equips[k] = v;
            }

            foreach (var (k, v) in Equips4)
            {
                Equips[k] = v;
            }

            // 伙伴
            foreach (var v in PartnetPowers.Values)
            {
                Partners.TryGetValue(v.PartnerId, out var cfg);
                if (cfg == null) continue;
                cfg.LevelAttrs ??= new Dictionary<uint, Dictionary<AttrType, float>>();
                cfg.LevelAttrs.TryGetValue(v.Level, out var attrs);
                if (attrs == null)
                {
                    attrs = new Dictionary<AttrType, float>();
                    cfg.LevelAttrs[v.Level] = attrs;
                }

                foreach (var property in v.Attrs.EnumerateObject())
                {
                    GameDefine.EquipAttrTypeMap.TryGetValue(property.Name, out var attrType);
                    if (attrType == AttrType.Unkown) continue;
                    var value = property.Value.GetSingle();
                    if (!GameDefine.EquipNumericalAttrType.ContainsKey(attrType))
                    {
                        value /= 10;
                    }

                    attrs[attrType] = value;
                }
            }
        }
    }

    public class MedicineEffect
    {
        public int AddHp;
        public int AddMp;
        public int MulHp;
        public int MulMp;
        public bool DYinShen;
        public bool YinShen;
    }
}