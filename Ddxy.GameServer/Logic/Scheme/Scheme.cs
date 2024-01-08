using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.GameServer.Logic.Equip;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;
using Newtonsoft.Json;

namespace Ddxy.GameServer.Logic.Scheme
{
    public class Scheme
    {
        private PlayerGrain _player;

        private SchemeEntity _entity;
        private SchemeEntity _lastEntity; //上一次更新的Entity

        // 按照位置顺序穿戴的装备id集合
        public List<uint> Equips { get; private set; }

        // 按照位置顺序穿戴的配饰id集合
        public List<uint> Ornaments { get; private set; }

        // 激活的套装id
        public uint OrnamentSuitId { get; set; }

        // 激活的套装技能
        public List<uint> OrnamentSkills { get; private set; }

        /// <summary>
        /// 加点属性
        /// </summary>
        public Attrs ApAttrs { get; private set; }

        /// <summary>
        /// 修炼属性
        /// </summary>
        public Attrs XlAttrs { get; private set; }

        public Attrs Attrs { get; private set; }

        public uint Id => _entity.Id;

        /// <summary>
        /// 激活状态
        /// </summary>
        public bool Active
        {
            get => _entity.Active;
            set => _entity.Active = value;
        }

        public string Name => _entity.Name;

        /// <summary>
        /// 当前剩余的潜能
        /// </summary>
        public uint Potential { get; private set; }

        /// <summary>
        /// 武器装备id
        /// </summary>
        public uint WeaponId => Equips[0];

        /// <summary>
        /// 翅膀id
        /// </summary>
        public uint WingId => Equips[5];

        //回梦丹修改的转生修正
        private List<ReliveRecord> _relives;
        private Attrs _fixAttrs;

        public Scheme(PlayerGrain player, SchemeEntity entity)
        {
            _player = player;
            _entity = entity;
            _lastEntity = new SchemeEntity();
            _lastEntity.CopyFrom(_entity);

            // 解析属性
            ApAttrs = new Attrs(_entity.ApAttrs);
            XlAttrs = new Attrs(_entity.XlAttrs);

            InitRelives();

            Attrs = new Attrs(6);

            // 依次是武器、项链、衣服、头盔、鞋子
            Equips = new List<uint> {0, 0, 0, 0, 0, 0};
            // 依次是披风、挂件、腰带、戒指、戒指
            Ornaments = new List<uint> {0, 0, 0, 0, 0};
            // 当前激活的配饰套装技能id
            OrnamentSkills = new List<uint>(3);

            // 解析装备数据
            if (!string.IsNullOrWhiteSpace(_entity.Equips))
            {
                var sync = false;
                var list = JsonConvert.DeserializeObject<List<uint>>(_entity.Equips);
                for (var i = 0; i < list.Count; i++)
                {
                    if (i >= Equips.Count) break;
                    if (list[i] > 0)
                    {
                        // 检查装备是否存在
                        var equip = _player.EquipMgr.FindEquip(list[i]);
                        if (equip == null || equip.Place != EquipPlace.Wear)
                        {
                            list[i] = 0;
                            sync = true;
                        }

                        Equips[i] = list[i];
                    }
                }

                if (sync) SyncEquips();
            }

            // 解析配饰数据
            if (!string.IsNullOrWhiteSpace(_entity.Ornaments))
            {
                var sync = false;
                var list = JsonConvert.DeserializeObject<List<uint>>(_entity.Ornaments);
                for (var i = 0; i < list.Count; i++)
                {
                    if (i >= Ornaments.Count) break;
                    if (list[i] > 0)
                    {
                        // 检查配饰是否存在
                        var ornament = _player.EquipMgr.FindOrnament(list[i]);
                        if (ornament == null || ornament.Place != EquipPlace.Wear)
                        {
                            list[i] = 0;
                            sync = true;
                        }

                        Ornaments[i] = list[i];
                    }
                }

                if (sync) SyncOrnaments();
            }

            RefreshOrnamentSkills();

            CheckPotential();
            CheckXlLevel();
            CalculateAttrs();
        }

        public async Task Destroy()
        {
            await SaveData();
            _lastEntity = null;
            _entity = null;
            ApAttrs.Dispose();
            ApAttrs = null;
            XlAttrs.Dispose();
            XlAttrs = null;
            Attrs.Dispose();
            Attrs = null;

            Equips.Clear();
            Equips = null;
            Ornaments.Clear();
            Ornaments = null;
            OrnamentSkills.Clear();
            OrnamentSkills = null;

            _player = null;
        }

        // 保存数据
        public async Task SaveData()
        {
            if (!_entity.Equals(_lastEntity))
            {
                var ret = await DbService.UpdateEntity(_lastEntity, _entity);
                if (ret) _lastEntity.CopyFrom(_entity);
            }
        }

        public SchemeData BuildPbData()
        {
            // Attrs.Set(AttrType.HpMax, 197);
            // _player.LogInformation($" BuildPbData 玩家:{_player.RoleId}, 气血:{Attrs.Get(AttrType.Hp)}, 气血最大值:{Attrs.Get(AttrType.HpMax)} ");
            var pbData = new SchemeData
            {
                Id = _entity.Id,
                Name = _entity.Name,
                Active = _entity.Active,
                Equips = {Equips},
                Ornaments = {Ornaments},
                OrnamentSuitId = OrnamentSuitId,
                OrnamentSkills = {OrnamentSkills},
                ApAttrs = {ApAttrs.ToList()},
                Potential = Potential,
                XlAttrs = {XlAttrs.ToList()},
                XlLevel = _player.Entity.XlLevel,
                XlLevelMax = RoleRefine.GetMaxRefineLevel(_player.Entity.Relive),
                FixAttrs = {_fixAttrs.ToList()},
                Relives = {_relives},
                Attrs = {Attrs.ToList()}
            };
            return pbData;
        }

        public Task SendInfo()
        {
            return _player.SendPacket(GameCmd.S2CSchemeInfo, new S2C_SchemeInfo {Data = BuildPbData()});
        }

        /// <summary>
        /// 潜能变化，会引起潜能和属性的变化
        /// </summary>
        public Task RefreshAttrs()
        {
            CheckPotential();
            CalculateAttrs();
            return _player.SendPacket(GameCmd.S2CSchemeAttrs, new S2C_SchemeAttrs
            {
                Id = _entity.Id,
                Potential = Potential,
                XlLevelMax = RoleRefine.GetMaxRefineLevel(_player.Entity.Relive),
                Attrs = {Attrs.ToList()}
            });
        }

        public async Task AddPoint(IList<AttrPair> list)
        {
            if (_player.CheckSafeLocked()) return;

            var total = 0f;
            var dic = new Dictionary<AttrType, float>();
            foreach (var pair in list)
            {
                if (!GameDefine.ApAttrs.ContainsKey(pair.Key)) continue;

                var v = pair.Value;
                total += v;
                if (dic.ContainsKey(pair.Key))
                {
                    dic[pair.Key] += v;
                }
                else
                {
                    dic[pair.Key] = v;
                }
            }

            if (dic.Count == 0 || total <= 0 || total > Potential) return;

            dic.TryGetValue(AttrType.GenGu, out var value);
            if (value > 0) ApAttrs.Add(AttrType.GenGu, value);

            dic.TryGetValue(AttrType.LingXing, out value);
            if (value > 0) ApAttrs.Add(AttrType.LingXing, value);

            dic.TryGetValue(AttrType.LiLiang, out value);
            if (value > 0) ApAttrs.Add(AttrType.LiLiang, value);

            dic.TryGetValue(AttrType.MinJie, out value);
            if (value > 0) ApAttrs.Add(AttrType.MinJie, value);

            // 同步给Entity, 会自动入库
            _entity.ApAttrs = ApAttrs.ToJson();

            // 重新计算潜能点和属性
            CheckPotential();
            CalculateAttrs();

            // 下发加点结果
            var resp = new S2C_SchemeAddPoint
            {
                Potential = Potential,
                ApAttrs = {ApAttrs.ToList()},
                Attrs = {Attrs.ToList()}
            };

            await _player.SendPacket(GameCmd.S2CSchemeAddPoint, resp);
        }

