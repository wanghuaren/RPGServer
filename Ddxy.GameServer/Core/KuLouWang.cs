using System;
using System.Collections.Generic;
using Ddxy.Common.Utils;

namespace Ddxy.GameServer.Core
{
    public class KuLouWang : IDisposable
    {
        public uint CfgId { get; }

        public uint MapId { get; }

        public uint OnlyId { get; private set; }

        public int MapX { get; private set; }

        public int MapY { get; private set; }

        public List<uint> Applies { get; private set; }

        public IDisposable BattleWaiter { get; set; }

        public uint CreateTime;

        public KuLouWang(uint cfgId, uint mapId)
        {
            CfgId = cfgId;
            MapId = mapId;

            OnlyId = 0;
            Applies = new List<uint>();
        }

        public void Dispose()
        {
            Applies.Clear();
            Applies = null;
        }

        public void Reset()
        {
            Applies.Clear();
            BattleWaiter?.Dispose();
            BattleWaiter = null;
        }

        public void Dead()
        {
            OnlyId = 0;
            Reset();
        }

        public void Birth(uint onlyId, int mapX, int mapY)
        {
            OnlyId = onlyId;
            MapX = mapX;
            MapY = mapY;
            CreateTime = TimeUtil.TimeStamp;
        }
    }
}