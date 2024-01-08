using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.SectWar
{
    /// <summary>
    /// 比武场
    /// </summary>
    public class SectWarArena : IDisposable
    {
        /// <summary>
        /// 发起者
        /// </summary>
        public SectWarMember Player1 { get; set; }

        /// <summary>
        /// 接受者
        /// </summary>
        public SectWarMember Player2 { get; set; }

        public List<Tuple<uint, uint>> History { get; private set; }

        public IDisposable ForceTimer { get; set; }

        public SectWarArena()
        {
            History = new List<Tuple<uint, uint>>(3);
        }

        public void Reset(bool clearHistory)
        {
            Player1 = null;
            Player2 = null;
            ForceTimer?.Dispose();
            ForceTimer = null;
            if (clearHistory) History?.Clear();
        }

        public ValueTask<int> Battle()
        {
            if (Player1 == null || Player2 == null) return new ValueTask<int>(1);

            History.Add(new Tuple<uint, uint>(Player1.Id, Player2.Id)); //记录比武双方
            Player1.BattleRoleId = Player2.Id;
            Player2.BattleRoleId = Player1.Id;
            ForceTimer?.Dispose();
            ForceTimer = null;
            return Player1.StartPvp(Player2.Id, BattleType.SectWarArena);
        }

        public bool CheckEnable(uint roleId)
        {
            if (History.Count == 0) return true;
            // 上一场参加的玩家不能继续参加
            var (p1, p2) = History[^1];
            return p1 != roleId && p2 != roleId;
        }

        public void Dispose()
        {
            Player1 = null;
            Player2 = null;
            History?.Clear();
            History = null;
            ForceTimer?.Dispose();
            ForceTimer = null;
        }
    }
}