        // 使用超级星梦石重置加点
        public async ValueTask<bool> ResetApAttrs()
        {
            if (_player.CheckSafeLocked()) return false;

            ApAttrs.Clear();
            _entity.ApAttrs = ApAttrs.ToJson();
            CheckPotential();
            CalculateAttrs();

            // 下发加点结果
            var resp = new S2C_SchemeAddPoint
            {
                Potential = Potential,
                ApAttrs = {ApAttrs.ToList()},
                Attrs = {Attrs.ToList()}
            };

            await _player.SendPacket(GameCmd.S2CSchemeAddPoint, resp);

            // 装备有可能穿戴不了
            for (var i = Equips.Count - 1; i >= 0; i--)
            {
                var eqId = Equips[i];
                if (eqId == 0) continue;
                var equip = _player.EquipMgr.FindEquip(eqId);
                if (equip == null) continue;
                if (!equip.CheckEnable())
                {
                    await SetEquip(0, equip.Cfg.Index);
                }
            }

            return true;
        }

        public async Task AddXlPoint(IList<AttrPair> list)
        {
            if (_player.CheckSafeLocked()) return;
            if (list == null || list.Count == 0) return;

            var total = 0f;
            var dic = new Dictionary<AttrType, float>();
            foreach (var pair in list)
            {
                if (pair.Value <= 0 || !RoleRefine.MaxAttrValues.ContainsKey(pair.Key)) continue;
                // 不能超过最大值
                var maxV = RoleRefine.GetMaxAttrValue(_player.Entity.Relive, pair.Key);
                if (pair.Value + XlAttrs.Get(pair.Key) > maxV) continue;

                total += pair.Value;
                dic[pair.Key] = pair.Value;
            }

            if (dic.Count == 0 || total <= 0) return;

            total += XlAttrs.Values.Sum();
            if (total > _player.Entity.XlLevel)
            {
                _player.SendNotice("修炼点不足");
                return;
            }

            // 抗混冰忘睡上限
            // var kBingMax = Attrs.Get(AttrType.KbingFengMax);
            // var kBing = dic.GetValueOrDefault(AttrType.DhunLuan, 0f) + XlAttrs.Get(AttrType.DhunLuan) +
            //             dic.GetValueOrDefault(AttrType.DfengYin, 0f) + XlAttrs.Get(AttrType.DfengYin) +
            //             dic.GetValueOrDefault(AttrType.DyiWang, 0f) + XlAttrs.Get(AttrType.DyiWang) +
            //             dic.GetValueOrDefault(AttrType.DhunShui, 0f) + XlAttrs.Get(AttrType.DhunShui);
            // if (kBing > kBingMax)
            // {
            //     _player.SendNotice("超过抗混冰忘睡上限");
            //     return;
            // }

            // 添加修炼点
            foreach (var (k, v) in dic)
            {
                XlAttrs.Add(k, v);
            }

            _entity.XlAttrs = XlAttrs.ToJson();

            CalculateAttrs();

            // 下发加点结果
            var resp = new S2C_SchemeXiuLian
            {
                XlAttrs = {XlAttrs.ToList()},
                Attrs = {Attrs.ToList()}
            };

            await _player.SendPacket(GameCmd.S2CSchemeXiuLian, resp);
        }

        public Task ResetXlPoint()
        {
            if (_player.CheckSafeLocked()) return Task.CompletedTask;
            XlAttrs.Clear();
            _entity.XlAttrs = XlAttrs.ToJson();

            CalculateAttrs();

            // 下发加点结果
            var resp = new S2C_SchemeXiuLian
            {
                XlAttrs = {XlAttrs.ToList()},
                Attrs = {Attrs.ToList()}
            };

            return _player.SendPacket(GameCmd.S2CSchemeXiuLian, resp);
        }

        public async Task ResetFix(IList<ReliveRecord> list)
        {
            if (_player.CheckSafeLocked()) return;
            if (list == null || list.Count == 0) return;

            // 1/2/3转必须一次性提交, 4转没有修正
            if (_player.Entity.Relive <= 3 && list.Count != _player.Entity.Relive) return;
            // 使用回梦丹
            var ret = await _player.AddBagItem(10201, -1, tag: "修改前世修正");
            if (!ret) return;

            _relives.Clear();
            foreach (var record in list)
            {
                _relives.Add(record);
            }

            SyncRelives();
            // 重新计算前世修正
            _fixAttrs = GetReliveFix(_relives);
            CalculateAttrs();

            await _player.SendPacket(GameCmd.S2CSchemeResetFix, new S2C_SchemeResetFix
            {
                Id = _entity.Id,
                Relives = {_relives},
                FixAttrs = {_fixAttrs.ToList()},
                Attrs = {Attrs.ToList()}
            });
        }

        /// <summary>
        /// 脱下所有装备和配饰，重置属性加点和修炼加点
        /// </summary>
        public async Task Reset(bool relive, bool clearPoints = true)
        {
            // 脱下所有装备
            for (var i = 0; i < Equips.Count; i++)
            {
                if (Equips[i] > 0)
                {
                    var equip = _player.EquipMgr.FindEquip(Equips[i]);
                    if (equip != null)
                    {
                        equip.Place = EquipPlace.Bag;
                    }
                }

                Equips[i] = 0;
            }

            // 脱下所有配饰
            for (var i = 0; i < Ornaments.Count; i++)
            {
                if (Ornaments[i] > 0)
                {
                    var ornament = _player.EquipMgr.FindOrnament(Ornaments[i]);
                    if (ornament != null)
                    {
                        ornament.Place = EquipPlace.Bag;
                    }
                }

                Ornaments[i] = 0;
            }

            SyncEquips();
            SyncOrnaments();

            // 清空加点
            if (clearPoints)
            {
                ApAttrs.Clear();
                _entity.ApAttrs = ApAttrs.ToJson();
                XlAttrs.Clear();
                _entity.XlAttrs = XlAttrs.ToJson();
            }

            // 同步转生记录
            if (relive)
            {
                _relives.Add(_player.Relives[^1]);
                _fixAttrs = GetReliveFix(_relives);
            }

            CheckPotential();
            CheckXlLevel();
            RefreshOrnamentSkills();
            CalculateAttrs();
            await SendInfo();

            if (Active)
            {
                await _player.RefreshWeapon();
                await _player.RefreshWing();
            }
        }

        // 修改名字
        public async Task SetName(string name)
        {
            if (_player.CheckSafeLocked()) return;
            _entity.Name = name;
            await _player.SendPacket(GameCmd.S2CSchemeName, new S2C_SchemeName
            {
                Id = _entity.Id,
                Name = name
            });
        }

