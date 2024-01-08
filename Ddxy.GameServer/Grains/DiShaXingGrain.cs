using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    /// <summary>
    /// 地煞星, 整点刷新, 每30分钟刷新一次
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class DiShaXingGrain : Grain, IDiShaXingGrain
    {
        private uint _serverId;
        private IServerGrain _serverGrain;

        private bool _isActive;

        private IDisposable _refreshTick;
        private ILogger<DiShaXingGrain> _logger;

        private const string Name = "地煞星";

        public DiShaXingGrain(ILogger<DiShaXingGrain> logger)
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

            var now = DateTimeOffset.Now;
            var m = now.Minute;
            var s = now.Second;
            if (m >= 30) m -= 30;
            var delay = (29 - m) * 60 + (60 - s);
            if ((m is 30 or 0) && s <= 5) delay = 1;
            // 每30分钟刷新
            _refreshTick = RegisterTimer(Refresh, null, TimeSpan.FromSeconds(delay), TimeSpan.FromMinutes(30));

            // FIXME: 激活后马上刷新1次
            _ = Refresh(null);
            LogInfo("激活成功");
            return Task.CompletedTask;
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _isActive = false;
            _refreshTick?.Dispose();
            _refreshTick = null;
            _serverGrain = null;

            foreach (var ws in _stars)
            {
                ws.Dispose();
            }
            _stars.Clear();
            _stars = null;

            LogInfo("注销成功");
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new(_isActive);
        }

        public ValueTask<bool> IsStar(uint onlyId)
        {
            if (!_isActive) return new ValueTask<bool>(false);
            var ret = _stars.Exists(p => p.OnlyId == onlyId);
            return new ValueTask<bool>(ret);
        }

        public async ValueTask<int> ApplyChallenge(uint onlyId, uint roleId, uint roleStar)
        {
            if (!_isActive) return 1;
            var ws = _stars.FirstOrDefault(p => p.OnlyId == onlyId);
            if (ws == null) return 1;
            if (ws.Applies.Count > 0) return 2;
            if (ws.Level > roleStar) return 3;
            // 检测当前是否在挑战其他星
            var xs = _stars.Exists(it => it.Applies != null && it.Applies.Contains(roleId));
            if (xs) return 4;

            ws.Applies.Add(roleId);

            // 5s钟之后开始
            ws.BattleWaiter = RegisterTimer(async state =>
                {
                    if (!_isActive) return;
                    if (state is not WorldStar star) return;
                    star.BattleWaiter?.Dispose();
                    star.BattleWaiter = null;
                    if (star.Applies.Count == 0) return;
                    await TrigleStarBattle(star.OnlyId, star.Applies[0], star.Level);
                }, ws,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(1));

            await Task.CompletedTask;
            return 0;
        }

        public Task ChallengeResult(uint onlyId, bool win)
        {
            if (!_isActive) return Task.CompletedTask;
            var ws = _stars.FirstOrDefault(p => p.OnlyId == onlyId);
            if (ws is { OnlyId: > 0 })
            {
                if (win)
                {
                    _ = _serverGrain.DeleteNpc(onlyId);
                    ws.Dead();
                }
                else
                {
                    ws.Reset();
                }
            }

            return Task.CompletedTask;
        }

        private async Task TrigleStarBattle(uint npcOnlyId, uint roleId, byte level)
        {
            if (!_isActive) return;
            var ws = _stars.FirstOrDefault(p => p.OnlyId == npcOnlyId);
            if (ws == null || ws.Applies.Count == 0 || ws.Applies[0] != roleId) return;
            var grain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            var ret = await grain.OnStarBattle(npcOnlyId, level);
            if (!ret)
            {
                ws.Reset();
            }
        }

        private async Task Refresh(object args)
        {
            if (!_isActive) return;
            var total = 0;
            foreach (var ws in _stars)
            {
                ws.Reset();
                if (ws.OnlyId > 0)
                {
                    // 防止意外, 距离创建时间超过1个小时强制进行销毁
                    if (TimeUtil.TimeStamp - ws.CreateTime < 3600)
                    {
                        continue;
                    }

                    // 强制删除
                    _ = _serverGrain.DeleteNpc(ws.OnlyId);
                    ws.Dead();
                }

                // 随机一个位置
                var pos = MapUtil.RandomPos(ws.MapId);
                if (pos == null)
                {
                    LogError($"在地图[{ws.MapId}]上未找到合适的位置放置地煞星");
                    continue;
                }

                // 去创建npc
                var onlyId = await _serverGrain.CreateNpc(new Immutable<byte[]>(Packet.Serialize(new CreateNpcRequest
                {
                    MapId = ws.MapId,
                    MapX = pos.X,
                    MapY = pos.Y,
                    CfgId = ws.CfgId,
                    Owner = new NpcOwner
                    {
                        Type = NpcOwnerType.Activity,
                        Value = (uint)ActivityId.DiShaXing
                    }
                })));
                ws.Birth(onlyId, pos.X, pos.Y);

                total++;
            }

            LogDebug($"刷新{total}个");
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"地煞星[{_serverId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"地煞星[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"地煞星[{_serverId}]:{msg}");
        }

        private List<WorldStar> _stars = new()
        {
            //地狗星-1007 大唐边境
            new WorldStar(30168, 1007, 1),
            new WorldStar(30169, 1007, 1),
            new WorldStar(30170, 1007, 1),
            new WorldStar(30169, 1007, 1),
            new WorldStar(30170, 1007, 1),
            new WorldStar(30168, 1007, 1),
            new WorldStar(30169, 1007, 1),
            new WorldStar(30170, 1007, 1),
            new WorldStar(30169, 1007, 1),
            new WorldStar(30170, 1007, 1),
            new WorldStar(30168, 1007, 1),
            new WorldStar(30169, 1007, 1),
            new WorldStar(30170, 1007, 1),
            new WorldStar(30169, 1007, 1),
            new WorldStar(30170, 1007, 1),
            new WorldStar(30168, 1007, 1),
            new WorldStar(30169, 1007, 1),
            new WorldStar(30170, 1007, 1),
            new WorldStar(30169, 1007, 1),
            new WorldStar(30170, 1007, 1),
            //地平星-1005 方寸山
            new WorldStar(30171, 1005, 2),
            new WorldStar(30172, 1005, 2),
            new WorldStar(30173, 1005, 2),
            new WorldStar(30172, 1005, 2),
            new WorldStar(30173, 1005, 2),
            new WorldStar(30171, 1005, 2),
            new WorldStar(30172, 1005, 2),
            new WorldStar(30173, 1005, 2),
            new WorldStar(30172, 1005, 2),
            new WorldStar(30173, 1005, 2),
            new WorldStar(30171, 1005, 2),
            new WorldStar(30172, 1005, 2),
            new WorldStar(30173, 1005, 2),
            new WorldStar(30172, 1005, 2),
            new WorldStar(30173, 1005, 2),
            new WorldStar(30171, 1005, 2),
            new WorldStar(30172, 1005, 2),
            new WorldStar(30173, 1005, 2),
            new WorldStar(30172, 1005, 2),
            new WorldStar(30173, 1005, 2),
            //地悠星-1001 普陀山
            new WorldStar(30174, 1001, 3),
            new WorldStar(30175, 1001, 3),
            new WorldStar(30176, 1001, 3),
            new WorldStar(30175, 1001, 3),
            new WorldStar(30176, 1001, 3),
            new WorldStar(30174, 1001, 3),
            new WorldStar(30175, 1001, 3),
            new WorldStar(30176, 1001, 3),
            new WorldStar(30175, 1001, 3),
            new WorldStar(30176, 1001, 3),
            new WorldStar(30174, 1001, 3),
            new WorldStar(30175, 1001, 3),
            new WorldStar(30176, 1001, 3),
            new WorldStar(30175, 1001, 3),
            new WorldStar(30176, 1001, 3),
            new WorldStar(30174, 1001, 3),
            new WorldStar(30175, 1001, 3),
            new WorldStar(30176, 1001, 3),
            new WorldStar(30175, 1001, 3),
            new WorldStar(30176, 1001, 3),
            //地异星-1002 地府
            new WorldStar(30177, 1002, 4),
            new WorldStar(30178, 1002, 4),
            new WorldStar(30179, 1002, 4),
            new WorldStar(30177, 1002, 4),
            new WorldStar(30178, 1002, 4),
            new WorldStar(30179, 1002, 4),
            new WorldStar(30177, 1002, 4),
            new WorldStar(30178, 1002, 4),
            new WorldStar(30179, 1002, 4),
            new WorldStar(30177, 1002, 4),
            new WorldStar(30178, 1002, 4),
            new WorldStar(30179, 1002, 4),
            new WorldStar(30177, 1002, 4),
            new WorldStar(30178, 1002, 4),
            new WorldStar(30179, 1002, 4),
            new WorldStar(30177, 1002, 4),
            new WorldStar(30178, 1002, 4),
            new WorldStar(30179, 1002, 4),
            new WorldStar(30177, 1002, 4),
            new WorldStar(30178, 1002, 4),
            //地微星-1004 大唐境内
            new WorldStar(30180, 1004, 5),
            new WorldStar(30181, 1004, 5),
            new WorldStar(30182, 1004, 5),
            new WorldStar(30180, 1004, 5),
            new WorldStar(30181, 1004, 5),
            new WorldStar(30182, 1004, 5),
            new WorldStar(30180, 1004, 5),
            new WorldStar(30181, 1004, 5),
            new WorldStar(30182, 1004, 5),
            new WorldStar(30180, 1004, 5),
            new WorldStar(30181, 1004, 5),
            new WorldStar(30182, 1004, 5),
            new WorldStar(30180, 1004, 5),
            new WorldStar(30181, 1004, 5),
            new WorldStar(30182, 1004, 5),
            new WorldStar(30180, 1004, 5),
            new WorldStar(30181, 1004, 5),
            new WorldStar(30182, 1004, 5),
            new WorldStar(30180, 1004, 5),
            new WorldStar(30181, 1004, 5),
            //地奇星-1012 天宫
            new WorldStar(30183, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30183, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30183, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30183, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30183, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30183, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30184, 1012, 6),
            new WorldStar(30183, 1012, 6),
            new WorldStar(30184, 1012, 6),
            //地查星-1012 天宫
            new WorldStar(30186, 1012, 7),
            new WorldStar(30187, 1012, 7),
            new WorldStar(30188, 1012, 7),
            new WorldStar(30186, 1012, 7),
            new WorldStar(30187, 1012, 7),
            new WorldStar(30188, 1012, 7),
            new WorldStar(30186, 1012, 7),
            new WorldStar(30187, 1012, 7),
            new WorldStar(30188, 1012, 7),
            new WorldStar(30186, 1012, 7),
            new WorldStar(30187, 1012, 7),
            new WorldStar(30188, 1012, 7),
            new WorldStar(30186, 1012, 7),
            new WorldStar(30187, 1012, 7),
            new WorldStar(30188, 1012, 7),
            new WorldStar(30186, 1012, 7),
            new WorldStar(30187, 1012, 7),
            new WorldStar(30188, 1012, 7),
            new WorldStar(30186, 1012, 7),
            new WorldStar(30187, 1012, 7),
            //地稽星-1010 东海渔村
            new WorldStar(30189, 1010, 8),
            new WorldStar(30190, 1010, 8),
            new WorldStar(30191, 1010, 8),
            new WorldStar(30189, 1010, 8),
            new WorldStar(30190, 1010, 8),
            new WorldStar(30191, 1010, 8),
            new WorldStar(30189, 1010, 8),
            new WorldStar(30190, 1010, 8),
            new WorldStar(30191, 1010, 8),
            new WorldStar(30189, 1010, 8),
            new WorldStar(30190, 1010, 8),
            new WorldStar(30191, 1010, 8),
            new WorldStar(30189, 1010, 8),
            new WorldStar(30190, 1010, 8),
            new WorldStar(30191, 1010, 8),
            new WorldStar(30189, 1010, 8),
            new WorldStar(30190, 1010, 8),
            new WorldStar(30191, 1010, 8),
            new WorldStar(30189, 1010, 8),
            new WorldStar(30190, 1010, 8),
            //地慧星-1011 长安
            new WorldStar(30192, 1011, 9),
            new WorldStar(30193, 1011, 9),
            new WorldStar(30194, 1011, 9),
            new WorldStar(30192, 1011, 9),
            new WorldStar(30193, 1011, 9),
            new WorldStar(30194, 1011, 9),
            new WorldStar(30192, 1011, 9),
            new WorldStar(30193, 1011, 9),
            new WorldStar(30194, 1011, 9),
            new WorldStar(30192, 1011, 9),
            new WorldStar(30193, 1011, 9),
            new WorldStar(30194, 1011, 9),
            new WorldStar(30192, 1011, 9),
            new WorldStar(30193, 1011, 9),
            new WorldStar(30194, 1011, 9),
            new WorldStar(30192, 1011, 9),
            new WorldStar(30193, 1011, 9),
            new WorldStar(30194, 1011, 9),
            new WorldStar(30192, 1011, 9),
            new WorldStar(30193, 1011, 9),
            //地魁星-1004 大唐境内
            new WorldStar(30195, 1004, 10),
            new WorldStar(30196, 1004, 10),
            new WorldStar(30197, 1004, 10),
            new WorldStar(30195, 1004, 10),
            new WorldStar(30196, 1004, 10),
            new WorldStar(30197, 1004, 10),
            new WorldStar(30195, 1004, 10),
            new WorldStar(30196, 1004, 10),
            new WorldStar(30197, 1004, 10),
            new WorldStar(30195, 1004, 10),
            new WorldStar(30196, 1004, 10),
            new WorldStar(30197, 1004, 10),
            new WorldStar(30195, 1004, 10),
            new WorldStar(30196, 1004, 10),
            new WorldStar(30197, 1004, 10),
            new WorldStar(30195, 1004, 10),
            new WorldStar(30196, 1004, 10),
            new WorldStar(30197, 1004, 10),
            new WorldStar(30195, 1004, 10),
            new WorldStar(30196, 1004, 10),
            //地灵星-1005 方寸山
            new WorldStar(30198, 1005, 11),
            new WorldStar(30199, 1005, 11),
            new WorldStar(30200, 1005, 11),
            new WorldStar(30198, 1005, 11),
            new WorldStar(30199, 1005, 11),
            new WorldStar(30200, 1005, 11),
            new WorldStar(30198, 1005, 11),
            new WorldStar(30199, 1005, 11),
            new WorldStar(30200, 1005, 11),
            new WorldStar(30198, 1005, 11),
            new WorldStar(30199, 1005, 11),
            new WorldStar(30200, 1005, 11),
            new WorldStar(30198, 1005, 11),
            new WorldStar(30199, 1005, 11),
            new WorldStar(30200, 1005, 11),
            new WorldStar(30198, 1005, 11),
            new WorldStar(30199, 1005, 11),
            new WorldStar(30200, 1005, 11),
            new WorldStar(30198, 1005, 11),
            new WorldStar(30199, 1005, 11),
            //地隐星-1001 普陀山
            new WorldStar(30201, 1001, 12),
            new WorldStar(30202, 1001, 12),
            new WorldStar(30203, 1001, 12),
            new WorldStar(30201, 1001, 12),
            new WorldStar(30202, 1001, 12),
            new WorldStar(30203, 1001, 12),
            new WorldStar(30201, 1001, 12),
            new WorldStar(30202, 1001, 12),
            new WorldStar(30203, 1001, 12),
            new WorldStar(30201, 1001, 12),
            new WorldStar(30202, 1001, 12),
            new WorldStar(30203, 1001, 12),
            new WorldStar(30201, 1001, 12),
            new WorldStar(30202, 1001, 12),
            new WorldStar(30203, 1001, 12),
            new WorldStar(30201, 1001, 12),
            new WorldStar(30202, 1001, 12),
            new WorldStar(30203, 1001, 12),
            new WorldStar(30201, 1001, 12),
            new WorldStar(30202, 1001, 12),
            //地佑星-1008 白骨山
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            new WorldStar(70001, 1008, 13),
            //地凌星 -1019 平顶山
            new WorldStar(70002, 1019, 14),
            new WorldStar(70002, 1019, 14),
            new WorldStar(70002, 1019, 14),
            new WorldStar(70002, 1019, 14),
            new WorldStar(70002, 1019, 14),
            new WorldStar(70002, 1019, 14),
            new WorldStar(70002, 1019, 14),
            new WorldStar(70002, 1019, 14),
            new WorldStar(70002, 1019, 14),
            new WorldStar(70002, 1019, 14),
            //地云星 -1009 龙宫
            new WorldStar(70003, 1009, 15),
            new WorldStar(70003, 1009, 15),
            new WorldStar(70003, 1009, 15),
            new WorldStar(70003, 1009, 15),
            new WorldStar(70003, 1009, 15),
            new WorldStar(70003, 1009, 15),
            new WorldStar(70003, 1009, 15),
            //16星 -1015 蟠桃园
            new WorldStar(70004, 1015, 16),
            new WorldStar(70004, 1015, 16),
            new WorldStar(70004, 1015, 16),
            new WorldStar(70004, 1015, 16),
            new WorldStar(70004, 1015, 16),
            new WorldStar(70004, 1015, 16),
            new WorldStar(70004, 1015, 16),
            //17星 -1017 傲来国
            new WorldStar(70005, 1017, 17),
            new WorldStar(70005, 1017, 17),
            new WorldStar(70005, 1017, 17),
            new WorldStar(70005, 1017, 17),
            //18星 -1013 蓝若寺
            new WorldStar(70006, 1013, 18),
            new WorldStar(70006, 1013, 18)
        };
    }
}