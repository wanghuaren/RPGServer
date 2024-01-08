using FreeSql.DataAnnotations;

namespace Ddxy.GameServer.Data.Entity
{
	[Table(Name = "pay")]
	public class PayEntity
	{
		[Column(IsPrimary = true, IsIdentity = true)]
		public uint Id { get; set; }

		public uint Rid { get; set; }

		public uint Money { get; set; }

		public uint Jade { get; set; }

		public uint BindJade { get; set; }

		[Column(MapType = typeof(byte))]
		public PayChannel PayChannel { get; set; }

		[Column(MapType = typeof(byte))]
		public PayType PayType { get; set; }

		public string Remark { get; set; }

		public string Order { get; set; }

		[Column(MapType = typeof(byte))]
		public OrderStatus Status { get; set; }

		public uint CreateTime { get; set; }

		public uint UpdateTime { get; set; }

		public uint DelivTime { get; set; }
	}
}