        // 穿戴/卸下装备
        public async Task SetEquip(uint equipId, int pos)
        {
            if (_player.CheckSafeLocked()) return;
            // 检查pos是否合法
            if (pos is <= 0 or >= 7)
            {
                _player.SendNotice("位置非法");
                return;
            }

            if (Equips[pos - 1] == equipId)
            {
                _player.SendNotice("装备已穿戴");
                return;
            }

            if (equipId == 0)
            {
                // 脱下
                var equip = _player.EquipMgr.FindEquip(Equips[pos - 1]);
                Equips[pos - 1] = 0;
                // 检查是否在其他方案中穿戴
                if (equip != null)
                {
                    var isWear = _player.SchemeMgr.All.Any(p => p.Equips.Contains(equip.Id));
                    equip.Place = isWear ? EquipPlace.Wear : EquipPlace.Bag;
                }
            }
            else
            {
                // 穿戴, 检查该装备能否戴在这个位置上
                var equip = _player.EquipMgr.FindEquip(equipId);
                if (equip == null || equip.Cfg.Index != pos)
                {
                    _player.SendNotice("装备位置错误");
                    return;
                }

                // 检查是否在仓库中
                if (equip.Place == EquipPlace.Repo)
                {
                    _player.SendNotice("装备在仓库中，无法穿戴");
                    return;
                }

                // 检查是否已经在方案中
                if (Equips.Contains(equip.Id))
                {
                    _player.SendNotice("装备已经在本方案中, 无法穿戴");
                    return;
                }

                // 检查能否佩戴在角色身上
                if (equip.Category != EquipCategory.Unkown && !equip.CheckEnable()) return;

                // 原装备
                var oldEquip = _player.EquipMgr.FindEquip(Equips[pos - 1]);
                // 新装备穿戴
                Equips[pos - 1] = equipId;
                equip.Place = EquipPlace.Wear;

                // 检查是否在其他方案中穿戴
                if (oldEquip != null)
                {
                    var isWear = _player.SchemeMgr.All.Any(p => p.Equips.Contains(oldEquip.Id));
                    oldEquip.Place = isWear ? EquipPlace.Wear : EquipPlace.Bag;
                }
            }
            
            SyncEquips();
            CalculateAttrs();
            await _player.SendPacket(GameCmd.S2CSchemeEquip, new S2C_SchemeEquip
            {
                Pos = (uint) pos,
                Equip = equipId,
                Attrs = {Attrs.ToList()}
            });

            if (Active && pos == 1) await _player.RefreshWeapon();
            if (Active && pos == 6) await _player.RefreshWing();
            
            // 可能脱下,更换装备，导致其他一些装备有可能穿戴不了
            for (var i = Equips.Count - 1; i >= 0; i--)
            {
                var eqId = Equips[i];
                if (eqId == 0) continue;
                var equip = _player.EquipMgr.FindEquip(eqId);
                if (equip == null) continue;
                if (!equip.CheckEnable())
                {
                    await SetEquip(0, equip.Cfg.Index);
                    break;
                }
            }
        }

        /// <summary>
        /// 装备被删除, 需要刷新属性评分和属性
        /// </summary>
        public async Task OnEquipDelete(uint equipId)
        {
            var idx = Equips.FindIndex(p => p == equipId);
            if (idx < 0) return;
            Equips[idx] = 0;
            SyncEquips();
            CalculateAttrs();
            await _player.SendPacket(GameCmd.S2CSchemeEquip, new S2C_SchemeEquip
            {
                Pos = (uint) (idx + 1),
                Equip = 0,
                Attrs = {Attrs.ToList()}
            });

            if (Active && idx == 0) await _player.RefreshWeapon();
            if (Active && idx == 5) await _player.RefreshWing();
        }

        /// <summary>
        /// 装备属性有升级，需要刷新评分和属性
        /// </summary>
        public async Task OnEquipUpdate(uint equipId)
        {
            var idx = Equips.FindIndex(p => p == equipId);
            if (idx < 0) return;

            await RefreshAttrs();
            if (Active && idx == 0) await _player.RefreshWeapon();
            if (Active && idx == 5) await _player.RefreshWing();
        }

        public async Task SetOrnament(uint ornamentId, int pos)
        {
            if (_player.CheckSafeLocked()) return;
            // 检查pos是否合法
            if (pos <= 0 || pos >= 6) return;
            if (Ornaments[pos - 1] == ornamentId) return;

            if (ornamentId == 0)
            {
                // 脱下
                var ornament = _player.EquipMgr.FindOrnament(Ornaments[pos - 1]);
                Ornaments[pos - 1] = 0;
                // 检查是否在其他方案中穿戴
                if (ornament != null)
                {
                    var isWear = _player.SchemeMgr.All.Any(p => p.Ornaments.Contains(ornament.Id));
                    ornament.Place = isWear ? EquipPlace.Wear : EquipPlace.Bag;
                }
            }
            else
            {
                // 穿戴, 检查该装备能否戴在这个位置上
                var ornament = _player.EquipMgr.FindOrnament(ornamentId);
                if (ornament == null)
                {
                    _player.SendNotice("佩饰不存在");
                    return;
                }

                if (ornament.Cfg.Index != pos)
                {
                    if (ornament.Cfg.Index != 4 && pos != 4 && pos != 5)
                    {
                        _player.SendNotice("佩戴位置错误");
                        return;
                    }
                }

                // 检查是否在仓库中
                if (ornament.Place == EquipPlace.Repo)
                {
                    _player.SendNotice("配饰在仓库中，无法穿戴");
                    return;
                }

                // 检查是否已经在方案中
                if (Ornaments.Contains(ornament.Id))
                {
                    _player.SendNotice("配饰已经在本方案中, 无法穿戴");
                    return;
                }

                // 检查能否佩戴在角色身上
                if (!ornament.CheckEnable()) return;

                // 原佩饰
                var oldOrnament = _player.EquipMgr.FindOrnament(Ornaments[pos - 1]);
                // 新佩饰穿戴
                Ornaments[pos - 1] = ornamentId;
                ornament.Place = EquipPlace.Wear;
                // 检查是否在其他方案中穿戴
                if (oldOrnament != null)
                {
                    var isWear = _player.SchemeMgr.All.Any(p => p.Ornaments.Contains(oldOrnament.Id));
                    oldOrnament.Place = isWear ? EquipPlace.Wear : EquipPlace.Bag;
                }
            }

            SyncOrnaments();

            // 刷新套装技能
            RefreshOrnamentSkills();
            CalculateAttrs();
            await _player.SendPacket(GameCmd.S2CSchemeOrnament, new S2C_SchemeOrnament
            {
                Pos = (uint) pos,
                Ornament = ornamentId,
                Attrs = {Attrs.ToList()},
                SuitId = OrnamentSuitId,
                Skills = {OrnamentSkills}
            });
        }

        /// <summary>
        /// 配饰被删除, 需要刷新属性评分和属性
        /// </summary>
        public async Task OnOrnamentDelete(uint ornamentId)
        {
            var idx = Ornaments.FindIndex(p => p == ornamentId);
            if (idx < 0) return;
            Ornaments[idx] = 0;
            SyncOrnaments();
            RefreshOrnamentSkills();
            CalculateAttrs();
            await _player.SendPacket(GameCmd.S2CSchemeOrnament, new S2C_SchemeOrnament
            {
                Pos = (uint) (idx + 1),
                Ornament = 0,
                Attrs = {Attrs.ToList()},
                SuitId = OrnamentSuitId,
                Skills = {OrnamentSkills}
            });
        }

