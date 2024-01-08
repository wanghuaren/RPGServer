using System;
using System.Collections.Generic;

namespace Ddxy.GrainInterfaces.Core
{
    [Serializable]
    public class Roles
    {
        /// <summary>
        /// 本次激活的RoleId
        /// </summary>
        public uint Last { get; set; }
        
        /// <summary>
        /// 该用户所有的角色id及其所在区服id
        /// </summary>
        public Dictionary<uint, uint> All { get; set; }
    }
}