namespace Ddxy.GameServer.Data.Vo
{
    public class RoleSsjlVo
    {
        // 区服ID
        public uint ServerId { get; set; }
        // 赛季
        public uint Season { get; set; }
        // 是否已经报名
        public bool Signed { get; set; }
        // 是否已经开始抓捕?
        public bool Started { get; set; }
        // 下次抓捕倒计时
        public uint NextTime { get; set; }
        // 结束剩余时间
        public uint EndTime { get; set; }
        // 神兽ID
        public uint ShenShouId { get; set; }
    }
}