        /// <summary>
        /// 配饰属性有改变，需要刷新评分和属性
        /// </summary>
        public async Task OnOrnamentUpdate(uint ornamentId)
        {
            var idx = Ornaments.FindIndex(p => p == ornamentId);
            if (idx < 0) return;
            await RefreshAttrs();
        }

        /// <summary>
        /// 刷新套装技能
        /// </summary>
        private void RefreshOrnamentSkills()
        {
            OrnamentSuitId = 0;
            OrnamentSkills.Clear();

            // 首先检查是否5件戴满
            if (Ornaments.Any(p => p == 0)) return;

            // 获取配饰对象
            var list = new List<Ornament>(Ornaments.Count);
            foreach (var oid in Ornaments)
            {
                list.Add(_player.EquipMgr.FindOrnament(oid));
            }

            if (list.Any(p => p == null)) return;

            // 检查是否属于同一件套装, 直接用挂件或腰带比较节约性能
            var gj = list[1];
            if (gj.Cfg?.Suit == null || gj.Cfg.Suit.Length == 0) return;
            var suitId = gj.Cfg.Suit[0];
            if (suitId == 0) return;

            var isSameSuit = true;
            foreach (var orn in list)
            {
                // 这里不用再计算挂件
                if (orn.Id == gj.Id) continue;

                // 检查其他部件的套装id
                if (orn.Cfg?.Suit == null || orn.Cfg.Suit.Length == 0 || !orn.Cfg.Suit.Contains(suitId))
                {
                    isSameSuit = false;
                    break;
                }
            }

            if (!isSameSuit) return;

            // 同1个套装，至少可以实现1技能
            ConfigService.OrnamentSuits.TryGetValue(suitId, out var suitCfg);
            if (suitCfg?.Skills == null || suitCfg.Skills.Length != 3) return;

            // 5件佩饰最低品阶是把玩，激活把玩技能
            // 5件佩饰最低品阶是珍藏，激活把玩技能+珍藏技能
            // 5件佩饰全是无价，全部激活
            var minGrade = list.Min(p => p.Grade);
            if (minGrade == 3)
            {
                OrnamentSkills.Add(suitCfg.Skills[0]);
                // OrnamentSkills.Add(suitCfg.Skills[1]);
                OrnamentSkills.Add(suitCfg.Skills[2]);
            }
            else if (minGrade == 2)
            {
                OrnamentSkills.Add(suitCfg.Skills[0]);
                OrnamentSkills.Add(suitCfg.Skills[1]);
            }
            else
            {
                OrnamentSkills.Add(suitCfg.Skills[0]);
            }

            OrnamentSuitId = suitId;
        }

        // 计算剩余潜能
        private void CheckPotential()
        {
            // 计算剩余的潜能
            var total = _player.LevelPotential + _player.ExchangedPotential;
            var consume = (uint) MathF.Ceiling(ApAttrs.Values.Sum(p => p));
            if (consume > total)
            {
                // 清空所有加点
                ApAttrs.Clear();
                _entity.ApAttrs = ApAttrs.ToJson();
                Potential = total;
            }
            else
            {
                Potential = total - consume;
            }
        }

        private void CheckXlLevel()
        {
            var consume = (uint) MathF.Ceiling(XlAttrs.Values.Sum(p => p));
            if (consume > _player.Entity.XlLevel)
            {
                XlAttrs.Clear();
                _entity.XlAttrs = XlAttrs.ToJson();
            }
        }

        private void CalculateAttrs()
        {
            // 变身卡
            var bianshenAttrs = _player.GetBianshenAttrs();
            var other = new Attrs();
            if (bianshenAttrs != null)
            {
                foreach (var (k, v) in bianshenAttrs)
                {
                    other.Add(k, v);
                }
            }
            //神器切割属性
            var qiegeAttrs = _player.GetQieGeAttrs();
            if (qiegeAttrs != null)
            {
                foreach (var (k, v) in qiegeAttrs)
                {
                    other.Add(k, v);
                }
            } 
            //神之力属性
            var szlAttrs = _player.GetShenZhiLiAttrs();
            if (szlAttrs != null)
            {
                foreach (var (k, v) in szlAttrs)
                {
                    other.Add(k, v);
                }
            } 
            // 20211103 星阵改为各自加层，而不是队长给自己和他人加成
            var xingzhenAttrs = _player.GetXingzhenAttrs();
            if (xingzhenAttrs != null)
            {
                foreach (var (k, v) in xingzhenAttrs)
                {
                    other.Add(k, v);
                }
            }
            // 随身特效及足迹
            var skinAttrs = _player.GetSkinAttrs();
            if (skinAttrs != null)
            {
                foreach (var (k, v) in skinAttrs)
                {
                    other.Add(k, v);
                }
            }
            // 天策符
            var tianceAttrs = _player.GetTianceAttrPlayer();
            if (tianceAttrs != null)
            {
                foreach (var (k, v) in tianceAttrs)
                {
                    other.Add(k, v);
                }
            }
            Attrs.Clear();
            CalculateAddPointAttrs();
            CalculateEquipAttrs();
            CalculateOtherAttrs(other);
            CalculateLevelAttrs(other);
            CalculateReliveAttrs();
            CalculateXiuLianAttrs();
            // 天策符--特殊处理
            foreach (var f in _player.Tiance.list)
            {
                // 跳过没有装备的
                if (f.State == TianceFuState.Unknown) continue;
                var skCfg = ConfigService.TianceSkillList.GetValueOrDefault(f.SkillId, null);
                if (skCfg == null)
                {
                    _player.LogError($"没有找到天策符技能配置（角色特殊加成）RoleId:{_player.RoleId}, fid:{f.Id}, skillId:{f.SkillId}, name:{f.Name}");
                    continue;
                }
                var skillId = f.SkillId;
                // 天演策等级加成
                float additionTlv = _player.GetTianyanceLvAddition(f.Grade);
                // 枯荣符后处理--对角色加成
                if (skillId >= SkillId.KuRong1 && skillId <= SkillId.KuRong3)
                {
                    foreach (var a in skCfg.attr)
                    {
                        var ak = GameDefine.EquipAttrTypeMap.GetValueOrDefault(a.Key, AttrType.Unkown);
                        if (ak != AttrType.Unkown)
                        {
                            float add = (float)(a.Value.increase ? a.Value.baseAddition * f.Addition : a.Value.baseAddition)/1000.0f;
#if false
                            // 非数值性，千分比
                            if (!GameDefine.EquipNumericalAttrType.ContainsKey(ak))
                            {
                                add = add / 10;
                            }
#endif
                            var value = Attrs.Get(ak);
                            if (value > 0)
                            {
                                // 固定牺牲速度
                                if (add < 0)
                                {
                                    Attrs.Set(ak, value * (1 + add));
                                }
                                else
                                {
                                    Attrs.Set(ak, value * (1 + add * (1 + additionTlv)));
                                }
                            }
                        }
                    }
                    continue;
                }
            }
        }

