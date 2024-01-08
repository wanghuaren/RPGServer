namespace Ddxy.GateServer.Option
{
    public class AppOptions
    {
        public int MaxConcurrentRequests { get; set; }
        public int RequestQueueLimit { get; set; }
        public string StreamId { get; set; }
    }
}