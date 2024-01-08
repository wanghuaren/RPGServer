using System;
using System.Threading.Tasks;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.SectWar
{
    public class SectWarCannon : IDisposable
    {
        /// <summary>
        /// 抢占者
        /// </summary>
        public SectWarMember Graber { get; set; }

        /// <summary>
        /// 抢占者抢占后开始计时
        /// </summary>
        public IDisposable Timer { get; set; }

        // 龙神大炮的点燃时间为20s
        public const int Seconds = 20;
        
        public uint GrabTime { get; set; }

        public void Reset()
        {
            Graber = null;
            Timer?.Dispose();
            Timer = null;
            GrabTime = 0;
        }

        public void Dispose()
        {
            Graber = null;
            Timer?.Dispose();
            Timer = null;
            GrabTime = 0;
        }

        public ValueTask<int> Battle(SectWarMember member)
        {
            if (Graber == null || member == null) return new ValueTask<int>(1);

            Graber.BattleRoleId = member.Id;
            member.BattleRoleId = Graber.Id;

            return Graber.StartPvp(member.Id, BattleType.SectWarCannon);
        }
    }
}