        // 计算变身卡、星阵等带来的属性加成
        private void CalculateOtherAttrs(Attrs other)
        {
            foreach (var (k, v) in other)
            {
                if (v == 0) continue;
                // 这些属性在这里先不计算, 因为HpMax、MpMax、Atk、Spd在CalculateLevelAttrs的时候算,
                // 整体提升百分比的属性在CalculateEquipBaseAttr中调用
                if (GameDefine.EquipIgnoreAttrs.ContainsKey(k)) continue;
                // 获取属性的计算方式和目标属性
                if (GameDefine.AttrCalcMap.ContainsKey(k))
                {
                    var (attrType, calcType) = GameDefine.AttrCalcMap[k];
                    switch (calcType)
                    {
                        case AttrCalcType.AddPercent:
                            Attrs.AddPercent(attrType, v / 100f);
                            break;
                        case AttrCalcType.Percent:
                            Attrs.SetPercent(attrType, v / 100f);
                            break;
                        default:
                            Attrs.Add(attrType, v);
                            break;
                    }
                }
                else
                {
                    Attrs.Add(k, v);
                }
            }
        }
        // 变身卡、星阵等 基础百分比气血等 属性放在 等级计算等级之后
        private void CalculateOtherBaseAttrs(Attrs other)
        {
            foreach (var (k, v) in other)
            {
                if (v == 0) continue;

                if (k == AttrType.Ahp || k == AttrType.Amp || k == AttrType.Aatk || k == AttrType.Aspd)
                {
                    if (GameDefine.AttrCalcMap.ContainsKey(k))
                    {
                        var (attrType, calcType) = GameDefine.AttrCalcMap[k];
                        switch (calcType)
                        {
                            case AttrCalcType.AddPercent:
                                Attrs.AddPercent(attrType, v / 100f);
                                break;
                            case AttrCalcType.Percent:
                                Attrs.SetPercent(attrType, v / 100f);
                                break;
                            default:
                                Attrs.Add(attrType, v);
                                break;
                        }
                    }
                    else
                    {
                        Attrs.Add(k, v);
                    }
                }
            }
        }
        /// <summary>
        /// 变身卡、星阵等带来的HpMax、MpMax、Atk、Spd在计算CalculateLevelAttrs时计算
        /// </summary>
        private static float GetOtherAttr(Attrs other, AttrType attrType)
        {
            return other.Get(attrType);
        }

        // 计算潜能加点给角色带来的属性加成
        private void CalculateAddPointAttrs()
        {
            var level = _player.Entity.Level;

            Attrs.Set(AttrType.GenGu, level + ApAttrs.Get(AttrType.GenGu));
            Attrs.Set(AttrType.LingXing, level + ApAttrs.Get(AttrType.LingXing));
            Attrs.Set(AttrType.LiLiang, level + ApAttrs.Get(AttrType.LiLiang));
            Attrs.Set(AttrType.MinJie, level + ApAttrs.Get(AttrType.MinJie));
        }

        // 计算装备带来的属性加成
        private void CalculateEquipAttrs()
        {
            foreach (var eid in Equips)
            {
                if (eid == 0) continue;
                var equip = _player.EquipMgr.FindEquip(eid);
                if (equip == null) continue;

                foreach (var (k, v) in equip.Attrs)
                {
                    if (v == 0) continue;
                    // 这些属性在这里先不计算, 因为HpMax、MpMax、Atk、Spd在CalculateLevelAttrs的时候算,
                    // 整体提升百分比的属性在CalculateEquipBaseAttr中调用
                    if (GameDefine.EquipIgnoreAttrs.ContainsKey(k)) continue;
                    // 获取属性的计算方式和目标属性
                    if (GameDefine.AttrCalcMap.ContainsKey(k))
                    {
                        var (attrType, calcType) = GameDefine.AttrCalcMap[k];
                        switch (calcType)
                        {
                            case AttrCalcType.AddPercent:
                                Attrs.AddPercent(attrType, v / 100f);
                                break;
                            case AttrCalcType.Percent:
                                Attrs.SetPercent(attrType, v / 100f);
                                break;
                            default:
                                Attrs.Add(attrType, v);
                                break;
                        }
                    }
                    else
                    {
                        Attrs.Add(k, v);
                    }
                }
            }

            foreach (var oid in Ornaments)
            {
                if (oid == 0) continue;
                var ornament = _player.EquipMgr.FindOrnament(oid);
                if (ornament == null) continue;

                foreach (var (k, v) in ornament.Attrs)
                {
                    if (v == 0) continue;
                    // 这些属性在这里先不计算
                    if (GameDefine.EquipIgnoreAttrs.ContainsKey(k)) continue;
                    // 获取属性的计算方式和目标属性
                    if (GameDefine.AttrCalcMap.ContainsKey(k))
                    {
                        var (attrType, calcType) = GameDefine.AttrCalcMap[k];
                        switch (calcType)
                        {
                            case AttrCalcType.AddPercent:
                                Attrs.AddPercent(attrType, v / 100f);
                                break;
                            case AttrCalcType.Percent:
                                Attrs.SetPercent(attrType, v / 100f);
                                break;
                            default:
                                Attrs.Add(attrType, v);
                                break;
                        }
                    }
                    else
                    {
                        Attrs.Add(k, v);
                    }
                }
            }

            // 套装技能带来的属性加成
            foreach (var skid in OrnamentSkills)
            {
                if (skid == 0) continue;
                ConfigService.OrnamentSkill.TryGetValue(skid, out var skCfg);
                if (skCfg?.Attrs2 == null || skCfg.Attrs2.Count == 0) continue;

                foreach (var (k, v) in skCfg.Attrs2)
                {
                    if (v == 0) continue;
                    // 这些属性在这里先不计算
                    if (GameDefine.EquipIgnoreAttrs.ContainsKey(k)) continue;
                    // 获取属性的计算方式和目标属性
                    if (GameDefine.AttrCalcMap.ContainsKey(k))
                    {
                        var (attrType, calcType) = GameDefine.AttrCalcMap[k];
                        switch (calcType)
                        {
                            case AttrCalcType.AddPercent:
                                Attrs.AddPercent(attrType, v / 100f);
                                break;
                            case AttrCalcType.Percent:
                                Attrs.SetPercent(attrType, v / 100f);
                                break;
                            default:
                                Attrs.Add(attrType, v);
                                break;
                        }
                    }
                    else
                    {
                        Attrs.Add(k, v);
                    }
                }
            }
        }

