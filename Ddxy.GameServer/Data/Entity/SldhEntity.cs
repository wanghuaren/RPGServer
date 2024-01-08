using System;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "sldh")]
    public class SldhEntity : IEquatable<SldhEntity>
    {
        /// <summary>
        /// 用户id
        /// </summary>
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        /// <summary>
        /// 区服id
        /// </summary>
        [Column(Name = "sid")]
        public uint ServerId { get; set; }

        /// <summary>
        /// 当前第几季
        /// </summary>
        public uint Season { get; set; }

        /// <summary>
        /// 当前第几轮
        /// </summary>
        public uint Turn { get; set; }

        /// <summary>
        /// 上次开始时间
        /// </summary>
        public uint LastTime { get; set; }
        
        /// <summary>
        /// 上季获得水路战胜称号的角色id集合
        /// </summary>
        public string Slzs { get; set; }

        public void CopyFrom(SldhEntity other)
        {
            Id = other.Id;
            ServerId = other.ServerId;
            Season = other.Season;
            Turn = other.Turn;
            LastTime = other.LastTime;
        }

        public bool Equals(SldhEntity other)
        {
            if (other == null) return false;
            return Id == other.Id && ServerId == other.ServerId &&
                   Season == other.Season && Turn == other.Turn && LastTime == other.LastTime;
        }
    }
}