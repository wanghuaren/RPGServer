using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace Ddxy.Common.Model.Api
{
	[Serializable]
	public class XinNotifyReq
	{
		[Required]
		[BindProperty(Name = "version")]
		public string version { get; set; }

		[Required]
		[BindProperty(Name = "customerid")]
		public string customerid { get; set; }

		[Required]
		[BindProperty(Name = "sdorderno")]
		public string sdorderno { get; set; }

		[Required]
		[BindProperty(Name = "total_fee")]
		public string total_fee { get; set; }

		[Required]
		[BindProperty(Name = "paytype")]
		public string paytype { get; set; }

		[Required]
		[BindProperty(Name = "sdpayno")]
		public string sdpayno { get; set; }

		[Required]
		[BindProperty(Name = "remark")]
		public string remark { get; set; }

		[Required]
		[BindProperty(Name = "sign")]
		public string sign { get; set; }

		[BindProperty(Name = "successtime")]
		public string SuccessTime { get; set; }

		[Required]
		[BindProperty(Name = "status")]
		public string status { get; set; }

		[Required]
		[BindProperty(Name = "status")]
		public string Status { get; set; }

		[BindProperty(Name = "extend")]
		public string Extend { get; set; }

		[Required]
		[BindProperty(Name = "signmethod")]
		public string SignMethod { get; set; }

		[Required]
		[BindProperty(Name = "sign")]
		public string Sign { get; set; }
	}


	[Serializable]
	public class MYXinNotifyReq
	{

		public int status { get; set; }

		public int customerid { get; set; }

		public string sdpayno { get; set; }

		public string sdorderno { get; set; }

		public decimal total_fee { get; set; }

		public string paytype { get; set; }

		public string remark { get; set; }

		public string sign { get; set; }
	}

	[Serializable]
	public class MYXinNotifyReq2
	{
        //state	订单充值状态	1.充值成功 2.充值失败
        //customerid	商户ID	商户注册的时候，网关自动分配的商户ID
        //sd51no	订单在网关的订单号	该订单在网关系统的订单号
        //sdcustomno	商户订单号	该订单在商户系统的流水号
        //ordermoney	订单实际金额	商户订单实际金额 单位：（元）
        //cardno	支付类型	支付类型，为固定值 32
        //mark	商户自定义信息	未启用暂时返回空值
        //sign	md5签名字符串	发送给商户的签名字符串
        //resign	md5二次签名字符串	发送给商户的签名字符串
        //des	支付备注	描述订单支付成功或失败的系统备注
		public int state { get; set; }

		public string customerid { get; set; }

		public string sd51no { get; set; }

		public string sdcustomno { get; set; }

		public decimal ordermoney { get; set; }

		public string cardno { get; set; }

		public string mark { get; set; }

		public string resign { get; set; }

		public string des { get; set; }
    }
	[Serializable]
	public class YunDingNotifyReq
	{
		//字段名			变量名			必填 类型		示例值					描述
		//商户ID			pid				是 Int		1001  
		//易支付订单号	trade_no		是 String	20160806151343349021	云鼎支付订单号
		//商户订单号		out_trade_no	是 String	20160806151343349		商户系统内部的订单号
		//支付方式		type			是 String	alipay					支付方式列表
		//商品名称		name			是 String	VIP会员
		//商品金额		money			是 String	1.00  
		//支付状态		trade_status	是 String	TRADE_SUCCESS			只有TRADE_SUCCESS是成功
		//业务扩展参数	param			否 String
		//签名字符串		sign			是 String	202cb962ac59075b964b07152d234b70 签名算法点此查看
		//签名类型		sign_type		是 String	MD5 默认为MD5
		public int pid { get; set; }

		public string trade_no { get; set; }

		public string out_trade_no { get; set; }

		public string type { get; set; }

		public decimal name { get; set; }

		public string money { get; set; }

		public string trade_status { get; set; }

		// public string param { get; set; }

		public string sign { get; set; }

		public string sign_type { get; set; }
	}
}