        // 基础百分比气血等 属性放在 等级计算等级之后
        private void CalculateEquipBaseAttr()
        {
            foreach (var eid in Equips)
            {
                if (eid == 0) continue;
                var equip = _player.EquipMgr.FindEquip(eid);
                if (equip == null) continue;

                foreach (var (k, v) in equip.Attrs)
                {
                    if (v == 0) continue;

                    if (k == AttrType.Ahp || k == AttrType.Amp || k == AttrType.Aatk || k == AttrType.Aspd)
                    {
                        if (GameDefine.AttrCalcMap.ContainsKey(k))
                        {
                            var (attrType, calcType) = GameDefine.AttrCalcMap[k];
                            switch (calcType)
                            {
                                case AttrCalcType.AddPercent:
                                    Attrs.AddPercent(attrType, v / 100f);
                                    break;
                                case AttrCalcType.Percent:
                                    Attrs.SetPercent(attrType, v / 100f);
                                    break;
                                default:
                                    Attrs.Add(attrType, v);
                                    break;
                            }
                        }
                        else
                        {
                            Attrs.Add(k, v);
                        }
                    }
                }
            }

            foreach (var oid in Ornaments)
            {
                if (oid == 0) continue;
                var ornament = _player.EquipMgr.FindOrnament(oid);
                if (ornament == null) continue;

                foreach (var (k, v) in ornament.Attrs)
                {
                    if (v == 0) continue;

                    if (k == AttrType.Ahp || k == AttrType.Amp || k == AttrType.Aatk || k == AttrType.Aspd)
                    {
                        if (GameDefine.AttrCalcMap.ContainsKey(k))
                        {
                            var (attrType, calcType) = GameDefine.AttrCalcMap[k];
                            switch (calcType)
                            {
                                case AttrCalcType.AddPercent:
                                    Attrs.AddPercent(attrType, v / 100f);
                                    break;
                                case AttrCalcType.Percent:
                                    Attrs.SetPercent(attrType, v / 100f);
                                    break;
                                default:
                                    Attrs.Add(attrType, v);
                                    break;
                            }
                        }
                        else
                        {
                            Attrs.Add(k, v);
                        }
                    }
                }
            }

            // 套装技能带来的属性加成
            foreach (var skid in OrnamentSkills)
            {
                if (skid == 0) continue;
                ConfigService.OrnamentSkill.TryGetValue(skid, out var skCfg);
                if (skCfg?.Attrs2 == null || skCfg.Attrs2.Count == 0) continue;

                foreach (var (k, v) in skCfg.Attrs2)
                {
                    if (v == 0) continue;

                    if (k == AttrType.Ahp || k == AttrType.Amp || k == AttrType.Aatk || k == AttrType.Aspd)
                    {
                        if (GameDefine.AttrCalcMap.ContainsKey(k))
                        {
                            var (attrType, calcType) = GameDefine.AttrCalcMap[k];
                            switch (calcType)
                            {
                                case AttrCalcType.AddPercent:
                                    Attrs.AddPercent(attrType, v / 100f);
                                    break;
                                case AttrCalcType.Percent:
                                    Attrs.SetPercent(attrType, v / 100f);
                                    break;
                                default:
                                    Attrs.Add(attrType, v);
                                    break;
                            }
                        }
                        else
                        {
                            Attrs.Add(k, v);
                        }
                    }
                }
            }
        }

        private void CalculateLevelAttrs(Attrs other)
        {
            var level = _player.Entity.Level;
            var levelP = MathF.Floor((100 - level) / 5f);

            // 基础属性点
            GameDefine.Bases.TryGetValue((Race) _player.Entity.Race, out var bases);
            if (bases == null) return;

            // 获取4个属性的成长点
            GameDefine.Grows.TryGetValue((Race) _player.Entity.Race, out var grows);
            if (grows == null) return;

            // 转生修正
            var fixAttrs = _fixAttrs;

            // hp -> 根骨
            var attrValue = Attrs.Get(AttrType.GenGu);
            grows.TryGetValue(AttrType.GenGu, out var growValue);
            bases.TryGetValue(AttrType.Hp, out var baseValue);
            var fixValue = fixAttrs.Get(AttrType.Hp, 1);
            var hp = (Int64) MathF.Floor((MathF.Floor((level + levelP) * attrValue * growValue + baseValue) +
                                        GetEquipAttr(AttrType.HpMax) + GetOtherAttr(other, AttrType.HpMax)) * (1 + fixValue / 100));

            // mp -> 灵性
            attrValue = Attrs.Get(AttrType.LingXing);
            grows.TryGetValue(AttrType.LingXing, out growValue);
            bases.TryGetValue(AttrType.Mp, out baseValue);
            fixValue = fixAttrs.Get(AttrType.Mp, 1);
            var mp = (int) MathF.Floor((MathF.Floor((level + levelP) * attrValue * growValue + baseValue) +
                                        GetEquipAttr(AttrType.MpMax) + GetOtherAttr(other, AttrType.MpMax)) * (1 + fixValue / 100));

            // atk -> 力量
            attrValue = Attrs.Get(AttrType.LiLiang);
            grows.TryGetValue(AttrType.LiLiang, out growValue);
            bases.TryGetValue(AttrType.Atk, out baseValue);
            fixValue = fixAttrs.Get(AttrType.Atk, 1);
            var atk = (int) MathF.Floor((level + levelP) * attrValue * growValue / 5 + baseValue +
                                        (GetEquipAttr(AttrType.Atk) + GetOtherAttr(other, AttrType.Atk)) * (1 + fixValue / 100));

            // spd -> 敏捷
            attrValue = Attrs.Get(AttrType.MinJie);
            grows.TryGetValue(AttrType.MinJie, out growValue);
            bases.TryGetValue(AttrType.Spd, out baseValue);
            fixValue = fixAttrs.Get(AttrType.Spd, 1);
            var spd = (int) MathF.Floor(
                (MathF.Floor((10 + attrValue) * growValue) + GetEquipAttr(AttrType.Spd) + GetOtherAttr(other, AttrType.Spd)) *
                (1 + fixValue / 100));

            Attrs.Set(AttrType.Hp, hp);
            Attrs.Set(AttrType.HpMax, hp);
            Attrs.Set(AttrType.Mp, mp);
            Attrs.Set(AttrType.MpMax, mp);
            Attrs.Set(AttrType.Atk, atk);
            Attrs.Set(AttrType.Spd, spd);

            CalculateEquipBaseAttr();
            CalculateOtherBaseAttrs(other);

            Attrs.Set(AttrType.HpMax, Attrs.Get(AttrType.Hp));
            Attrs.Set(AttrType.MpMax, Attrs.Get(AttrType.Mp));

            switch ((Race) _player.Entity.Race)
            {
                case Race.Ren:
                {
                    // 抗混乱 封印 昏睡 毒 每4级 + 1
                    var val = (int) MathF.Floor(level / 4f);
                    Attrs.Add(AttrType.DhunLuan, val);
                    Attrs.Add(AttrType.DfengYin, val);
                    Attrs.Add(AttrType.DhunShui, val);
                    Attrs.Add(AttrType.Ddu, val);
                }
                    break;
                case Race.Xian:
                {
                    // 抗水 火 雷 风 每8级 + 1
                    var val = (int) MathF.Floor(level / 8f);
                    Attrs.Add(AttrType.Dshui, val);
                    Attrs.Add(AttrType.Dhuo, val);
                    Attrs.Add(AttrType.Dlei, val);
                    Attrs.Add(AttrType.Dfeng, val);
                }
                    break;
                case Race.Mo:
                {
                    // 抗物理 混乱 封印 昏睡 毒 每8级 + 1
                    var val = (int) MathF.Floor(level / 8f);
                    Attrs.Add(AttrType.DwuLi, val);
                    Attrs.Add(AttrType.DhunLuan, val);
                    Attrs.Add(AttrType.DfengYin, val);
                    Attrs.Add(AttrType.DhunShui, val);
                    Attrs.Add(AttrType.Ddu, val);
                    // 抗 水 火 雷 风 每12级 + 1
                    val = (int) MathF.Floor(level / 12f);
                    Attrs.Add(AttrType.Dshui, val);
                    Attrs.Add(AttrType.Dhuo, val);
                    Attrs.Add(AttrType.Dlei, val);
                    Attrs.Add(AttrType.Dfeng, val);
                }
                    break;
                case Race.Gui:
                {
                    // 抗 混乱 封印 昏睡 毒 遗忘 鬼火 三尸 每6级 + 1
                    var val = (int) MathF.Floor(level / 6f);
                    Attrs.Add(AttrType.DhunLuan, val);
                    Attrs.Add(AttrType.DfengYin, val);
                    Attrs.Add(AttrType.DhunShui, val);
                    Attrs.Add(AttrType.Ddu, val);
                    Attrs.Add(AttrType.DyiWang, val);
                    Attrs.Add(AttrType.DguiHuo, val);
                    Attrs.Add(AttrType.DsanShi, val);

                    // 抗 水 火 雷 风 每8级 + 1
                    val = (int) MathF.Floor(level / 8f);
                    Attrs.Add(AttrType.Dshui, val);
                    Attrs.Add(AttrType.Dhuo, val);
                    Attrs.Add(AttrType.Dlei, val);
                    Attrs.Add(AttrType.Dfeng, val);
                    // 命中 闪避 每4级 + 1
                    val = (int) MathF.Floor(level / 4f);
                    Attrs.Add(AttrType.PmingZhong, val);
                    Attrs.Add(AttrType.PshanBi, val);
                }
                    break;
                // 龙族
                case Race.Long:
                {
                    // 每升6级  抗物理、混乱、封印、昏睡、毒、遗忘+1%
                    var val = (int) MathF.Floor(level / 6f);
                    Attrs.Add(AttrType.DwuLi, val);
                    Attrs.Add(AttrType.DhunLuan, val);
                    Attrs.Add(AttrType.DfengYin, val);
                    Attrs.Add(AttrType.DhunShui, val);
                    Attrs.Add(AttrType.Ddu, val);
                    Attrs.Add(AttrType.DyiWang, val);

                    // 每升20级 狂暴率、命中率+1%
                    val = (int) MathF.Floor(level / 20f);
                    Attrs.Add(AttrType.PkuangBao, val);
                    Attrs.Add(AttrType.PmingZhong, val);
                }
                    break;
            }
        }

