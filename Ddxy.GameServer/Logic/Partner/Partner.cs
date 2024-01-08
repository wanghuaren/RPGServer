using System;
using System.Threading.Tasks;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Config;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Partner
{
    public class Partner
    {
        private PlayerGrain _player;
        private PartnerEntity _entity;
        private PartnerEntity _lastEntity; //上一次更新的Entity

        public uint Id => _entity.Id;

        public uint CfgId => _entity.CfgId;

        public uint Pos
        {
            get => _entity.Pos;
            set => _entity.Pos = value;
        }

        public bool Active => _entity.Pos > 0;

        public Attrs Attrs { get; private set; }

        public PartnerConfig Cfg { get; private set; }

        private ulong _expMax;

        public Partner(PlayerGrain player, PartnerEntity entity)
        {
            _player = player;
            _entity = entity;
            _lastEntity = new PartnerEntity();
            _lastEntity.CopyFrom(_entity);

            Cfg = ConfigService.Partners[_entity.CfgId];

            // 矫正等级和经验
            _entity.Level = (byte) Math.Clamp(_entity.Level, GetLevelRange(_entity.Relive, true),
                GetLevelRange(_entity.Relive, false));
            _expMax = ConfigService.GetPartnerUpgradeExp(_entity.Relive, _entity.Level);
            if (_entity.Exp >= _expMax) _entity.Exp = _expMax;

            Attrs = new Attrs();
            BuildAttrs();
        }

        public async Task Destroy()
        {
            await SaveData(false);
            _lastEntity = null;
            _entity = null;
            _player = null;

            Attrs.Dispose();
            Attrs = null;

            Cfg = null;
        }

        public async Task SaveData(bool copy = true)
        {
            if (_entity.Equals(_lastEntity)) return;
            var ret = await DbService.UpdateEntity(_lastEntity, _entity);
            if (ret) _lastEntity.CopyFrom(_entity);
        }

        public PartnerData BuildPbData()
        {
            var pbData = new PartnerData
            {
                Id = _entity.Id,
                CfgId = _entity.CfgId,
                Relive = _entity.Relive,
                Level = _entity.Level,
                Exp = _entity.Exp,
                ExpMax = _expMax,
                Skills = {Cfg.Skills},
                Pos = _entity.Pos,
                Attrs = {Attrs.ToList()}
            };

            if (pbData.Skills is {Count: > 0})
            {
                var profic = ConfigService.GetRoleSkillMaxExp(_entity.Relive);
                for (var i = 0; i < pbData.Skills.Count; i++)
                {
                    pbData.Profics.Add(profic);
                }
            }

            return pbData;
        }

        public BattleMemberData BuildBattleMemberData(int pos)
        {
            var data = new BattleMemberData
            {
                Type = LivingThingType.Partner,
                Pos = pos,
                OwnerId = _player.RoleId,

                Id = _entity.Id,
                CfgId = _entity.CfgId,
                Name = Cfg.Name,
                Res = Cfg.Res,
                Relive = _entity.Relive,
                Level = _entity.Level,

                Race = (Race) Cfg.Race,
                Sex = (Sex) Cfg.Sex
            };

            foreach (var (k, v) in Attrs)
            {
                if (v != 0) data.Attrs.Add(new AttrPair {Key = k, Value = v});
            }

            if (Cfg.Skills != null)
            {
                foreach (var skId in Cfg.Skills)
                {
                    if (skId > 0) data.Skills.Add(skId, 0);
                }
            }

            return data;
        }

        public TeamObjectData BuildTeamObjectData()
        {
            return new TeamObjectData
            {
                Type = TeamObjectType.Partner,
                OnlyId = 0,
                DbId = CfgId,
                Name = Cfg.Name,
                CfgId = CfgId,
                Relive = _entity.Relive,
                Level = _entity.Level,
                Online = false
            };
        }

        public Task SendInfo()
        {
            return _player.SendPacket(GameCmd.S2CPartnerInfo, new S2C_PartnerInfo {Data = BuildPbData()});
        }

        public async Task<bool> AddExp(ulong value, bool send = true)
        {
            if (_entity.Relive > _player.Entity.Relive ||
                _entity.Relive == _player.Entity.Relive && (_entity.Level > _player.Entity.Level ||
                                                            _entity.Level == _player.Entity.Level &&
                                                            _entity.Exp >= _expMax))
            {
                _player.SendNotice("伙伴等级已超过角色等级");
                return false;
            }

            if (_entity.Level >= NextLevel && _entity.Exp >= _expMax)
            {
                _player.SendNotice("经验已达上限");
                return false;
            }

            if (_entity.Level < NextLevel)
            {
                var finalExp = _entity.Exp + value; //最终的经验
                var finalLevel = _entity.Level;
                var maxExp = ConfigService.GetPartnerUpgradeExp(_entity.Relive, finalLevel);
                //当前等级的最大经验值
                while (finalExp >= maxExp)
                {
                    // if (finalLevel >= NextLevel)
                    // {
                    //     finalLevel = (byte) NextLevel;
                    //     break;
                    // }

                    if (_entity.Relive >= _player.Entity.Relive && finalLevel >= _player.Entity.Level)
                    {
                        finalLevel = _player.Entity.Level;
                        break;
                    }

                    finalExp -= maxExp;
                    // 递增1级等级
                    finalLevel += 1;
                    // 计算新等级的最大经验值
                    maxExp = ConfigService.GetPartnerUpgradeExp(_entity.Relive, finalLevel);
                }

                if (_entity.Relive >= _player.Entity.Relive)
                    finalLevel = Math.Min(finalLevel, _player.Entity.Level);
                // finalLevel = (byte) Math.Min(finalLevel, NextLevel);
                maxExp = ConfigService.GetPartnerUpgradeExp(_entity.Relive, finalLevel);
                _entity.Exp = Math.Min(finalExp, maxExp);
                _expMax = maxExp;

                if (finalLevel != _entity.Level)
                {
                    SetLevel(finalLevel, false);
                }
            }
            else
            {
                _entity.Exp = Math.Min(_entity.Exp + value, _expMax);
            }

            if (send) await SendInfo();
            return true;
        }

        // 玩家35级以下，宠物会自动同步等级
        public void SetLevel(byte level, bool send = true)
        {
            _entity.Level = level;
            _expMax = ConfigService.GetPartnerUpgradeExp(_entity.Relive, _entity.Level);
            BuildAttrs();
            if (send) SendInfo();
        }

        public void Exchange(Partner other)
        {
            var tmpRelive = _entity.Relive;
            _entity.Relive = other._entity.Relive;
            other._entity.Relive = tmpRelive;

            var tmpLevel = _entity.Level;
            _entity.Level = other._entity.Level;
            other._entity.Level = tmpLevel;

            var tmpExp = _entity.Exp;
            _entity.Exp = other._entity.Exp;
            other._entity.Exp = tmpExp;

            _expMax = ConfigService.GetPartnerUpgradeExp(_entity.Relive, _entity.Level);
            BuildAttrs();
            SendInfo();

            other._expMax = ConfigService.GetPartnerUpgradeExp(other._entity.Relive, other._entity.Level);
            other.BuildAttrs();
            other.SendInfo();
        }

        public bool Relive()
        {
            if (_entity.Relive >= 3)
            {
                _player.SendNpcNotice(10094, "不能再转生了");
                return false;
            }

            if (_entity.Relive >= _player.Entity.Relive && _entity.Level >= _player.Entity.Level)
            {
                _player.SendNpcNotice(10094, "伙伴等级无法超过人物等级");
                return false;
            }

            var ret = false;
            if (_entity.Relive == 0 && _entity.Level == 100)
            {
                _entity.Relive = 1;
                _entity.Level = 80;
                ret = true;
            }
            else if (_entity.Relive == 1 && _entity.Level == 120)
            {
                _entity.Relive = 2;
                _entity.Level = 100;
                ret = true;
            }
            else if (_entity.Relive == 2 && _entity.Level == 140)
            {
                _entity.Relive = 3;
                _entity.Level = 120;
                ret = true;
            }

            if (!ret)
            {
                _player.SendNpcNotice(10094, "等级不够");
                return false;
            }

            _expMax = ConfigService.GetPartnerUpgradeExp(_entity.Relive, _entity.Level);
            BuildAttrs();
            SendInfo();

            return true;
        }

        public int GetLevelRange(byte relive, bool min)
        {
            if (relive == 3) return min ? 120 : 180;
            if (relive == 2) return min ? 100 : 140;
            if (relive == 1) return min ? 80 : 120;
            return min ? 0 : 100;
        }

        public int NextLevel
        {
            get
            {
                var next = _entity.Level + 1;
                if (_entity.Relive == 0)
                    next = Math.Min(next, 100);
                else if (_entity.Relive == 1)
                    next = Math.Min(next, 120);
                else if (_entity.Relive == 2)
                    next = Math.Min(next, 140);
                else if (_entity.Relive == 3)
                    next = Math.Min(next, 180);
                return next;
            }
        }

        private void BuildAttrs()
        {
            Attrs.Clear();
            // 根据转生和等级来确定属性
            var powerKey = _entity.Relive * 1000 + _entity.Level;
            Cfg.LevelAttrs.TryGetValue((uint) powerKey, out var attrs);
            if (attrs != null)
            {
                foreach (var (k, v) in attrs)
                {
                    if (v != 0) Attrs.Set(k, v);
                }
            }

            var hpMax = Attrs.Get(AttrType.HpMax);
            Attrs.Set(AttrType.Hp, hpMax);
            var mpMax = Attrs.Get(AttrType.MpMax);
            Attrs.Set(AttrType.Mp, mpMax);
        }
    }
}