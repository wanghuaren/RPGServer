using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Logic.Aoi;
using Ddxy.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    [CollectionAgeLimit(AlwaysActive = true)]
    public class MapGrain : Grain, IMapGrain
    {
        private readonly ILogger<MapGrain> _logger;

        private uint _mapId;
        private bool _isActive;

        // onlyId -> AoiNode
        private Dictionary<uint, AoiNode> _nodes;

        // 快慢针 十字链表
        private AoiNodeLinkedList _xLinks;
        private AoiNodeLinkedList _yLinks;

        // 存储所有的在线玩家及其Grain引用, key为onlyId
        private Dictionary<uint, IPlayerGrain> _players;

        // 所有的player的AoiNode, 方便快速遍历
        private List<AoiNode> _playerNodes;

        // 每0.5s更新一次
        private IDisposable _updateTimer;

        public MapGrain(ILogger<MapGrain> logger)
        {
            _logger = logger;
        }

        public override Task OnActivateAsync()
        {
            _mapId = Convert.ToUInt32(this.GetPrimaryKeyString().Split('_')[1]);
            return base.OnActivateAsync();
        }

        public override Task OnDeactivateAsync()
        {
            return ShutDown();
        }

        public Task StartUp()
        {
            if (_isActive) return Task.CompletedTask;
            _isActive = true;
            _nodes = new Dictionary<uint, AoiNode>(10000);
            _xLinks = new AoiNodeLinkedList(100, AoiNodeLinkedListType.X);
            _yLinks = new AoiNodeLinkedList(100, AoiNodeLinkedListType.Y);

            _players = new Dictionary<uint, IPlayerGrain>(2000);
            _playerNodes = new List<AoiNode>(2000);

            // 开启每0.5s更新一次
            _updateTimer = RegisterTimer(Update, null, TimeSpan.FromSeconds(0.1), TimeSpan.FromSeconds(0.5));

            return Task.CompletedTask;
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;
            _isActive = false;
            _updateTimer?.Dispose();
            _updateTimer = null;

            // 销毁所有AoiNode
            foreach (var node in _nodes.Values)
            {
                node.Dispose();
            }

            // 清空Node池和所有的链表节点
            _nodes.Clear();
            _xLinks.Clear();
            _yLinks.Clear();
            _players.Clear();
            _playerNodes.Clear();

            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new(_isActive);
        }

        public Task Enter(Immutable<byte[]> mapObjectBytes, uint deviceWidth, uint deviceHeight)
        {
            if (!_isActive) return Task.CompletedTask;
            var mapObject = MapObjectData.Parser.ParseFrom(mapObjectBytes.Value);
            var node = AddMapObject(mapObject, deviceWidth, deviceHeight);

            if (node.IsPlayer)
            {
                var idx = _playerNodes.FindIndex(p => p == null);
                if (idx >= 0)
                {
                    _playerNodes[idx] = node;
                }
                else
                {
                    _playerNodes.Add(node);
                }

                if (node.Data.Online.Value)
                {
                    _players[node.OnlyId] = GrainFactory.GetGrain<IPlayerGrain>(node.Data.RoleId);
                }

                // LogDebug($"玩家[{node.Data.RoleId}]进入[{node.X},{node.Y}]");
            }

            return Task.CompletedTask;
        }

        public async Task Exit(uint onlyId)
        {
            if (!_isActive) return;
            if (_nodes.Remove(onlyId, out var node))
            {
                // 从十字链表中移除
                _xLinks.Remove(node.XNode);
                _yLinks.Remove(node.YNode);
                node.Dispose();

                if (node.IsPlayer)
                {
                    var idx = _playerNodes.FindIndex(p => p != null && p.OnlyId == onlyId);
                    // 不要Remove，赋值为null表示该位置为空
                    if (idx >= 0) _playerNodes[idx] = null;

                    // 移除Grain引用
                    _players.Remove(onlyId);

                    // LogDebug($"玩家[{node.Data.RoleId}]离开地图");
                }
            }

            await Task.CompletedTask;
        }

        // 玩家恢复上线, 上报屏幕尺寸
        public async Task PlayerOnline(uint onlyId, uint deviceWidth, uint deviceHeight)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            // 更新Grain引用
            _players[onlyId] = GrainFactory.GetGrain<IPlayerGrain>(node.Data.RoleId);
            node.IsPause = false;
            node.IsFirstResume = false;
            // 标记上线，并且清空视野缓存
            node.BuildViewRect(deviceWidth, deviceHeight);
            node.SetOnline(true);
            node.ResetViewObjects();
            await Task.CompletedTask;
        }

        // 玩家离线
        public async Task PlayerOffline(uint onlyId)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            // 更新Grain引用
            _players.Remove(onlyId);
            node.IsPause = false;
            node.IsFirstResume = false;
            // 标记下线，清空视野缓存
            node.SetOnline(false);
            node.ResetViewObjects();
            await Task.CompletedTask;
        }

        public async Task PlayerPause(uint onlyId, bool pause)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            if (node.IsPause == pause) return;
            node.IsPause = pause;
            if (!pause) node.IsFirstResume = true;
            await Task.CompletedTask;
        }

        // 玩家进入战斗
        public async Task PlayerEnterBattle(uint onlyId, uint battleId, uint campId)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            // 标记进入战斗
            node.SetBattleInfo(battleId, campId);
            await Task.CompletedTask;
        }

        // 玩家离开战斗
        public async Task PlayerExitBattle(uint onlyId)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            // 标记退出战斗
            node.SetBattleInfo(0, 0);
            await Task.CompletedTask;
        }

        public async Task PlayerMove(uint onlyId, int x, int y, bool blink)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.Data.Blink = blink;
            Move(node, x, y);
            await Task.CompletedTask;
        }

        public async Task TeamMove(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return;
            if (reqBytes.Value == null) return;
            var req = TeamMoveRequest.Parser.ParseFrom(reqBytes.Value);
            foreach (var item in req.List)
            {
                if (item == null) continue;
                _nodes.TryGetValue(item.OnlyId, out var node);
                if (node is not {IsPlayer: true}) continue;
                node.Data.Blink = item.Blink;
                Move(node, item.X, item.Y);
            }

            await Task.CompletedTask;
        }

        public async Task SetPlayerName(uint onlyId, string name)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.SetName(name);
            await Task.CompletedTask;
        }

        public async Task SetPlayerLevel(uint onlyId, uint relive, uint level)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.SetRelive(relive);
            node.SetLevel(level);
            await Task.CompletedTask;
        }

        public async Task SetPlayerCfgId(uint onlyId, uint cfgId)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.SetCfgId(cfgId);
            await Task.CompletedTask;
        }

        public async Task SetPlayerColor(uint onlyId, uint color1, uint color2)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.SetColor(color1, color2);
            await Task.CompletedTask;
        }

        public async Task SetPlayerTeam(uint onlyId, uint teamId, uint teamLeader, uint memberCount)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.SetTeam(teamId, teamLeader, memberCount);
            await Task.CompletedTask;
        }

        public async Task SetPlayerSect(uint onlyId, uint sectId)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.SetSect(sectId);
            await Task.CompletedTask;
        }

        public async Task SetPlayerWeapon(uint onlyId, Immutable<byte[]> weaponDataBytes)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            MapObjectEquipData weapon;
            if (weaponDataBytes.Value == null)
            {
                weapon = new MapObjectEquipData {CfgId = 0};
            }
            else
            {
                weapon = MapObjectEquipData.Parser.ParseFrom(weaponDataBytes.Value);
            }

            node.SetWeapon(weapon);
            await Task.CompletedTask;
        }

        public async Task SetPlayerWing(uint onlyId, Immutable<byte[]> wingDataBytes)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            MapObjectEquipData wing;
            if (wingDataBytes.Value == null)
            {
                wing = new MapObjectEquipData {CfgId = 0};
            }
            else
            {
                wing = MapObjectEquipData.Parser.ParseFrom(wingDataBytes.Value);
            }

            node.SetWing(wing);
            await Task.CompletedTask;
        }

        public async Task SetPlayerSkins(uint onlyId, List<int> skins)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not { IsPlayer: true }) return;
            node.SetSkins(skins);
            await Task.CompletedTask;
        }

        public async Task SetPlayerVipLevel(uint onlyId, uint vipLevel)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not { IsPlayer: true }) return;
            node.SetVipLevel(vipLevel);
            await Task.CompletedTask;
        }

        public async Task SetPlayerBianshen(uint onlyId, int cardId)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not { IsPlayer: true }) return;
            node.SetBianshen(cardId);
            await Task.CompletedTask;
        }

        public async Task SetPlayerQieGeLevel(uint onlyId, uint qieGeLevel)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not { IsPlayer: true }) return;
            node.SetQieGeLevel(qieGeLevel);
            await Task.CompletedTask;
        }

        public async Task SetPlayerMount(uint onlyId, uint cfgId)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.SetMount(cfgId);
            await Task.CompletedTask;
        }

        public async Task SetPlayerTitle(uint onlyId, Immutable<byte[]> titleDataBytes)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            TitleData title = null;
            if (titleDataBytes.Value != null)
                title = TitleData.Parser.ParseFrom(titleDataBytes.Value);
            node.SetTitle(title);
            await Task.CompletedTask;
        }

        public async Task SetPlayerSldhGroup(uint onlyId, uint sldhGroup)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.Data.SldhGroup = sldhGroup;
            await Task.CompletedTask;
        }

        public async Task SetPlayerWzzzGroup(uint onlyId, uint wzzzGroup)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.Data.WzzzGroup = wzzzGroup;
            await Task.CompletedTask;
        }

        public async Task SetPlayerTeamLeave(uint onlyId, bool leave)
        {
            if (!_isActive) return;
            _nodes.TryGetValue(onlyId, out var node);
            if (node is not {IsPlayer: true}) return;
            node.Data.TeamLeave = leave;
            await Task.CompletedTask;
        }

        private AoiNode AddMapObject(MapObjectData data, uint deviceWidth, uint deviceHeight)
        {
            if (data.MapId != _mapId)
            {
                LogError($"地图对象[{data.OnlyId}]的地图ID[{data.MapId}]不匹配当前地图");
                return null;
            }

            if (data.MapX == null || data.MapY == null)
            {
                LogError($"地图对象[{data.OnlyId}]的地图ID[data.MapId]没有mapX或mapY信息");
                return null;
            }

            var node = new AoiNode(data, deviceWidth, deviceHeight);
            _nodes[node.OnlyId] = node;
            _xLinks.Insert(node);
            _yLinks.Insert(node);

            return node;
        }

        private void SendPlayerView(AoiNode node, bool force = false)
        {
            _players.TryGetValue(node.OnlyId, out var grain);
            if (grain == null) return;

            // 强制刷新就先清空之前的视图缓存
            if (force) node.ResetViewObjects();
            node.UpdateView(_mapId, _nodes);

            var msg = new S2C_MapSyncView();
            // 新增的对象要下发全部属性
            foreach (var onlyId in node.AddObjects)
            {
                if (!_nodes.TryGetValue(onlyId, out var tmpNode)) continue;
                var info = tmpNode.Data;

                var objData = new MapObjectData
                {
                    OnlyId = info.OnlyId,
                    Type = info.Type,
                    CfgId = info.CfgId,
                    Name = info.Name,
                    MapX = info.MapX,
                    MapY = info.MapY,
                    Blink = info.Blink,
                    RoleId = info.RoleId,
                    Relive = info.Relive,
                    Level = info.Level,
                    Color1 = info.Color1,
                    Color2 = info.Color2,
                    TeamId = info.TeamId,
                    TeamLeader = info.TeamLeader,
                    TeamMemberCount = info.TeamMemberCount,
                    SectId = info.SectId,
                    Weapon = info.Weapon,
                    Wing = info.Wing,
                    Mount = info.Mount,
                    Title = info.Title,
                    Online = info.Online,
                    Battle = info.Battle,
                    BattleInfo = info.BattleInfo,
                    Skins = {info.Skins},
                    Bianshen = info.Bianshen,
                    VipLevel = info.VipLevel,
                    QiegeLevel = info.QiegeLevel,
                };
                msg.AddList.Add(objData);
            }

            // 删除的对象只下发id即可
            foreach (var onlyId in node.DelObjects)
            {
                msg.DelList.Add(onlyId);
            }

            // 本帧保留下来且有改变过的，下发变动属性
            foreach (var onlyId in node.KeepObjects)
            {
                if (!_nodes.TryGetValue(onlyId, out var tmpNode)) continue;
                var info = tmpNode.Data;

                // 从后台恢复前台后的第一帧需要全量更新Update, 防止我在后台的过程中，一直保持在视野内的其他玩家移动了位置，更换了坐骑，经过tick后changed已经变成了false，从而我没法刷新
                if (node.IsFirstResume && info.Type == LivingThingType.Player)
                {
                    var objData = new MapObjectData
                    {
                        OnlyId = info.OnlyId,
                        Type = info.Type,
                        CfgId = info.CfgId,
                        Name = info.Name,
                        MapX = info.MapX,
                        MapY = info.MapY,
                        Blink = info.Blink,
                        RoleId = info.RoleId,
                        Relive = info.Relive,
                        Level = info.Level,
                        Color1 = info.Color1,
                        Color2 = info.Color2,
                        TeamId = info.TeamId,
                        TeamLeader = info.TeamLeader,
                        TeamMemberCount = info.TeamMemberCount,
                        SectId = info.SectId,
                        Weapon = info.Weapon,
                        Wing = info.Wing,
                        Mount = info.Mount,
                        Title = info.Title,
                        Online = info.Online,
                        Battle = info.Battle,
                        BattleInfo = info.BattleInfo,
                        Skins = {info.Skins},
                        Bianshen = info.Bianshen,
                        VipLevel = info.VipLevel,
                        QiegeLevel = info.QiegeLevel,
                    };
                    msg.UpList.Add(objData);
                }
                else
                {
                    // 什么都没改变
                    if (!tmpNode.IsChanged) continue;

                    // 下面根据变更项进行增量更新
                    var objData = new MapObjectData {OnlyId = info.OnlyId, Blink = info.Blink};
                    if (tmpNode.IsPosChanged)
                    {
                        objData.MapX = info.MapX;
                        objData.MapY = info.MapY;
                    }

                    if (onlyId != node.OnlyId)
                    {
                        if (tmpNode.IsNameChanged) objData.Name = info.Name;
                        if (tmpNode.IsLevelChanged) objData.Level = info.Level;
                        if (tmpNode.IsReliveChanged) objData.Relive = info.Relive;
                        if (tmpNode.IsCfgIdChanged) objData.CfgId = info.CfgId;
                        if (tmpNode.IsColorChanged)
                        {
                            objData.Color1 = info.Color1;
                            objData.Color2 = info.Color2;
                        }

                        if (tmpNode.IsTeamChanged)
                        {
                            objData.TeamId = info.TeamId;
                            objData.TeamLeader = info.TeamLeader;
                            objData.TeamMemberCount = info.TeamMemberCount;
                        }

                        if (tmpNode.IsSectChanged)
                        {
                            objData.SectId = info.SectId;
                        }

                        if (tmpNode.IsWeaponChanged) objData.Weapon = info.Weapon;
                        if (tmpNode.IsWingChanged) objData.Wing = info.Wing;
                        if (tmpNode.IsMountChanged) objData.Mount = info.Mount;
                        if (tmpNode.IsTitleChanged) objData.Title = info.Title;
                        if (tmpNode.IsOnlineChanged) objData.Online = info.Online;
                        if (tmpNode.IsBattleChanged)
                        {
                            objData.Battle = info.Battle;
                            objData.BattleInfo = info.BattleInfo;
                        }
                        if (tmpNode.IsSkinsChanged)
                        {
                            objData.Skins.Add(info.Skins);
                        }
                        // TODO: 特殊处理默认值，表示不处理
                        else
                        {
                            objData.Skins.Add(-1);
                        }
                        // TODO: 特殊处理默认值，表示不处理
                        if (tmpNode.IsBianshenChanged)
                        {
                            objData.Bianshen = info.Bianshen == 0 ? -1 : info.Bianshen;
                        }
                        if (tmpNode.IsVipLevelChanged)
                        {
                            objData.VipLevel = info.VipLevel;
                        }
                        if (tmpNode.IsQieGeLevelChanged)
                        {
                            objData.QiegeLevel = info.QiegeLevel;
                        }
                    }

                    msg.UpList.Add(objData);
                }
            }

            // 从后台恢复前台后的第一帧, update内容做全量更新, 更新完毕后要记得撤回
            node.IsFirstResume = false;

            // 如果没有任何变化就不发送了
            if (msg.AddList.Count == 0 && msg.DelList.Count == 0 && msg.UpList.Count == 0) return;

            // 交给PlayerGrain发送
            SendPacket(grain, GameCmd.S2CMapSyncView, msg);
        }

        private Task Update(object _)
        {
            // 遍历所有的玩家节点, 在线且不在战场中可以同步视野
            foreach (var node in _playerNodes)
            {
                if (node is {Syncable: true}) SendPlayerView(node);
            }

            // 全部重置移动标记
            foreach (var node in _playerNodes)
            {
                node?.ResetChanged();
            }

            return Task.CompletedTask;
        }

        private void Move(AoiNode node, int x, int y)
        {
            if (node.X != x)
            {
                if (x > node.X)
                {
                    var cur = node.XNode.Next;
                    while (cur != null)
                    {
                        if (x < cur.Value.X)
                        {
                            _xLinks.Remove(node.XNode);
                            node.X = x;
                            node.XNode = _xLinks.AddBefore(cur, node);
                            break;
                        }

                        if (cur.Next == null)
                        {
                            _xLinks.Remove(node.XNode);
                            node.X = x;
                            node.XNode = _xLinks.AddAfter(cur, node);
                            break;
                        }

                        cur = cur.Next;
                    }
                }
                else
                {
                    var cur = node.XNode.Previous;
                    while (cur != null)
                    {
                        if (x > cur.Value.X)
                        {
                            _xLinks.Remove(node.XNode);
                            node.X = x;
                            node.XNode = _xLinks.AddAfter(cur, node);
                            break;
                        }

                        if (cur.Previous == null)
                        {
                            _xLinks.Remove(node.XNode);
                            node.X = x;
                            node.XNode = _xLinks.AddAfter(cur, node);
                            break;
                        }

                        cur = cur.Previous;
                    }
                }
            }

            // ReSharper disable once InvertIf
            if (node.Y != y)
            {
                if (y > node.Y)
                {
                    var cur = node.YNode.Next;

                    while (cur != null)
                    {
                        if (y < cur.Value.Y)
                        {
                            _yLinks.Remove(node.YNode);
                            node.Y = y;
                            node.YNode = _yLinks.AddBefore(cur, node);
                            break;
                        }

                        if (cur.Next == null)
                        {
                            _yLinks.Remove(node.YNode);
                            node.Y = y;
                            node.YNode = _yLinks.AddAfter(cur, node);
                            break;
                        }

                        cur = cur.Next;
                    }
                }
                else
                {
                    var cur = node.YNode.Previous;
                    while (cur != null)
                    {
                        if (y > cur.Value.Y)
                        {
                            _yLinks.Remove(node.YNode);
                            node.Y = y;
                            node.YNode = _yLinks.AddBefore(cur, node);
                            break;
                        }

                        if (cur.Previous == null)
                        {
                            _yLinks.Remove(node.YNode);
                            node.Y = y;
                            node.YNode = _yLinks.AddAfter(cur, node);
                            break;
                        }

                        cur = cur.Previous;
                    }
                }
            }

            node.X = x;
            node.Y = y;
        }

        // 投递给PlayerGrain, 由它自己负责去发送
        private void SendPacket(IPlayerGrain grain, GameCmd cmd, IMessage msg)
        {
            _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(cmd, msg)));
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"地图[{_mapId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"地图[{_mapId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"地图[{_mapId}]:{msg}");
        }
    }
}