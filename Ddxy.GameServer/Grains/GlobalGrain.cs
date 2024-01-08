using System.Collections.Generic;
using System.Threading.Tasks;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Vo;
using Ddxy.GameServer.Util;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    [CollectionAgeLimit(AlwaysActive = true)]
    public class GlobalGrain : Grain, IGlobalGrain
    {
        private bool _isActive;
        // 记录启动的区服及其在线玩家数量
        private IDictionary<uint, int> _servers = new Dictionary<uint, int>(10);

        // 存储所有激活的玩家
        private Dictionary<uint, bool> _players = new();

        // 战斗id发生器
        private IdGen<byte> _battleIdGen = new(1000);

        private ResVersionVo _resVersion;
        private Immutable<byte[]> _resVersionBytes;

        public override async Task OnActivateAsync()
        {
            _resVersion = await RedisService.GetResVersion();
            if (_resVersion != null && !string.IsNullOrWhiteSpace(_resVersion.Version))
            {
                _resVersionBytes = new Immutable<byte[]>(Json.SerializeToBytes(_resVersion));
            }
            else
            {
                _resVersionBytes = new Immutable<byte[]>(null);
            }
        }

        public override async Task OnDeactivateAsync()
        {
            _servers.Clear();
            _servers = null;
            _players.Clear();
            _players = null;
            _battleIdGen.Dispose();
            _battleIdGen = null;
            _resVersion = null;
            await Task.CompletedTask;
        }

        public Task StartUp()
        {
            if (_isActive) return Task.CompletedTask;
            _isActive = true;
            return Task.CompletedTask;
        }

        public Task UpdateServer(uint serverId, int onlineNum)
        {
            _servers[serverId] = onlineNum;
            return Task.CompletedTask;
        }

        public Task RemoveServer(uint serverId)
        {
            _servers.Remove(serverId);
            return Task.CompletedTask;
        }

        public ValueTask<int> CheckServer(uint serverId)
        {
            if (!_servers.TryGetValue(serverId, out var onlineNum))
            {
                return new ValueTask<int>(-1);
            }
            
            return new ValueTask<int>(onlineNum);
        }

        public Task UpdatePlayer(uint roleId)
        {
            _players[roleId] = true;
            return Task.CompletedTask;
        }

        public Task RemovePlayer(uint roleId)
        {
            _players.Remove(roleId);
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckPlayer(uint roleId)
        {
            return new ValueTask<bool>(_players.ContainsKey(roleId));
        }

        public ValueTask<uint> CreateBattle()
        {
            var id = _battleIdGen.Gain();
            return new ValueTask<uint>(id);
        }

        public Task RemoveBattle(uint battleId)
        {
            _battleIdGen.Recycle(battleId);
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckBattle(uint battleId)
        {
            var ret = _battleIdGen.Exists(battleId);
            return new ValueTask<bool>(ret);
        }

        public Task<Immutable<byte[]>> GetResVersion()
        {
            return Task.FromResult(_resVersionBytes);
        }

        public async Task SetResVersion(string version, bool force)
        {
            version = version?.Trim();
            _resVersion ??= new ResVersionVo();
            _resVersion.Version = version;
            _resVersion.Force = force;

            if (string.IsNullOrWhiteSpace(_resVersion.Version))
            {
                await RedisService.DelResVersion();
                _resVersionBytes = new Immutable<byte[]>(null);
            }
            else
            {
                await RedisService.SetResVersion(_resVersion);
                _resVersionBytes = new Immutable<byte[]>(Json.SerializeToBytes(_resVersion));
            }
        }
    }
}