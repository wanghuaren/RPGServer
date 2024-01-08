using System;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "friend")]
    public class FriendEntity : IEquatable<FriendEntity>
    {
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        public uint Role1 { get; set; }

        public uint Role2 { get; set; }

        public void CopyFrom(FriendEntity other)
        {
            Id = other.Id;
            Role1 = other.Role1;
            Role2 = other.Role2;
        }

        public bool Equals(FriendEntity other)
        {
            if (other == null) return false;
            return Id == other.Id && Role1 == other.Role1 && Role2 == other.Role2;
        }
    }
}