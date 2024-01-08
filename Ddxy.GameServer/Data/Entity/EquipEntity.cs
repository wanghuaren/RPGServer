using System;
using System.Text.Json.Serialization;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "equip")]
    public class EquipEntity : IEquatable<EquipEntity>
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
        /// 装备类型
        /// </summary>
        public byte Category { get; set; }

        /// <summary>
        /// 配置id
        /// </summary>
        public uint CfgId { get; set; }

        /// <summary>
        /// 升星数
        /// </summary>
        public uint StarCount { get; set; }

        /// <summary>
        /// 升星经验
        /// </summary>
        public uint StarExp { get; set; }

        /// <summary>
        /// 宝石镶嵌数量
        /// </summary>
        public byte Gem { get; set; }

        /// <summary>
        /// 品阶
        /// </summary>
        public byte Grade { get; set; }

        /// <summary>
        /// 装备存放位置
        /// </summary>
        public byte Place { get; set; }

        /// <summary>
        /// 基础属性
        /// </summary>
        public string BaseAttrs { get; set; }

        /// <summary>
        /// 炼化属性
        /// </summary>
        public string RefineAttrs { get; set; }

        /// <summary>
        /// 需求属性
        /// </summary>
        public string NeedAttrs { get; set; }

        /// <summary>
        /// 炼化预览数据
        /// </summary>
        public string Refine { get; set; }

        /// <summary>
        /// 炼化预览数据
        /// </summary>
        public string RefineList { get; set; }

        /// <summary>
        /// 重铸预览数据
        /// </summary>
        public string Recast { get; set; }

        /// <summary>
        /// 注册时间
        /// </summary>
        public uint CreateTime { get; set; }

        [Column(IsIgnore = true)] public string Name { get; set; }
        [Column(IsIgnore = true)] public int Pos { get; set; }

        public void CopyFrom(EquipEntity other)
        {
            Id = other.Id;
            RoleId = other.RoleId;
            Category = other.Category;
            CfgId = other.CfgId;
            StarCount = other.StarCount;
            StarExp = other.StarExp;
            Gem = other.Gem;
            Grade = other.Grade;
            Place = other.Place;
            BaseAttrs = other.BaseAttrs;
            RefineAttrs = other.RefineAttrs;
            NeedAttrs = other.NeedAttrs;
            Refine = other.Refine;
            RefineList = other.RefineList;
            Recast = other.Recast;
            CreateTime = other.CreateTime;
        }

        public bool Equals(EquipEntity other)
        {
            if (null == other) return false;
            return Id == other.Id && RoleId == other.RoleId && Category == other.Category && CfgId == other.CfgId &&
                   Gem == other.Gem && Grade == other.Grade && Place == other.Place &&
                   BaseAttrs.Equals(other.BaseAttrs) && RefineAttrs.Equals(other.RefineAttrs) &&
                   NeedAttrs.Equals(other.NeedAttrs) && Refine.Equals(other.Refine) && RefineList.Equals(other.RefineList) && Recast.Equals(other.Recast) &&
                   CreateTime == other.CreateTime && 
                   StarCount == other.StarCount && StarExp == other.StarExp;
        }
    }
}