using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.International.Converters.PinYinConverter;
using ToolGood.Words;

namespace Ddxy.GameServer.Util
{
    public static class TextFilter
    {
        private static readonly StringSearchEx2 Search = new StringSearchEx2();

        private const string UrlPattern = @"^(https?|ftp|file|ws)?://([\w-]+\.)+[\w-]+(/[\w- ./?%&=]*)?$";

        private static readonly string[] LimitBroadcasts =
        {
            "qq",
            "QQ",
            "扣扣",
            "球",
            "企鹅",
            "微信",
            "威信",
            "v信",
            "V信",
            "君羊",
            "君.羊",
            "龙马服",
            "残端",
            "私服",
            "咸鱼",
            "闲鱼",
            "闲玉",
            "无限",
            "无线",
            "送仙玉",
            "百万玉",
            "100万玉",
            "百万仙玉",
            "无限玉",
            "新版",
            "福利",
            "新开",
            "新端",
            "新服",
            "裙",
            "峮",
            "群",
            "㪊",
            "wx",
            "vx",
            "://",
            ".com",
            ".cn",
            ".org",
            ".xyz",
            "孟极",
            "传送",
            "氪金",
            "时空门",
            "无限玉",
            "无限仙玉",
            "墨城",
            "mo城",
            "定制",
            "私聊",
            "同款",
            "新区",
            "满档",
            "㊀",
            "㊁",
            "㊂",
            "㊃",
            "㊄",
            "㊅",
            "㊆",
            "㊇",
            "㊈",
        };

        private static readonly string[] LimitPinYins =
        {
            "tong", "kuan", "you", "xi", "song", "xian", "yu", "shou", "chong", "qun", "kou", "qiu"
        };

        private static readonly char[] NumChars =
        {
            'q', 'Q', 'O',
            '1', '2', '3', '4', '5', '6', '7', '8', '9', '0',
            '１', '２', '３', '４', '５', '６', '７', '８', '９', '０',
            '一', '二', '三', '四', '五', '六', '七', '八', '九', '零', '〇',
            '①', '②', '③', '④', '⑤', '⑥', '⑦', '⑧', '⑨',
            '⁰', '¹', '²', '³', '⁴', '⁵', '⁶', '⁷', '⁸', '⁹',
            '₀', '₁', '₂', '₃', '₄', '₅', '₆', '₇', '₈', '₉',
            '⑴', '⑵', '⑶', '⑷', '⑸', '⑹', '⑺', '⑻', '⑼',
            '⒈', '⒉', '⒊', '⒋', '⒌', '⒍', '⒎', '⒏', '⒐',
            'Ⅰ', 'Ⅱ', 'Ⅲ', 'Ⅳ', 'Ⅵ', 'Ⅶ', 'Ⅷ', 'Ⅸ',
            '❶', '❷', '❸', '❹', '❺', '❻', '❼', '❽', '❾',
            '➊', '➋', '➌', '➍', '➎', '➏', '➐', '➑', '➒', '➓',
            '⓵', '⓶', '⓷', '⓸', '⓹', '⓺', '⓻', '⓼', '⓽',
            '㊀', '㊁', '㊂', '㊃', '㊄', '㊅', '㊆', '㊇', '㊈',
            '㈠', '㈡', '㈢', '㈣', '㈤', '㈥', '㈦', '㈧', '㈨',
            '壹', '贰', '叁', '叄', '肆', '伍', '陆', '柒', '捌', '扒', '玖',
            '伞', '溜', '君', '羊', '久', '巴',
            '玉', '仙', '裙', '群', '西', '游',
            '⑬', '⑭', '㉑', '⑱', '㉒', '⑰', '⑯', '⑮', '⑳', '㉔', '➉', '㉓', '⑲', '㉕', 
            '㉖', '㉘', '㉙', '㉗', '㉚', '㉝', '㉛', '㉜', '㉞', '㉟', '㊱', '㊲', '㊳', 
            '㊵', '㊴', '㊷', '㊸', '㊿', '㊺', '㊼', '㊶', '㊹', '㊽', '㊻', '㊾',
        };

        public static bool CheckLimitWord(string text)
        {
            var numCnt = 0;
            foreach (var c in text)
            {
                if (NumChars.Contains(c))
                {
                    numCnt++;
                    if (numCnt >= 3)
                    {
                        return false;
                    }
                }
            }

            // 检查url
            var reg = new Regex(UrlPattern);
            if (reg.IsMatch(text)) return false;

            text = Regex.Replace(text, @"\s", "");
            if (LimitBroadcasts.Any(text.Contains)) return false;

            // 转换为拼音, 检查xian yu
            // if (CheckPinYin(text)) return false;

            return true;
        }

        static TextFilter()
        {
            Search.SetKeywords(new List<string>
            {
                "系统",
                "官方",
                "测试",
                "gm",
                "Gm",
                "GM",
                "管理",
                "内测",
                "内部",
                "技术",
                "公告",
                "公测",
                "垃圾",
                "毛泽东",
                "周恩来",
                "恩来",
                "刘少奇",
                "少奇",
                "习近平",
                "近平",
                "习大大",
                "习仲勋",
                "李克强",
                "朱德",
                "丁磊",
                "你妈",
                "共产党",
                "gcd",
                "大话",
                "西游",
                "私服",
                "残端",
                "Q",
                "q",
                "扣扣",
                "微信",
                "a",
                "b",
                "c",
                "d",
                "e",
                "f",
                "g",
                "h",
                "i",
                "j",
                "k",
                "l",
                "m",
                "n",
                "o",
                "p",
                "q",
                "r",
                "s",
                "t",
                "u",
                "v",
                "w",
                "x",
                "y",
                "z",
                "A",
                "B",
                "C",
                "D",
                "E",
                "F",
                "G",
                "H",
                "I",
                "J",
                "K",
                "L",
                "M",
                "N",
                "O",
                "P",
                "Q",
                "R",
                "S",
                "T",
                "U",
                "V",
                "W",
                "X",
                "Y",
                "Z"
            });
        }

        /// <summary>
        /// 检测是否包含非法字符
        /// </summary>
        public static bool HasDirty(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return Search.ContainsAny(text);
        }

        /// <summary>
        /// 过滤非法字符，变成*
        /// </summary>
        public static string Filte(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return text;
            return Search.Replace(text);
        }

        public static string GetPinYin(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return string.Empty;
            var dic = new Dictionary<string, bool>();
            foreach (var ch in str)
            {
                try
                {
                    var cc = new ChineseChar(ch);
                    foreach (var py in cc.Pinyins)
                    {
                        if (string.IsNullOrWhiteSpace(py)) continue;
                        // 最后1个是数字
                        var tmp = py.ToLower().Substring(0, py.Length - 1);
                        dic.TryAdd(tmp, true);
                    }
                }
                catch
                {
                    // ignore
                }
            }

            return string.Join(" ", dic.Keys);
        }

        public static bool CheckPinYin(string str)
        {
            var pinyin = GetPinYin(str);
            var cnt = 0;
            foreach (var cc in LimitPinYins)
            {
                if (pinyin.Contains(cc))
                {
                    cnt++;
                    if (cnt >= 4) break;
                }
            }

            return cnt >= 4;
        }
    }
}