using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Logic.Sect;
using Ddxy.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    [CollectionAgeLimit(AlwaysActive = true)]
    public class SectGrain : Grain, ISectGrain
    {
        private ILogger<SectGrain> _logger;
        private uint _sectId;
        private bool _isActive;
        private SectEntity _entity;
        private SectEntity _lastEntity;
        private SectData _sectData;

        private Dictionary<uint, SectMember> _members; //所有成员包括帮主
        private SectMember _owner; //帮主

        private Dictionary<uint, SectApplyJoinData> _applyList; //申请列表, key是roleId

        private IServerGrain _serverGrain;
        private IDisposable _updateTimer;
        private uint _tickCnt;

        private bool _sectWaring; //当前是否在参加帮战中

        //获取副帮主
        public SectMember GetFuBangZhu()
        {
            if (!_isActive) return null;
            foreach (var mb in _members.Values)
            {
                if (mb.Online)
                {
                    if (mb.SectMemberType == SectMemberType.FuBangZhu)
                    {
                        return mb;
                    }
                }
            }
            return null;
        }

        public SectGrain(ILogger<SectGrain> logger)
        {
            _logger = logger;
        }

        public override Task OnActivateAsync()
        {
            _sectId = (uint) this.GetPrimaryKeyLong();
            return Task.CompletedTask;
        }

        public override Task OnDeactivateAsync()
        {
            return ShutDown();
        }

        public async Task StartUp()
        {
            if (_isActive) return;
            _isActive = true;
            // 从数据库中获取数据
            _entity = await DbService.QuerySect(_sectId);
            if (_entity == null || _entity.OwnerId == 0)
            {
                LogDebug("不存在, 立即解散");
                await Dismiss();
                return;
            }

            _lastEntity = new SectEntity();
            _lastEntity.CopyFrom(_entity);
            _serverGrain = GrainFactory.GetGrain<IServerGrain>(_entity.ServerId);
            if (!await _serverGrain.CheckActive())
            {
                DeactivateOnIdle();
                return;
            }

            _members = new Dictionary<uint, SectMember>(100);
            _applyList = new Dictionary<uint, SectApplyJoinData>();

            // 获取所有成员信息, 不知道这里是否需要等待很久, 如果成员较多，而且角色表记录数非常庞大的时候
            var list = await DbService.QuerySectMembers(_sectId);
            foreach (var mbd in list)
            {
                var sm = new SectMember {Data = mbd, Grain = GrainFactory.GetGrain<IPlayerGrain>(mbd.Id)};
                // 皮肤
                sm.Data.Skins.Clear();
                sm.Data.Skins.AddRange(await RedisService.GetRoleSkin(mbd.Id));
                // 武器
                sm.Data.Weapon = await RedisService.GetRoleWeapon(mbd.Id);
                // 翅膀
                sm.Data.Wing = await RedisService.GetRoleWing(mbd.Id);

                _members.Add(mbd.Id, sm);
                // 记录帮主
                if (mbd.Id == _entity.OwnerId) _owner = sm;
            }

            // 如果成员为空
            if (_members.Count == 0 || _owner == null)
            {
                LogDebug("没有成员, 立即解散");
                await Dismiss();
                return;
            }

            BuildSectData();
            UploadInfoToServer();

            _updateTimer = RegisterTimer(OnUpdate, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            LogDebug("激活成功");

            await Task.CompletedTask;
        }

        public async Task ShutDown()
        {
            if (!_isActive) return;
            _updateTimer?.Dispose();
            _updateTimer = null;

            if (_entity != null && _members.Count > 0)
            {
                await SaveData();
            }

            _members?.Clear();
            _members = null;
            _sectData = null;
            _entity = null;
            _serverGrain = null;
            _applyList?.Clear();
            _applyList = null;
            _isActive = false;
            LogDebug("注销成功");
        }

        public ValueTask<bool> CheckActive()
        {
            return new(_isActive);
        }

        public async Task Join(Immutable<byte[]> reqBytes)
        {
            if (!CheckOwner()) return;
            // 参与帮战期间
            if (_sectWaring) return;

            var req = SectMemberData.Parser.ParseFrom(reqBytes.Value);
            if (req.Type == SectMemberType.BangZhu)
            {
                // 说明是帮主创建后第一次进入
                _owner.OnEnterSect(_sectData.Id, _sectData.Name, _owner.Id);
                // 给帮主下发帮派信息
                _owner.SendSectData(_sectData);
                // 下发帮派成员
                _owner.SendPacket(GameCmd.S2CSectMemberList, new S2C_SectMemberList {List = {req}});
                // 推送帮派的申请列表
                _owner.SendPacket(GameCmd.S2CSectJoinApplyList, new S2C_SectJoinApplyList());
            }
            else
            {
                // 成员进入, 默认为帮众
                req.Type = SectMemberType.BangZhong;
                var sm = new SectMember {Data = req, Grain = GrainFactory.GetGrain<IPlayerGrain>(req.Id)};
                _members[sm.Id] = sm;
                BuildSectData();
                UploadInfoToServer();

                sm.OnEnterSect(_sectData.Id, _sectData.Name, _owner.Id);
                // 给成员下发帮派信息
                sm.SendSectData(_sectData);
                // 下发第一页的成员
                {
                    var resp = new S2C_SectMemberList
                    {
                        PageIndex = 1,
                        Total = (uint) _members.Count
                    };
                    var query = _members.Values.Take(GameDefine.SectMemberListPageSize);
                    foreach (var xmb in query)
                    {
                        resp.List.Add(xmb.Data);
                    }

                    sm.SendPacket(GameCmd.S2CSectMemberList, resp);
                }
            }

            await Task.CompletedTask;
        }

        public async Task Exit(uint roleId)
        {
            if (!CheckOwner()) return;
            _members.TryGetValue(roleId, out var sm);
            if (sm == null) return;
            // 参与帮战期间
            if (_sectWaring)
            {
                SendPacket(sm.Grain, GameCmd.S2CNotice, new S2C_Notice {Text = "帮战期间不能退出帮派"});
                return;
            }

            if (roleId == _entity.OwnerId || sm.Data.Type == SectMemberType.BangZhu)
            {
                // 帮主退出就会解散帮会
                await Dismiss();
                return;
            }

            _members.Remove(roleId);
            sm.OnExitSect(_sectId, _sectData.Name, _owner.Id);

            BuildSectData();
            UploadInfoToServer();
        }

        public async ValueTask<bool> Dismiss()
        {
            if (!_isActive) return false;
            // 参与帮战期间
            if (_sectWaring) return false;

            if (_members != null)
            {
                try
                {
                    var ownerId = _owner?.Id ?? 0;
                    foreach (var xmb in _members.Values)
                    {
                        xmb.SendPacket(GameCmd.S2CNotice, new S2C_Notice {Text = "帮派已解散"});
                        xmb.OnExitSect(_sectId, _sectData.Name, ownerId);
                    }
                }
                catch
                {
                    // ignore
                }
            }

            // 删除数据库
            await DbService.DeleteEntity<SectEntity>(_sectId);
            // 通知Server
            await _serverGrain.DeleteSect(_sectId);

            DeactivateOnIdle();
            return true;
        }

        public async ValueTask<bool> Online(uint roleId)
        {
            if (!CheckOwner()) return false;

            _members.TryGetValue(roleId, out var mb);
            if (mb == null) return false;
            mb.Data.Online = true;
            // 推送帮派信息, 帮派成员暂时不推, 等前端打开界面的时候来主动请求
            mb.SendSectData(_sectData);

            // 给帮主推送帮派的申请列表, 只发第一页
            if (_owner.Id == roleId)
            {
                mb.SendPacket(GameCmd.S2CSectJoinApplyList,
                    new S2C_SectJoinApplyList
                    {
                        Total = (uint) _applyList.Count,
                        List = {_applyList.Values.Take(GameDefine.SectApplyJoinListPageSize)}
                    });
            }

            await Task.CompletedTask;
            return true;
        }

        public async Task Offline(uint roleId)
        {
            if (!CheckOwner()) return;

            _members.TryGetValue(roleId, out var mb);
            if (mb == null) return;
            mb.Data.Online = false;
            await Task.CompletedTask;
        }

        public async Task SetPlayerName(uint roleId, string name)
        {
            if (!CheckOwner()) return;

            _members.TryGetValue(roleId, out var mb);
            if (mb == null) return;
            mb.Data.Name = name;

            if (roleId == _owner.Id)
            {
                BuildSectData();
                UploadInfoToServer();
            }

            await Task.CompletedTask;
        }

        public async Task SetPlayerLevel(uint roleId, uint relive, uint level)
        {
            if (!CheckOwner()) return;

            _members.TryGetValue(roleId, out var mb);
            if (mb == null) return;
            mb.Data.Relive = relive;
            mb.Data.Level = level;

            await Task.CompletedTask;
        }

        public async Task SetPlayerCfgId(uint roleId, uint cfgId)
        {
            if (!CheckOwner()) return;

            _members.TryGetValue(roleId, out var mb);
            if (mb == null) return;
            mb.Data.CfgId = cfgId;

            await Task.CompletedTask;
        }

        public async Task SetPlayerSkin(uint roleId, List<int> skinUse)
        {
            if (!CheckOwner()) return;
            _members.TryGetValue(roleId, out var mb);
            if (mb == null) return;
            mb.Data.Skins.Clear();
            mb.Data.Skins.AddRange(skinUse);

            await Task.CompletedTask;
        }

        public async Task SetPlayerWeapon(uint roleId, uint cfgId, int category, uint gem, uint level)
        {
            if (!CheckOwner()) return;
            _members.TryGetValue(roleId, out var mb);
            if (mb == null) return;
            mb.Data.Weapon.CfgId = cfgId;
            mb.Data.Weapon.Category = (EquipCategory)category;
            mb.Data.Weapon.Gem = gem;
            mb.Data.Weapon.Level = level;

            await Task.CompletedTask;
        }

        public async Task SetPlayerWing(uint roleId, uint cfgId, int category, uint gem, uint level)
        {
            if (!CheckOwner()) return;
            _members.TryGetValue(roleId, out var mb);
            if (mb == null) return;
            mb.Data.Wing.CfgId = cfgId;
            mb.Data.Wing.Category = (EquipCategory)category;
            mb.Data.Wing.Gem = gem;
            mb.Data.Wing.Level = level;

            await Task.CompletedTask;
        }

        public async Task ApplyJoin(Immutable<byte[]> reqBytes)
        {
            if (!CheckOwner()) return;
            var apply = SectApplyJoinData.Parser.ParseFrom(reqBytes.Value);
            var applyGrain = GrainFactory.GetGrain<IPlayerGrain>(apply.RoleId);

            // 参与帮战期间
            // if (_sectWaring)
            // {
            //     SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "帮战期间不能申请入帮"});
            //     return;
            // }

            // 检查是否满员
            if (MemberNum >= 1000)
            {
                SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "帮派满员，请选择其他帮派"});
                return;
            }

            // 申请人数
            if (_applyList.Count >= 500)
            {
                SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "申请人数过多，请选择其他帮派"});
                return;
            }

            // 查找该角色是否已经申请过
            if (_applyList.ContainsKey(apply.RoleId))
            {
                SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "已经申请过，请等待帮主的处理"});
                return;
            }

            // 记录申请
            _applyList.Add(apply.RoleId, apply);
            // 通知申请成功
            SendPacket(applyGrain, GameCmd.S2CNotice, new S2C_Notice {Text = "已申请，请等待帮主确认"});

            // 通知帮主处理
            _owner.SendPacket(GameCmd.S2CSectJoinApplyAdd, new S2C_SectJoinApplyAdd {Data = apply});
            // 通知副帮主处理
            SectMember fubangzhu = GetFuBangZhu();
            if (fubangzhu != null)
            {
                fubangzhu.SendPacket(GameCmd.S2CSectJoinApplyAdd, new S2C_SectJoinApplyAdd { Data = apply });
            }
            await Task.CompletedTask;
        }

        /// <summary>
        /// 队长处理加入申请
        /// </summary>
        public async Task HandleJoinApply(uint roleId, uint applyId, bool agree)
        {
            if (!CheckOwner()) return;
            // 检测自己是否是队长
            SectMember fubangzhu = GetFuBangZhu();
            if (_owner.Id != roleId && (fubangzhu == null || fubangzhu.Id != roleId)) return;
            _applyList.TryGetValue(applyId, out var apply);
            if (apply == null) return;

            // 参与帮战期间
            // if (_sectWaring)
            // {
            //     SendPacket(GrainFactory.GetGrain<IPlayerGrain>(_owner.Id), GameCmd.S2CNotice,
            //         new S2C_Notice {Text = "帮战期间不能处理申请"});
            //     return;
            // }

            // 清理申请
            _applyList.Remove(applyId);
            // 通知帮主
            _owner.SendPacket(GameCmd.S2CSectJoinApplyDel, new S2C_SectJoinApplyDel { RoleId = applyId });
            //通知副帮主
            if (fubangzhu != null)
            {
                fubangzhu.SendPacket(GameCmd.S2CSectJoinApplyDel, new S2C_SectJoinApplyDel { RoleId = applyId });
            }           

            // 同意了, 让PlayerGrain主动加入进来，传递自己的信息
            if (agree)
            {
                // 检查是否满员
                if (MemberNum >= 1000)
                {
                    _owner.SendPacket(GameCmd.S2CNotice, new S2C_Notice {Text = "帮派已满员"});
                    return;
                }

                var applyGrain = GrainFactory.GetGrain<IPlayerGrain>(apply.RoleId);
                _ = applyGrain.OnSectJoinApplyAgree(_sectData.Id, _sectData.OwnerId);
            }

            await Task.CompletedTask;
        }

        public async Task Kickout(uint roleId, uint mbRoleId)
        {
            if (!CheckOwner()) return;
            // 不能踢自己
            if (roleId == mbRoleId) return;

            // 帮主或副帮主才可以踢人
            _members.TryGetValue(roleId, out var opsm);
            if (opsm == null ||
                opsm.Data.Type != SectMemberType.BangZhu &&
                opsm.Data.Type != SectMemberType.FuBangZhu)
                return;

            // 不能踢帮主
            _members.TryGetValue(mbRoleId, out var sm);
            if (sm == null || sm.Data.Type == SectMemberType.BangZhu) return;

            // 参与帮战期间
            if (_sectWaring)
            {
                SendPacket(opsm.Grain, GameCmd.S2CNotice, new S2C_Notice {Text = "帮战期间不能踢除成员"});
                return;
            }

            _members.Remove(mbRoleId);
            sm.OnExitSect(_sectId, _sectData.Name, _owner.Id);

            // 通知操作者
            opsm.SendPacket(GameCmd.S2CSectMemberLeave, new S2C_SectMemberLeave {RoleId = mbRoleId});

            // 更新给服务器
            BuildSectData();
            UploadInfoToServer();

            await Task.CompletedTask;
        }

        public async ValueTask<bool> Contrib(uint roleId, uint jade)
        {
            if (!CheckOwner()) return false;
            _members.TryGetValue(roleId, out var sm);
            if (sm == null) return false;
            sm.Data.Contrib += jade;
            // 增加贡献值并通知Server更新排序
            _entity.Contrib += jade;
            BuildSectData();
            UploadInfoToServer();
            await Task.CompletedTask;
            return true;
        }

        /// <summary>
        /// 帮派的管理层，设有帮主1名，副帮主1名，左护法1名，右护法1名，长老5名，堂主2名。  //   这里可以委任副帮主
        /// </summary>
        public async Task<string> Appoint(uint roleId, uint targetRoleId, byte job)
        {
            if (!CheckOwner()) return "操作出错";
            _members.TryGetValue(roleId, out var sm);
            if (sm == null) return "不在帮派内";
            _members.TryGetValue(targetRoleId, out var targetSm);
            if (targetSm == null) return "对方不在帮派内";

            var type = (SectMemberType) job;

            var isEnable = false;
            // 帮主可以对所有人任命，副帮主只能对低于副帮主职位的人进行任命
            if (sm.Data.Type == SectMemberType.BangZhu ||
                sm.Data.Type == SectMemberType.FuBangZhu && sm.Data.Type < targetSm.Data.Type)
            {
                // 帮主可以将目标任命为任何职位, 副帮主只能任命低于副帮主的职位
                if (sm.Data.Type == SectMemberType.BangZhu ||
                    sm.Data.Type == SectMemberType.FuBangZhu && type > SectMemberType.FuBangZhu)
                {
                    isEnable = true;
                }

                // 任命帮主必须先任命副帮主
                if (isEnable && type == SectMemberType.BangZhu && targetSm.Data.Type != SectMemberType.FuBangZhu)
                {
                    return "请先对他任命副帮主";
                }
            }

            if (!isEnable) return "权限不够";

            SectMember lastSm = null;

            switch (type)
            {
                case SectMemberType.BangZhu:
                case SectMemberType.FuBangZhu:
                case SectMemberType.ZuoHuFa:
                case SectMemberType.YouHuFa:
                {
                    // 原来的帮主变成帮众
                    lastSm = _members.Values.FirstOrDefault(p => p.Data.Type == type);
                    if (lastSm != null) lastSm.Data.Type = SectMemberType.BangZhong;
                    // 新的帮主
                    targetSm.Data.Type = type;
                    break;
                }
                case SectMemberType.ZhangLao:
                {
                    // 检测是否已经5人
                    var num = _members.Values.Count(p => p.Data.Type == SectMemberType.ZhangLao);
                    if (num >= 5) return "帮派中已经存在5个长老, 请先卸任其中1个长老";
                    // 新的长老
                    targetSm.Data.Type = SectMemberType.ZhangLao;
                    break;
                }
                case SectMemberType.TangZhu:
                {
                    // 检测是否已经2人
                    var num = _members.Values.Count(p => p.Data.Type == SectMemberType.TangZhu);
                    if (num >= 2) return "帮派中已经存在2个堂主, 请先卸任其中1个堂主";
                    // 新的堂主
                    targetSm.Data.Type = SectMemberType.TangZhu;
                    break;
                }
                case SectMemberType.TuanZhang:
                {
                    // 检测是否已经5人
                    var num = _members.Values.Count(p => p.Data.Type == SectMemberType.TuanZhang);
                    if (num >= 5) return "帮派中已经存在5个团长, 请先卸任其中1个团长";
                    // 新的团长
                    targetSm.Data.Type = SectMemberType.TuanZhang;
                    break;
                }
                case SectMemberType.BangZhong:
                {
                    targetSm.Data.Type = SectMemberType.BangZhong;
                    break;
                }
                default:
                {
                    return "无法识别该职位";
                }
            }

            // 帮主, 需要特殊处理
            if (type == SectMemberType.BangZhu)
            {
                // 实时修改
                _entity.OwnerId = targetSm.Id;
                _lastEntity.OwnerId = _entity.OwnerId;
                await DbService.UpdateSectOwner(_sectId, targetSm.Id);
                // 更换帮主
                _owner = targetSm;
                BuildSectData();
                UploadInfoToServer();
            }

            // 通知原该职位的成员
            lastSm?.OnSectJob(_sectId, _sectData.Name, lastSm.Id, lastSm.Data.Type);
            // 通知新职位的成员
            targetSm.OnSectJob(_sectId, _sectData.Name, targetSm.Id, targetSm.Data.Type);
            // 如果操作者不是原职位成员, 需要通知
            if (lastSm == null || lastSm.Id != sm.Id)
            {
                if (lastSm != null)
                    sm.OnSectJob(_sectId, _sectData.Name, lastSm.Id, lastSm.Data.Type);
                sm.OnSectJob(_sectId, _sectData.Name, targetSm.Id, targetSm.Data.Type);
            }

            await Task.CompletedTask;
            return null;
        }

        public async Task<string> Silent(uint roleId, uint targetRoleId)
        {
            if (!CheckOwner()) return "操作出错";
            _members.TryGetValue(roleId, out var sm);
            if (sm == null) return "不在帮派内";
            _members.TryGetValue(targetRoleId, out var targetSm);
            if (targetSm == null) return "对方不在帮派内";
            if (roleId == targetRoleId) return "我狠起来连自己都不放过";

            // 帮主或副帮主才可以禁言，并且只能禁言职位比自己低的人
            var isEnable = (sm.Data.Type == SectMemberType.BangZhu || sm.Data.Type == SectMemberType.FuBangZhu) &&
                           sm.Data.Type < targetSm.Data.Type;
            if (!isEnable) return "权限不够";

            await Task.CompletedTask;
            targetSm.OnSectSilent(_sectId, _sectData.Name, sm.Id, sm.Data.Name, sm.Data.Type);
            return null;
        }

        public async Task<string> ChangeDesc(uint roleId, string desc)
        {
            if (!CheckOwner()) return "操作出错";
            _members.TryGetValue(roleId, out var sm);
            if (sm == null) return "不在帮派内";
            // 帮主或副帮主才可以修改帮派介绍
            var isEnable = sm.Data.Type == SectMemberType.BangZhu || sm.Data.Type == SectMemberType.FuBangZhu;
            if (!isEnable) return "权限不够";

            _entity.Desc = desc;
            BuildSectData();
            UploadInfoToServer();

            await Task.CompletedTask;
            return null;
        }

        public async Task GetMemberList(uint roleId, int pageIndex)
        {
            if (!CheckOwner()) return;

            if (pageIndex < 1) pageIndex = 1;
            _members.TryGetValue(roleId, out var mb);
            if (mb == null) return;

            var resp = new S2C_SectMemberList
            {
                PageIndex = (uint) pageIndex,
                Total = (uint) _members.Count
            };
            var query = _members.Values
                .OrderBy(it => it.Data.Type)
                .ThenByDescending(it => it.Data.Contrib)
                .Skip((pageIndex - 1) * GameDefine.SectMemberListPageSize)
                .Take(GameDefine.SectMemberListPageSize);
            foreach (var sm in query)
            {
                resp.List.Add(sm.Data);
            }

            mb.SendPacket(GameCmd.S2CSectMemberList, resp);
            await Task.CompletedTask;
        }

        // 申请列表永远都是只下发第一条
        public async Task GetJoinApplyList(uint roleId)
        {
            if (!CheckOwner() || _owner.Id != roleId) return;
            var resp = new S2C_SectJoinApplyList
            {
                Total = (uint) _applyList.Count,
                List = {_applyList.Values.Take(GameDefine.SectApplyJoinListPageSize)}
            };
            _owner.SendPacket(GameCmd.S2CSectJoinApplyList, resp);
            await Task.CompletedTask;
        }

        public Task Broadcast(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return Task.CompletedTask;
            foreach (var mb in _members.Values)
            {
                if (mb.Online)
                {
                    _ = mb.Grain.SendMessage(reqBytes);
                }
            }

            return Task.CompletedTask;
        }

        public Task SyncSectWaring(bool value)
        {
            if (!_isActive) return Task.CompletedTask;
            _sectWaring = value;
            return Task.CompletedTask;
        }

        private async Task OnUpdate(object _)
        {
            _tickCnt++;
            if (_tickCnt >= 300)
            {
                _tickCnt = 0;
                await SaveData();
            }
        }

        private int MemberNum => _members?.Count ?? 0;

        /// <summary>
        /// 保存数据库
        /// </summary>
        private async Task SaveData()
        {
            if (_entity == null || _lastEntity == null) return;
            if (_entity.Equals(_lastEntity)) return;
            var ret = await DbService.UpdateEntity(_lastEntity, _entity);
            if (ret) _lastEntity.CopyFrom(_entity);
        }

        /// <summary>
        /// 上报信息给Server
        /// </summary>
        private void UploadInfoToServer()
        {
            if (_entity == null) return;
            _serverGrain.UpdateSect(new Immutable<byte[]>(Packet.Serialize(_sectData)));
        }

        /// <summary>
        /// 重新构建SectData
        /// </summary>
        private void BuildSectData()
        {
            if (!_isActive) return;
            _entity.MemberNum = (uint) _members.Count;

            _sectData ??= new SectData();
            _sectData.Id = _entity.Id;
            _sectData.Name = _entity.Name;
            _sectData.Desc = _entity.Desc;
            _sectData.Total = _entity.MemberNum;
            _sectData.OwnerId = _owner.Id;
            _sectData.OwnerName = _owner.Data.Name;
            _sectData.Contrib = _entity.Contrib;
            _sectData.CreateTime = _entity.CreateTime;
        }

        private bool CheckOwner()
        {
            if (_owner == null || _entity == null)
            {
                _ = Dismiss();
                return false;
            }

            return _isActive;
        }

        private void SendPacket(IPlayerGrain grain, GameCmd command, IMessage msg)
        {
            if (grain == null || !_isActive) return;
            _ = grain.SendMessage(new Immutable<byte[]>(Packet.Serialize(command, msg)));
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"帮派[{_sectId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"帮派[{_sectId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"帮派[{_sectId}]:{msg}");
        }
    }
}