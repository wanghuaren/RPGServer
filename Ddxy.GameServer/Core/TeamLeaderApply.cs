using System;
using System.Collections.Generic;

namespace Ddxy.GameServer.Core
{
    public class TeamLeaderApply : IDisposable
    {
        /// <summary>
        /// 申请者角色id
        /// </summary>
        public uint RoleId { get; }
        
        /// <summary>
        /// 倒计时
        /// </summary>
        public IDisposable Timer { get; set; }
        
        /// <summary>
        /// 已经明确拒绝的角色id
        /// </summary>
        public List<uint> Refuse { get; set; }

        public TeamLeaderApply(uint roleId)
        {
            RoleId = roleId;
            Refuse = new List<uint>(4);
        }

        public void Dispose()
        {
            Timer?.Dispose();
            Timer = null;
            
            Refuse?.Clear();
            Refuse = null;
        }
    }
}