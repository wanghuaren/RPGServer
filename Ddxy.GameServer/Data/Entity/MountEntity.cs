using System;
using System.Text.Json.Serialization;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "mount")]
    public class MountEntity : IEquatable<MountEntity>
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
        [JsonIgnore]
        public uint RoleId { get; set; }

        /// <summary>
        /// 配置id
        /// </summary>
        public uint CfgId { get; set; }

        /// <summary>
        /// 坐骑名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 等级
        /// </summary>
        public byte Level { get; set; }

        /// <summary>
        /// 经验值
        /// </summary>
        public ulong Exp { get; set; }

        /// <summary>
        /// 基础气血
        /// </summary>
        public uint Hp { get; set; }

        /// <summary>
        /// 基础速度
        /// </summary>
        public int Spd { get; set; }

        /// <summary>
        /// 基础成长率
        /// </summary>
        public uint Rate { get; set; }

        /// <summary>
        /// 坐骑技能及其熟练度
        /// </summary>
        public string Skills { get; set; }

        /// <summary>
        /// 已管制的宠物id集合
        /// </summary>
        public string Pets { get; set; }

        /// <summary>
        /// 未替换的洗练数据
        /// </summary>
        public string WashData { get; set; }

        /// <summary>
        /// 是否乘骑
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// 是否已经解锁？
        /// </summary>
        public bool Locked { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public uint CreateTime { get; set; }

        public void CopyFrom(MountEntity other)
        {
            Id = other.Id;
            RoleId = other.RoleId;
            CfgId = other.CfgId;
            Name = other.Name;
            Level = other.Level;
            Exp = other.Exp;
            Hp = other.Hp;
            Spd = other.Spd;
            Rate = other.Rate;
            Skills = other.Skills;
            Pets = other.Pets;
            WashData = other.WashData;
            Active = other.Active;
            Locked = other.Locked;
            CreateTime = other.CreateTime;
        }

        public bool Equals(MountEntity other)
        {
            if (other == null) return false;
            return Id == other.Id && RoleId == other.RoleId && CfgId == other.CfgId &&
                   Name.Equals(other.Name) && Level == other.Level && Exp == other.Exp &&
                   Hp == other.Hp && Spd == other.Spd && Rate == other.Rate &&
                   Skills.Equals(other.Skills) && Pets.Equals(other.Pets) && WashData.Equals(other.WashData) &&
                   Active == other.Active && Locked == other.Locked && CreateTime == other.CreateTime;
        }
    }
}