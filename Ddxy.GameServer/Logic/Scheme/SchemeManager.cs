using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Grains;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Scheme
{
    /// <summary>
    /// 套装方案管理器
    /// </summary>
    public class SchemeManager
    {
        private PlayerGrain _player;
        public List<Scheme> All { get; private set; }

        /// <summary>
        /// 当前使用的方案
        /// </summary>
        public Scheme Scheme { get; private set; }

        public SchemeManager(PlayerGrain player)
        {
            _player = player;
            All = new List<Scheme>(10);
        }

        public async Task Init()
        {
            var entities = await DbService.QuerySchemes(_player.RoleId);
            foreach (var entity in entities)
            {
                var data = new Scheme(_player, entity);
                All.Add(data);
                if (entity.Active)
                {
                    Scheme = data;
                }
            }

            // 修正Place, 记录方案穿戴的所有装备和配饰
            var wearEquips = new List<uint>();
            var wearOrnaments = new List<uint>();
            foreach (var scheme in All)
            {
                var changed = false;
                // foreach (var eid in scheme.Equips)
                for (var i = 0; i < scheme.Equips.Count; i++)
                {
                    var eid = scheme.Equips[i];
                    // 获得装备
                    var e = _player.EquipMgr.Equips.GetValueOrDefault(eid, null);
                    if (eid > 0 && e != null)
                    {
                        // 检测装备种族匹配
                        var ec = ConfigService.Equips.GetValueOrDefault(e.CfgId, null);
                        if (ec != null && (ec.Race == 9 || ec.Race == (byte)_player.Entity.Race))
                        {
                            wearEquips.Add(eid);
                        }
                        // 卸下装备
                        else
                        {
                            // 不是一件装备，是翅膀？
                            if (ec == null)
                            {
                                var wc = ConfigService.Wings.GetValueOrDefault(e.CfgId, null);
                                // 没找到翅膀配置卸下
                                if (wc == null)
                                {
                                    scheme.Equips[i] = 0;
                                    changed = true;
                                }
                            }
                            // 装备直接卸下
                            else
                            {
                                scheme.Equips[i] = 0;
                                changed = true;
                            }
                        }
                    }
                }
                // 刷新方案装备
                if (changed)
                {
                    scheme.SyncEquips();
                    await scheme.RefreshAttrs();
                }

                foreach (var oid in scheme.Ornaments)
                {
                    if (oid > 0)
                    {
                        wearOrnaments.Add(oid);
                    }
                }
            }

            _player.EquipMgr.CheckPlace(wearEquips, wearOrnaments);
        }

        public async Task Destroy()
        {
            foreach (var scheme in All)
            {
                await scheme.Destroy();
            }

            All.Clear();
            All = null;
            Scheme = null;
            _player = null;
        }

        public async Task SaveData()
        {
            foreach (var scheme in All)
            {
                await scheme.SaveData();
            }
        }

        public Task SendList()
        {
            var resp = new S2C_SchemeList();
            foreach (var scheme in All)
            {
                resp.List.Add(scheme.BuildPbData());
            }

            return _player.SendPacket(GameCmd.S2CSchemeList, resp);
        }

        public async Task ActiveScheme(uint id)
        {
            if (_player.CheckSafeLocked()) return;
            
            if (id == Scheme.Id) return;
            var newScheme = All.Find(p => p.Id == id);
            if (newScheme == null) return;

            // 10w银两
            var ret = await _player.CostMoney(MoneyType.Silver, 100000, tag: "切换属性方案");
            if (!ret) return;

            Scheme.Active = false;
            newScheme.Active = true;
            Scheme = newScheme;

            await _player.SendPacket(GameCmd.S2CSchemeActive, new S2C_SchemeActive {Id = id});
            await _player.RefreshWeapon();
            await _player.RefreshWing();
        }

        public async Task SetSchemeName(uint id, string name)
        {
            if (_player.CheckSafeLocked()) return;
            
            if (string.IsNullOrWhiteSpace(name)) return;
            name = name.Trim();
            // 检测是否有相同的名字
            if (All.Any(p => p.Name.Equals(name)))
            {
                _player.SendNotice("已有相同名字的方案");
                return;
            }

            var scheme = All.Find(p => p.Id == id);
            if (scheme == null)
            {
                _player.SendNotice("方案不存在");
                return;
            }

            await scheme.SetName(name);
        }
    }
}