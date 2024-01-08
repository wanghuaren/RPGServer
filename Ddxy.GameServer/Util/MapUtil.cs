using System;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Config;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Util
{
    public static class MapUtil
    {
        public static Pos RandomPos(uint mapId)
        {
            ConfigService.Maps.TryGetValue(mapId, out var mapCfg);
            if (mapCfg == null) return null;
            ConfigService.Terrains.TryGetValue(mapCfg.Terrain, out var terrainCfg);
            if (terrainCfg == null) return null;
            var random = new Random();

            var x = random.Next(0, terrainCfg.Cols);
            var y = random.Next(0, terrainCfg.Rows);
            var pos = FindAvaliablePoint(x, y, terrainCfg);
            return pos;
        }

        public static Pos FindAvaliablePoint(int x, int y, TerrainConfig terrainCfg)
        {
            if (x >= terrainCfg.Cols) x = terrainCfg.Cols - 1;
            if (y >= terrainCfg.Rows) y = terrainCfg.Rows - 1;

            if (terrainCfg.Blocks[y][x] != 0) return new Pos {X = x, Y = y};

            var count = 1;
            while (true)
            {
                if (count > terrainCfg.Cols && count > terrainCfg.Rows) return null;
                // 往上偏count行
                if (y + count < terrainCfg.Rows && terrainCfg.Blocks[y + count][x] != 0)
                {
                    return new Pos {X = x, Y = y + count};
                }

                // 往右偏count列
                if (x + count < terrainCfg.Cols && terrainCfg.Blocks[y][x + count] != 0)
                {
                    return new Pos {X = x + count, Y = y};
                }

                // 往下偏count行
                if (y >= count && terrainCfg.Blocks[y - count][x] != 0)
                {
                    return new Pos {X = x, Y = y - count};
                }

                // 往左偏count行
                if (x >= count && terrainCfg.Blocks[y][x - count] != 0)
                {
                    return new Pos {X = x - count, Y = y};
                }

                // 往右上角
                if (y + count < terrainCfg.Rows && x + count < terrainCfg.Cols &&
                    terrainCfg.Blocks[y + count][x + count] != 0)
                {
                    return new Pos {X = x + count, Y = y + count};
                }

                // 往左下角
                if (y >= count && x > count &&
                    terrainCfg.Blocks[y - count][x - count] != 0)
                {
                    return new Pos {X = x - count, Y = y - count};
                }

                // 往右下角
                if (y >= count && x + count < terrainCfg.Cols &&
                    terrainCfg.Blocks[y - count][x + count] != 0)
                {
                    return new Pos {X = x + count, Y = y - count};
                }

                // 往左上角
                if (y + count < terrainCfg.Rows && x >= count &&
                    terrainCfg.Blocks[y + count][x - count] != 0)
                {
                    return new Pos {X = x - count, Y = y + count};
                }

                count++;
            }
        }
    }
}