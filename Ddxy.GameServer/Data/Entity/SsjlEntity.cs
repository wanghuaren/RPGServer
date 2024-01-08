using System;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "ssjl")]
    public class SsjlEntity : IEquatable<SsjlEntity>
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
        /// 上次开始时间
        /// </summary>
        public uint LastTime { get; set; }

        /// <summary>
        /// 上季获得的神兽及角色ID
        /// </summary>
        public string Reward { get; set; }

        public void CopyFrom(SsjlEntity other)
        {
            Id = other.Id;
            ServerId = other.ServerId;
            Season = other.Season;
            LastTime = other.LastTime;
            Reward = other.Reward;
        }

        public bool Equals(SsjlEntity other)
        {
            if (other == null) return false;
            return Id == other.Id
            && ServerId == other.ServerId
            && Season == other.Season
            && LastTime == other.LastTime
            && Reward.Equals(other.Reward);
        }
    }
}