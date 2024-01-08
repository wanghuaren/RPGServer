using System;
using System.Text.Json.Serialization;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "role")]
    public class RoleEntity : IEquatable<RoleEntity>
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 所属用户id
        /// </summary>
        [Column(Name = "uid")]
        public uint UserId { get; set; }

        /// <summary>
        /// 所属用户id
        /// </summary>
        [Column(Name = "sid")]
        public uint ServerId { get; set; }

        /// <summary>
        /// 所属代理, 0表示直属运营商
        /// </summary>
        public uint ParentId { get; set; }

        /// <summary>
        /// 角色状态
        /// </summary>
        [Column(MapType = typeof(byte))]
        public RoleStatus Status { get; set; }

        /// <summary>
        /// 用户类型
        /// </summary>
        [Column(MapType = typeof(byte))]
        public UserType Type { get; set; }

        /// <summary>
        /// 角色昵称
        /// </summary>
        [Column(Name = "nickname")]
        [JsonPropertyName("nickname")]
        public string NickName { get; set; }

        /// <summary>
        /// 角色配置id
        /// </summary>
        public uint CfgId { get; set; }

        /// <summary>
        /// 性别
        /// </summary>
        public byte Sex { get; set; }

        /// <summary>
        /// 种族
        /// </summary>
        public byte Race { get; set; }

        /// <summary>
        /// 转生等级
        /// </summary>
        public byte Relive { get; set; }

        /// <summary>
        /// 等级
        /// </summary>
        public byte Level { get; set; }

        /// <summary>
        /// 经验值
        /// </summary>
        public ulong Exp { get; set; }

        /// <summary>
        /// 银币值
        /// </summary>
        public uint Silver { get; set; }

        /// <summary>
        /// 仙玉值
        /// </summary>
        public uint Jade { get; set; }

        /// <summary>
        /// 绑定仙玉
        /// </summary>
        public uint BindJade { get; set; }

        /// <summary>
        /// 帮派贡献总和
        /// </summary>
        public uint Contrib { get; set; }

        /// <summary>
        /// 水路大会功绩
        /// </summary>
        public uint SldhGongJi { get; set; }

        /// <summary>
        /// 王者之战积分
        /// </summary>
        public uint WzzzJiFen { get; set; }

        /// <summary>
        /// 成神之路 爬塔层数
        /// </summary>
        [Column(Name = "cszlLayer")]
        public uint CszlLayer { get; set; }

        /// <summary>
        /// 郭氏积分
        /// </summary>
        public uint GuoShi { get; set; }

        /// <summary>
        /// 拥有的皮肤（暂时只含足迹和特效）
        /// </summary>
        public string Skins { get; set; }

        /// <summary>
        /// 玩家的操作次数 (1->孩子定制 2->星阵定制 3->配饰炼化)
        /// </summary>
        [Column(Name = "operate")]
        public string OperateTimes { get; set; }

        /// <summary>
        /// 变身卡及五行修炼
        /// </summary>
        public string Bianshen { get; set; }

        /// <summary>
        /// 星阵
        /// </summary>
        public string Xingzhen { get; set; }

        /// <summary>
        /// 孩子信息
        /// </summary>
        public string Child { get; set; }

        /// <summary>
        /// 经验兑换属性点次数
        /// </summary>
        public uint ExpExchangeTimes { get; set; }

        /// <summary>
        /// 所在地图id
        /// </summary>
        public uint MapId { get; set; }

        /// <summary>
        /// 地图坐标X
        /// </summary>
        public int MapX { get; set; }

        /// <summary>
        /// 地图坐标X
        /// </summary>
        public int MapY { get; set; }

        /// <summary>
        /// 6个技能的熟练度用,分割
        /// </summary>
        public string Skills { get; set; }

        /// <summary>
        /// 染色1
        /// </summary>
        public uint Color1 { get; set; }

        /// <summary>
        /// 染色2
        /// </summary>
        public uint Color2 { get; set; }

        /// <summary>
        /// 当前加入的帮派id
        /// </summary>
        public uint SectId { get; set; }

        /// <summary>
        /// 当前帮派贡献
        /// </summary>
        public uint SectContrib { get; set; }

        /// <summary>
        /// 在帮派中的职位
        /// </summary>
        public byte SectJob { get; set; }

        /// <summary>
        /// 入帮时间
        /// </summary>
        public uint SectJoinTime { get; set; }

        /// <summary>
        /// 修炼等级
        /// </summary>
        public uint XlLevel { get; set; }

        /// <summary>
        /// 击杀地煞星，星级
        /// </summary>
        public uint Star { get; set; }

        /// <summary>
        /// 善恶值,监禁的结束时间
        /// </summary>
        public uint Shane { get; set; }

        /// <summary>
        /// 转生信息
        /// </summary>
        public string Relives { get; set; }

        /// <summary>
        /// 等级奖励
        /// </summary>
        public string Rewards { get; set; }

        /// <summary>
        /// 水陆大会
        /// </summary>
        public string Sldh { get; set; }

        /// <summary>
        /// 王者之战
        /// </summary>
        public string Wzzz { get; set; }

        /// <summary>
        /// 单人PK
        /// </summary>
        public string SinglePk { get; set; }

        //大乱斗
        public string DaLuanDou { get; set; }

        /// <summary>
        /// 各种开关
        /// </summary>
        public int Flags { get; set; }

        /// <summary>
        /// 自动技能
        /// </summary>
        public uint AutoSkill { get; set; }

        /// <summary>
        /// 是否自动同步
        /// </summary>
        public bool AutoSyncSkill { get; set; }

        /// <summary>
        /// 总充值额
        /// </summary>
        public uint TotalPay { get; set; }

        /// <summary>
        /// 累计充值领取金额集合
        /// </summary>
        public string TotalPayRewards { get; set; }

        /// <summary>
        /// 总充值额
        /// </summary>
        public uint EwaiPay { get; set; }

        /// <summary>
        /// 额外累计充值领取金额集合
        /// </summary>
        public string EwaiPayRewards { get; set; }

        /// <summary>
        /// 总充值额
        /// </summary>
        public uint TotalPayBS { get; set; }

        /// <summary>
        /// 今日充值额
        /// </summary>
        public uint DailyPay { get; set; }

        /// <summary>
        /// 上次记录的今日充值时间
        /// </summary>
        public uint DailyPayTime { get; set; }

        /// <summary>
        /// 今日充值领取金额集合
        /// </summary>
        public string DailyPayRewards { get; set; }

        /// <summary>
        /// 安全锁
        /// </summary>
        public string SafeCode { get; set; }

        /// <summary>
        /// 是否安全锁定
        /// </summary>
        public bool SafeLocked { get; set; }

        /// <summary>
        /// 我绑定的推广人
        /// </summary>
        public uint Spread { get; set; }

        /// <summary>
        /// 我绑定推广人的时间
        /// </summary>
        public uint SpreadTime { get; set; }

        /// <summary>
        /// 是否在线
        /// </summary>
        public bool Online { get; set; }

        /// <summary>
        /// 上次上线时间
        /// </summary>
        public uint OnlineTime { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public uint CreateTime { get; set; }

        [Column(IsIgnore = true)] public uint SldhWin { get; set; }

        [Column(IsIgnore = true)] public uint SldhScore { get; set; }

        [Column(IsIgnore = true)] public uint WzzzWin { get; set; }

        [Column(IsIgnore = true)] public uint WzzzScore { get; set; }

        [Column(IsIgnore = true)] public uint SinglePkWin { get; set; }
        [Column(IsIgnore = true)] public uint SinglePkLost { get; set; }

        [Column(IsIgnore = true)] public uint SinglePkScore { get; set; }

        [Column(IsIgnore = true)] public uint DaLuanDouWin { get; set; }
        [Column(IsIgnore = true)] public uint DaLuanDouLost { get; set; }
        [Column(IsIgnore = true)] public uint DaLuanDouScore { get; set; }

        [Column(IsIgnore = true)] public string AgencyInvitCode { get; set; }

        [Column(IsIgnore = true)] public string SpreadName { get; set; }

        public void CopyFrom(RoleEntity other)
        {
            Id = other.Id;
            UserId = other.UserId;
            ServerId = other.ServerId;

            Status = other.Status;
            Type = other.Type;
            NickName = other.NickName;
            CfgId = other.CfgId;
            Sex = other.Sex;
            Race = other.Race;
            Relive = other.Relive;
            Level = other.Level;
            Exp = other.Exp;
            Silver = other.Silver;
            Jade = other.Jade;
            BindJade = other.BindJade;
            Contrib = other.Contrib;
            SldhGongJi = other.SldhGongJi;
            WzzzJiFen = other.WzzzJiFen;
            CszlLayer = other.CszlLayer;
            GuoShi = other.GuoShi;
            MapId = other.MapId;
            MapX = other.MapX;
            MapY = other.MapY;
            Skills = other.Skills;
            Color1 = other.Color1;
            Color2 = other.Color2;
            SectId = other.SectId;
            SectContrib = other.SectContrib;
            SectJob = other.SectJob;
            SectJoinTime = other.SectJoinTime;
            XlLevel = other.XlLevel;
            Star = other.Star;
            Shane = other.Shane;
            Relives = other.Relives;
            Rewards = other.Rewards;
            Sldh = other.Sldh;
            Wzzz = other.Wzzz;
            SinglePk = other.SinglePk;
            DaLuanDou = other.DaLuanDou;
            Flags = other.Flags;
            AutoSkill = other.AutoSkill;
            AutoSyncSkill = other.AutoSyncSkill;
            TotalPay = other.TotalPay;
            TotalPayRewards = other.TotalPayRewards;
            EwaiPay = other.EwaiPay;
            EwaiPayRewards = other.EwaiPayRewards;
            TotalPayBS = other.TotalPayBS;
            DailyPay = other.DailyPay;
            DailyPayTime = other.DailyPayTime;
            DailyPayRewards = other.DailyPayRewards;
            SafeCode = other.SafeCode;
            SafeLocked = other.SafeLocked;
            Spread = other.Spread;
            SpreadTime = other.SpreadTime;
            ParentId = other.ParentId;
            Online = other.Online;
            OnlineTime = other.OnlineTime;
            CreateTime = other.CreateTime;
            Skins = other.Skins;
            OperateTimes = other.OperateTimes;
            Bianshen = other.Bianshen;
            Xingzhen = other.Xingzhen;
            Child = other.Child;
            ExpExchangeTimes = other.ExpExchangeTimes;
        }

        public bool Equals(RoleEntity other)
        {
            if (other == null) return false;
            return Status.Equals(other.Status) &&
                   Type == other.Type &&
                   NickName.Equals(other.NickName) &&
                   CfgId == other.CfgId &&
                   Sex == other.Sex &&
                   Race == other.Race &&
                   Relive == other.Relive &&
                   Level == other.Level &&
                   Exp == other.Exp &&
                   Silver == other.Silver &&
                   Jade == other.Jade &&
                   BindJade == other.BindJade &&
                   Contrib == other.Contrib &&
                   SldhGongJi == other.SldhGongJi &&
                   WzzzJiFen == other.WzzzJiFen &&
                   CszlLayer == other.CszlLayer &&
                   GuoShi == other.GuoShi &&
                   MapId == other.MapId &&
                   MapX == other.MapX &&
                   MapY == other.MapY &&
                   Skills.Equals(other.Skills) &&
                   Color1 == other.Color1 &&
                   Color2 == other.Color2 &&
                   SectId == other.SectId &&
                   SectContrib == other.SectContrib &&
                   SectJob == other.SectJob &&
                   SectJoinTime == other.SectJoinTime &&
                   XlLevel == other.XlLevel &&
                   Star == other.Star &&
                   Shane == other.Shane &&
                   Relives.Equals(other.Relives) &&
                   Rewards == other.Rewards &&
                   Sldh.Equals(other.Sldh) &&
                   Wzzz.Equals(other.Wzzz) &&
                   SinglePk.Equals(other.SinglePk) &&
                   DaLuanDou.Equals(other.DaLuanDou) &&
                   Flags == other.Flags &&
                   AutoSkill == other.AutoSkill &&
                   AutoSyncSkill == other.AutoSyncSkill &&
                   TotalPay == other.TotalPay &&
                   TotalPayRewards.Equals(other.TotalPayRewards) &&
                   EwaiPay == other.EwaiPay &&
                   EwaiPayRewards.Equals(other.EwaiPayRewards) &&
                   TotalPayBS == other.TotalPayBS &&
                   DailyPay == other.DailyPay &&
                   DailyPayTime == other.DailyPayTime &&
                   DailyPayRewards.Equals(other.DailyPayRewards) &&
                   SafeCode.Equals(other.SafeCode) &&
                   SafeLocked == other.SafeLocked &&
                   Spread == other.Spread &&
                   SpreadTime == other.SpreadTime &&
                   ParentId == other.ParentId &&
                   Online == other.Online &&
                   OnlineTime == other.OnlineTime &&
                   CreateTime == other.CreateTime &&
                   Skins.Equals(other.Skins) &&
                   OperateTimes.Equals(other.OperateTimes) &&
                   Bianshen.Equals(other.Bianshen) &&
                   Xingzhen.Equals(other.Xingzhen) &&
                   Child.Equals(other.Child) &&
                   ExpExchangeTimes == other.ExpExchangeTimes;
        }
    }

    /// <summary>
    /// 角色状态
    /// </summary>
    public enum RoleStatus : byte
    {
        /// <summary>
        /// 正常状态
        /// </summary>
        Normal = 0,

        /// <summary>
        /// 冻结状态
        /// </summary>
        Frozen = 1,
    }
}