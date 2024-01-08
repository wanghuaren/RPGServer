using System;
using System.Collections.Generic;
using System.Linq;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Aoi
{
    public class AoiNode : IDisposable
    {
        public LinkedListNode<AoiNode> XNode; //在地图的十字链表中的节点
        public LinkedListNode<AoiNode> YNode; //在地图的十字链表中的节点

        /// <summary>
        /// 当前视野内的对象
        /// </summary>
        public HashSet<uint> ViewObjects { get; private set; }

        /// <summary>
        /// 本次更新，视野内新增的对象
        /// </summary>
        public HashSet<uint> AddObjects { get; private set; }

        /// <summary>
        /// 本次更新，视野内减少的对象
        /// </summary>
        public HashSet<uint> DelObjects { get; private set; }

        /// <summary>
        /// 本次更新，视野内保留的对象
        /// </summary>
        public HashSet<uint> KeepObjects { get; private set; }

        /// <summary>
        /// 视野内最大可视对象数量()
        /// </summary>
        public const int MaxViewObjectsNum = 30;

        // 视野范围
        private uint _xFov;
        private uint _yFov;
        private Random _random;

        public MapObjectData Data { get; }

        public uint OnlyId => Data.OnlyId;

        public LivingThingType Type => Data.Type;

        public bool IsPlayer => Data.Type == LivingThingType.Player;

        public bool IsPause;

        // 从后台恢复前台的第一帧, update的内容需要全量更新
        public bool IsFirstResume;

        public int X
        {
            get => Data.MapX.Value;
            set
            {
                if (value != Data.MapX.Value)
                {
                    Data.MapX.Value = value;
                    IsPosChanged = true;
                    IsChanged = true;
                }
            }
        }

        public int Y
        {
            get => Data.MapY.Value;
            set
            {
                if (value != Data.MapY.Value)
                {
                    Data.MapY.Value = value;
                    IsPosChanged = true;
                    IsChanged = true;
                }
            }
        }

        /// <summary>
        /// 当前是否开启AOI同步, 只对在线并且不在战场的Player开启视野同步
        /// </summary>
        public bool Syncable => Data.Online.Value && !Data.Battle.Value && !IsPause;

        /// <summary>
        /// 本帧和上帧之间我的数据是否有修改
        /// </summary>
        public bool IsChanged { get; private set; }

        public bool IsPosChanged { get; private set; }
        public bool IsNameChanged { get; private set; }
        public bool IsLevelChanged { get; private set; }
        public bool IsReliveChanged { get; private set; }
        public bool IsCfgIdChanged { get; private set; }
        public bool IsColorChanged { get; private set; }
        public bool IsTeamChanged { get; private set; }
        public bool IsSectChanged { get; private set; }
        public bool IsWeaponChanged { get; private set; }
        public bool IsWingChanged { get; private set; }
        public bool IsMountChanged { get; private set; }
        public bool IsOnlineChanged { get; private set; }
        public bool IsBattleChanged { get; private set; }
        public bool IsTitleChanged { get; private set; }
        public bool IsSkinsChanged { get; private set; }
        public bool IsVipLevelChanged { get; private set; }
        public bool IsBianshenChanged { get; private set; }
        public bool IsQieGeLevelChanged { get; private set; }

        public AoiNode(MapObjectData data, uint deviceWidth, uint deviceHeight)
        {
            Data = data;
            _random = new Random();
            // 默认按照设计分辨率来
            BuildViewRect(deviceWidth, deviceHeight);

            if (Type == LivingThingType.Player)
            {
                ViewObjects = new HashSet<uint>();
                KeepObjects = new HashSet<uint>();
                AddObjects = new HashSet<uint>();
                DelObjects = new HashSet<uint>();
            }
        }

        public void Dispose()
        {
            ViewObjects?.Clear();
            ViewObjects = null;
            KeepObjects?.Clear();
            KeepObjects = null;
            AddObjects?.Clear();
            AddObjects = null;
            DelObjects?.Clear();
            DelObjects = null;

            XNode = null;
            YNode = null;
            _random = null;
        }

        public void BuildViewRect(uint deviceWidth, uint deviceHeight)
        {
            // if (deviceWidth == 0) deviceWidth = 1024;
            // else if (deviceWidth > 4000) deviceWidth = 4000;
            //
            // if (deviceHeight == 0) deviceHeight = 576;
            // else if (deviceHeight > 2200) deviceHeight = 2200;

            // var scale = 1.0f / mapScale;

            // 左右上下各修正1个单元
            // _xFov = (uint) MathF.Ceiling(deviceWidth * scale / 20f) + 2;
            // _yFov = (uint) MathF.Ceiling(deviceHeight * scale / 20f) + 2;

            _xFov = (uint) MathF.Ceiling(deviceWidth * 0.5f);
            _yFov = (uint) MathF.Ceiling(deviceHeight * 0.5f);
        }

        /// <summary>
        /// 更新节点的周边节点集合, 一定是Player
        /// </summary>
        public void UpdateView(uint mapId, Dictionary<uint, AoiNode> nodes)
        {
            // 缓存上一帧的可视对象
            KeepObjects = ViewObjects.Select(p => p).ToHashSet();

            // 计算本帧可视的对象
            UpdateViewObjects(mapId);

            // 计算本帧新增的对象, 考虑到队员可以看到自己, 会导致一次Add
            AddObjects = ViewObjects.Except(KeepObjects).ToHashSet();
            var addMe = AddObjects.Contains(OnlyId);
            if (addMe) AddObjects.Remove(OnlyId);
            // 计算本帧离开的对象
            DelObjects = KeepObjects.Except(ViewObjects).ToHashSet();
            DelObjects.Remove(OnlyId);
            // 计算本帧保留的对象
            KeepObjects = KeepObjects.Intersect(ViewObjects).ToHashSet();
            if (addMe) KeepObjects.Add(OnlyId);

            // 数量裁剪, 避免前端渲染过多
            if (AddObjects.Count + KeepObjects.Count > MaxViewObjectsNum)
            {
                int delNum;
                bool delFromKeep;
                // 为了避免视野内对象闪烁, 优先保留keepObjects
                if (KeepObjects.Count >= MaxViewObjectsNum)
                {
                    delNum = KeepObjects.Count - MaxViewObjectsNum;
                    delFromKeep = true;
                    // 要确保NPC和自己的队友全部刷出来
                    var adds = new HashSet<uint>();
                    foreach (var onlyId in AddObjects)
                    {
                        // NPC和队友需要保留
                        if (nodes.TryGetValue(onlyId, out var otherNode) && otherNode != null)
                        {
                            if (otherNode.Type == LivingThingType.Npc ||
                                Data.TeamId.Value > 0 && Data.TeamId.Value == otherNode.Data.TeamId.Value)
                            {
                                adds.Add(onlyId);
                            }
                        }
                    }

                    AddObjects.Clear();
                    foreach (var onlyId in adds)
                    {
                        AddObjects.Add(onlyId);
                    }
                }
                else
                {
                    delNum = AddObjects.Count - (MaxViewObjectsNum - KeepObjects.Count);
                    delFromKeep = false;
                }

                if (delNum > 0)
                {
                    // 记录所有可能会被裁剪的单位
                    var deleteOnlyIds = new List<uint>(100);
                    var set = delFromKeep ? KeepObjects : AddObjects;
                    foreach (var onlyId in set)
                    {
                        // 自己不能裁剪, NPC不能裁剪
                        if (onlyId != OnlyId && nodes.TryGetValue(onlyId, out var otherNode) && otherNode != null)
                        {
                            // NPC不裁剪
                            if (otherNode.Type == LivingThingType.Npc)
                                continue;
                            // 自己的队友不裁剪
                            if (Data.TeamId.Value > 0 && Data.TeamId.Value == otherNode.Data.TeamId.Value)
                                continue;
                            deleteOnlyIds.Add(onlyId);
                        }
                    }

                    // 离散删除, 避免空洞+扎堆
                    if (deleteOnlyIds.Count > delNum)
                    {
                        // 采用保留法
                        for (int i = 0, len = deleteOnlyIds.Count - delNum; i < len; i++)
                        {
                            if (deleteOnlyIds.Count == 0) break;
                            var idx = _random.Next(0, deleteOnlyIds.Count);
                            deleteOnlyIds.RemoveAt(idx);
                        }
                    }

                    // 执行移除
                    foreach (var onlyId in deleteOnlyIds)
                    {
                        if (delFromKeep)
                        {
                            KeepObjects.Remove(onlyId);
                            // 这里要注意, 添加到移除列表, 因为该对象上帧是可见对象
                            DelObjects.Add(onlyId);
                        }
                        else
                        {
                            AddObjects.Remove(onlyId);
                        }
                    }
                }

                // 同步本帧可视对象
                ViewObjects.Clear();
                foreach (var onlyId in KeepObjects)
                {
                    ViewObjects.Add(onlyId);
                }

                foreach (var onlyId in AddObjects)
                {
                    ViewObjects.Add(onlyId);
                }
            }
        }

        public void ResetViewObjects()
        {
            ViewObjects.Clear();
            KeepObjects.Clear();
            AddObjects.Clear();
            DelObjects.Clear();
        }

        public void ResetChanged()
        {
            IsPosChanged = false;
            IsNameChanged = false;
            IsLevelChanged = false;
            IsReliveChanged = false;
            IsCfgIdChanged = false;
            IsColorChanged = false;
            IsTeamChanged = false;
            IsSectChanged = false;
            IsWeaponChanged = false;
            IsWingChanged = false;
            IsMountChanged = false;
            IsOnlineChanged = false;
            IsBattleChanged = false;
            IsTitleChanged = false;
            IsSkinsChanged = false;
            IsVipLevelChanged = false;
            IsBianshenChanged = false;
            IsQieGeLevelChanged = false;

            IsChanged = false;
        }

        public void SetName(string name)
        {
            if (string.Equals(name, Data.Name)) return;
            Data.Name = name;

            IsNameChanged = true;
            IsChanged = true;
        }

        public void SetLevel(uint level)
        {
            if (level == Data.Level.Value) return;
            Data.Level.Value = level;

            IsLevelChanged = true;
            IsChanged = true;
        }

        public void SetRelive(uint relive)
        {
            if (relive == Data.Relive.Value) return;
            Data.Relive.Value = relive;

            IsReliveChanged = true;
            IsChanged = true;
        }

        public void SetCfgId(uint cfgId)
        {
            if (cfgId == Data.CfgId) return;
            Data.CfgId = cfgId;

            IsCfgIdChanged = true;
            IsChanged = true;
        }

        public void SetColor(uint color1, uint color2)
        {
            if (color1 == Data.Color1.Value && color2 == Data.Color2.Value) return;
            Data.Color1.Value = color1;
            Data.Color2.Value = color2;

            IsColorChanged = true;
            IsChanged = true;
        }

        public void SetTeam(uint teamId, uint teamLeader, uint memberCount)
        {
            if (teamId == Data.TeamId.Value && teamLeader == Data.TeamLeader.Value &&
                memberCount == Data.TeamMemberCount.Value) return;
            Data.TeamId.Value = teamId;
            Data.TeamLeader.Value = teamLeader;
            Data.TeamMemberCount.Value = memberCount;

            IsTeamChanged = true;
            IsChanged = true;
        }

        public void SetSect(uint sectId)
        {
            if (sectId == Data.SectId.Value) return;
            Data.SectId.Value = sectId;
            IsSectChanged = true;
            IsChanged = true;
        }

        public void SetWeapon(MapObjectEquipData weapon)
        {
            Data.Weapon = weapon;

            IsWeaponChanged = true;
            IsChanged = true;
        }

        public void SetWing(MapObjectEquipData wing)
        {
            Data.Wing = wing;

            IsWingChanged = true;
            IsChanged = true;
        }

        public void SetSkins(List<int> skins)
        {
            Data.Skins.Clear();
            Data.Skins.Add(skins);
            IsSkinsChanged = true;
            IsChanged = true;
        }

        public void SetVipLevel(uint vipLevel)
        {
            Data.VipLevel = vipLevel;
            IsVipLevelChanged = true;
            IsChanged = true;
        }

        public void SetBianshen(int cardId)
        {
            Data.Bianshen = cardId;
            IsBianshenChanged = true;
            IsChanged = true;
        }

        public void SetQieGeLevel(uint qieGeLevel)
        {
            Data.QiegeLevel = qieGeLevel;
            IsQieGeLevelChanged = true;
            IsChanged = true;
        }

        public void SetMount(uint cfgId)
        {
            if (Data.Mount.Value == cfgId) return;
            Data.Mount.Value = cfgId;

            IsMountChanged = true;
            IsChanged = true;
        }

        public void SetOnline(bool online)
        {
            if (online == Data.Online.Value) return;
            Data.Online.Value = online;

            IsOnlineChanged = true;
            IsChanged = true;
        }

        public void SetBattle(bool battle)
        {
            if (battle == Data.Battle.Value) return;
            Data.Battle.Value = battle;

            IsBattleChanged = true;
            IsChanged = true;
        }

        public void SetBattleInfo(uint battleId, uint campId)
        {
            Data.BattleInfo = new InBattleInfo() { BattleId = battleId, CampId = campId };
            this.SetBattle(battleId != 0);
        }

        public void SetTitle(TitleData title)
        {
            if (title == null && (Data.Title == null || Data.Title.Id == 0)) return;
            if (title != null && Data.Title != null && title.Id == Data.Title.Id)
                return;
            // 用空对象来提示前端
            title ??= new TitleData();
            Data.Title = title;

            IsTitleChanged = true;
            IsChanged = true;
        }

        private void UpdateViewObjects(uint mapId)
        {
            ViewObjects.Clear();

            // 如果在家, 看不到别人
            if (4001 == mapId) return;

            // 如果我是队员我要能看到自己, 要考虑自己是否暂离队伍了
            if (Data.TeamId.Value > 0 && Data.TeamLeader.Value != Data.RoleId && !Data.TeamLeave)
            {
                ViewObjects.Add(Data.OnlyId);
            }

            var xNext = XNode.Next;
            var xPrevious = XNode.Previous;
            var yNext = YNode.Next;
            var yPrevious = YNode.Previous;

            // x同时向前，向后
            while (true)
            {
                if (xNext == null && xPrevious == null && yNext == null && yPrevious == null) break;
                // x向前
                if (xNext != null)
                {
                    if (Abs(X, xNext.Value.X) > _xFov)
                    {
                        xNext = null;
                    }
                    else
                    {
                        if (Abs(Y, xNext.Value.Y) <= _yFov)
                        {
                            if (!ViewObjects.Contains(xNext.Value.OnlyId))
                            {
                                if (CheckSee(xNext.Value, mapId))
                                {
                                    ViewObjects.Add(xNext.Value.OnlyId);
                                }
                            }
                        }

                        xNext = xNext.Next;
                    }
                }

                // x向后
                if (xPrevious != null)
                {
                    if (Abs(X, xPrevious.Value.X) > _xFov)
                    {
                        xPrevious = null;
                    }
                    else
                    {
                        if (Abs(Y, xPrevious.Value.Y) <= _yFov)
                        {
                            if (!ViewObjects.Contains(xPrevious.Value.OnlyId))
                            {
                                if (CheckSee(xPrevious.Value, mapId))
                                {
                                    ViewObjects.Add(xPrevious.Value.OnlyId);
                                }
                            }
                        }

                        xPrevious = xPrevious.Previous;
                    }
                }

                // y向上
                if (yNext != null)
                {
                    if (Abs(Y, yNext.Value.Y) > _yFov)
                    {
                        yNext = null;
                    }
                    else
                    {
                        if (Abs(X, yNext.Value.X) <= _xFov)
                        {
                            if (!ViewObjects.Contains(yNext.Value.OnlyId))
                            {
                                if (CheckSee(yNext.Value, mapId))
                                {
                                    ViewObjects.Add(yNext.Value.OnlyId);
                                }
                            }
                        }

                        yNext = yNext.Next;
                    }
                }

                // y向下
                if (yPrevious != null)
                {
                    if (Abs(Y, yPrevious.Value.Y) > _yFov)
                    {
                        yPrevious = null;
                    }
                    else
                    {
                        if (Abs(X, yPrevious.Value.X) <= _xFov)
                        {
                            if (!ViewObjects.Contains(yPrevious.Value.OnlyId))
                            {
                                if (CheckSee(yPrevious.Value, mapId))
                                {
                                    ViewObjects.Add(yPrevious.Value.OnlyId);
                                }
                            }
                        }

                        yPrevious = yPrevious.Previous;
                    }
                }
            }
        }


        /// <summary>
        /// 检查目标是否可见
        /// </summary>
        private bool CheckSee(AoiNode target, uint mapId)
        {
            if (target.OnlyId == Data.OnlyId) return false;

            if (target.Type == LivingThingType.Npc)
            {
                return target.Data.Owner.Type switch
                {
                    NpcOwnerType.System => true,
                    NpcOwnerType.Activity => true,
                    // 队员可以看到队长的NPC
                    NpcOwnerType.Player => target.Data.Owner.Value == Data.RoleId ||
                                           Data.TeamId.Value > 0 && target.Data.Owner.Value == Data.TeamLeader.Value,
                    NpcOwnerType.Team => Data.TeamId.Value > 0 && target.Data.Owner.Value == Data.TeamId.Value,
                    _ => false
                };
            }

            if (target.Type == LivingThingType.Player)
            {
                // 自己的队友无论如何都可以看到
                if (Data.TeamId.Value > 0 && target.Data.TeamId.Value == Data.TeamId.Value) return true;

                // 帮派地图只能看到自己这个帮派的成员
                if (mapId == 3002)
                {
                    return Data.SectId.Value > 0 && target.Data.SectId.Value == Data.SectId.Value;
                }

                // 帮战地图只能看到自己这一场帮战的成员
                if (mapId == 5001)
                {
                    return Data.SectWarId > 0 && target.Data.SectWarId == Data.SectWarId;
                }

                // 水路地图只能看到同一组
                if (mapId == 3001)
                {
                    return Data.SldhGroup > 0 && target.Data.SldhGroup == Data.SldhGroup;
                }

                // 大雁塔、只能看到自己的队友
                if (mapId is >= 2000 and <= 2006)
                {
                    return Data.TeamId.Value > 0 && target.Data.TeamId.Value == Data.TeamId.Value;
                }
            }

            return true;
        }

        /// <summary>
        /// 不用Math.Abs是为了防止 uint相减之后变成负数，从而变成一个超大的uint
        /// </summary>
        private static int Abs(int n1, int n2)
        {
            if (n1 >= n2) return n1 - n2;
            return n2 - n1;
        }

        public static float Distance(int x1, int y1, int x2, int y2)
        {
            var dx = Abs(x1, x2);
            var dy = Abs(y1, y2);
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public static bool IsVeryClose(int x1, int y1, int x2, int y2)
        {
            return Distance(x1, y1, x2, y2) <= 10;
        }
    }
}