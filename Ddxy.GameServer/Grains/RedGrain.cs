using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.Protocol;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;
using Ddxy.GameServer.Data.Entity;

namespace Ddxy.GameServer.Grains
{
    /// <summary>
    /// 红包服务, 每天晚上10点刷新
    /// </summary>
    [CollectionAgeLimit(AlwaysActive = true)]
    public class RedGrain : Grain, IRedGrain
    {
        private bool _isActive;
        private uint _serverId;
        private IServerGrain _serverGrain;

        private Dictionary<uint, RedSendRecordEntity> _sendEntities = new();
        private Dictionary<uint, RedSendRecordEntity> _lastSendEntities = new();

        private uint _lastSendTimestamp = 0;
        private ILogger<RedGrain> _logger;

        private Random _random = new Random();

        // 每秒调用一次
        private IDisposable _updateTimer;
        // 保存数据的频率
        private int _saveDataTick = 0;

        private const string Name = "红包服务";

        private const uint OneMonth = 7 * 24 * 3600;

        public RedGrain(ILogger<RedGrain> logger)
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

        public async Task StartUp()
        {
            if (_isActive) return;
            _isActive = true;
            _serverGrain = GrainFactory.GetGrain<IServerGrain>(_serverId);
            // 所有发送的，但是没有领完的红包
            _sendEntities.Clear();
            _lastSendEntities.Clear();
            var endTimestamp = _lastSendTimestamp = TimeUtil.TimeStamp - OneMonth;
            var entityList = await DbService.Sql.Queryable<RedSendRecordEntity>()
                            .Where(it => it.ServerId == _serverId && it.SendTime >= endTimestamp)
                            .ToListAsync();
            uint maxSendTime = 0;
            foreach (var entity in entityList)
            {
                await entity.ParseReciver($"红包服务[{_serverId}]:", _logger);
                _sendEntities.Add(entity.Id, entity);
                _lastSendEntities.Add(entity.Id, entity.MakeCopy());
                if (entity.SendTime >= maxSendTime)
                {
                    maxSendTime = entity.SendTime;
                }
            }
            _lastSendTimestamp = maxSendTime;
            _saveDataTick = 0;
            // 每秒tick一次
            _updateTimer = RegisterTimer(Update, null, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1));
            LogDebug("激活成功");
        }

        public async Task ShutDown()
        {
            if (!_isActive) return;
            _isActive = false;
            _updateTimer?.Dispose();
            _updateTimer = null;
            _saveDataTick = 0;
            // 保存
            await SaveAllData();
            _sendEntities.Clear();
            _sendEntities = null;
            _lastSendEntities.Clear();
            _lastSendEntities = null;

            _serverGrain = null;
            LogDebug("注销成功");
            DeactivateOnIdle();
        }

        public ValueTask<bool> CheckActive()
        {
            return new ValueTask<bool>(_isActive);
        }

