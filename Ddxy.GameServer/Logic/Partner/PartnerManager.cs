using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.Protocol;
using Orleans.Concurrency;

namespace Ddxy.GameServer.Logic.Partner
{
    public class PartnerManager
    {
        private PlayerGrain _player;

        /// <summary>
        /// 我获得的所有伙伴
        /// </summary>
        public Dictionary<uint, Partner> All { get; private set; }

        /// <summary>
        /// 当前参战的伙伴, 按照位置顺序排放
        /// </summary>
        public List<Partner> Actives { get; private set; }

        // 出战partner id列表
        private uint[] _actives;

        public PartnerManager(PlayerGrain player)
        {
            _player = player;
            All = new Dictionary<uint, Partner>(10);
            _actives = new uint[] {0, 0, 0, 0};
            Actives = new List<Partner>(_actives.Length);
        }

        public async Task Init()
        {
            Array.Clear(_actives, 0, _actives.Length);

            var entities = await DbService.QueryPartners(_player.RoleId);
            foreach (var entity in entities)
            {
                var partner = new Partner(_player, entity);
                All.Add(partner.Id, partner);

                if (partner.Pos > 0 && partner.Pos <= _actives.Length)
                {
                    _actives[partner.Pos - 1] = partner.Id;
                }
            }

            await RefreshActives(false);

            await Task.CompletedTask;
        }

        public async Task Destroy()
        {
            var tasks = from p in All.Values select p.Destroy();
            await Task.WhenAll(tasks);

            Actives = null;
            _actives = null;
            All.Clear();
            All = null;

            _player = null;
        }

        public Task SaveData()
        {
            var tasks = from p in All.Values select p.SaveData();
            return Task.WhenAll(tasks);
        }

        public async Task SendList()
        {
            var resp = new S2C_PartnerList
            {
                List = {All.Values.Select(p => p.BuildPbData())},
                Actives = {_actives}
            };
            await _player.SendPacket(GameCmd.S2CPartnerList, resp);
        }

        public IEnumerable<TeamObjectData> BuildTeamMembers()
        {
            return Actives.Select(p => p.BuildTeamObjectData());
        }

        // 玩家升级了，会增加潜能
        public async Task OnPlayerLevelUp()
        {
            // 判断能否解锁新的伙伴
            foreach (var v in ConfigService.Partners.Values)
            {
                if (v.Unlock > _player.Entity.Level) continue;
                // 已经有一个同样配置id的伙伴
                if (All.Values.FirstOrDefault(p => p.CfgId == v.Id) != null) continue;
                // 默认等于1级
                await AddPartner(v.Id, 1);
            }

            // 玩家35级以下, 伙伴会自动同步玩家的等级
            if (_player.Entity.Level <= GameDefine.LimitPartnerLevel)
            {
                foreach (var partner in All.Values)
                {
                    partner.SetLevel(_player.Entity.Level);
                }
                
                // 如果是队长，记得通知Team变更
                if (_player.IsTeamLeader)
                {
                    var req = new UpdateTeamPartnerRequest();
                    req.Member.AddRange(Actives.Select(p => p.BuildTeamObjectData()));
                    _ = _player.TeamGrain.UpdatePartner(new Immutable<byte[]>(Packet.Serialize(req)));
                }
            }
        }

        public async Task AddPartner(uint cfgId, byte level)
        {
            ConfigService.Partners.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return;
            var entity = new PartnerEntity
            {
                RoleId = _player.RoleId,
                CfgId = cfgId,
                Relive = 0,
                Level = level,
                Exp = 0,
                Pos = 0,
                CreateTime = TimeUtil.TimeStamp
            };
            await DbService.InsertEntity(entity);
            if (entity.Id == 0) return;

            var partner = new Partner(_player, entity);
            All.Add(partner.Id, partner);

            await partner.SendInfo();
        }

