using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Ddxy.Common.Model.Api;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Data.Entity;

namespace Ddxy.GameServer.Util
{
	public static class XinPayUtil
	{
		public const string Version = "1.0.0";

		public const string SignMethod = "md5";

		public const string GateWay = "http://www.yingchenfu.com/pay/apisubmit/apisubmit.php";
		public const string key = "c59698f101080ae42fd0806b81ea1f74fa2d886b";
		// public const string return_url = "http://43.242.204.47:20000/api/return_url"; 

		public static string GetPayType(PayType payType)
		{
			return payType switch
			{
				PayType.WxSaoMa => "wxpay",
				PayType.WxWap => "wxpay",
				PayType.ZfbSaoMa => "alipay",
				PayType.ZfbWap => "alipay",
				_ => "",
			};
		}

		public static string GetPayType2(PayType payType)
		{
			//微信扫码     https://open.tnwx88.com/intf/wpay.html
			//支付宝扫码   https://open.tnwx88.com/intf/wapzpay.html
			//QQ支付扫码  https://open.tnwx88.com/intf/spay.html
			//京东扫码     https://open.tnwx88.com/intf/dpay.html
			//QQ支付(H5模式)  https://open.tnwx88.com/intf/wapspay.html
			//微信支付(H5模式)  https://open.tnwx88.com/intf/wapwpay.html
			//支付宝(H5模式)  https://open.tnwx88.com/intf/wapali.html

			//云鼎支付	https://www.yundingzhifu.cn/submit.php
			//云盟支付 http://zf.lcymkj.com/submit.php
			//云盟支付扫码 http://zf.lcymkj.com/qrcode.php
			return payType switch
			{
				PayType.WxSaoMa => "http://sy.n9ui5x.cyou/submit.php",
				PayType.WxWap => "http://sy.n9ui5x.cyou/submit.php",
				PayType.ZfbSaoMa => "http://sy.n9ui5x.cyou/submit.php",
				PayType.ZfbWap => "http://sy.n9ui5x.cyou/submit.php",
				_ => "",
			};
		}

		public static string md5signaa(string str)
		{
			return GetMD5Str.Md5Sum(str);
		}


		public static string Order(string memberid, string orderid, string amount, string orderdatetime, string paytype, string notifyurl, string signMd5Key, uint RoleId)
		{
			amount = amount + ".00";
			Dictionary<string, string> signDic = new Dictionary<string, string>
			{
				["version"] = "1.0",
				["customerid"] = memberid,
				["total_fee"] = amount,
				["sdorderno"] = orderid,
				["notifyurl"] = notifyurl,
				["returnurl"] = "aaa",
			};
			string str = "version=1.0&customerid=" + memberid + "&total_fee=" + amount + "&sdorderno=" + orderid + "&notifyurl=" + notifyurl + "&returnurl=aaa&" + signMd5Key;
			string aa = md5signaa(str);			
			signDic.Add("paytype", paytype);
			signDic.Add("bankcode", " ");			
			signDic.Add("get_code", "0");
			signDic.Add("retmsg", "1");
			signDic.Add("remark", RoleId +"");//这个是没用的，改成输出角色ID
			signDic.Add("sign", aa);
			return Uri.EscapeUriString("http://www.yingchenfu.com/pay/apisubmit/apisubmit.php?" + Dic2Query(signDic));
		}

