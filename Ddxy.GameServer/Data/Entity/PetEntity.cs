using System;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "pet")]
    public class PetEntity : IEquatable<PetEntity>
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 所属角色id
        /// </summary>
        [Column(Name = "rid")]
        public uint RoleId { get; set; }

        /// <summary>
        /// 配置id
        /// </summary>
        public uint CfgId { get; set; }

        /// <summary>
        /// 宠物名称
        /// </summary>
        public string Name { get; set; }

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
        /// 亲密度
        /// </summary>
        public uint Intimacy { get; set; }

        /// <summary>
        /// 基础气血
        /// </summary>
        public uint Hp { get; set; }

        /// <summary>
        /// 基础法力
        /// </summary>
        public uint Mp { get; set; }

        /// <summary>
        /// 基础攻击
        /// </summary>
        public uint Atk { get; set; }

        /// <summary>
        /// 基础速度
        /// </summary>
        public int Spd { get; set; }

        /// <summary>
        /// 基础成长率
        /// </summary>
        public uint Rate { get; set; }

        /// <summary>
        /// 洗练品阶
        /// </summary>
        public byte Quality { get; set; }

        /// <summary>
        /// 龙骨
        /// </summary>
        public uint Keel { get; set; }

        /// <summary>
        /// 使用聚魂丹的个数
        /// </summary>
        public uint Unlock { get; set; }

        /// <summary>
        /// 技能id集合
        /// </summary>
        public string Skills { get; set; }

        /// <summary>
        /// 神兽技能
        /// </summary>
        public uint SsSkill { get; set; }

        /// <summary>
        /// 觉醒等级
        /// </summary>
        public uint JxLevel { get; set; }

        /// <summary>
        /// 加点值
        /// </summary>
        public string ApAttrs { get; set; }

        /// <summary>
        /// 五行
        /// </summary>
        public string Elements { get; set; }

        /// <summary>
        /// 修炼等级
        /// </summary>
        public uint RefineLevel { get; set; }

        /// <summary>
        /// 修炼经验
        /// </summary>
        public uint RefineExp { get; set; }

        /// <summary>
        /// 修炼属性
        /// </summary>
        public string RefineAttrs { get; set; }

        /// <summary>
        /// %10表示飞升次数, /10表示飞升增加的属性 1hp 2mp 3atk 4spd
        /// </summary>
        public uint Fly { get; set; }

        /// <summary>
        /// 变色 -1:变色未成功，0:未变色, >0变色结果
        /// </summary>
        public int Color { get; set; }

        /// <summary>
        /// 闪现支援顺序
        /// </summary>
        public uint SxOrder { get; set; }

        /// <summary>
        /// 自动技能
        /// </summary>
        public uint AutoSkill { get; set; }

        /// <summary>
        /// 未替换的洗练数据
        /// </summary>
        public string WashData { get; set; }

        /// <summary>
        /// 是否参战
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public uint CreateTime { get; set; }

        public void CopyFrom(PetEntity other)
        {
            Id = other.Id;
            RoleId = other.RoleId;
            CfgId = other.CfgId;
            Name = other.Name;
            Relive = other.Relive;
            Level = other.Level;
            Exp = other.Exp;
            Intimacy = other.Intimacy;
            Hp = other.Hp;
            Mp = other.Mp;
            Atk = other.Atk;
            Spd = other.Spd;
            Rate = other.Rate;
            Quality = other.Quality;
            Keel = other.Keel;
            Unlock = other.Unlock;
            Skills = other.Skills;
            SsSkill = other.SsSkill;
            JxLevel = other.JxLevel;
            ApAttrs = other.ApAttrs;
            Elements = other.Elements;
            RefineLevel = other.RefineLevel;
            RefineExp = other.RefineExp;
            RefineAttrs = other.RefineAttrs;
            Fly = other.Fly;
            Color = other.Color;
            SxOrder = other.SxOrder;
            AutoSkill = other.AutoSkill;
            WashData = other.WashData;
            Active = other.Active;
            CreateTime = other.CreateTime;
        }

        public bool Equals(PetEntity other)
        {
            if (other == null) return false;
            return Id == other.Id && RoleId == other.RoleId && CfgId == other.CfgId && Name.Equals(other.Name) &&
                   Relive == other.Relive && Level == other.Level && Exp == other.Exp &&
                   Intimacy == other.Intimacy && Hp == other.Hp && Mp == other.Mp && Atk == other.Atk &&
                   Spd == other.Spd && Rate == other.Rate && Quality == other.Quality && Keel == other.Keel &&
                   Unlock == other.Unlock && Skills.Equals(other.Skills) && SsSkill == other.SsSkill &&
                   ApAttrs.Equals(other.ApAttrs) && Elements.Equals(other.Elements) &&
                   RefineLevel == other.RefineLevel && RefineExp == other.RefineExp &&
                   RefineAttrs.Equals(other.RefineAttrs) && Fly == other.Fly && Color == other.Color &&
                   SxOrder == other.SxOrder && AutoSkill == other.AutoSkill &&
                   JxLevel == other.JxLevel &&
                   Active == other.Active && WashData.Equals(other.WashData) && CreateTime == other.CreateTime;
        }
    }
}