using System.Collections.Generic;
namespace Ddxy.GameServer.Data.Config
{
    public class BanChatConfig
    {
        public int numberLimit { get; set; }
        public List<string> numberList { get; set; }
        public List<string> wordList { get; set; }
    }
}