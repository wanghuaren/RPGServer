namespace Ddxy.Common.Model
{
	public class XinPayOptions
	{
		public string MemberId { get; set; }
		public string SignMd5Key { get; set; }
		public string OrderPrefix { get; set; }
		public string JadeNotifyUrl { get; set; }
		public string BindJadeNotifyUrl { get; set; }
		public string ReturnUrl { get; set; }
		public string CallbackUrl { get; set; }
	}
}