		//云鼎支付订单 
		public static string YunDingOrder(string memberid, string orderid, string amount, string orderdatetime, string payTypeStr, string payTypeUrl, string notifyurl, string returnurl, string signMd5Key, uint RoleId)
		{
            var signDic = new Dictionary<string, string>()
            {
                ["pid"] = memberid,
                ["type"] = payTypeStr,
                ["notify_url"] = notifyurl,
                ["return_url"] = returnurl,
                ["out_trade_no"] = orderid,
                ["name"] = $"{RoleId}",
                ["money"] = amount,
                ["sign_type"] = "MD5",
            };
			var nameStr = signDic["name"];
            var str = $"money={amount}&name={nameStr}&notify_url={notifyurl}&out_trade_no={orderid}&pid={memberid}&return_url={returnurl}&type={payTypeStr}{signMd5Key}";
            signDic.Add("sign", md5signaa(str).ToLower());

            return Uri.EscapeUriString(payTypeUrl) + "?" + Dic2Query(signDic);
		}
		public static string Order2(string memberid, string orderid, string amount, string orderdatetime, string paytype, string notifyurl, string signMd5Key, uint RoleId)
		{
            //customerid	商户ID	否	商户在网关系统上的商户号
            //sdcustomno	商户流水号	否	订单在商户系统中的流水号
            //orderAmount	支付金额	否	订单支付金额；单位:分(人民币)
            //cardno	支付方式	否	固定值32
            //noticeurl	通知商户Url	否	在网关返回信息时通知商户的地址，该地址不能带任何参数，否则异步通知会不成功
            //backurl	回调Url	否	在支付成功后跳转回商户的地址,该地址不能使用“&”参数
            //sign	md5签名	否	发送给网关的签名字符串,为以上参数加商户在网关系秘钥（key）一起按照顺序MD5加密并转为大写的字符串
            //mark	商户自定义信息	否	商户自定义信息，不能包含中文字符，因为可能编码不一致导致MD5加密结果不一致
            //(2)sign加密时参数要按照顺序，否则加密后无法通过验证,范例：
            //Md5str="customerid="&customerid&"&sdcustomno="&sdcustomno&"&orderAmount="&orderAmount&"&cardno="&cardno&"&noticeurl=" &noticeurl&"&backurl="&backurl.key
            //示例：(key值为：2s52e2e41s5e1sf2sf5e)
            //Md5str="customerid=123456&sdcustomno=2014060121365446&orderAmount=100&cardno=32&noticeurl=http://xxx.xxx/weixinNotify&backurl=http://xxx.xxx/payMS/weixinNotify2s52e2e41s5e1sf2sf5e"
            uint fengAmount = Convert.ToUInt32(amount) * 100;
            var signDic = new Dictionary<string, string>()
            {
                ["customerid"] = memberid,
                ["sdcustomno"] = orderid,
                ["orderAmount"] = $"{fengAmount}",
                ["cardno"] = "32",
                ["noticeurl"] = notifyurl,
                ["backurl"] = "aaa",
				["mark"] = $"{RoleId}",
            };
            var str = $"customerid={memberid}&sdcustomno={orderid}&orderAmount={fengAmount}&cardno=32&noticeurl={notifyurl}&backurl=aaa{signMd5Key}";
            signDic.Add("sign", md5signaa(str).ToUpper());

            return Uri.EscapeUriString(paytype) + "?" + Dic2Query(signDic);
		}

		public static string SignNotify(XinNotifyReq req, string signMd5Key)
		{
			Dictionary<string, string> dic = new Dictionary<string, string>
			{
				["customerid"] = req.customerid,
				["status"] = req.status,
				["sdpayno"] = req.sdpayno,
				["sdorderno"] = req.sdorderno,
				["total_fee"] = req.total_fee,
				["paytype"] = req.paytype,
				["remark"] = req.remark,				
			};			
			return Sign(dic, sorted: true, signMd5Key);
		}
		public static string Sign(Dictionary<string, string> dic, bool sorted, string secretKey)
		{
			string[] keys = dic.Keys.ToArray();
			if (sorted)
			{
				Array.Sort(keys, new Comparison<string>(string.CompareOrdinal));
			}
			StringBuilder sb = new StringBuilder();
			string[] array = keys;
			foreach (string key in array)
			{
				sb.Append(key);
				sb.Append('=');
				sb.Append(dic[key]);
				sb.Append('&');
			}
			sb.Remove(sb.Length - 1, 1);
			sb.Append(secretKey);

			return GetMD5Str.Md5Sum(sb.ToString());
		}

		private static string Dic2Query(IReadOnlyDictionary<string, string> dic)
		{
			StringBuilder sb = new StringBuilder();
			foreach (KeyValuePair<string, string> item in dic)
			{
				item.Deconstruct(out var key, out var value);
				string i = key;
				string v = value;
				sb.Append(i);
				sb.Append('=');
				sb.Append(v);
				sb.Append('&');
			}
			if (sb.Length == 0)
			{
				return string.Empty;
			}
			return sb.ToString(0, sb.Length - 1);
		}
	}
}