        private void CalculateReliveAttrs()
        {
            if (_player.Entity.Relive == 0) return;
            foreach (var (k, v) in _fixAttrs)
            {
                if (!GameDefine.ReliveIgnoreAttrs.ContainsKey(k))
                {
                    Attrs.Add(k, v);
                }
            }
        }

        private void CalculateXiuLianAttrs()
        {
            // FIXME: 种族基础抗性上限，暂时上调20
            var bingFengMaxBase = 120;
            var race = (Race) _player.Entity.Race;
            switch (race)
            {
                case Race.Ren:
                    bingFengMaxBase = 160;
                    break;
                case Race.Xian:
                case Race.Mo:
                    bingFengMaxBase = 130;
                    break;
                case Race.Gui:
                    bingFengMaxBase = 140;
                    break;
                // 龙族
                case Race.Long:
                    bingFengMaxBase = 150;
                    break;
            }

            // 雅声 物换星移
            if (OrnamentSkills.Contains(9032))
            {
                bingFengMaxBase += 10;
            }
            else if (OrnamentSkills.Contains(9031))
            {
                bingFengMaxBase += 5;
            }

            // 以直报怨 把玩
            if (OrnamentSkills.Contains(9040))
            {
                ConfigService.OrnamentSkill.TryGetValue(9040, out var skCfg);
                if (skCfg?.Attrs2 != null && skCfg.Attrs2.TryGetValue(AttrType.KbingFengMax, out var bfDelta) &&
                    bfDelta > 0)
                {
                    bingFengMaxBase = (int) MathF.Ceiling(bingFengMaxBase * (1 + bfDelta / 100.0f));
                }
            }


            var bingFengMax = bingFengMaxBase + XlAttrs.Get(AttrType.KbingFengMax) * 1.5f;
            Attrs.Set(AttrType.KbingFengMax, bingFengMax);

            Attrs.Add(AttrType.DfengYin, XlAttrs.Get(AttrType.DfengYin) * 5);
            Attrs.Add(AttrType.DhunLuan, XlAttrs.Get(AttrType.DhunLuan) * 5);
            Attrs.Add(AttrType.DhunShui, XlAttrs.Get(AttrType.DhunShui) * 5);
            Attrs.Add(AttrType.DyiWang, XlAttrs.Get(AttrType.DyiWang) * 5);
            // 天策符 载物符 明镜符
            // 抗封印上限提高
            var fskill = _player.GetTianceFuBySkillId(SkillId.MingJing1, SkillId.MingJing3);
            var addon = fskill == null ? 0 : (0.07f + fskill.Grade * 0.02f * _player.Tiance.level / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f) / 2;
            if (Attrs.Get(AttrType.DfengYin) > bingFengMax * (1 + addon))
                Attrs.Set(AttrType.DfengYin, bingFengMax * (1 + addon));
            // 天策符 载物符 清明符
            // 抗混乱上限提高
            fskill = _player.GetTianceFuBySkillId(SkillId.QingMing1, SkillId.QingMing3);
            addon = fskill == null ? 0 : (0.07f + fskill.Grade * 0.02f * _player.Tiance.level / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f) / 2;
            if (Attrs.Get(AttrType.DhunLuan) > bingFengMax * (1 + addon))
                Attrs.Set(AttrType.DhunLuan, bingFengMax * (1 + addon));
            // 天策符 载物符 沉着符
            // 抗昏睡上限提高
            fskill = _player.GetTianceFuBySkillId(SkillId.ChenZhuo1, SkillId.ChenZhuo3);
            addon = fskill == null ? 0 : (0.07f + fskill.Grade * 0.02f * _player.Tiance.level / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f) / 2;
            if (Attrs.Get(AttrType.DhunShui) > bingFengMax * (1 + addon))
                Attrs.Set(AttrType.DhunShui, bingFengMax * (1 + addon));
            // 天策符 载物符 强心符
            // 抗遗忘上限提高
            fskill = _player.GetTianceFuBySkillId(SkillId.QiangXin1, SkillId.QiangXin3);
            addon = fskill == null ? 0 : (0.07f + fskill.Grade * 0.02f * _player.Tiance.level / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f) / 2;
            if (Attrs.Get(AttrType.DyiWang) > bingFengMax * (1 + addon))
                Attrs.Set(AttrType.DyiWang, bingFengMax * (1 + addon));

            Attrs.Add(AttrType.Dfeng, XlAttrs.Get(AttrType.Dfeng) * 5);
            Attrs.Add(AttrType.Dhuo, XlAttrs.Get(AttrType.Dhuo) * 5);
            Attrs.Add(AttrType.Dshui, XlAttrs.Get(AttrType.Dshui) * 5);
            Attrs.Add(AttrType.Dlei, XlAttrs.Get(AttrType.Dlei) * 5);
            Attrs.Add(AttrType.Ddu, XlAttrs.Get(AttrType.Ddu) * 5);
            Attrs.Add(AttrType.DguiHuo, XlAttrs.Get(AttrType.DguiHuo) * 5);
            Attrs.Add(AttrType.DsanShi, XlAttrs.Get(AttrType.DsanShi) * 5);

            Attrs.Add(AttrType.PxiShou, XlAttrs.Get(AttrType.PxiShou) * 4);
            // Attrs.Add(AttrType.PmingZhong, XlAttrs.Get(AttrType.PmingZhong) * 4);
            // Attrs.Add(AttrType.PshanBi, XlAttrs.Get(AttrType.PshanBi) * 4);
            // Attrs.Add(AttrType.PlianJi, XlAttrs.Get(AttrType.PlianJi) * 4);
            // Attrs.Add(AttrType.PlianJiLv, XlAttrs.Get(AttrType.PlianJiLv) * 4);
            // Attrs.Add(AttrType.PkuangBao, XlAttrs.Get(AttrType.PkuangBao) * 4);
            // Attrs.Add(AttrType.PpoFang, XlAttrs.Get(AttrType.PpoFang) * 4);
            // Attrs.Add(AttrType.PpoFangLv, XlAttrs.Get(AttrType.PpoFangLv) * 4);
            Attrs.Add(AttrType.PfanZhenLv, XlAttrs.Get(AttrType.PfanZhenLv) * 4);
            Attrs.Add(AttrType.PfanZhen, XlAttrs.Get(AttrType.PfanZhen) * 4);

            // 套装技能
            if (OrnamentSkills is {Count: > 0})
            {
                foreach (var sid in OrnamentSkills)
                {
                    switch (sid)
                    {
                        case 9031:
                        {
                            // 雅声 物换星移-珍藏
                            Attrs.Add(AttrType.DhunLuan, -5, true);
                            Attrs.Add(AttrType.DhunShui, -5, true);
                            Attrs.Add(AttrType.DyiWang, -5, true);
                            // 增加上限需要在最上面设置，所以这里不再重复增加
                            // Attrs.Add(AttrType.KbingFengMax, 5);
                            break;
                        }
                        case 9032:
                        {
                            // 雅声 物换星移-无价
                            Attrs.Add(AttrType.DhunLuan, -5, true);
                            Attrs.Add(AttrType.DhunShui, -5, true);
                            Attrs.Add(AttrType.DyiWang, -5, true);
                            // Attrs.Add(AttrType.KbingFengMax, 10);
                        }
                            break;
                        case 1011:
                        {
                            // 罗睺 违心一致-无价
                            var delta = MathF.Floor(Attrs.Get(AttrType.GenGu) / 300);
                            if (delta > 0) Attrs.Add(AttrType.HdhunLuan, delta);
                            break;
                        }
                        case 1012:
                        {
                            // 罗睺 违心一致-珍藏
                            var delta = MathF.Floor(Attrs.Get(AttrType.GenGu) / 200);
                            if (delta > 0) Attrs.Add(AttrType.HdhunLuan, delta);
                        }
                            break;
                        case 1061:
                        {
                            // 玄冥 镂玉裁冰-珍藏
                            var delta = MathF.Floor(Attrs.Get(AttrType.GenGu) / 300);
                            if (delta > 0) Attrs.Add(AttrType.HdfengYin, delta);
                        }
                            break;
                        case 1081:
                        {
                            // 菩提 彻骨寒冰-珍藏
                            var delta = MathF.Floor(Attrs.Get(AttrType.GenGu) / 300);
                            if (delta > 0) Attrs.Add(AttrType.HdfengYin, delta);
                            break;
                        }
                        case 1082:
                        {
                            // 菩提 彻骨寒冰-无价
                            var delta = MathF.Floor(Attrs.Get(AttrType.GenGu) / 200);
                            if (delta > 0) Attrs.Add(AttrType.HdfengYin, delta);

                            delta = MathF.Floor(Attrs.Get(AttrType.MinJie) / 400);
                            if (delta > 0) Attrs.Add(AttrType.HdfengYin, delta);
                        }
                            break;
                        case 1031:
                        {
                            // 卧龙 醉生梦死-珍藏
                            var delta = MathF.Floor(Attrs.Get(AttrType.GenGu) / 300);
                            if (delta > 0) Attrs.Add(AttrType.HdhunShui, delta);
                        }
                            break;
                        case 4031:
                        {
                            // 化蝶 遗形忘性-珍藏
                            var delta = MathF.Floor(Attrs.Get(AttrType.GenGu) / 300);
                            if (delta > 0) Attrs.Add(AttrType.HdyiWang, delta);
                        }
                            break;
                        case 4021:
                        {
                            // 九幽 雨魄云魂-珍藏
                            var delta = MathF.Floor(Attrs.Get(AttrType.GenGu) / 300);
                            if (delta > 0) Attrs.Add(AttrType.HdyiWang, delta);
                        }
                            break;
                    }
                }
            }
        }