        public async Task ActivePartner(uint id, uint pos)
        {
            All.TryGetValue(id, out var partner);
            if (partner == null || partner.Pos == pos) return;
            // 检查pos
            if (pos > _actives.Length) return;

            if (pos == 0)
            {
                // 休息
                if (partner.Active) _actives[partner.Pos - 1] = 0;
            }
            else
            {
                // 参战
                _actives[(int) (pos - 1)] = partner.Id;
            }

            await RefreshActives();

            // 如果是队长，记得通知Team变更
            if (_player.IsTeamLeader)
            {
                var req = new UpdateTeamPartnerRequest();
                req.Member.AddRange(Actives.Select(p => p.BuildTeamObjectData()));
                _ = _player.TeamGrain.UpdatePartner(new Immutable<byte[]>(Packet.Serialize(req)));
            }
        }

        public async ValueTask<bool> AddPartnerExp(uint id, ulong exp)
        {
            All.TryGetValue(id, out var partner);
            if (partner == null) return false;
            var ret = await partner.AddExp(exp);
            if (ret && partner.Active && _player.IsTeamLeader)
            {
                var req = new UpdateTeamPartnerRequest();
                req.Member.AddRange(Actives.Select(p => p.BuildTeamObjectData()));
                _ = _player.TeamGrain.UpdatePartner(new Immutable<byte[]>(Packet.Serialize(req)));
            }

            return ret;
        }

        public async Task ExchangePartner(uint id1, uint id2, uint cost)
        {
            if (id1 == id2 || id1 == 0 || id2 == 0) return;
            All.TryGetValue(id1, out var partner1);
            All.TryGetValue(id2, out var partner2);
            if (partner1 == null || partner2 == null) return;
            // 银两
            var ret = await _player.CostMoney(MoneyType.Silver, 3000000, tag: "伙伴传功");
            if (!ret) return;

            // 交换转生等级、等级、经验, 会引发属性变动
            partner1.Exchange(partner2);

            if ((partner1.Active || partner2.Active) && _player.IsTeamLeader)
            {
                var req = new UpdateTeamPartnerRequest();
                req.Member.AddRange(Actives.Select(p => p.BuildTeamObjectData()));
                _ = _player.TeamGrain.UpdatePartner(new Immutable<byte[]>(Packet.Serialize(req)));
            }
        }

        public async Task RelivePartner(uint id)
        {
            All.TryGetValue(id, out var partner);
            if (partner == null) return;
            var ret = partner.Relive();
            if (ret && partner.Active && _player.IsTeamLeader)
            {
                var req = new UpdateTeamPartnerRequest();
                req.Member.AddRange(Actives.Select(p => p.BuildTeamObjectData()));
                _ = _player.TeamGrain.UpdatePartner(new Immutable<byte[]>(Packet.Serialize(req)));
            }

            await Task.CompletedTask;
        }

        private Task RefreshActives(bool send = true)
        {
            // 先选择出有效的值集合
            var array = _actives.Where(p => p > 0).ToArray();
            Array.Clear(_actives, 0, _actives.Length);

            // 重新把有效值排在前面
            var idx = 0;
            foreach (var pid in array)
            {
                // 确保存在这个id
                if (All.ContainsKey(pid))
                {
                    _actives[idx++] = pid;
                }
            }

            // 全部的伙伴都先把pos置零
            Actives.Clear();
            foreach (var v in All.Values)
            {
                v.Pos = 0;
            }

            // 重新绑定Partner的Pos
            for (var i = 0; i < _actives.Length; i++)
            {
                if (_actives[i] == 0) break;
                var partner = All[_actives[i]];
                partner.Pos = (uint) (i + 1);
                Actives.Add(partner);
            }

            if (send)
            {
                return _player.SendPacket(GameCmd.S2CPartnerActiveList, new S2C_PartnerActiveList
                {
                    List = {_actives}
                });
            }

            return Task.CompletedTask;
        }
    }
}