        public async Task Enter(uint roleId, uint sectId)
        {
            var resp = new S2C_RedEnterMain();
            if (_isActive)
            {
                var end = _lastSendTimestamp;
                // 查询新的
                var rows = await DbService.Sql.Queryable<RedSendRecordEntity>()
                                .Where(it => it.ServerId == _serverId
                                 && it.SendTime > end
                                 && ((it.RedType == (byte)RedType.Sect) ? (sectId > 0 ? it.SectId == sectId : it.SectId == 0) : true))
                                .OrderByDescending(it => it.SendTime)
                                .ToListAsync();
                // 插入老的
                List<RedSendRecordEntity> old = new List<RedSendRecordEntity>(_sendEntities.Values);
                old.Sort((a, b) => (int)(b.SendTime - a.SendTime));
                rows.AddRange(old);
                // 开始处理
                foreach (var row in rows)
                {
                    RedSendRecordEntity entity = null;
                    if (_sendEntities.ContainsKey(row.Id))
                    {
                        entity = _sendEntities[row.Id];
                    }
                    else
                    {
                        await row.ParseReciver($"红包服务[{_serverId}]:", _logger);
                        entity = row;
                        _sendEntities[row.Id] = row;
                    }
                    if (entity.Sender == null)
                    {
                        var pgrain = GrainFactory.GetGrain<IPlayerGrain>(row.RoleId);
                        var bytes = await pgrain.GetRoleInfo();
                        if (bytes.Value == null)
                        {
                            LogError($"进入[{row.Id}]，无法获得玩家[{row.RoleId}]信息");
                            continue;
                        }
                        entity.Sender = RoleInfo.Parser.ParseFrom(bytes.Value);
                    }
                    var i = new RedItem()
                    {
                        Id = entity.Id,
                        Type = (RedType)entity.RedType,
                        Wish = entity.Wish,
                    };
                    i.Role = entity.Sender;
                    if (entity.ReciverList.Contains(roleId))
                    {
                        i.State = RedState.Done;
                    }
                    else if (entity.ReciverList.Count >= entity.Total)
                    {
                        i.State = RedState.Out;
                    }
                    else
                    {
                        i.State = RedState.Wait;
                    }
                    resp.List.Add(i);
                }
            }
            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            _ = playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CRedEnterMain, resp)));
        }

        public async Task Detail(uint roleId, uint redId)
        {
            var resp = new S2C_RedDetail();
            if (_isActive && _sendEntities.ContainsKey(redId))
            {
                var entity = _sendEntities[redId];
                resp.Id = redId;
                resp.Timestamp = entity.SendTime;
                resp.Wish = entity.Wish;
                resp.Jade = entity.Jade;
                resp.Total = entity.Total;
                resp.Role = entity.Sender;
                // 检查红包是否已经抢包信息
                var okay = true;
                foreach (var rid in entity.ReciverList)
                {
                    if (!entity.Getter.ContainsKey(rid))
                    {
                        okay = false;
                        break;
                    }
                }
                // 没有则构建
                if (!okay)
                {
                    var rows = await DbService.Sql.Queryable<RedReciveRecordEntity>()
                                    .Where(it => it.ServerId == _serverId && it.RedId == redId)
                                    .OrderBy(it => it.ReciveTime)
                                    .ToListAsync();
                    foreach (var row in rows)
                    {
                        var getter = new RedGetter() { Jade = row.Jade, ReciveTime = row.ReciveTime };
                        var bytes = await GrainFactory.GetGrain<IPlayerGrain>(row.ReciveId).GetRoleInfo();
                        if (bytes.Value == null)
                        {
                            LogError($"详情[{row.Id}]，无法获得玩家[{row.ReciveId}]信息");
                        }
                        else
                        {
                            getter.Role = RoleInfo.Parser.ParseFrom(bytes.Value);
                        }
                        entity.Getter.Add(row.ReciveId, getter);
                    }
                }
                resp.GetterList.AddRange(entity.Getter.Values);
            }
            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            _ = playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CRedDetail, resp)));
        }

        public async Task History(uint roleId, byte redType, bool recived)
        {
            var resp = new S2C_RedHistory() { Type = (RedType)redType, Recived = recived };
            var end = TimeUtil.TimeStamp - OneMonth;
            if (recived)
            {
                var rows = await DbService.Sql.Queryable<RedReciveRecordEntity>()
                                        .Where(it => it.ServerId == _serverId && it.RedType == redType && it.ReciveId == roleId && it.ReciveTime >= end)
                                        .OrderBy(it => it.ReciveTime)
                                        .ToListAsync();
                foreach (var row in rows)
                {
                    var log = new RedLog() { Id = row.RedId, Timestamp = row.ReciveTime, Jade = row.Jade };
                    var bytes = await GrainFactory.GetGrain<IPlayerGrain>(row.SendId).GetRoleInfo();
                    if (bytes.Value == null)
                    {
                        LogError($"历史[{row.Id}]，无法获得玩家[{row.SendId}]信息");
                    }
                    else
                    {
                        log.Role = RoleInfo.Parser.ParseFrom(bytes.Value);
                    }
                    resp.LogList.Add(log);
                }
            }
            else
            {
                var rows = await DbService.Sql.Queryable<RedSendRecordEntity>()
                                                        .Where(it => it.ServerId == _serverId && it.RedType == redType && it.RoleId == roleId && it.SendTime >= end)
                                                        .OrderBy(it => it.SendTime)
                                                        .ToListAsync();
                foreach (var row in rows)
                {
                    var log = new RedLog() { Id = row.Id, Timestamp = row.SendTime, Jade = row.Jade };
                    resp.LogList.Add(log);
                }
            }
            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            _ = playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CRedHistory, resp)));
        }

        public async ValueTask<uint> Send(uint roleId, uint sectId, byte redType, string wish, uint jade, uint total)
        {
            if (!_isActive) return 0;
            var entity = new RedSendRecordEntity()
            {
                ServerId = _serverId,
                RoleId = roleId,
                RedType = redType,
                SectId = sectId,
                Jade = jade,
                Total = total,
                Wish = wish,
                Left = total,
                Reciver = "[]",
                SendTime = this._lastSendTimestamp = TimeUtil.TimeStamp,
            };
            using var repo = DbService.Sql.GetRepository<RedSendRecordEntity>();
            await repo.InsertAsync(entity);
            if (entity.Id > 0)
            {
                await entity.ParseReciver($"红包服务[{_serverId}]:", _logger);
                var bytes = await GrainFactory.GetGrain<IPlayerGrain>(entity.RoleId).GetRoleInfo();
                if (bytes.Value == null)
                {
                    LogError($"发送[{entity.Id}]，无法获得玩家[{entity.RoleId}]信息");
                }
                else
                {
                    entity.Sender = RoleInfo.Parser.ParseFrom(bytes.Value);
                }
                _sendEntities.Add(entity.Id, entity);
                _lastSendEntities.Add(entity.Id, entity.MakeCopy());
            }
            return entity.Id;
        }

        public async Task Get(uint roleId, uint redId)
        {
            var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
            if (_isActive || _sendEntities == null || !_sendEntities.ContainsKey(redId))
            {
                var entity = _sendEntities[redId];
                if (entity.ReciverList.Contains(roleId))
                {
                    await playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice() { Text = "此红包，你已经领取了！" })));
                    return;
                }
                if (entity.Left <= 0 || entity.RedJadeList.Count <= 0)
                {
                    await playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice() { Text = "晚了，已被抢光了" })));
                    return;
                }
                try
                {
                    var index = _random.Next(entity.RedJadeList.Count);
                    var getter = new RedGetter() { ReciveTime = TimeUtil.TimeStamp, Jade = entity.RedJadeList[index] };
                    var rentity = new RedReciveRecordEntity()
                    {
                        ServerId = _serverId,
                        ReciveId = roleId,
                        SendId = entity.RoleId,
                        RedId = entity.Id,
                        RedType = entity.RedType,
                        Jade = getter.Jade,
                        ReciveTime = TimeUtil.TimeStamp,
                    };
                    using var repo = DbService.Sql.GetRepository<RedReciveRecordEntity>();
                    await repo.InsertAsync(rentity);
                    if (rentity.Id > 0)
                    {
                        entity.RedJadeList.RemoveAt(index);
                        var bytes = await playerGrain.GetRoleInfo();
                        if (bytes.Value == null)
                        {
                            LogError($"领取[{entity.Id}]，无法获得玩家[{roleId}]信息");
                        }
                        else
                        {
                            getter.Role = RoleInfo.Parser.ParseFrom(bytes.Value);
                        }
                        // 加仙玉
                        await playerGrain.AddMoney((byte)MoneyType.Jade, (int)getter.Jade, "抢红包", true);
                        // 更新接收者列表
                        entity.Getter.Add(roleId, getter);
                        entity.ReciverList.Add(roleId);
                        entity.SyncReciver();
                        entity.Left -= 1;
                        var resp = new S2C_RedGet()
                        {
                            Id = redId,
                            Timestamp = entity.SendTime,
                            Wish = entity.Wish,
                            Jade = entity.Jade,
                            Total = entity.Total,
                            Role = entity.Sender,
                        };
                        resp.GetterList.AddRange(entity.Getter.Values);
                        await playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CRedGet, resp)));
                    }
                    else
                    {
                        await playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice() { Text = "晚了，已被抢光了" })));
                    }
                }
                catch (Exception ex)
                {
                    await playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice() { Text = "晚了，已被抢光了" })));
                    LogError($"抢红包出错[{ex.Message}][{ex.StackTrace}]");
                }
            }
            else
            {
                await playerGrain.SendMessage(new Immutable<byte[]>(Packet.Serialize(GameCmd.S2CNotice, new S2C_Notice() { Text = "晚了，已被抢光了" })));
            }
        }
        // 每秒调用一次
        private async Task Update(object _)
        {
            if (!_isActive)
            {
                _updateTimer?.Dispose();
                _updateTimer = null;
                return;
            }
            // 到点保存数据
            _saveDataTick++;
            if (_saveDataTick >= 60)
            {
                _saveDataTick = 0;
                await SaveAllData();
            }
        }
        private async Task SaveAllData()
        {
            foreach (var (key, value) in _sendEntities)
            {
                var last = _lastSendEntities.GetValueOrDefault(key, null);
                if (value != null && last != null && !value.Equals(last))
                {
                    var ret = await DbService.UpdateEntity(last, value);
                    if (ret) last.CopyFrom(value);
                }
            }
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"红包服务[{_serverId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"红包服务[{_serverId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"红包服务[{_serverId}]:{msg}");
        }
    }
}