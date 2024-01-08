using System;
using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
    [Table(Name = "scheme")]
    public class SchemeEntity : IEquatable<SchemeEntity>
    {
        [Column(IsPrimary = true, IsIdentity = true)]
        public uint Id { get; set; }

        [Column(Name = "rid")] public uint RoleId { get; set; }

        public string Name { get; set; }

        public string Equips { get; set; }

        public string Ornaments { get; set; }

        public string ApAttrs { get; set; }

        public string XlAttrs { get; set; }

        public string Relives { get; set; }

        public bool Active { get; set; }

        public uint CreateTime { get; set; }

        public void CopyFrom(SchemeEntity other)
        {
            Id = other.Id;
            RoleId = other.RoleId;
            Name = other.Name;
            Equips = other.Equips;
            Ornaments = other.Ornaments;
            ApAttrs = other.ApAttrs;
            Relives = other.Relives;
            XlAttrs = other.XlAttrs;
            Active = other.Active;
            CreateTime = other.CreateTime;
        }

        public bool Equals(SchemeEntity other)
        {
            if (other == null) return false;
            return Id == other.Id && RoleId == other.RoleId && Name.Equals(other.Name) &&
                   Equips.Equals(other.Equips) && Ornaments.Equals(other.Ornaments) &&
                   ApAttrs.Equals(other.ApAttrs) && XlAttrs.Equals(other.XlAttrs) &&
                   Relives.Equals(other.Relives) && Active == other.Active && CreateTime == other.CreateTime;
        }
    }
}