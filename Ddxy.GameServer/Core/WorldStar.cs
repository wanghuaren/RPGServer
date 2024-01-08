using System;
using System.Collections.Generic;
using Ddxy.Common.Utils;

namespace Ddxy.GameServer.Core
{
    public class WorldStar : IDisposable
    {
        public uint CfgId { get; }

        public uint MapId { get; }

        public byte Level { get; }

        public uint OnlyId { get; private set; }

        public int MapX { get; private set; }

        public int MapY { get; private set; }

        public List<uint> Applies { get; private set; }

        public IDisposable BattleWaiter { get; set; }

        public uint CreateTime;

        public WorldStar(uint cfgId, uint mapId, byte level)
        {
            CfgId = cfgId;
            MapId = mapId;
            Level = level;

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