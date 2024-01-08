using System;
using System.Collections.Generic;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Data
{
    public static class GameDefine
    {
        // 心跳间隔时间(s)
        public const uint HeartBeatInterval = 5;

        // 根据上次心跳时间判定为离线的间隔时间
#if DEBUG
        public const uint HeartBeatOfflineTime = HeartBeatInterval * 100;
#else
        public const uint HeartBeatOfflineTime = HeartBeatInterval * 3;
#endif

        // 后台保持会话时间s, 后台保持3分钟
        public const uint BackgroundTime = 180;


        // 保存数据入库的间隔时间(s)
        public const uint SaveDataInterval = 1800;

        // 每日可击杀地煞星的次数
        public const int DiShaXingNumDaily = 999;

        // 每日可击杀金蟾送宝的次数
        public const int JinChanSongBaoNumDaily = 20;

        // 每日可击杀金翅大鹏的次数
        public const int EagleNumDaily = 99;

        // 每日可击杀灵猴的次数
        public const int LingHouNumDaily = 5;

        public const uint LingHouMinMoney = 2000;

        public const uint LingHouRetMoney = 10000;

        // 玩家PK的最小等级80
        public const uint PkLevelLimit = 80;

        // PK赢了之后监禁时间(s)
#if DEBUG
        public const uint PrisionTime = 10;
#else
        public const uint PrisionTime = 3600;
#endif

        // 最大宠物个数
        public const int MaxPetNum = 30;

        // 伙伴跟随角色升级的最大等级, 超过35级以后就不跟随角色升级
        public const uint LimitPartnerLevel = 35;

        // 背包里能存放的类型最大数量, 含装备，配饰
        public const uint LimitBagItemKindNum = 500;
        // 仓库里能存放的类型最大数量, 含装备，配饰
        public const uint LimitRepoItemKindNum = 300;

        // 装备重铸消耗银币
        public const int EquipRecastCostSilver = 2000;

        // 配饰重铸消耗银币
        public const int OrnamentRecastCostSilver = 800000;

        // 队伍列表PageSize
        public const int TeamListPageSize = 10;

        // 帮派中申请列表PageSize
        public const int TeamApplyJoinListPageSize = 8;

        // 帮派列表PageSize
        public const int SectListPageSize = 10;

        // 帮派成员PageSize
        public const int SectMemberListPageSize = 10;

        // 帮派中申请列表PageSize
        public const int SectApplyJoinListPageSize = 8;

        // 好友数量上限
        public const int FriendMaxNum = 50;

        // 甄不亏
        public const uint ZhenBuKuiNpcCfgId = 60002;

        // 灵猴
        public const uint LingHouNpcCfgId = 40100;

        // 骷髅王
        public const uint KuLouWangNpcCfgId = 70100;

        // 金蟾送宝
        public const uint JinChanSongBaoNpcCfgId = 70101;

        // 金翅大鹏
        public const uint EagleNpcCfgId = 70102;

        // FIXME: 多倍充值 注意这会控制仙玉充值和积分充值两种充值
        public const int MultiChargeFactor = 1;

        // FIXME: 每1元换多少仙玉 默认值 可后台修改
        public const int JadePerYuan = 10000;

        // FIXME: 每1元换多少积分 默认值 可后台修改
        public const int BindJadePerYuan = 100;

        //积分充值，给双倍
        public const int BindJadeMulti = 4;

        //客户活动仙玉，给多倍
        public const int CustomerJadeMulti = 1;
        //客户活动积分，给多倍
        public const int CustomerBindJadeMulti = 1;

        //充值类型(仙玉还是积分)
        public const uint ChargeType = 4;

        // FIXME: 玩家充值, 推广人获得百分之多少提成--仙玉
        public const float RechargeRewardJadePercent = 1.0f;

        // FIXME: 玩家充值, 推广人获得百分之多少提成--积分
        public const float RechargeRewardBindJadePercent = 1.0f;

        // FIXME: 弹幕发送时间间隔 秒
        public const uint DanMuSendDelayLimit = 10;

        // 转盘积分消耗
        public const uint LuckyDrawTurnCost = 1;

        // 限时充值排行榜活动结束后图标好久消失?
        public const uint LimitChargeIconDelay = 7 * 24 * 60 * 60;

        // 限时等级排行榜活动结束后图标好久消失?
        public const uint LimitLevelIconDelay = 7 * 24 * 60 * 60;

        // 天策符--碎片物品ID
        public const uint TianceFuSuiPianItemId = 100320;
        // 天策符--待鉴定物品ID
        public static readonly List<uint> TianceFuDaiJianDingItemId = new() { 100321, 100322, 100323 };
        // 天策符--类型对应待鉴定物品ID
        public static readonly Dictionary<TianceFuType, uint> TianceFuType2DaiJianDingItemId = new()
        {
            [TianceFuType.QianQun] = 100321,
            [TianceFuType.ZaiWu] = 100322,
            [TianceFuType.YueShou] = 100323,
        };
        // 天演策--最大等级
        public const uint TianYanCeMaxLevel = 150;
        // 天策符--最大等级
        public const int TianCeFuMaxLevel = 10;

        public static readonly Dictionary<MoneyType, string> MoneyName = new()
        {
            [MoneyType.Silver] = "银币",
            [MoneyType.Jade] = "仙玉",
            // [MoneyType.BindJade] = "绑定仙玉",
            [MoneyType.BindJade] = "积分",
            [MoneyType.Contrib] = "贡币",
            [MoneyType.SldhGongJi] = "水路功绩",
            [MoneyType.GuoShi] = "郭氏积分",
            [MoneyType.WzzzJiFen] = "王者之战积分",
        };

        /// <summary>
        /// 加点属性集合
        /// </summary>
        public static Dictionary<AttrType, bool> ApAttrs = new()
        {
            [AttrType.GenGu] = true,
            [AttrType.LingXing] = true,
            [AttrType.LiLiang] = true,
            [AttrType.MinJie] = true,
        };

        /// <summary>
        /// 基础一级属性
        /// </summary>
        public static Dictionary<Race, Dictionary<AttrType, float>> Bases =
            new()
            {
                [Race.Ren] = new Dictionary<AttrType, float>
                {
                    [AttrType.Hp] = 360,
                    [AttrType.Mp] = 300,
                    [AttrType.Atk] = 70,
                    [AttrType.Spd] = 8
                },
                [Race.Xian] = new Dictionary<AttrType, float>
                {
                    [AttrType.Hp] = 270,
                    [AttrType.Mp] = 350,
                    [AttrType.Atk] = 80,
                    [AttrType.Spd] = 9
                },
                [Race.Mo] = new Dictionary<AttrType, float>
                {
                    [AttrType.Hp] = 330,
                    [AttrType.Mp] = 210,
                    [AttrType.Atk] = 80,
                    [AttrType.Spd] = 10
                },
                [Race.Gui] = new Dictionary<AttrType, float>
                {
                    [AttrType.Hp] = 300,
                    [AttrType.Mp] = 390,
                    [AttrType.Atk] = 60,
                    [AttrType.Spd] = 10
                },
                // 龙族
                [Race.Long] = new Dictionary<AttrType, float>
                {
                    [AttrType.Hp] = 300,
                    [AttrType.Mp] = 240,
                    [AttrType.Atk] = 80,
                    [AttrType.Spd] = 10
                }
            };

        /// <summary>
        /// 不同种族的属性成长点
        /// </summary>
        public static Dictionary<Race, Dictionary<AttrType, float>> Grows =
            new()
            {
                [Race.Ren] = new Dictionary<AttrType, float>
                {
                    [AttrType.GenGu] = 1.2f,
                    [AttrType.LingXing] = 1,
                    [AttrType.LiLiang] = 1,
                    [AttrType.MinJie] = 0.8f
                },
                [Race.Xian] = new Dictionary<AttrType, float>
                {
                    [AttrType.GenGu] = 1f,
                    [AttrType.LingXing] = 1.3f,
                    [AttrType.LiLiang] = 0.7f,
                    [AttrType.MinJie] = 1
                },
                [Race.Mo] = new Dictionary<AttrType, float>
                {
                    [AttrType.GenGu] = 1.1f,
                    [AttrType.LingXing] = 0.6f,
                    [AttrType.LiLiang] = 1.3f,
                    [AttrType.MinJie] = 1
                },
                [Race.Gui] = new Dictionary<AttrType, float>
                {
                    [AttrType.GenGu] = 1.2f,
                    [AttrType.LingXing] = 1,
                    [AttrType.LiLiang] = 0.95f,
                    [AttrType.MinJie] = 0.85f
                },
                // 龙族
                [Race.Long] = new Dictionary<AttrType, float>
                {
                    [AttrType.GenGu] = 1f,
                    [AttrType.LingXing] = 0.7f,
                    [AttrType.LiLiang] = 1.3f,
                    [AttrType.MinJie] = 1f
                }
            };

        public static Dictionary<AttrType, byte> ReliveIgnoreAttrs = new()
        {
            [AttrType.Hp] = 0,
            [AttrType.HpMax] = 0,
            [AttrType.Mp] = 0,
            [AttrType.MpMax] = 0,
            [AttrType.Atk] = 0,
            [AttrType.Spd] = 0
        };

        /// <summary>
        /// 转生1修正
        /// </summary>
        public static Dictionary<Race, Dictionary<Sex, Dictionary<AttrType, float>>> Relive1FixAttr =
            new()
            {
                [Race.Ren] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new()
                    {
                        [AttrType.DhunLuan] = 10,
                        [AttrType.DfengYin] = 10,
                        [AttrType.DhunShui] = 10
                    },
                    [Sex.Female] = new()
                    {
                        [AttrType.Ddu] = 10,
                        [AttrType.DfengYin] = 10,
                        [AttrType.DhunShui] = 10
                    }
                },
                [Race.Xian] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new()
                    {
                        [AttrType.Dlei] = 10,
                        [AttrType.Dshui] = 10,
                        [AttrType.Dfeng] = 10
                    },
                    [Sex.Female] = new()
                    {
                        [AttrType.Dlei] = 10,
                        [AttrType.Dshui] = 10,
                        [AttrType.Dhuo] = 10
                    }
                },
                [Race.Mo] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new()
                    {
                        [AttrType.Hp] = 8.2f,
                        [AttrType.Mp] = 8.2f,
                        [AttrType.Spd] = 6.15f
                    },
                    [Sex.Female] = new()
                    {
                        [AttrType.Hp] = 8.2f,
                        [AttrType.Mp] = 8.2f,
                        [AttrType.DzhenShe] = 9.2f
                    }
                },
                [Race.Gui] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new()
                    {
                        [AttrType.DguiHuo] = 10,
                        [AttrType.DyiWang] = 10,
                        [AttrType.DsanShi] = 10
                    },
                    [Sex.Female] = new()
                    {
                        [AttrType.DguiHuo] = 10,
                        [AttrType.DyiWang] = 10,
                        [AttrType.DwuLi] = 15.3f
                    }
                },
                // 龙族
                [Race.Long] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new()
                    {
                        [AttrType.Hp] = 8.2f,
                        [AttrType.PshanBi] = 5.2f,
                        [AttrType.DwuLi] = 2.7f
                    },
                    [Sex.Female] = new()
                    {
                        [AttrType.Hp] = 8.2f,
                        [AttrType.PshanBi] = 5.2f,
                        [AttrType.DwuLi] = 2.7f
                    }
                }
            };


        /// <summary>
        /// 转生2修正
        /// </summary>
        public static Dictionary<Race, Dictionary<Sex, Dictionary<AttrType, float>>> Relive2FixAttr =
            new Dictionary<Race, Dictionary<Sex, Dictionary<AttrType, float>>>
            {
                [Race.Ren] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new Dictionary<AttrType, float>
                    {
                        [AttrType.DhunLuan] = 15,
                        [AttrType.DfengYin] = 15,
                        [AttrType.DhunShui] = 15
                    },
                    [Sex.Female] = new Dictionary<AttrType, float>
                    {
                        [AttrType.Ddu] = 15,
                        [AttrType.DfengYin] = 15,
                        [AttrType.DhunShui] = 15
                    }
                },
                [Race.Xian] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new Dictionary<AttrType, float>
                    {
                        [AttrType.Dlei] = 15,
                        [AttrType.Dshui] = 15,
                        [AttrType.Dfeng] = 15
                    },
                    [Sex.Female] = new Dictionary<AttrType, float>
                    {
                        [AttrType.Dlei] = 15,
                        [AttrType.Dshui] = 15,
                        [AttrType.Dhuo] = 15
                    }
                },
                [Race.Mo] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new Dictionary<AttrType, float>
                    {
                        [AttrType.Hp] = 12.3f,
                        [AttrType.Mp] = 12.3f,
                        [AttrType.Spd] = 9.23f
                    },
                    [Sex.Female] = new Dictionary<AttrType, float>
                    {
                        [AttrType.Hp] = 12.3f,
                        [AttrType.Mp] = 12.3f,
                        [AttrType.DzhenShe] = 13.8f
                    }
                },
                [Race.Gui] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new Dictionary<AttrType, float>
                    {
                        [AttrType.DguiHuo] = 15,
                        [AttrType.DyiWang] = 15,
                        [AttrType.DsanShi] = 15
                    },
                    [Sex.Female] = new Dictionary<AttrType, float>
                    {
                        [AttrType.DguiHuo] = 15,
                        [AttrType.DyiWang] = 15,
                        [AttrType.DwuLi] = 23
                    }
                },
                // 龙族
                [Race.Long] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new()
                    {
                        [AttrType.Hp] = 12.3f,
                        [AttrType.PshanBi] = 7.8f,
                        [AttrType.DwuLi] = 4.1f
                    },
                    [Sex.Female] = new()
                    {
                        [AttrType.Hp] = 12.3f,
                        [AttrType.PshanBi] = 7.8f,
                        [AttrType.DwuLi] = 4.1f
                    }
                }
            };


        /// <summary>
        /// 转生3修正
        /// </summary>
        public static Dictionary<Race, Dictionary<Sex, Dictionary<AttrType, float>>> Relive3FixAttr =
            new Dictionary<Race, Dictionary<Sex, Dictionary<AttrType, float>>>
            {
                [Race.Ren] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new Dictionary<AttrType, float>
                    {
                        [AttrType.DhunLuan] = 20,
                        [AttrType.DfengYin] = 20,
                        [AttrType.DhunShui] = 20
                    },
                    [Sex.Female] = new Dictionary<AttrType, float>
                    {
                        [AttrType.Ddu] = 20,
                        [AttrType.DfengYin] = 20,
                        [AttrType.DhunShui] = 20
                    }
                },
                [Race.Xian] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new Dictionary<AttrType, float>
                    {
                        [AttrType.Dlei] = 20,
                        [AttrType.Dshui] = 20,
                        [AttrType.Dfeng] = 20
                    },
                    [Sex.Female] = new Dictionary<AttrType, float>
                    {
                        [AttrType.Dlei] = 20,
                        [AttrType.Dshui] = 20,
                        [AttrType.Dhuo] = 20
                    }
                },
                [Race.Mo] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new Dictionary<AttrType, float>
                    {
                        [AttrType.Hp] = 16.4f,
                        [AttrType.Mp] = 16.4f,
                        [AttrType.Spd] = 12.3f
                    },
                    [Sex.Female] = new Dictionary<AttrType, float>
                    {
                        [AttrType.Hp] = 16.4f,
                        [AttrType.Mp] = 16.4f,
                        [AttrType.DzhenShe] = 18.5f
                    }
                },
                [Race.Gui] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new Dictionary<AttrType, float>
                    {
                        [AttrType.DguiHuo] = 20,
                        [AttrType.DyiWang] = 20,
                        [AttrType.DsanShi] = 20
                    },
                    [Sex.Female] = new Dictionary<AttrType, float>
                    {
                        [AttrType.DguiHuo] = 20,
                        [AttrType.DyiWang] = 20,
                        [AttrType.DwuLi] = 30.6f
                    }
                },
                // 龙族
                [Race.Long] = new Dictionary<Sex, Dictionary<AttrType, float>>
                {
                    [Sex.Male] = new()
                    {
                        [AttrType.Hp] = 16.4f,
                        [AttrType.PshanBi] = 10.4f,
                        [AttrType.DwuLi] = 5.4f
                    },
                    [Sex.Female] = new()
                    {
                        [AttrType.Hp] = 16.4f,
                        [AttrType.PshanBi] = 10.4f,
                        [AttrType.DwuLi] = 5.4f
                    }
                }
            };

        /// <summary>
        /// 角色默认技能
        /// </summary>
        public static Dictionary<Race, Dictionary<Sex, List<SkillId>>> DefSkills =
            new Dictionary<Race, Dictionary<Sex, List<SkillId>>>
            {
                [Race.Ren] = new Dictionary<Sex, List<SkillId>>
                {
                    [Sex.Male] = new List<SkillId>
                    {
                        SkillId.JieDaoShaRen,
                        SkillId.ShiXinKuangLuan,

                        SkillId.MiHunZui,
                        SkillId.BaiRiMian,

                        SkillId.ZuoBiShangGuan,
                        SkillId.SiMianChuGe
                    },
                    [Sex.Female] = new List<SkillId>
                    {
                        SkillId.HeDingHongFen,
                        SkillId.WanDuGongXin,

                        SkillId.MiHunZui,
                        SkillId.BaiRiMian,

                        SkillId.ZuoBiShangGuan,
                        SkillId.SiMianChuGe
                    }
                },
                [Race.Xian] = new Dictionary<Sex, List<SkillId>>
                {
                    [Sex.Male] = new List<SkillId>
                    {
                        SkillId.FengLeiYongDong,
                        SkillId.XiuLiQianKun,

                        SkillId.DianShanLeiMing,
                        SkillId.TianZhuDiMie,

                        SkillId.JiaoLongChuHai,
                        SkillId.JiuLongBingFeng
                    },
                    [Sex.Female] = new List<SkillId>
                    {
                        SkillId.LieHuoJiaoYang,
                        SkillId.JiuYinChunHuo,

                        SkillId.DianShanLeiMing,
                        SkillId.TianZhuDiMie,

                        SkillId.JiaoLongChuHai,
                        SkillId.JiuLongBingFeng
                    }
                },
                [Race.Mo] = new Dictionary<Sex, List<SkillId>>
                {
                    [Sex.Male] = new List<SkillId>
                    {
                        SkillId.TianWaiFeiMo,
                        SkillId.QianKunJieSu,

                        SkillId.ShouWangShenLi,
                        SkillId.MoShenFuShen,

                        SkillId.XiaoHunShiGu,
                        SkillId.YanLuoZhuiMing
                    },
                    [Sex.Female] = new List<SkillId>
                    {
                        SkillId.MoShenHuTi,
                        SkillId.HanQingMoMo,

                        SkillId.ShouWangShenLi,
                        SkillId.MoShenFuShen,

                        SkillId.XiaoHunShiGu,
                        SkillId.YanLuoZhuiMing
                    }
                },
                [Race.Gui] = new Dictionary<Sex, List<SkillId>>
                {
                    [Sex.Male] = new List<SkillId>
                    {
                        SkillId.XueShaZhiGu,
                        SkillId.XiXingDaFa,

                        SkillId.LuoRiRongJin,
                        SkillId.XueHaiShenChou,

                        SkillId.ShiXinFeng,
                        SkillId.MengPoTang
                    },
                    [Sex.Female] = new List<SkillId>
                    {
                        SkillId.QinSiBingWu,
                        SkillId.QianNvYouHun,

                        SkillId.LuoRiRongJin,
                        SkillId.XueHaiShenChou,

                        SkillId.ShiXinFeng,
                        SkillId.MengPoTang
                    }
                },
                // 龙族
                [Race.Long] = new Dictionary<Sex, List<SkillId>>
                {
                    [Sex.Male] = new List<SkillId>
                    {
                        SkillId.LingXuYuFeng,
                        SkillId.FeiJuJiuTian,

                        SkillId.PeiRanMoYu,
                        SkillId.ZeBeiWanWu,

                        SkillId.FengLeiWanYun,
                        SkillId.ZhenTianDongDi,

                        SkillId.NiLin,
                    },
                    [Sex.Female] = new List<SkillId>
                    {
                        SkillId.BaiLangTaoTian,
                        SkillId.CangHaiHengLiu,

                        SkillId.PeiRanMoYu,
                        SkillId.ZeBeiWanWu,

                        SkillId.FengLeiWanYun,
                        SkillId.ZhenTianDongDi,

                        SkillId.NiLin,
                    }
                }
            };

        /// <summary>
        /// 孩子特技和技能ID对应关系
        /// </summary>
        public static Dictionary<string, SkillId> childSkillString2SkillIds = new()
        {
            // 精气爆满
            ["jing_qi_bao_man"] = SkillId.JingQiBaoMan,
            // 返生香
            ["fan_sheng_xiang"] = SkillId.FanShengXiang,
            // 失心狂乱
            ["shi_xin_kuang_luan"] = SkillId.ShiXinKuangLuan,
            // 四面楚歌
            ["si_mian_chu_ge"] = SkillId.SiMianChuGe,
            // 百日眠
            ["bai_ri_mian"] = SkillId.BaiRiMian,
            // 万毒攻心
            ["wang_du_gong_xin"] = SkillId.WanDuGongXin,
            // 九龙冰封
            ["jiu_long_bing_feng"] = SkillId.JiuLongBingFeng,
            // 天诛地灭
            ["tian_zhu_di_mie"] = SkillId.TianZhuDiMie,
            // 袖里乾坤
            ["xiu_li_qian_kun"] = SkillId.XiuLiQianKun,
            // 九阴纯火
            ["jiu_yin_chun_huo"] = SkillId.JiuYinChunHuo,
            // 乾坤借速
            ["qian_kun_jie_su"] = SkillId.QianKunJieSu,
            // 魔神附身
            ["mo_shen_fu_shen"] = SkillId.MoShenFuShen,
            // 阎罗追命
            ["yan_luo_zhui_ming"] = SkillId.YanLuoZhuiMing,
            // 含情脉脉
            ["han_qing_mo_mo"] = SkillId.HanQingMoMo,
            // 吸星大法
            ["xi_xing_da_fa"] = SkillId.XiXingDaFa,
            // 血海深仇
            ["xue_hai_shen_chou"] = SkillId.XueHaiShenChou,
            // 孟婆汤
            ["meng_po_tang"] = SkillId.MengPoTang,
            // 倩女幽魂
            ["qian_nv_you_hun"] = SkillId.QianNvYouHun,
            // 飞举九天
            ["fei_ju_jiu_tian"] = SkillId.FeiJuJiuTian,
            // 震天动地
            ["zhen_tian_dong_di"] = SkillId.ZhenTianDongDi,
            // 沧海横流
            ["cang_hai_heng_liu"] = SkillId.CangHaiHengLiu,
            // 泽被万物
            ["ze_bei_wan_wu"] = SkillId.ZeBeiWanWu,
        };
        /// <summary>
        /// 孩子技能ID和名称对应关系
        /// </summary>
        public static Dictionary<SkillId, string> ChildSkillId2Names = new()
        {
            [SkillId.JingQiBaoMan] = "激流暗涌",
            [SkillId.FanShengXiang] = "三生三世",
            [SkillId.ShiXinKuangLuan] = "孙子兵法",
            [SkillId.SiMianChuGe] = "千里冰封",
            [SkillId.BaiRiMian] = "摇篮曲",
            [SkillId.WanDuGongXin] = "肝肠寸断",
            [SkillId.JiuLongBingFeng] = "排江倒海",
            [SkillId.TianZhuDiMie] = "雷霆万钧",
            [SkillId.XiuLiQianKun] = "风卷残云",
            [SkillId.JiuYinChunHuo] = "江枫渔火",
            [SkillId.QianKunJieSu] = "清风徐来",
            [SkillId.MoShenFuShen] = "嗜血狂攻",
            [SkillId.YanLuoZhuiMing] = "摄魂诀",
            [SkillId.HanQingMoMo] = "莲台心法",
            [SkillId.XiXingDaFa] = "尾闾尸虫",
            [SkillId.XueHaiShenChou] = "幽冥冷火",
            [SkillId.MengPoTang] = "三生无石",
            [SkillId.QianNvYouHun] = "倩女幽魂",
            [SkillId.FeiJuJiuTian] = "飞举九天",
            [SkillId.ZhenTianDongDi] = "震天动地",
            [SkillId.CangHaiHengLiu] = "沧海横流",
            [SkillId.ZeBeiWanWu] = "泽被万物",
        };
        /// <summary>
        /// 共用的孩子动作名称
        /// </summary>
        public static List<string> ChildAniNameList = new()
        {
            "angry",
            "casino",
            "fish",
            "hugrydesk",
            "hugryguqin",
            "hugry",
            "kite",
            "rodfish",
            "rundish",
            "runkite",
            "shout",
            "wanderdesk",
            "wanderguqin",
            "yawnguqin"
        };

        /// <summary>
        /// 技能类型
        /// </summary>
        public static Dictionary<SkillType, AttrType> SkillTypeStrengthen =
            new Dictionary<SkillType, AttrType>
            {
                [SkillType.Chaos] = AttrType.HdhunLuan,
                [SkillType.Toxin] = AttrType.Hddu,
                [SkillType.Sleep] = AttrType.HdhunShui,
                [SkillType.Seal] = AttrType.HdfengYin,
                [SkillType.Feng] = AttrType.Hdfeng,
                [SkillType.Huo] = AttrType.Hdhuo,
                [SkillType.Lei] = AttrType.Hdlei,
                [SkillType.Shui] = AttrType.Hdshui,

                [SkillType.GhostFire] = AttrType.HdguiHuo,
                [SkillType.Forget] = AttrType.HdyiWang,
                [SkillType.ThreeCorpse] = AttrType.HdsanShi,
                [SkillType.Frighten] = AttrType.HdzhenShe,
                [SkillType.Physics] = AttrType.HdwuLi
            };

        public static Dictionary<SkillType, AttrType> SkillTypeKangXing =
            new Dictionary<SkillType, AttrType>
            {
                [SkillType.Chaos] = AttrType.DhunLuan,
                [SkillType.Seal] = AttrType.DfengYin,
                [SkillType.Sleep] = AttrType.DhunShui,
                [SkillType.Toxin] = AttrType.Ddu,
                [SkillType.Feng] = AttrType.Dfeng,
                [SkillType.Huo] = AttrType.Dhuo,
                [SkillType.Shui] = AttrType.Dshui,
                [SkillType.Lei] = AttrType.Dlei,
                [SkillType.GhostFire] = AttrType.DguiHuo,
                [SkillType.Forget] = AttrType.DyiWang,
                [SkillType.ThreeCorpse] = AttrType.DsanShi,
                [SkillType.Frighten] = AttrType.DzhenShe,
                [SkillType.Physics] = AttrType.DwuLi
            };

        /// <summary>
        /// 五行属性及其克制属性
        /// </summary>
        public static Dictionary<AttrType, AttrType> WuXingStrengThen =
            new Dictionary<AttrType, AttrType>
            {
                [AttrType.Jin] = AttrType.Mu,
                [AttrType.Mu] = AttrType.Tu,
                [AttrType.Tu] = AttrType.Shui,
                [AttrType.Shui] = AttrType.Huo,
                [AttrType.Huo] = AttrType.Jin
            };

        /// <summary>
        /// 五行强力克 -> 五行属性的映射关系
        /// </summary>
        public static Dictionary<AttrType, AttrType> WuXingKeStrengThen =
            new Dictionary<AttrType, AttrType>
            {
                [AttrType.Qjin] = AttrType.Jin,
                [AttrType.Qmu] = AttrType.Mu,
                [AttrType.Qshui] = AttrType.Shui,
                [AttrType.Qhuo] = AttrType.Huo,
                [AttrType.Qtu] = AttrType.Tu
            };

        // 技能升级消耗的贡币值
        public static List<ValueTuple<uint, uint>> SkillUpgradeConsume = new List<ValueTuple<uint, uint>>
        {
            (1422, 6434),
            (1725, 6740),
            (2031, 7050),
            (2338, 7365),
            (2648, 7684),
            (2961, 8007),
            (3275, 8334),
            (3592, 8665),
            (3911, 9000),
            (4232, 9340),
            (4555, 9684),
            (4881, 10032),
            (5208, 10384),
            (5538, 10740),
            (5871, 11100),
            (6205, 11465),
            (6542, 11834),
            (6881, 12207),
            (7222, 12584),
            (7565, 12965),
            (7911, 13350),
            (8258, 13740),
            (8608, 14134),
            (8961, 14532),
            (9315, 14934),
            (9672, 15340),
            (10031, 15750),
            (10392, 16165),
            (10755, 16584),
            (11121, 17007),
            (11488, 17434),
            (11858, 17865),
            (12231, 18300),
            (12605, 18740),
            (12982, 19184),
            (13361, 19632),
            (13742, 20084),
            (14125, 20540),
            (14511, 21000),
            (14898, 21465),
            (15288, 21934),
            (15681, 22407),
            (16075, 22884),
            (16472, 23365),
            (16871, 23850),
            (17272, 24340),
            (17675, 24834),
            (18081, 25332),
            (18488, 25834),
            (18898, 26340),
            (32358, 51314),
            (33353, 52918),
            (34365, 54553),
            (35393, 56218),
            (36438, 57914),
            (37499, 59640),
            (38576, 61398),
            (39670, 63187),
            (40780, 65006),
            (41908, 66857),
            (43052, 68740),
            (44212, 70653),
            (45390, 72599),
            (46584, 74576),
            (47795, 76584),
            (49024, 78625),
            (50269, 80697),
            (51531, 82802),
            (52811, 84938),
            (54107, 87107),
            (55421, 89308),
            (56753, 91542),
            (58101, 93808),
            (59467, 96106),
            (60850, 98438),
            (62251, 100802),
            (63670, 103199),
            (65106, 105628),
            (66559, 108091),
            (68030, 110587),
            (69519, 113116),
            (71026, 115679),
            (72550, 118274),
            (74092, 120904),
            (75652, 123566),
            (77230, 126263),
            (78826, 128993),
            (80440, 131756),
            (82072, 134554),
            (83723, 137385),
            (85391, 140251),
            (87077, 143150),
            (88782, 146084),
            (90505, 149052),
            (92246, 152054),
            (94005, 155090),
            (95783, 158161),
            (97579, 161266),
            (99394, 164406),
            (101227, 167581),
            (317122, 572122),
            (324114, 584970),
            (331195, 597984),
            (338366, 611167),
            (345626, 624517),
            (352977, 638037),
            (360418, 651726),
            (367949, 665585),
            (375571, 679615),
            (383285, 693815),
            (391090, 708188),
            (398988, 722732),
            (406977, 737449),
            (415058, 752340),
            (423233, 767404),
            (431500, 782643),
            (439861, 798057),
            (448315, 813646),
            (456863, 829410),
            (465505, 845352),
            (474241, 861470),
            (483072, 877766),
            (491998, 894240),
            (501019, 910892),
            (510136, 927723),
            (519348, 944734),
            (528657, 961925),
            (538062, 979296),
            (547563, 996848),
            (557161, 1014582),
            (566856, 1032498),
            (576648, 1050596),
            (586538, 1068877),
            (596526, 1087342),
            (606612, 1105990),
            (616796, 1124823),
            (627079, 1143841),
            (637461, 1163044),
            (647942, 1182433),
            (658522, 1202009),
            (669202, 1221771),
            (679981, 1241720),
            (690861, 1261857),
            (701841, 1282183),
            (712922, 1302696),
            (724103, 1323399),
            (735386, 1344292),
            (746770, 1365374),
            (758255, 1386647),
            (769843, 1408111),
            (781532, 1429766),
            (793324, 1451613),
            (805218, 1473652),
            (817215, 1495884),
            (829315, 1518308),
            (841518, 1540927),
            (853825, 1563739),
            (866235, 1586746),
            (878749, 1609948),
            (891367, 1633345),
            (904090, 1656937),
            (916918, 1680726),
            (929850, 1704711),
            (942887, 1728893),
            (956029, 1753273),
            (969277, 1777851),
            (982631, 1802626),
            (996091, 1827601),
            (1009657, 1852774),
            (1023329, 1878147),
            (1037108, 1903720),
            (1050994, 1929493),
            (1064986, 1955467),
            (1079086, 1981642),
            (1093294, 2008019),
            (1107609, 2034598),
            (1122032, 2061378),
            (1136563, 2088362),
            (1151203, 2115549),
            (1165951, 2142939),
            (1180808, 2170533),
            (1195774, 2198332),
            (1210849, 2226335),
            (1226034, 2254544),
            (1241328, 2282958),
            (1256732, 2311577),
            (1272246, 2340404),
            (1287870, 2369436),
            (1303604, 2398676),
            (1319450, 2428124),
            (1335406, 2457779),
            (1351473, 2487642),
            (1367651, 2517714),
            (1383941, 2547995),
            (1400343, 2578486),
            (1416856, 2609186),
            (1433482, 2640096),
            (1450219, 2671217),
            (1467069, 2702548),
            (1484032, 2734091),
            (1501108, 2765846),
            (1518297, 2797812),
            (1535599, 2829991),
            (1553014, 2862382),
            (1570543, 2894986),
            (1588186, 2927804),
            (1605943, 2960836),
            (1623814, 2994082),
            (1641800, 3027542),
            (1659900, 3061218),
            (1678115, 3095108),
            (1696445, 3129214),
            (1714890, 3163537),
            (1733450, 3198075),
            (1752126, 3232830),
            (1770918, 3267802),
            (1789826, 3302992),
            (1808850, 3338399),
            (1827990, 3374024),
            (1847247, 3409868),
            (1866620, 3445931),
            (1886110, 3482212),
            (1905718, 3518713),
            (1925442, 3555434),
            (1945284, 3592376),
            (1965244, 3629537),
            (1985321, 3666920),
            (2005516, 3704523),
            (2025830, 3742349),
            (2046261, 3780396),
            (2066812, 3818665),
            (2087481, 3857157),
            (2108269, 3895872),
            (2129175, 3934809),
            (2150202, 3973971),
            (2171347, 4013356),
            (2192612, 4052966),
            (2213997, 4092800),
            (2235502, 4132859),
            (2257127, 4173144),
            (2278872, 4213653),
            (2300738, 4254389),
            (2322724, 4295351),
            (2344831, 4336539),
            (2367060, 4377955),
            (2389409, 4419597),
            (2411879, 4461467),
            (2434472, 4503565),
            (2457185, 4545891),
            (2480021, 4588445)
        };

        // 转生等级->最大修炼等级
        // public static Dictionary<byte, uint> MaxXiuLianLevel = new Dictionary<byte, uint>
        // {
        //     [0] = 25,
        //     [1] = 50,
        //     [2] = 75,
        //     [3] = 100
        // };

        // public static Dictionary<AttrType, float> InitAttrsDic()
        // {
        //     var dic = Enum.GetValues(typeof(AttrType)).Cast<AttrType>()
        //         .ToDictionary<AttrType, AttrType, float>(t => t, t => 0);
        //     return dic;
        // }

        // public static Dictionary<AttrType, float> InitSimpleAttrsDic()
        // {
        //     var dic = new Dictionary<AttrType, float>
        //     {
        //         [AttrType.GenGu] = 0,
        //         [AttrType.LingXing] = 0,
        //         [AttrType.LiLiang] = 0,
        //         [AttrType.MinJie] = 0
        //     };
        //     return dic;
        // }

        // public static Dictionary<byte, uint> InitAttrsDicWithByteKey()
        // {
        //     var dic = Enum.GetValues(typeof(AttrType)).Cast<AttrType>()
        //         .ToDictionary<AttrType, byte, uint>(t => (byte) t, t => 0);
        //     return dic;
        // }

        // public static Dictionary<byte, uint> InitSimpleAttrsDicWithByteKey()
        // {
        //     var dic = new Dictionary<byte, uint>
        //     {
        //         [(byte) AttrType.GenGu] = 0,
        //         [(byte) AttrType.LingXing] = 0,
        //         [(byte) AttrType.LiLiang] = 0,
        //         [(byte) AttrType.MinJie] = 0
        //     };
        //     return dic;
        // }

        public static Dictionary<string, AttrType> EquipAttrTypeMap = new Dictionary<string, AttrType>
        {
            ["FatalRate"] = AttrType.PkuangBao,
            ["HitRate"] = AttrType.PmingZhong,
            ["PhyDefNef"] = AttrType.PpoFang,
            ["PhyDefNefRate"] = AttrType.PpoFangLv,
            ["AdAtkEhan"] = AttrType.Aatk,
            ["Atk"] = AttrType.Atk,
            ["AdSpdEhan"] = AttrType.JqSpd,
            ["HpMax"] = AttrType.HpMax,
            ["MpMax"] = AttrType.MpMax,
            ["HpPercent"] = AttrType.Ahp,
            ["MpPercent"] = AttrType.Amp,
            ["AtkPercent"] = AttrType.Patk,
            ["Speed"] = AttrType.Spd,
            ["RainDef"] = AttrType.Dshui,
            ["ThunderDef"] = AttrType.Dlei,
            ["FireDef"] = AttrType.Dhuo,
            ["WindDef"] = AttrType.Dfeng,
            ["RainDefNeg"] = AttrType.Hdshui,
            ["ThunderDefNeg"] = AttrType.Hdlei,
            ["FireDefNeg"] = AttrType.Hdhuo,
            ["WindDefNeg"] = AttrType.Hdfeng,
            ["SealDef"] = AttrType.DfengYin,
            ["DisorderDef"] = AttrType.DhunLuan,
            ["SleepDef"] = AttrType.DhunShui,
            ["PoisonDef"] = AttrType.Ddu,
            ["SealDefNeg"] = AttrType.HdfengYin,
            ["DisorderDefNeg"] = AttrType.HdhunLuan,
            ["SleepDefNeg"] = AttrType.HdhunShui,
            ["PoisonDefNeg"] = AttrType.Hddu,
            ["ForgetDef"] = AttrType.DyiWang,
            ["GfireDef"] = AttrType.DguiHuo,
            ["SanshiDef"] = AttrType.DsanShi,
            ["ForgetDefNeg"] = AttrType.HdyiWang,
            ["GfireDefNeg"] = AttrType.HdguiHuo,
            ["SanshiDefNeg"] = AttrType.HdsanShi,
            ["ShockDefNeg"] = AttrType.HdzhenShe,
            ["CharmEhan"] = AttrType.JqMeiHuo,
            ["PhyDef"] = AttrType.PxiShou,
            ["AdDefEhan"] = AttrType.Adefend,
            ["ShockDef"] = AttrType.DzhenShe,
            ["HitCombo"] = AttrType.PlianJi,
            ["HitComboRate"] = AttrType.PlianJiLv,
            ["VoidRate"] = AttrType.PshanBi,

            ["Basecon"] = AttrType.GenGu,
            ["Wakan"] = AttrType.LingXing,
            ["Power"] = AttrType.LiLiang,
            ["Agility"] = AttrType.MinJie,

            ["RainFatalRate"] = AttrType.KshuiLv,
            ["ThunderFatalRate"] = AttrType.KleiLv,
            ["FireFatalRate"] = AttrType.KhuoLv,
            ["WindFatalRate"] = AttrType.KfengLv,
            ["SanshiFatalRate"] = AttrType.KsanShiLv,
            ["GfireFatalRate"] = AttrType.KguiHuoLv,
            ["RainFatalHurt"] = AttrType.Kshui,
            ["ThunderFatalHurt"] = AttrType.Klei,
            ["FireFatalHurt"] = AttrType.Khuo,
            ["WindFatalHurt"] = AttrType.Kfeng,
            ["SanshiFatalHurt"] = AttrType.KsanShi,
            ["GfireFatalHurt"] = AttrType.KguiHuo,

            ["Kgold"] = AttrType.Qjin,
            ["Kwood"] = AttrType.Qmu,
            ["Kwater"] = AttrType.Qshui,
            ["Kfire"] = AttrType.Qhuo,
            ["Kearth"] = AttrType.Qtu,

            ["Gold"] = AttrType.Jin,
            ["Wood"] = AttrType.Mu,
            ["Water"] = AttrType.Shui,
            ["Fire"] = AttrType.Huo,
            ["Earth"] = AttrType.Tu,

            // 百分比加速
            ["Aspd"] = AttrType.Aspd,
            // 反震
            ["PfanZhen"] = AttrType.PfanZhen,
            // 反震率
            ["PfanZhenLv"] = AttrType.PfanZhenLv,
            // 加强混乱
            ["JqHunLuan"] = AttrType.JqHunLuan,
            // 加强封印
            ["JqFengYin"] = AttrType.JqFengYin,
            // 加强昏睡
            ["JqHunShui"] = AttrType.JqHunShui,
            // 加强毒
            ["JqDu"] = AttrType.JqDu,
            // 加强风
            ["JqFeng"] = AttrType.JqFeng,
            // 加强火
            ["JqHuo"] = AttrType.JqHuo,
            // 加强水
            ["JqShui"] = AttrType.JqShui,
            // 加强雷
            ["JqLei"] = AttrType.JqLei,
            // 加强鬼火
            ["JqGuiHuo"] = AttrType.JqGuiHuo,
            // 加强遗忘
            ["JqYiWang"] = AttrType.JqYiWang,
            // 加强三尸
            ["JqSanShi"] = AttrType.JqSanShi,
            // 加强震慑
            ["JqZhenShe"] = AttrType.JqZhenShe,
            // 加强加防
            ["JqDefend"] = AttrType.JqDefend,

            // 龙族
            // 加强震击
            ["AdZhenJi"] = AttrType.JqZhenJi,
            // 加强横扫
            ["AdHengSao"] = AttrType.JqHengSao,
            // 加强治愈
            ["AdZhiYu"] = AttrType.JqZhiYu,
            /// 加强破甲
            ["AdPoJia"] = AttrType.JqPoJia,

            // 抗混冰忘睡上限
            ["KBhbwsMax"] = AttrType.KbingFengMax,
        };

        /// <summary>
        /// 这些属性算绝对值
        /// </summary>
        public static Dictionary<AttrType, byte> EquipNumericalAttrType = new Dictionary<AttrType, byte>
        {
            [AttrType.Atk] = 0,
            [AttrType.Spd] = 0,
            [AttrType.PlianJi] = 0,

            [AttrType.Hp] = 0,
            [AttrType.Mp] = 0,

            [AttrType.HpMax] = 0,
            [AttrType.MpMax] = 0,

            [AttrType.GenGu] = 0,
            [AttrType.LingXing] = 0,
            [AttrType.LiLiang] = 0,
            [AttrType.MinJie] = 0,

            [AttrType.Jin] = 0,
            [AttrType.Mu] = 0,
            [AttrType.Shui] = 0,
            [AttrType.Huo] = 0,
            [AttrType.Tu] = 0,
        };

        /// <summary>
        /// 这些属性，在CalculateEquipAttrs时暂时先不计算
        /// </summary>
        public static Dictionary<AttrType, byte> EquipIgnoreAttrs = new Dictionary<AttrType, byte>
        {
            [AttrType.Hp] = 0,
            [AttrType.Mp] = 0,
            [AttrType.HpMax] = 0,
            [AttrType.MpMax] = 0,
            [AttrType.Atk] = 0,
            [AttrType.Spd] = 0,

            [AttrType.Php] = 0,
            [AttrType.Pmp] = 0,
            [AttrType.Patk] = 0,
            [AttrType.Pspd] = 0,
        };

        /// <summary>
        /// 属性计算方式, key是原始attrType，value的Item1是目标AttrType，Item2是计算方式
        /// </summary>
        public static Dictionary<AttrType, Tuple<AttrType, AttrCalcType>> AttrCalcMap =
            new Dictionary<AttrType, Tuple<AttrType, AttrCalcType>>
            {
                [AttrType.Ahp] = new Tuple<AttrType, AttrCalcType>(AttrType.Hp, AttrCalcType.AddPercent),
                [AttrType.Amp] = new Tuple<AttrType, AttrCalcType>(AttrType.Mp, AttrCalcType.AddPercent),
                [AttrType.Aatk] = new Tuple<AttrType, AttrCalcType>(AttrType.Atk, AttrCalcType.AddPercent),
                [AttrType.Aspd] = new Tuple<AttrType, AttrCalcType>(AttrType.Spd, AttrCalcType.AddPercent),

                [AttrType.Php] = new Tuple<AttrType, AttrCalcType>(AttrType.Hp, AttrCalcType.Percent),
                [AttrType.Pmp] = new Tuple<AttrType, AttrCalcType>(AttrType.Mp, AttrCalcType.Percent),
                [AttrType.Pspd] = new Tuple<AttrType, AttrCalcType>(AttrType.Spd, AttrCalcType.Percent),
                [AttrType.Patk] = new Tuple<AttrType, AttrCalcType>(AttrType.Atk, AttrCalcType.Percent)
            };


        public static Dictionary<AttrType, float> AttrTypeCalcScoreScale = new Dictionary<AttrType, float>
        {
            [AttrType.Hp] = 0.1f,
            [AttrType.HpMax] = 0.1f,
            [AttrType.Mp] = 0.1f,
            [AttrType.MpMax] = 0.1f,
            [AttrType.Atk] = 0.2f,
            [AttrType.Spd] = 0.5f,
            [AttrType.GenGu] = 0.1f,
            [AttrType.LingXing] = 0.1f,
            [AttrType.LiLiang] = 0.1f,
            [AttrType.MinJie] = 0.1f
        };

        /// <summary>
        /// 装备的宝石镶嵌配置, 第一维为装备位置，第二维为镶嵌等级
        /// </summary>
        public static uint[][] EquipGems =
        {
            new uint[] {30041, 30042, 30043, 30044, 30045, 30046, 30047},
            new uint[] {30021, 30022, 30023, 30024, 30025, 30026, 30027},
            new uint[] {30031, 30032, 30033, 30034, 30035, 30036, 30037},
            new uint[] {30001, 30002, 30003, 30004, 30005, 30006, 30007},
            new uint[] {30011, 30012, 30013, 30014, 30015, 30016, 30017}
        };

        /// <summary>
        /// 6阶仙器材料
        /// </summary>
        public static Dictionary<int, uint> EquipGrade6Items = new Dictionary<int, uint>()
        {
            [1] = 500041,
            [2] = 500044,
            [3] = 500042,
            [4] = 500043,
            [5] = 500045,
        };

        /// <summary>
        /// 精炼成功率，万分比
        /// </summary>
        public static Dictionary<uint, uint> JingLianGradeRate = new Dictionary<uint, uint>()
        {
            [1] = 7000,
            [2] = 6000,
            [3] = 5000,
            [4] = 4000,
            [5] = 3000,
            [6] = 2000,
            [7] = 1000,
        };

        /// <summary>
        /// 神兵升级破碎的概率
        /// </summary>
        public static uint[] Equip3BrokeProbs =
        {
            // 1->2 40%
            40,
            // 2->3 50%
            50,
            // 3->4 56%
            75,
            // 4->5 90%
            90
        };

        /// <summary>
        /// 道具合成配置数据, key是合成后的道具id, value是材料集合，表示需要每一种材料及其数量
        /// </summary>
        public static Dictionary<uint, List<Tuple<uint, uint>>> ItemComposes =
            new Dictionary<uint, List<Tuple<uint, uint>>>
            {
                // 紫宝石
                [30002] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30001, 6)
                },
                [30003] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30002, 5)
                },
                [30004] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30003, 4)
                },
                [30005] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30004, 3)
                },
                [30006] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30005, 2)
                },
                [30007] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30006, 2)
                },
                // 橙宝石
                [30012] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30011, 6)
                },
                [30013] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30012, 5)
                },
                [30014] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30013, 4)
                },
                [30015] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30014, 3)
                },
                [30016] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30015, 2)
                },
                [30017] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30016, 2)
                },
                // 绿宝石
                [30022] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30021, 6)
                },
                [30023] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30022, 5)
                },
                [30024] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30023, 4)
                },
                [30025] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30024, 3)
                },
                [30026] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30025, 2)
                },
                [30027] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30026, 2)
                },
                // 蓝宝石
                [30032] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30031, 6)
                },
                [30033] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30032, 5)
                },
                [30034] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30033, 4)
                },
                [30035] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30034, 3)
                },
                [30036] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30035, 2)
                },
                [30037] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30036, 2)
                },
                // 红宝石
                [30042] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30041, 6)
                },
                [30043] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30042, 5)
                },
                [30044] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30043, 4)
                },
                [30045] = new List<Tuple<uint, uint>>
                {
                    new Tuple<uint, uint>(30044, 3)
                },
                [30046] = new()
                {
                    new Tuple<uint, uint>(30045, 2)
                },
                [30047] = new()
                {
                    new Tuple<uint, uint>(30046, 2)
                },

                // 见闻录上中下 -> 高级藏宝图
                [50004] = new()
                {
                    new Tuple<uint, uint>(10301, 1),
                    new Tuple<uint, uint>(10302, 1),
                    new Tuple<uint, uint>(10303, 1)
                }
            };

        /// <summary>
        /// 技能日常任务积分兑换奖励
        /// </summary>
        public static readonly List<Tuple<uint, uint>> TaskActivePrizes = new()
            {
            new Tuple<uint, uint>(90004, 10000000),
            new Tuple<uint, uint>(500077, 20),
            new Tuple<uint, uint>(500077, 30),
            new Tuple<uint, uint>(500077, 40),
            new Tuple<uint, uint>(500077, 50),
            new Tuple<uint, uint>(500077, 60)
        };

        /// <summary>
        /// 可以PK的地图
        /// </summary>
        public static readonly Dictionary<uint, bool> PkMaps = new()
        {
            // // 长安
            // [1011] = true,
            // 监狱
            [1201] = true,
            // 天宫
            [1002] = true,
            // 皇宫
            // [1000] = true,
            // 金銮殿
            // [1206] = true,
            // 地府
            [1012] = true,
            // 白骨洞
            [1202] = true,
            // 斜月三星洞
            [1203] = true,
            // 兜率宫
            [1208] = true,
            // 兰若寺
            [1013] = true,
            // 灵兽村
            [1014] = true,
            // 帮派
            [3002] = true,
            // 家
            // [4001] = true,
            // 大乱斗
            [3004] = true,
        };
    }

    public enum AttrCalcType : byte
    {
        AddNum = 1,
        AddPercent = 2,
        Percent = 3
    }

    public class RoleReliveInfo
    {
        public byte Level;
        public byte ToLevel;
        public uint Price;
    }
}