        /// <summary>
        /// 装备和配饰带来的HpMax、MpMax、Atk、Spd在计算CalculateLevelAttrs时计算
        /// </summary>
        private float GetEquipAttr(AttrType attrType)
        {
            var sum = 0f;
            foreach (var id in Equips)
            {
                var equip = _player.EquipMgr.FindEquip(id);
                if (equip == null) continue;
                sum += equip.Attrs.Get(attrType);
            }

            foreach (var id in Ornaments)
            {
                var ornament = _player.EquipMgr.FindOrnament(id);
                if (ornament == null) continue;
                sum += ornament.Attrs.Get(attrType);
            }

            return sum;
        }

        // 获取转生修正的属性值
        public Attrs GetReliveFix(IList<ReliveRecord> list)
        {
            var fixList = new List<Dictionary<Race, Dictionary<Sex, Dictionary<AttrType, float>>>>
                {GameDefine.Relive1FixAttr, GameDefine.Relive2FixAttr, GameDefine.Relive3FixAttr};

            var attrs = new Attrs();
            for (var i = 0; i < list.Count; i++)
            {
                if (i >= fixList.Count) break;
                fixList[i].TryGetValue(list[i].Race, out var raceDic);
                if (raceDic == null) continue;
                raceDic.TryGetValue(list[i].Sex, out var attrDic);
                if (attrDic == null) continue;

                foreach (var (k, v) in attrDic)
                {
                    attrs.Add(k, v);
                }
            }

            return attrs;
        }

        public void SyncEquips()
        {
            if (Equips.Any(p => p > 0))
            {
                _entity.Equips = Json.Serialize(Equips);
            }
            else
            {
                _entity.Equips = string.Empty;
            }
        }

        private void SyncOrnaments()
        {
            if (Ornaments.Any(p => p > 0))
            {
                _entity.Ornaments = Json.Serialize(Ornaments);
            }
            else
            {
                _entity.Ornaments = string.Empty;
            }
        }

        // 解析relives
        private void InitRelives()
        {
            _relives = new List<ReliveRecord>(3);
            if (_player.Entity.Relive > 0)
            {
                // 先读取角色转生记录
                foreach (var record in _player.Relives)
                {
                    // 由于接下来会修改record,所以这里进行Clone
                    _relives.Add(record.Clone());
                }
            }

            if (!string.IsNullOrWhiteSpace(_entity.Relives))
            {
                // 转生修正 覆盖
                var lines = _entity.Relives.Split(",");
                for (var i = 0; i < lines.Length; i++)
                {
                    if (i >= _player.Entity.Relive) break;
                    var chars = lines[i];
                    if (string.IsNullOrWhiteSpace(chars)) continue;
                    byte.TryParse(chars[0].ToString(), out var race);
                    byte.TryParse(chars[1].ToString(), out var sex);
                    _relives[i].Race = (Race) race;
                    _relives[i].Sex = (Sex) sex;
                }
            }

            // 计算转生属性
            if (_relives.Count > 0) _fixAttrs = GetReliveFix(_relives);
            else _fixAttrs = new Attrs();
        }

        // 同步relives到Entity
        private void SyncRelives()
        {
            if (_relives == null || _relives.Count == 0)
            {
                _entity.Relives = string.Empty;
                return;
            }

            var sb = new StringBuilder();
            for (var i = 0; i < _relives.Count; i++)
            {
                var record = _relives[i];
                sb.Append((byte) record.Race);
                sb.Append((byte) record.Sex);
                if (i < _relives.Count - 1)
                    sb.Append(",");
            }

            _entity.Relives = sb.ToString();
        }
    }
}