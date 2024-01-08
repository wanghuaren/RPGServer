using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    /// <summary>
    /// 甄不亏, 整点刷新, 停留20分钟就跑
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class ZhenBuKuiGrain : Grain, IZhenBuKuiGrain
    {
        private bool _isActive;
        private uint _serverId;
        private IServerGrain _serverGrain;

        private Random _random;
        private Dictionary<uint, ShopItem> _goods; //当前商品

        private uint _npcOnlyId;
        private uint _mapId;
        private int _mapX;
        private int _mapY;

        private IDisposable _runAwayTick;
        private ILogger<ZhenBuKuiGrain> _logger;

        private const string Name = "甄不亏";

        public bool IsOpen => _npcOnlyId > 0;

        public ZhenBuKuiGrain(ILogger<ZhenBuKuiGrain> logger)
        {
            _logger = logger;
        }

        public override Task OnActivateAsync()
        {
            _serverId = (uint)this.GetPrimaryKeyLong();
            return Task.CompletedTask;
        }

        public override Task OnDeactivateAsync()
        {
            return ShutDown();
        }

        public Task StartUp()
        {
            if (_isActive) return Task.CompletedTask;
            _isActive = true;
            _serverGrain = GrainFactory.GetGrain<IServerGrain>(_serverId);

            _random = new Random();
            _goods = new Dictionary<uint, ShopItem>(10);
            LogInfo("激活成功");
            CheckOpenTime();
            return Task.CompletedTask;
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _runAwayTick?.Dispose();
            _runAwayTick = null;

            _goods?.Clear();
            _goods = null;

            _npcOnlyId = 0;
            _mapId = 0;
            _mapX = 0;
            _mapY = 0;

            _serverGrain = null;
            _isActive = false;
            LogInfo("注销成功");
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new ValueTask<bool>(_isActive);
        }

        public async Task Online(uint roleId)
        {
            if (!_isActive) return;
            if (IsOpen)
            {
                var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CZhenBuKuiState,
                    new S2C_ZhenBuKuiState
                    {
                        MapId = _mapId,
                        MapX = _mapX,
                        MapY = _mapY
                    })));
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 获取商品列表
        /// </summary>
        public Task<Immutable<byte[]>> GetItems()
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            if (!IsOpen) return Task.FromResult(new Immutable<byte[]>(null));

            var resp = new S2C_NpcShopItems
            {
                NpcCfgId = GameDefine.ZhenBuKuiNpcCfgId,
                List = { _goods.Values }
            };

            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(resp)));
        }

        /// <summary>
        /// 查询商品详情
        /// </summary>
        public Task<Immutable<byte[]>> GetItem(uint cfgId)
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            if (!IsOpen) return Task.FromResult(new Immutable<byte[]>(null));
            _goods.TryGetValue(cfgId, out var item);
            if (item == null) return Task.FromResult(new Immutable<byte[]>(null));
            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(item)));
        }

        /// <summary>
        /// 购买商品
        /// </summary>
        public Task<Immutable<byte[]>> BuyItem(uint cfgId, uint num)
        {
            if (!_isActive) return Task.FromResult(new Immutable<byte[]>(null));
            if (!IsOpen) return Task.FromResult(new Immutable<byte[]>(null));
            _goods.TryGetValue(cfgId, out var item);
            if (item == null) return Task.FromResult(new Immutable<byte[]>(null));

            var buyNum = (int)num;
            if (buyNum > item.Num) buyNum = item.Num;

            var resp = item.Clone();
            // 这里返回实际购买的数量
            resp.Num = buyNum;

            if (buyNum > 0)
            {
                item.Num -= buyNum;
                // 移除物品
                if (item.Num == 0) _goods.Remove(cfgId);
            }

            // 所有商品已售罄
            if (_goods.Count == 0)
            {
                _ = OnActivityClose(null);
            }

            return Task.FromResult(new Immutable<byte[]>(Packet.Serialize(resp)));
        }

        private void CheckOpenTime()
        {
            _isActive = false;
            if (!_isActive) return;
            if (IsOpen) return;

            // 中午12点或21点
            var now = DateTimeOffset.Now;
            var today12 = new DateTimeOffset(now.Year, now.Month, now.Day, 12, 0, 0, TimeSpan.FromHours(8));
            var today21 = new DateTimeOffset(now.Year, now.Month, now.Day, 21, 0, 0, TimeSpan.FromHours(8));
            var tomorrow12 = today12.AddDays(1);

            var nextOpenTime = now;
            if (now < today12)
            {
                nextOpenTime = today12;
            }
            else if (now > today12 && now < today21)
            {
                nextOpenTime = today21;
            }
            else if (now > today21)
            {
                nextOpenTime = tomorrow12;
            }

            var delayTs = nextOpenTime.Subtract(now);
            if (delayTs.TotalSeconds <= 0) delayTs = TimeSpan.FromSeconds(1);
            LogInfo($"{delayTs.Days}日{delayTs.Hours}时{delayTs.Minutes}分{delayTs.Seconds}秒后开启");

            // 防止Timer等待太久而休眠, 超过1个小时的等待就用1个小时后再次来检查时间
            if (delayTs.TotalHours >= 1)
            {
                RegisterTimer(_ =>
                {
                    CheckOpenTime();
                    return Task.CompletedTask;
                }, null, TimeSpan.FromHours(1), TimeSpan.FromMilliseconds(-1));
            }
            else
            {
                RegisterTimer(OnActivityOpen, null, delayTs, TimeSpan.FromMilliseconds(-1));
            }
        }

        private async Task OnActivityOpen(object args)
        {
            if (!_isActive) return;
            if (IsOpen) return;

            var mapPos = Maps[_random.Next(Maps.Count)];
            ConfigService.Maps.TryGetValue(mapPos.MapId, out var mapCfg);
            if (mapCfg == null) return;
            var mapName = mapCfg.Name;

            // 填充商品
            _goods.Clear();
            foreach (var (_, v) in ConfigService.ZhenBuKuiShopItems)
            {
                var item = new ShopItem
                {
                    CfgId = v.ItemId,
                    Price = v.Price,
                    Num = v.Num,
                    Type = (NpcShopItemType)v.Type,
                    CostType = (MoneyType)v.Cost
                };
                _goods.Add(item.CfgId, item);
            }

            // 创建Npc
            _npcOnlyId = await _serverGrain.CreateNpc(new Immutable<byte[]>(Packet.Serialize(new CreateNpcRequest
            {
                MapId = mapPos.MapId,
                MapX = mapPos.MapX,
                MapY = mapPos.MapY,
                CfgId = GameDefine.ZhenBuKuiNpcCfgId,
                Owner = new NpcOwner
                {
                    Type = NpcOwnerType.Activity,
                    Value = (uint)ActivityId.ZhenBuKui
                }
            })));
            _mapId = mapPos.MapId;
            _mapX = mapPos.MapX;
            _mapY = mapPos.MapY;

            Broadcast(GameCmd.S2CZhenBuKuiState, new S2C_ZhenBuKuiState
            {
                MapId = _mapId,
                MapX = _mapX,
                MapY = _mapY
            });

            _logger.LogDebug("{Name}({Sid}) 开启:{MapName}({X},{Y})", Name, _serverId, mapName, mapPos.MapX, mapPos.MapY);
            // 待20分钟就跑
            _runAwayTick?.Dispose();
            _runAwayTick = RegisterTimer(OnActivityClose, null, TimeSpan.FromMinutes(20), TimeSpan.FromSeconds(1));
            await Task.CompletedTask;
        }

        private async Task OnActivityClose(object args)
        {
            if (!_isActive) return;
            _runAwayTick?.Dispose();
            _runAwayTick = null;

            _ = _serverGrain.DeleteNpc(_npcOnlyId);
            _npcOnlyId = 0;
            _mapId = 0;
            _mapX = 0;
            _mapY = 0;

            Broadcast(GameCmd.S2CZhenBuKuiState, new S2C_ZhenBuKuiState
            {
                MapId = _mapId,
                MapX = _mapX,
                MapY = _mapY
            });

            _logger.LogDebug("{Name}({Sid}) 跑了", Name, _serverId);
            
            CheckOpenTime();
            await Task.CompletedTask;
        }

        /// <summary>
        /// 全服广播
        /// </summary>
        private void Broadcast(GameCmd cmd, IMessage msg)
        {
            if (!_isActive) return;
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(cmd, msg)));
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"甄不亏[{_mapId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"甄不亏[{_mapId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"甄不亏[{_mapId}]:{msg}");
        }

        /// <summary>
        /// 甄不亏可能出现的地图及其位置
        /// </summary>
        private static readonly List<MapPosInfo> Maps = new()
        {
            // 1007 大唐边境
            new MapPosInfo { MapId = 1007, MapX = 148, MapY = 44 },
            new MapPosInfo { MapId = 1007, MapX = 100, MapY = 24 },
            new MapPosInfo { MapId = 1007, MapX = 109, MapY = 5 },
            new MapPosInfo { MapId = 1007, MapX = 44, MapY = 14 },
            new MapPosInfo { MapId = 1007, MapX = 84, MapY = 104 },
            new MapPosInfo { MapId = 1007, MapX = 29, MapY = 83 },
            // 1011 长安城
            new MapPosInfo { MapId = 1011, MapX = 49, MapY = 36 },
            new MapPosInfo { MapId = 1011, MapX = 143, MapY = 66 },
            new MapPosInfo { MapId = 1011, MapX = 122, MapY = 34 },
            new MapPosInfo { MapId = 1011, MapX = 223, MapY = 34 },
            new MapPosInfo { MapId = 1011, MapX = 239, MapY = 113 },
            new MapPosInfo { MapId = 1011, MapX = 143, MapY = 84 },
            new MapPosInfo { MapId = 1011, MapX = 40, MapY = 9 },
            // 1004 大唐境内
            new MapPosInfo { MapId = 1004, MapX = 124, MapY = 74 },
            new MapPosInfo { MapId = 1004, MapX = 120, MapY = 49 },
            new MapPosInfo { MapId = 1004, MapX = 119, MapY = 37 },
            new MapPosInfo { MapId = 1004, MapX = 38, MapY = 47 },
            new MapPosInfo { MapId = 1004, MapX = 70, MapY = 14 },
            // 1010 东海渔村
            new MapPosInfo { MapId = 1010, MapX = 129, MapY = 47 },
            new MapPosInfo { MapId = 1010, MapX = 141, MapY = 34 },
            new MapPosInfo { MapId = 1010, MapX = 60, MapY = 22 },
            new MapPosInfo { MapId = 1010, MapX = 24, MapY = 9 },
            new MapPosInfo { MapId = 1010, MapX = 41, MapY = 53 },
            new MapPosInfo { MapId = 1010, MapX = 82, MapY = 89 },
            new MapPosInfo { MapId = 1010, MapX = 42, MapY = 103 },
            new MapPosInfo { MapId = 1010, MapX = 114, MapY = 114 },
            // 1006 万寿山
            new MapPosInfo { MapId = 1006, MapX = 56, MapY = 73 },
            new MapPosInfo { MapId = 1006, MapX = 98, MapY = 48 },
            new MapPosInfo { MapId = 1006, MapX = 93, MapY = 8 },
            new MapPosInfo { MapId = 1006, MapX = 18, MapY = 30 },
            new MapPosInfo { MapId = 1006, MapX = 109, MapY = 94 },
            new MapPosInfo { MapId = 1006, MapX = 74, MapY = 136 },
            // 1015 蟠桃园
            new MapPosInfo { MapId = 1015, MapX = 70, MapY = 30 },
            new MapPosInfo { MapId = 1015, MapX = 37, MapY = 26 },
            new MapPosInfo { MapId = 1015, MapX = 14, MapY = 57 },
            new MapPosInfo { MapId = 1015, MapX = 39, MapY = 76 },
            new MapPosInfo { MapId = 1015, MapX = 53, MapY = 103 },
            new MapPosInfo { MapId = 1015, MapX = 14, MapY = 120 },
            new MapPosInfo { MapId = 1015, MapX = 90, MapY = 56 },
            // 1016 御马监
            new MapPosInfo { MapId = 1016, MapX = 34, MapY = 63 },
            new MapPosInfo { MapId = 1016, MapX = 112, MapY = 63 },
            new MapPosInfo { MapId = 1016, MapX = 96, MapY = 23 },
            new MapPosInfo { MapId = 1016, MapX = 68, MapY = 23 },
            new MapPosInfo { MapId = 1016, MapX = 37, MapY = 26 },
            // 1014 灵兽村
            new MapPosInfo { MapId = 1014, MapX = 50, MapY = 36 },
            new MapPosInfo { MapId = 1014, MapX = 14, MapY = 55 },
            new MapPosInfo { MapId = 1014, MapX = 64, MapY = 58 },
            new MapPosInfo { MapId = 1014, MapX = 113, MapY = 64 },
            new MapPosInfo { MapId = 1014, MapX = 117, MapY = 42 },
            new MapPosInfo { MapId = 1014, MapX = 138, MapY = 12 },
            // 1017 傲来国
            new MapPosInfo { MapId = 1017, MapX = 57, MapY = 11 },
            new MapPosInfo { MapId = 1017, MapX = 20, MapY = 10 },
            new MapPosInfo { MapId = 1017, MapX = 33, MapY = 42 },
            new MapPosInfo { MapId = 1017, MapX = 56, MapY = 64 },
            new MapPosInfo { MapId = 1017, MapX = 80, MapY = 71 },
            new MapPosInfo { MapId = 1017, MapX = 93, MapY = 90 },
            new MapPosInfo { MapId = 1017, MapX = 44, MapY = 103 },
            new MapPosInfo { MapId = 1017, MapX = 12, MapY = 74 },
            new MapPosInfo { MapId = 1017, MapX = 98, MapY = 29 },
            new MapPosInfo { MapId = 1017, MapX = 133, MapY = 51 },
            // 1008 白骨山
            new MapPosInfo { MapId = 1008, MapX = 80, MapY = 13 },
            new MapPosInfo { MapId = 1008, MapX = 79, MapY = 52 },
            new MapPosInfo { MapId = 1008, MapX = 57, MapY = 70 },
            new MapPosInfo { MapId = 1008, MapX = 36, MapY = 70 },
            new MapPosInfo { MapId = 1008, MapX = 30, MapY = 10 },
            // 1005 方寸山
            new MapPosInfo { MapId = 1005, MapX = 34, MapY = 22 },
            new MapPosInfo { MapId = 1005, MapX = 24, MapY = 65 },
            new MapPosInfo { MapId = 1005, MapX = 48, MapY = 48 },
            new MapPosInfo { MapId = 1005, MapX = 68, MapY = 78 },
            new MapPosInfo { MapId = 1005, MapX = 94, MapY = 89 },
            new MapPosInfo { MapId = 1005, MapX = 101, MapY = 28 },
            // 1019 平顶山
            new MapPosInfo { MapId = 1019, MapX = 154, MapY = 91 },
            new MapPosInfo { MapId = 1019, MapX = 112, MapY = 104 },
            new MapPosInfo { MapId = 1019, MapX = 101, MapY = 60 },
            new MapPosInfo { MapId = 1019, MapX = 78, MapY = 39 },
            new MapPosInfo { MapId = 1019, MapX = 28, MapY = 22 },
            new MapPosInfo { MapId = 1019, MapX = 32, MapY = 55 },
            new MapPosInfo { MapId = 1019, MapX = 110, MapY = 17 },
            // 1003 北俱芦洲
            new MapPosInfo { MapId = 1003, MapX = 24, MapY = 37 },
            new MapPosInfo { MapId = 1003, MapX = 61, MapY = 42 },
            new MapPosInfo { MapId = 1003, MapX = 102, MapY = 48 },
            new MapPosInfo { MapId = 1003, MapX = 120, MapY = 17 },
            new MapPosInfo { MapId = 1003, MapX = 87, MapY = 23 }
        };
    }
}