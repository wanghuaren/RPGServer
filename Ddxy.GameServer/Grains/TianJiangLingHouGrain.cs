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
    /// 天降灵猴，每天12点-18点, 活动开启时会生成50个灵猴，每个灵猴可以被打5次
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class TianJiangLingHouGrain : Grain, ITianJiangLingHouGrain
    {
        private uint _serverId;
        private IServerGrain _serverGrain;

        private bool _isOpen;
        private Random _random;
        private Dictionary<uint, int> _monkeys; //记录每个Money的NpcOnlyId和剩余次数
        private IDisposable _refreshTick;
        private ILogger<TianJiangLingHouGrain> _logger;
        private long _moneyPool; //灵猴资金池

        private const string Name = "天降灵猴";

        public TianJiangLingHouGrain(ILogger<TianJiangLingHouGrain> logger)
        {
            _logger = logger;
        }

        public override async Task OnActivateAsync()
        {
            _serverId = (uint) this.GetPrimaryKeyLong();
            _serverGrain = GrainFactory.GetGrain<IServerGrain>(_serverId);
            _isOpen = false;
            _random = new Random();
            _monkeys = new Dictionary<uint, int>(50);
            _moneyPool = 0L;

            await Task.CompletedTask;

            var now = DateTimeOffset.Now;
            var m = now.Minute;
            var s = now.Second;
            var delay = (59 - m) * 60 + (60 - s);
            if (m == 0 && s < 5) delay = 1;
            // 固定1小时刷新一次
            _refreshTick = RegisterTimer(OnActivityOpen, null, TimeSpan.FromSeconds(delay), TimeSpan.FromHours(1));

            // TODO 临时测试
            // _ = OnActivityOpen(null);
        }

        public override async Task OnDeactivateAsync()
        {
            _refreshTick?.Dispose();
            _refreshTick = null;
            _serverGrain = null;
            _monkeys.Clear();
            _monkeys = null;
            _logger = null;
            await Task.CompletedTask;
        }

        public Task StartUp()
        {
            return Task.CompletedTask;
        }

        public Task ShutDown()
        {
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public ValueTask<bool> IsOpen()
        {
            return new ValueTask<bool>(_isOpen);
        }

        public async ValueTask<int> Fight(uint roleId, uint npcOnlyId)
        {
            await Task.CompletedTask;

            if (!_monkeys.ContainsKey(npcOnlyId)) return 1;
            // 检查灵猴的剩余次数
            _monkeys.TryGetValue(npcOnlyId, out var cnt);
            if (cnt <= 0) return 1;

            cnt--;
            _monkeys[npcOnlyId] = cnt;

            return 0;
        }

        public async Task OnBattleResult(uint npcOnlyId, long win)
        {
            // 统计奖金池
            _moneyPool += win;
            if (_monkeys.TryGetValue(npcOnlyId, out var cnt) && cnt <= 0)
            {
                await _serverGrain.DeleteNpc(npcOnlyId);
            }
        }

        private async Task OnActivityOpen(object args)
        {
            if (_isOpen)
            {
                await OnActivityClose(false);
                return;
            }

            _isOpen = true;

            _monkeys.Clear();

            // 创建50个灵猴(注意不要重复在同一个位置)
            var copyList = new List<MapPosInfo>(Maps);
            for (var i = 0; i < 50; i++)
            {
                var idx = _random.Next(copyList.Count);
                var mapPos = copyList[idx];
                // 本次不再生成该索引
                copyList.RemoveAt(idx);

                // 创建Npc
                var request = new CreateNpcRequest
                {
                    MapId = mapPos.MapId,
                    MapX = mapPos.MapX,
                    MapY = mapPos.MapY,
                    CfgId = GameDefine.LingHouNpcCfgId,
                    Owner = new NpcOwner
                    {
                        Type = NpcOwnerType.Activity,
                        Value = (uint) ActivityId.TianJiangLingHou
                    }
                };
                var onlyId = await _serverGrain.CreateNpc(new Immutable<byte[]>(Packet.Serialize(request)));
                // 每个猴子可以被打10次, 打完10次后就消失
                _monkeys.Add(onlyId, 10);
            }

            Broadcast(GameCmd.S2CChat, new S2C_Chat
            {
                Msg = new ChatMessage
                {
                    Type = ChatMessageType.System,
                    Msg = "世界各地跑来好多偷钱的灵猴，请各位少侠前去驱赶"
                }
            });

            _logger.LogDebug($"{Name}({_serverId}) 开启");
            await Task.CompletedTask;
        }

        private async Task OnActivityClose(bool notice)
        {
            if (!_isOpen) return;
            _isOpen = false;

            // 清空灵猴
            foreach (var k in _monkeys.Keys)
            {
                _ = _serverGrain.DeleteNpc(k);
            }

            if (notice)
            {
                Broadcast(GameCmd.S2CChat, new S2C_Chat
                {
                    Msg = new ChatMessage
                    {
                        Type = ChatMessageType.System,
                        Msg = "灵猴们在众少侠的努力下，被赶走了！世界恢复了平静祥和"
                    }
                });
            }

            _logger.LogDebug($"{Name}({_serverId}) 结束");
            await Task.CompletedTask;
        }

        /// <summary>
        /// 全服广播
        /// </summary>
        private void Broadcast(GameCmd cmd, IMessage msg)
        {
            _ = _serverGrain.Broadcast(new Immutable<byte[]>(Packet.Serialize(cmd, msg)));
        }


        /// <summary>
        /// 灵猴可能出现的地图及其位置
        /// </summary>
        private static readonly List<MapPosInfo> Maps = new List<MapPosInfo>
        {
            // 1007 大唐边境
            new MapPosInfo {MapId = 1007, MapX = 133, MapY = 27},
            new MapPosInfo {MapId = 1007, MapX = 21, MapY = 111},
            new MapPosInfo {MapId = 1007, MapX = 76, MapY = 97},
            new MapPosInfo {MapId = 1007, MapX = 5, MapY = 65},
            new MapPosInfo {MapId = 1007, MapX = 165, MapY = 95},
            // 1011 长安城
            new MapPosInfo {MapId = 1011, MapX = 256, MapY = 51},
            new MapPosInfo {MapId = 1011, MapX = 71, MapY = 5},
            new MapPosInfo {MapId = 1011, MapX = 253, MapY = 74},
            new MapPosInfo {MapId = 1011, MapX = 176, MapY = 130},
            new MapPosInfo {MapId = 1011, MapX = 73, MapY = 138},
            new MapPosInfo {MapId = 1011, MapX = 106, MapY = 125},
            new MapPosInfo {MapId = 1011, MapX = 7, MapY = 67},
            new MapPosInfo {MapId = 1011, MapX = 32, MapY = 69},
            new MapPosInfo {MapId = 1011, MapX = 2, MapY = 107},
            new MapPosInfo {MapId = 1011, MapX = 28, MapY = 138},
            // 1004 大唐境内
            new MapPosInfo {MapId = 1004, MapX = 137, MapY = 90},
            new MapPosInfo {MapId = 1004, MapX = 141, MapY = 22},
            new MapPosInfo {MapId = 1004, MapX = 88, MapY = 6},
            new MapPosInfo {MapId = 1004, MapX = 4, MapY = 74},
            // 1010 东海渔村
            new MapPosInfo {MapId = 1010, MapX = 115, MapY = 63},
            new MapPosInfo {MapId = 1010, MapX = 52, MapY = 45},
            new MapPosInfo {MapId = 1010, MapX = 113, MapY = 97},
            new MapPosInfo {MapId = 1010, MapX = 75, MapY = 108},
            new MapPosInfo {MapId = 1010, MapX = 24, MapY = 103},
            new MapPosInfo {MapId = 1010, MapX = 94, MapY = 133},
            // 1006 万寿山
            new MapPosInfo {MapId = 1006, MapX = 80, MapY = 149},
            new MapPosInfo {MapId = 1006, MapX = 84, MapY = 149},
            new MapPosInfo {MapId = 1006, MapX = 88, MapY = 149},
            new MapPosInfo {MapId = 1006, MapX = 92, MapY = 149},
            new MapPosInfo {MapId = 1006, MapX = 96, MapY = 148},
            new MapPosInfo {MapId = 1006, MapX = 96, MapY = 144},
            new MapPosInfo {MapId = 1006, MapX = 91, MapY = 144},
            new MapPosInfo {MapId = 1006, MapX = 84, MapY = 144},
            new MapPosInfo {MapId = 1006, MapX = 78, MapY = 144},
            new MapPosInfo {MapId = 1006, MapX = 66, MapY = 139},
            new MapPosInfo {MapId = 1006, MapX = 72, MapY = 139},
            new MapPosInfo {MapId = 1006, MapX = 76, MapY = 139},
            new MapPosInfo {MapId = 1006, MapX = 82, MapY = 139},
            new MapPosInfo {MapId = 1006, MapX = 66, MapY = 134},
            new MapPosInfo {MapId = 1006, MapX = 75, MapY = 134},
            new MapPosInfo {MapId = 1006, MapX = 86, MapY = 134},
            new MapPosInfo {MapId = 1006, MapX = 92, MapY = 134},
            new MapPosInfo {MapId = 1006, MapX = 15, MapY = 134},
            new MapPosInfo {MapId = 1006, MapX = 9, MapY = 35},
            // 1015 蟠桃园
            new MapPosInfo {MapId = 1015, MapX = 41, MapY = 12},
            new MapPosInfo {MapId = 1015, MapX = 5, MapY = 19},
            new MapPosInfo {MapId = 1015, MapX = 76, MapY = 56},
            new MapPosInfo {MapId = 1015, MapX = 102, MapY = 66},
            new MapPosInfo {MapId = 1015, MapX = 43, MapY = 67},
            new MapPosInfo {MapId = 1015, MapX = 9, MapY = 125},
            new MapPosInfo {MapId = 1015, MapX = 4, MapY = 48},
            // 1016 御马监
            new MapPosInfo {MapId = 1016, MapX = 9, MapY = 16},
            new MapPosInfo {MapId = 1016, MapX = 137, MapY = 40},
            new MapPosInfo {MapId = 1016, MapX = 141, MapY = 65},
            new MapPosInfo {MapId = 1016, MapX = 78, MapY = 90},
            new MapPosInfo {MapId = 1016, MapX = 86, MapY = 34},
            // 1014 灵兽村
            new MapPosInfo {MapId = 1014, MapX = 8, MapY = 97},
            new MapPosInfo {MapId = 1014, MapX = 2, MapY = 60},
            new MapPosInfo {MapId = 1014, MapX = 148, MapY = 16},
            new MapPosInfo {MapId = 1014, MapX = 140, MapY = 6},
            new MapPosInfo {MapId = 1014, MapX = 88, MapY = 99},
            // 1017 傲来国
            new MapPosInfo {MapId = 1017, MapX = 8, MapY = 28},
            new MapPosInfo {MapId = 1017, MapX = 8, MapY = 77},
            new MapPosInfo {MapId = 1017, MapX = 10, MapY = 75},
            new MapPosInfo {MapId = 1017, MapX = 1, MapY = 72},
            new MapPosInfo {MapId = 1017, MapX = 34, MapY = 112},
            new MapPosInfo {MapId = 1017, MapX = 32, MapY = 97},
            new MapPosInfo {MapId = 1017, MapX = 100, MapY = 80},
            new MapPosInfo {MapId = 1017, MapX = 56, MapY = 63},
            new MapPosInfo {MapId = 1017, MapX = 68, MapY = 33},
            new MapPosInfo {MapId = 1017, MapX = 9, MapY = 8},
            new MapPosInfo {MapId = 1017, MapX = 27, MapY = 6},
            new MapPosInfo {MapId = 1017, MapX = 43, MapY = 6},
            new MapPosInfo {MapId = 1017, MapX = 59, MapY = 6},
            new MapPosInfo {MapId = 1017, MapX = 113, MapY = 6},
            new MapPosInfo {MapId = 1017, MapX = 142, MapY = 20},
            new MapPosInfo {MapId = 1017, MapX = 97, MapY = 37},
            new MapPosInfo {MapId = 1017, MapX = 137, MapY = 54},
            // 1008 白骨山
            new MapPosInfo {MapId = 1008, MapX = 105, MapY = 64},
            new MapPosInfo {MapId = 1008, MapX = 10, MapY = 40},
            new MapPosInfo {MapId = 1008, MapX = 48, MapY = 13},
            // 1005 方寸山
            new MapPosInfo {MapId = 1005, MapX = 14, MapY = 20},
            new MapPosInfo {MapId = 1005, MapX = 49, MapY = 55},
            new MapPosInfo {MapId = 1005, MapX = 59, MapY = 70},
            // 1019 平顶山
            new MapPosInfo {MapId = 1019, MapX = 182, MapY = 7},
            new MapPosInfo {MapId = 1019, MapX = 121, MapY = 2},
            new MapPosInfo {MapId = 1019, MapX = 147, MapY = 19},
            new MapPosInfo {MapId = 1019, MapX = 178, MapY = 9},
            new MapPosInfo {MapId = 1019, MapX = 15, MapY = 82},
            new MapPosInfo {MapId = 1019, MapX = 2, MapY = 62},
            new MapPosInfo {MapId = 1019, MapX = 2, MapY = 29},
            new MapPosInfo {MapId = 1019, MapX = 29, MapY = 3},
            new MapPosInfo {MapId = 1019, MapX = 47, MapY = 124},
            new MapPosInfo {MapId = 1019, MapX = 191, MapY = 97},
            new MapPosInfo {MapId = 1019, MapX = 191, MapY = 40},
            new MapPosInfo {MapId = 1019, MapX = 175, MapY = 58},
            new MapPosInfo {MapId = 1019, MapX = 177, MapY = 77},
            new MapPosInfo {MapId = 1019, MapX = 146, MapY = 64},
            // 1003 北俱芦洲
            new MapPosInfo {MapId = 1003, MapX = 23, MapY = 69},
            new MapPosInfo {MapId = 1003, MapX = 51, MapY = 74},
            new MapPosInfo {MapId = 1003, MapX = 84, MapY = 6},
            new MapPosInfo {MapId = 1003, MapX = 135, MapY = 22},
            new MapPosInfo {MapId = 1003, MapX = 139, MapY = 5},
            new MapPosInfo {MapId = 1003, MapX = 139, MapY = 45},
            new MapPosInfo {MapId = 1003, MapX = 139, MapY = 62},
            new MapPosInfo {MapId = 1003, MapX = 138, MapY = 80}
        };
    }
}