using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.GrainInterfaces;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Logic.Battle;
using Ddxy.GameServer.Logic.Battle.Skill;
using Ddxy.Protocol;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Concurrency;
using Orleans.Configuration;

namespace Ddxy.GameServer.Grains
{
    [CollectionAgeLimit(Minutes = 40)]
    public class BattleGrain : Grain, IBattleGrain
    {
        private ILogger<BattleGrain> _logger;
        private Random _random;

        private bool _isActive;
        private uint _battleId; // 战斗id
        private IDisposable _tempTimer;
        private IDisposable _stopTimer;
        private IDisposable _runAwayTimer;

        private BattleType _battleType; //战斗类型
        private StartBattleRequest _startRequest; //战斗请求原始数据

        private Dictionary<uint, BattleMember> _players; //当前上场的所有玩家 参战或观战玩家
        private Dictionary<uint, BattleMember> _members; //当前上场的所有单位
        private Dictionary<uint, BattleMember> _pets; //未上场的宠物

        private BattleCamp _camp1; //阵营1
        private BattleCamp _camp2; //阵营2
        private BattleXingzhenData _xingzhen1; //阵营1星阵
        private BattleXingzhenData _xingzhen2; //阵营2星阵

        // 金翅大鹏
        BattleMember _eagleMain = null;
        BattleMember _eagleLeft = null;
        BattleMember _eagleRight = null;
        private List<TurnItem> _turns; //顺序

        // 天策符 回风落雁符 上次触发回合
        // 被击倒或控制命中时，几率让敌方速度降低，持续3个回合，同一队伍两次触发至少间隔5个回合
        private uint _hfly_last_round_camp1 = 0;
        private uint _hfly_last_round_camp2 = 0;

        private uint _round; //当前回合数
        private uint _maxRound; //最大回合数
        private bool _playerCanOper; //当前玩家是否可以操作
        private LingHouInfo _lingHouInfo; // 天降灵猴数据

        private uint _onlyId;
        private uint _buffId;

        private bool _isPvp;

        // 觉醒技 凤鸣余音 适用技能
        private readonly List<SkillId> FengMingYuYinValidSkills = new()
        {
            SkillId.YouFengLaiYiJin,
            SkillId.YouFengLaiYiMu,
            SkillId.YouFengLaiYiShui,
            SkillId.YouFengLaiYiHuo,
            SkillId.YouFengLaiYiTu,
        };

        public BattleGrain(ILogger<BattleGrain> logger)
        {
            _logger = logger;
        }

        public override Task OnActivateAsync()
        {
            _battleId = (uint)this.GetPrimaryKeyLong();
            return Task.CompletedTask;
        }

        public override Task OnDeactivateAsync()
        {
            return ShutDown();
        }

        public async Task StartUp(Immutable<byte[]> reqBytes)
        {
            if (_isActive) return;
            _isActive = true;

            _random = new Random();

            _camp1 = new BattleCamp();
            _camp2 = new BattleCamp();
            _xingzhen1 = null;
            _xingzhen2 = null;
            _turns = new List<TurnItem>();
            // 参战或观战玩家
            _players = new Dictionary<uint, BattleMember>();
            _members = new Dictionary<uint, BattleMember>();
            _pets = new Dictionary<uint, BattleMember>();

            try
            {
                _startRequest = StartBattleRequest.Parser.ParseFrom(reqBytes.Value);
                _battleType = _startRequest.Type;
                _isPvp = SkillManager.IsPvp(_battleType);
                // 天降灵猴数据
                if (_battleType == BattleType.TianJiangLingHou) _lingHouInfo = new LingHouInfo();

                _maxRound = 31;
                // 帮战中最大回合60
                if (_battleType is BattleType.SectWarArena or BattleType.SectWarCannon or BattleType.SectWarDoor or
                    BattleType.SectWarFreePk)
                {
                    _maxRound = 61;
                }

                // 天策符 回风落雁符 上次触发回合
                // 被击倒或控制命中时，几率让敌方速度降低，持续3个回合，同一队伍两次触发至少间隔5个回合
                _hfly_last_round_camp1 = 0;
                _hfly_last_round_camp2 = 0;

                // 构建阵营1
                _xingzhen1 = _startRequest.Team1[0].Xinzhen;
                foreach (var bmd in _startRequest.Team1)
                {
                    var grain = bmd.Type == LivingThingType.Player ? GrainFactory.GetGrain<IPlayerGrain>(bmd.Id) : null;
                    var mb = new BattleMember(NextOnlyId(), 1, bmd, _xingzhen1, grain);
                    _camp1.Members.Add(mb);
                    if (mb.Pos == 0)
                    {
                        _pets.Add(mb.OnlyId, mb);
                    }
                    else if (mb.Pos > 0)
                    {
                        _turns.Add(new TurnItem {OnlyId = mb.OnlyId, Spd = mb.Spd});
                        _members.Add(mb.OnlyId, mb);
                    }
                    // 参战或观战玩家
                    if (grain != null)
                    {
                        _players.Add(bmd.Id, mb);
                    }
                }

                // 构建阵营2
                _xingzhen2 = _startRequest.Team2[0].Xinzhen;
                foreach (var bmd in _startRequest.Team2)
                {
                    var grain = bmd.Type == LivingThingType.Player ? GrainFactory.GetGrain<IPlayerGrain>(bmd.Id) : null;
                    var mb = new BattleMember(NextOnlyId(), 2, bmd, _xingzhen2, grain);
                    _camp2.Members.Add(mb);
                    if (mb.Pos == 0)
                    {
                        _pets.Add(mb.OnlyId, mb);
                    }
                    else if (mb.Pos > 0)
                    {
                        _turns.Add(new TurnItem {OnlyId = mb.OnlyId, Spd = mb.Spd});
                        _members.Add(mb.OnlyId, mb);
                    }
                    // 参战或观战玩家
                    if (grain != null)
                    {
                        _players.Add(bmd.Id, mb);
                    }
                    // 金翅大鹏
                    if (mb.IsEagleMain()) _eagleMain = mb;
                    if (mb.IsEagleLeft()) _eagleLeft = mb;
                    if (mb.IsEagleRight()) _eagleRight = mb;
                }
                // 金翅大鹏
                if (_eagleMain != null)
                {
                    _eagleMain.SetEagleLeftAndRight(_eagleLeft, _eagleRight);
                }

                // 根据OwnerId(RoleId)来确定Pet和Player的绑定关系
                foreach (var v in _camp1.Members)
                {
                    if (v.Data.OwnerId > 0)
                    {
                        var owner = _camp1.Members.FirstOrDefault(p => p.IsPlayer && p.Id == v.Data.OwnerId);
                        if (owner != null)
                        {
                            v.OwnerOnlyId = owner.OnlyId;
                            v.Online = owner.Online;
                            v.Grain = owner.Grain;
                            // 必须是宠物
                            if (v.Pos > 0 && v.IsPet)
                            {
                                owner.PetOnlyId = v.OnlyId;
                                v.BeCache = true;
                            }
                        }
                    }
                }

                foreach (var v in _camp2.Members)
                {
                    if (v.Data.OwnerId > 0)
                    {
                        var owner = _camp2.Members.FirstOrDefault(p => p.IsPlayer && p.Id == v.Data.OwnerId);
                        if (owner != null)
                        {
                            v.OwnerOnlyId = owner.OnlyId;
                            v.Online = owner.Online;
                            v.Grain = owner.Grain;
                            // 必须是宠物
                            if (v.Pos > 0 && v.IsPet)
                            {
                                owner.PetOnlyId = v.OnlyId;
                                v.BeCache = true;
                            }
                        }
                    }
                }

                // 下发给所有player客户端
                foreach (var k in _members.Keys)
                {
                    SendBattleStart(k);
                }

                // 1s钟后开始战斗
                _tempTimer?.Dispose();
                _tempTimer = RegisterTimer(StartBattle, null, TimeSpan.FromSeconds(1), TimeSpan.FromDays(1));

                // 30分钟后自动销毁战斗
                _stopTimer = RegisterTimer(DestroyBattleTimeout, null, TimeSpan.FromMinutes(30), TimeSpan.FromSeconds(5));
                // 帮战 比武场 对决
                if (_battleType == BattleType.SectWarArena)
                {
                    var sectWarGrain = GrainFactory.GetGrain<ISectWarGrain>(_startRequest.ServerId);
                    if (sectWarGrain != null)
                    {
                        _ = sectWarGrain.OnWarArenaEnter(_battleId, _startRequest.Team1[0].Id, _startRequest.Team2[0].Id);
                    }
                }
                LogDebug("激活成功");
            }
            catch (Exception ex)
            {
                LogError($"激活失败[{ex.Message}][{ex.StackTrace}]");
                // 算流局
                Draw();
            }

            await Task.CompletedTask;
        }

        public Task ShutDown()
        {
            if (!_isActive) return Task.CompletedTask;

            _tempTimer?.Dispose();
            _tempTimer = null;
            _stopTimer?.Dispose();
            _stopTimer = null;
            _runAwayTimer?.Dispose();
            _runAwayTimer = null;

            // 去Battles移除
            _ = GrainFactory.GetGrain<IGlobalGrain>(0).RemoveBattle(_battleId);

            foreach (var v in _camp1.Members)
            {
                v.Destroy();
            }
            _camp1.Members.Clear();
            _camp1.Effects.Clear();

            foreach (var v in _camp2.Members)
            {
                v.Destroy();
            }
            _camp2.Members.Clear();
            _camp2.Effects.Clear();

            _xingzhen1 = null;
            _xingzhen2 = null;

            // 参战或观战玩家
            _players.Clear();
            _players = null;
            _members.Clear();
            _members = null;
            _pets.Clear();
            _pets = null;
            _turns.Clear();
            _turns = null;

            if (_startRequest != null)
            {
                _startRequest.Team1.Clear();
                _startRequest.Team2.Clear();
                _startRequest = null;
            }

            _isActive = false;
            LogDebug("注销成功");
            DeactivateOnIdle();
            return Task.CompletedTask;
        }

        public ValueTask<bool> CheckActive()
        {
            return new ValueTask<bool>(_isActive);
        }

        public async Task Exit(uint roleId)
        {
            if (!_isActive) return;
            var mb = _members.Values.FirstOrDefault(p => p.IsPlayer && p.Id == roleId);
            if (mb == null) return;
            _members.Remove(mb.OnlyId);
            var members = mb.CampId == 1 ? _camp1.Members : _camp2.Members;
            var others = members.Where(it => it.OwnerOnlyId == mb.OnlyId).Select(it => it.OnlyId).ToList();
            foreach (var v in others)
            {
                _members.Remove(v);
            }

            if (_members.Count == 0)
            {
                await ShutDown();
            }
        }

        public async Task Online(uint roleId, bool pauseModel = false)
        {
            if (!_isActive) return;
            var mb = _members.Values.FirstOrDefault(p => p.IsPlayer && p.Id == roleId);
            if (mb == null) return;

            mb.Online = true;
            var members = mb.CampId == 1 ? _camp1.Members : _camp2.Members;
            foreach (var v in members)
            {
                if (v.OwnerOnlyId == mb.OnlyId)
                {
                    v.Online = true;
                }
            }

            if (!mb.IsAction)
            {
                mb.IsAction = true;
                // 上线后默认用上次技能
                mb.ActionData.ActionType = BattleActionType.Skill;
                mb.ActionData.ActionId = (uint) mb.LastSkill;
                mb.ActionData.Target = 0;
            }

            SendBattleStart(mb.OnlyId);
            await Task.CompletedTask;
        }

        public Task Offline(uint roleId, bool pauseModel = false)
        {
            if (!_isActive) return Task.CompletedTask;
            var mb = _members.Values.FirstOrDefault(p => p.IsPlayer && p.Id == roleId);
            if (mb != null)
            {
                mb.Online = false;
                var members = mb.CampId == 1 ? _camp1.Members : _camp2.Members;
                foreach (var v in members)
                {
                    if (v.OwnerOnlyId == mb.OnlyId)
                    {
                        v.Online = false;
                    }
                }
            }

            // 如果所有Player都不在线了就可以销毁战斗
            if (!pauseModel)
            {
                var isAllPlayerOffline = _members.Values.All(p => !p.IsPlayer || !p.Online);
                if (isAllPlayerOffline)
                {
                    Draw();
                    LogDebug("所有玩家已离线, 流局");
                }
            }

            return Task.CompletedTask;
        }

        // 玩家手动操作
        public async Task Attack(Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return;
            if (!_playerCanOper) return;
            var req = C2S_BattleAttack.Parser.ParseFrom(reqBytes.Value);

            _members.TryGetValue(req.OnlyId, out var mb);
            if (mb == null || mb.IsAction) return;
            // 判断选择的物品是否还有剩余？
            if (req.ActionType == BattleActionType.Prop)
            {
                var count = await mb.GetBagItemCount(req.ActionId);
                if (count <= 0)
                {
                    mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "此物品已在战斗中消耗完了" });
                    return;
                }
            }

            // 如果是Skill， 要检查技能id是否合法
            var skillId = (SkillId) req.ActionId;
            if (req.ActionType == BattleActionType.Skill && skillId != SkillId.NormalAtk &&
                skillId != SkillId.NormalDef)
            {
                while (true)
                {
                    // 被动技能不能放
                    var skill = SkillManager.GetSkill(skillId);
                    if (skill == null || skill.ActionType == SkillActionType.Passive)
                    {
                        req.ActionId = (uint) SkillId.NormalAtk;
                        break;
                    }

                    mb.Skills.TryGetValue(skillId, out var info);
                    if (info == null || !info.CanUse)
                    {
                        req.ActionId = (uint) SkillId.NormalAtk;
                        mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice {Text = $"{skill.Name}暂不可用"});
                    }

                    break;
                }
            }

            // 龙族 逆鳞技能
            if (skillId == SkillId.NiLin)
            {
                var skill = SkillManager.GetSkill(skillId);
                if (skill == null || !mb.HasSkill(skillId))
                {
                    mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "暂不可用" });
                    return;
                } else {
                    var gedr = new GetEffectDataRequest
                    {
                        Level = mb.Data.Level,
                        Relive = mb.Relive,
                        Intimacy = mb.Data.PetIntimacy,
                        Profic = mb.GetSkillProfic(skillId),
                        Atk = mb.Atk,
                        Deadnum = 0,
                        MaxMp = mb.MpMax,
                        Attrs = mb.Attrs,
                        OrnamentSkills = mb.OrnamentSkills,
                        BattleType = _battleType,
                        Member = mb
                    };
                    var effectData2Self = skill.GetEffectData2Self(gedr);
                    mb.AddBuff(new Buff(NextBuffId(), skill, effectData2Self));
                    // 广播给这个阵营所有Player
                    BroadcastCamp(mb.OnlyId, GameCmd.S2CBattleAttack, new S2C_BattleAttack
                    {
                        OnlyId = req.OnlyId,
                        ActionType = req.ActionType,
                        ActionId = req.ActionId,
                        TargetId = req.TargetId
                    });
                    return;
                }
            }
            mb.ActionData.ActionType = req.ActionType;
            mb.ActionData.ActionId = req.ActionId;
            mb.ActionData.Target = req.TargetId;
            mb.IsAction = true;

            // 广播给这个阵营所有Player
            BroadcastCamp(mb.OnlyId, GameCmd.S2CBattleAttack, new S2C_BattleAttack
            {
                OnlyId = req.OnlyId,
                ActionType = req.ActionType,
                ActionId = req.ActionId,
                TargetId = req.TargetId
            });

            // 如果本次回合所有单位都已操作
            if (CheckIfAllAttacked())
            {
                _tempTimer?.Dispose();
                _tempTimer = RegisterTimer(DoRoundWrap, null, TimeSpan.FromSeconds(0.5f), TimeSpan.FromSeconds(1));
            }

            await Task.CompletedTask;
        }

        // 进入观战
        public ValueTask<bool> EnterWatchBattle(uint campId, uint roleId)
        {
            if (!_isActive) return ValueTask.FromResult(false);
            if (!_players.ContainsKey(roleId))
            {
                _players.Add(roleId, new BattleMember(campId, GrainFactory.GetGrain<IPlayerGrain>(roleId)));
            }
            SendBattleStart2Watcher(roleId);
            return ValueTask.FromResult(true);
        }

        // 退出观战
        public ValueTask<bool> ExitWatchBattle(uint roleId)
        {
            if (!_isActive) return ValueTask.FromResult(true);
            _players.Remove(roleId);
            return ValueTask.FromResult(true);
        }

        // 发弹幕
        public Task SendDanMu(uint roleId, Immutable<byte[]> reqBytes)
        {
            if (!_isActive) return Task.CompletedTask;
            // FIXME: 检查是否参战或观战
            if (_players.ContainsKey(roleId))
            {
                Broadcast(reqBytes);
            }
            return Task.CompletedTask;
        }

        // 开始战斗
        private async Task StartBattle(object _)
        {
            try
            {
                _tempTimer?.Dispose();
                _tempTimer = null;

                // 所有宠物添加入场效果
                foreach (var (_, member) in _members)
                {
                    if (member.IsPet) LoadPetEnterEffect(member);
                }

                StartRound();
            }
            catch (Exception ex)
            {
                LogError($"开始战斗出错[{ex.Message}][{ex.StackTrace}]");
                // 算流局
                Draw();
            }

            await Task.CompletedTask;
        }

        // 超过了30分钟, 自动销毁战斗, 算流局
        private async Task DestroyBattleTimeout(object _)
        {
            _stopTimer?.Dispose();
            _stopTimer = null;
            Draw();
            await Task.CompletedTask;
        }

        // 开始一个回合
        private void StartRound()
        {
            _tempTimer?.Dispose();
            _tempTimer = null;

            if (_members.Count == 0)
            {
                ShutDown();
                return;
            }

            var winTeam = CheckWin();
            if (winTeam > 0)
            {
                TeamWin(winTeam);
                return;
            }

            _round++;
            // 超过30个回合后就流局
            if (_round >= _maxRound)
            {
                Draw();
                return;
            }

            _playerCanOper = true;

            // 先处理buff
            var resp = new S2C_BattleRoundBegine();
            foreach (var turnItem in _turns)
            {
                _members.TryGetValue(turnItem.OnlyId, out var member);
                if (member == null || member.Dead) continue;

                // 利涉大川
                if (member.HasSkill(SkillId.LiSheDaChuan))
                {
                    member.AddMp(MathF.Round(member.MpMax * 0.8f), _members);
                }

                // 无色无相
                if (member.HasSkill(SkillId.WuSeWuXiang) && !member.HasBuff(SkillType.YinShen))
                {
                    var skill = SkillManager.GetSkill(SkillId.WuSeWuXiang);
                    if (skill != null)
                    {
                        var effect = skill.GetEffectData(null);
                        if (_random.Next(0, 100) < effect.Percent)
                        {
                            var buff = new Buff(NextBuffId(), skill, effect)
                            {
                                Source = member.OnlyId,
                                Probability = 10000
                            };
                            member.AddBuff(buff);
                            // 觉醒技 无嗔无狂
                            // 天生技能“无色无相”触发时，有10%/20%/35%/50%概率令主人也进入隐身状态。
                            if (member.CanUseJxSkill( SkillId.WuChenWuKuang))
                            {
                                var baseValues = new List<float>() { 1000, 2000, 3500, 5000 };
                                var rangeValue = baseValues[(int)member.Data.PetJxGrade] - baseValues[(int)member.Data.PetJxGrade - 1];
                                var calcValue = baseValues[(int)member.Data.PetJxGrade - 1] + rangeValue * member.Data.PetJxLevel / 6;
                                if (_random.Next(10000) < calcValue)
                                {
                                    _members.TryGetValue(member.OwnerOnlyId, out var owner);
                                    if (owner != null && !owner.Dead)
                                    {
                                        var buff1 = new Buff(NextBuffId(), skill, effect)
                                        {
                                            Source = member.OnlyId,
                                            Probability = 10000
                                        };
                                        owner.AddBuff(buff1);
                                    }
                                }
                            }
                        }
                    }
                }

                // 乾坤 气定乾坤
                if (member.QiDingQianKun > 0f && _random.Next(0, 100) < member.QiDingQianKun)
                {
                    // 自行摆脱混乱、昏睡状态
                    member.RemoveBuff(SkillType.Chaos);
                    member.RemoveBuff(SkillType.Sleep);
                }

                // debuf
                var addHp = 0f;
                // 龙族 治愈技能BUFF，应该在回合开始后删除
                var toRemove = new List<uint>();
                foreach (var buff in new List<Buff>(member.Buffs))
                {
                    addHp += buff.StartRound(member, _members);
                    // 龙族 治愈技能BUFF，应该在回合开始后删除
                    var isLongZhiYu = buff.SkillId == SkillId.PeiRanMoYu || buff.SkillId == SkillId.ZeBeiWanWu;
                    if (isLongZhiYu && buff.IsEnd())
                    {
                        toRemove.Add(buff.Id);
                    }
                }
                // 龙族 治愈技能BUFF，应该在回合开始后删除
                foreach (var id in toRemove)
                {
                    member.RemoveBuff(id);
                }


                // buff.StartRound中可能会移除Buff导致member.Buffs发生了改变, 从而引发异常
                // var addHp = member.Buffs.Sum(buff => buff.StartRound(member, _members));
                var attack = new BattleAttackData
                {
                    OnlyId = member.OnlyId,
                    Type = addHp > 0 ? BattleAttackType.Hp : BattleAttackType.Hurt,
                    Value = (int) MathF.Floor(addHp),
                    Response = BattleResponseType.None,
                    Dead = member.Dead,
                    Hp = MathF.Floor(member.Hp),
                    Mp = (uint) MathF.Floor(member.Mp),
                    Param = 0
                };
                attack.Buffs.AddRange(member.GetBuffsSkillId());
                resp.Attacks.Add(attack);

                // 套装技能 把薪助火-珍藏/无价
                if (_round > 1 && (member.OrnamentSkills.ContainsKey(3031) || member.OrnamentSkills.ContainsKey(3032)))
                {
                    if (member.HasBuff(SkillType.Defense))
                    {
                        var campMembers = member.CampId == 1 ? _camp1.Members : _camp2.Members;
                        foreach (var cmb in campMembers)
                        {
                            if (cmb.Dead) continue;
                            var addHpPercent = 0.1f;
                            if (member.OrnamentSkills.ContainsKey(3032))
                            {
                                addHpPercent += MathF.Floor(member.Attrs.Get(AttrType.MinJie) / 200) * 0.01f;
                            }

                            addHp = MathF.Round(cmb.HpMax * addHpPercent);
                            cmb.AddHp(addHp, _members);
                            attack = new BattleAttackData
                            {
                                OnlyId = member.OnlyId,
                                Type = BattleAttackType.Hp,
                                Value = (int) addHp,
                                Response = BattleResponseType.None,
                                Dead = member.Dead,
                                Hp = MathF.Floor(member.Hp),
                                Mp = (uint) MathF.Floor(member.Mp),
                                Param = 0
                            };
                            attack.Buffs.AddRange(member.GetBuffsSkillId());
                            resp.Attacks.Add(attack);
                        }
                    }
                }

                // 套装技能 厚德载物
                if (_round > 1 && (member.OrnamentSkills.ContainsKey(9011) || member.OrnamentSkills.ContainsKey(9012)))
                {
                    var percent = member.OrnamentSkills.ContainsKey(9012) ? 0.10f : 0.05f;
                    addHp = MathF.Round(member.HpMax * percent);
                    member.AddHp(addHp, _members);
                    attack = new BattleAttackData
                    {
                        OnlyId = member.OnlyId,
                        Type = BattleAttackType.Hp,
                        Value = (int) addHp,
                        Response = BattleResponseType.None,
                        Dead = member.Dead,
                        Hp = MathF.Floor(member.Hp),
                        Mp = (uint) MathF.Floor(member.Mp),
                        Param = 0
                    };
                    attack.Buffs.AddRange(member.GetBuffsSkillId());
                    resp.Attacks.Add(attack);
                }

                // 璇玑 颖悟绝伦-珍藏
                if (_round > 1 && member.OrnamentSkills.ContainsKey(9021))
                {
                    var addMp = MathF.Round(member.MpMax * 0.05f);
                    member.AddMp(addMp);
                    attack = new BattleAttackData
                    {
                        OnlyId = member.OnlyId,
                        Type = BattleAttackType.Mp,
                        Value = (int) addMp,
                        Response = BattleResponseType.None,
                        Dead = member.Dead,
                        Hp = MathF.Floor(member.Hp),
                        Mp = (uint) MathF.Floor(member.Mp),
                        Param = 0
                    };
                    attack.Buffs.AddRange(member.GetBuffsSkillId());
                    resp.Attacks.Add(attack);
                }

                // 鲲鹏 鲲鹏之变 珍藏/无价
                if (_round > 1 && (member.OrnamentSkills.ContainsKey(3011) || member.OrnamentSkills.ContainsKey(3012)))
                {
                    var percent = 0.1f;
                    if (member.OrnamentSkills.ContainsKey(3012))
                    {
                        percent += MathF.Floor(member.Attrs.Get(AttrType.MinJie) / 200.0f) * 0.01f;
                    }

                    // 友方非倒地单位
                    var campMembers = member.CampId == 1 ? _camp1.Members : _camp2.Members;
                    foreach (var cmb in campMembers)
                    {
                        if (cmb.Dead || cmb.Pos <= 0) continue;

                        var addMp = MathF.Round(cmb.MpMax * percent);
                        if (addMp == 0) continue;
                        addMp = cmb.AddMp(addMp, _members);
                        attack = new BattleAttackData
                        {
                            OnlyId = cmb.OnlyId,
                            Type = BattleAttackType.Mp,
                            Value = (int) addMp,
                            Response = BattleResponseType.None,
                            Dead = cmb.Dead,
                            Hp = MathF.Floor(cmb.Hp),
                            Mp = (uint) MathF.Floor(cmb.Mp),
                            Param = 0
                        };
                        attack.Buffs.AddRange(cmb.GetBuffsSkillId());
                        resp.Attacks.Add(attack);
                    }
                }

                // 套装技能 魅影缠身
                if (_round > 1 && _isPvp &&
                    (member.OrnamentSkills.ContainsKey(4051) || member.OrnamentSkills.ContainsKey(4052)))
                {
                    var percent = 0.1f;
                    if (member.OrnamentSkills.ContainsKey(4052))
                    {
                        percent += MathF.Floor(member.Attrs.Get(AttrType.MinJie) / 200.0f) * 0.01f;
                    }

                    // 友方非倒地单位
                    var campMembers = member.CampId == 1 ? _camp1.Members : _camp2.Members;
                    foreach (var cmb in campMembers)
                    {
                        if (cmb.Dead || cmb.Pos <= 0) continue;

                        addHp = MathF.Round(cmb.MpMax * percent);
                        if (addHp == 0) continue;
                        addHp = cmb.AddHp(addHp, _members);
                        attack = new BattleAttackData
                        {
                            OnlyId = cmb.OnlyId,
                            Type = BattleAttackType.Hp,
                            Value = (int) addHp,
                            Response = BattleResponseType.None,
                            Dead = cmb.Dead,
                            Hp = MathF.Floor(cmb.Hp),
                            Mp = (uint) MathF.Floor(cmb.Mp),
                            Param = 0
                        };
                        attack.Buffs.AddRange(cmb.GetBuffsSkillId());
                        resp.Attacks.Add(attack);
                    }
                }

                member.CheckKongZhiRound(_round);

                // Console.WriteLine($"{member.Data.Name} {_round} spd = {member.Spd} atk = {member.Atk}");
            }

            foreach (var turnItem in _turns)
            {
                _members.TryGetValue(turnItem.OnlyId, out var member);
                if (member == null || member.Dead) continue;

                // 移花接木
                if (member.HasSkill(SkillId.YiHuaJieMu))
                {
                    var members = member.CampId == 1 ? _camp1.Members : _camp2.Members;
                    var totalNum = members.Count;
                    var freeNum = members.Count(p => !p.Dead && !p.HasDeBuff());
                    var percent = freeNum * 1.0f / totalNum;
                    if (percent < 0.4f)
                    {
                        member.RemoveDeBuff();
                        resp.Attacks.Add(new BattleAttackData
                        {
                            OnlyId = member.OnlyId,
                            Type = BattleAttackType.Hp,
                            Value = 0,
                            Response = BattleResponseType.None,
                            Dead = member.Dead,
                            Hp = MathF.Floor(member.Hp),
                            Mp = (uint) MathF.Floor(member.Mp),
                            Param = 0,
                            Buffs = {member.GetBuffsSkillId()}
                        });
                    }
                }

                // 安行疾斗, 如果主人是死亡状态，那么每回合开始清除异常状态
                if (member.IsPet && member.HasSkill(SkillId.AnXingJiDou))
                {
                    _members.TryGetValue(member.OwnerOnlyId, out var master);
                    if (master is {Dead: true})
                    {
                        member.RemoveDeBuff();
                        resp.Attacks.Add(new BattleAttackData
                        {
                            OnlyId = member.OnlyId,
                            Type = BattleAttackType.Hp,
                            Value = 0,
                            Response = BattleResponseType.None,
                            Dead = member.Dead,
                            Hp = MathF.Floor(member.Hp),
                            Mp = (uint) MathF.Floor(member.Mp),
                            Param = 0,
                            Buffs = {member.GetBuffsSkillId()}
                        });
                    }
                }
            }

            // 生成出手顺序
            GenTurnList();

            // 处理StageEffect
            CheckStageEffect(_camp1);
            CheckStageEffect(_camp2);

            // 扭转乾坤
            TryReverseStageEffect(_camp1);
            TryReverseStageEffect(_camp2);

            resp.Effects.AddRange(GetStageEffect());
            // 下发给所有玩家
            Broadcast(GameCmd.S2CBattleRoundBegine, resp);

            // 被buff烫死, 刚开局，等5秒钟结算
            winTeam = CheckWin();
            if (winTeam == 0)
            {
                var second = 31;
                if (CheckIfAllAttacked()) second = 4;

                _tempTimer?.Dispose();
                _tempTimer = RegisterTimer(DoRoundWrap, null, TimeSpan.FromSeconds(second), TimeSpan.FromSeconds(1));
            }
            else
            {
                var second = 1;
                // 如果是第一局，被buff烫死，等待5s再结算
                if (_round == 1) second = 5;

                _tempTimer?.Dispose();
                _tempTimer = RegisterTimer(TeamWinTimeout, winTeam, TimeSpan.FromSeconds(second),
                    TimeSpan.FromSeconds(1));
            }
        }

        private async Task DoRoundWrap(object _)
        {
            try
            {
                _tempTimer?.Dispose();
                _tempTimer = null;

               await DoRound();
            }
            catch (Exception ex)
            {
                LogError($"进行回合出错[{_round}][{ex.Message}][{ex.Source}][{ex.StackTrace}]");
                // 算流局
                Draw();
            }

            await Task.CompletedTask;
        }

        // 结束这个回合
        private async Task DoRound()
        {
            // 用户不能操作了
            _playerCanOper = false;

            var resp = new S2C_BattleRoundEnd {Round = _round};
            // var replaces = new Dictionary<uint, uint>();

            // 两个阵营还活着的人数
            var camp1alive = _members.Where(m => m.Value.CampId == 1 && !m.Value.Dead).Select(m => m.Key).Count();
            var camp2alive = _members.Where(m => m.Value.CampId != 1 && !m.Value.Dead).Select(m => m.Key).Count();

            // 整理保护列表, key是保护的目标onlyId，value是保护者onlyId
            var protectList = new Dictionary<uint, uint>();
            foreach (var turn in _turns)
            {
                _members.TryGetValue(turn.OnlyId, out var mb);
                if (mb == null) continue;
                if (mb.IsAction && mb.ActionData.ActionType == BattleActionType.Protect)
                {
                    var target = mb.ActionData.Target;
                    if (!protectList.ContainsKey(target)) protectList[target] = mb.OnlyId;
                }

                if (mb.HasSkill(SkillId.NvWaZhouNian))
                {
                    var members = mb.CampId == 1 ? _camp1.Members : _camp2.Members;
                    foreach (var xmb in members)
                    {
                        if (!xmb.Dead && xmb.Type != LivingThingType.Pet)
                        {
                            // 按照金、木、水、火、土的顺序
                            if (_round % 5 == 1) xmb.AddRoundAttr(AttrType.Qjin, 20);
                            if (_round % 5 == 2) xmb.AddRoundAttr(AttrType.Qmu, 20);
                            if (_round % 5 == 3) xmb.AddRoundAttr(AttrType.Qshui, 20);
                            if (_round % 5 == 4) xmb.AddRoundAttr(AttrType.Qhuo, 20);
                            if (_round % 5 == 0) xmb.AddRoundAttr(AttrType.Qtu, 20);
                        }
                    }
                }
                // 天策符 陌上开花 是否已经使用了2连击？
                mb.double_pugong_hited_round = 0;
                // 天策符 浩气凌霄 是否已经使用了2连击？
                mb.double_fashu_hited_round = 0;
                // 是否刚摆脱控制？
                mb.just_not_kongzhi = false;
                // 本阵营还存活人数
                mb.alive_count = (uint)(mb.CampId == 1 ? camp1alive : camp2alive);
                // 天策符 载物符 承天载物符
                // 激活次数？
                mb.ctzw_active_times = 0;
                // 天策符 御兽符 固本符
                // 召唤兽每存活1回合，增加冰混睡忘抗性
                if (_round > 1 && mb.IsPet && !mb.Dead)
                {
                    var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.GuBen3);
                    var grade = 3;
                    if (fskill == null)
                    {
                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.GuBen2);
                        grade = 2;
                        if (fskill == null)
                        {
                            fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.GuBen1);
                            grade = 1;
                        }
                    }
                    if (fskill != null)
                    {
                        var rate = (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f) / 2;
                        mb.Attrs.Set(AttrType.DfengYin, mb.Attrs.Get(AttrType.DfengYin) * (1 + rate));
                        mb.Attrs.Set(AttrType.DhunLuan, mb.Attrs.Get(AttrType.DhunLuan) * (1 + rate));
                        mb.Attrs.Set(AttrType.DhunShui, mb.Attrs.Get(AttrType.DhunShui) * (1 + rate));
                        mb.Attrs.Set(AttrType.DyiWang, mb.Attrs.Get(AttrType.DyiWang) * (1 + rate));
                    }
                }
                // 觉醒技 黄泉一笑 本回合触发次数
                mb.huang_quan_yi_xiao_times = 0;
            }

            var addTime = 0f;
            for (var turnIndex = 0; turnIndex < _turns.Count; turnIndex++)
            {
                var turn = _turns[turnIndex];
                // 出手的角色
                _members.TryGetValue(turn.OnlyId, out var mb);
                if (mb == null) continue;

                // if (mb.ActionData.ActionType == BattleActionType.Summon) {
                //    LogInfo($"玩家[{mb.Data.Id}][{mb.Data.Name}]召唤[{mb.ActionData.ActionId}]");
                // }
                if (mb.Dead)
                {
                    // 死亡后的玩家只能召还
                    if (mb.ActionData.ActionType != BattleActionType.SummonBack)
                    {
                        mb.IsRoundAction = true;
                        continue;
                    }
                }

                if (mb.HasSkill(SkillId.LuoZhiYunYan))
                {
                    if (_round == 1 || (mb.ActionData != null && mb.LastAction != null &&
                                        mb.ActionData.Like(mb.LastAction)))
                    {
                        mb.RemoveKongZhiBuff();
                    }

                    if (mb.ActionData != null) mb.LastAction = mb.ActionData.Clone();
                }
                // 记录连续被控制？
                if (mb.HasKongZhiBuff())
                {
                    mb.continue_be_kongzhi++;
                }
                // 天策符 载物符 意气
                // 如果连续2回合出手时被控，下一个回合有几率摆脱控制
                if (mb.IsPlayer && mb.continue_be_kongzhi >= 3)
                {
                    var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.YiQi3);
                    var grade = 3;
                    if (fskill == null)
                    {
                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.YiQi2);
                        grade = 2;
                        if (fskill == null)
                        {
                            fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.YiQi1);
                            grade = 1;
                        }
                    }
                    if (fskill != null && _random.Next(10000) >= (7500f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                    {
                        mb.RemoveKongZhiBuff();
                    }
                }

                if (mb.HasBuff(SkillType.Seal))
                {
                    // LogDebug($"玩家[{mb.Data.Id}][{mb.Data.Name}]被封印或昏睡");
                    continue;
                }

                // 昏睡状态下只能使用药品、召唤、召还
                if (mb.HasBuff(SkillType.Sleep))
                {
                    if (mb.ActionData.ActionType != BattleActionType.SummonBack &&
                        mb.ActionData.ActionType != BattleActionType.Summon &&
                        mb.ActionData.ActionType != BattleActionType.Prop)
                    {
                        continue;
                    }
                }

                if (mb.IsPartner && HasBb()) continue;

                // 修正内容
                if (mb.ActionData.ActionType == BattleActionType.Unkown)
                    mb.ActionData.ActionType = BattleActionType.Skill;
                if (mb.HasBuff(SkillType.Chaos)) mb.ActionData.ActionType = BattleActionType.Skill;
                if (_battleType == BattleType.TianJiangLingHou && mb.IsMonster)
                {
                    var first = GetFirstPlayer();
                    // 如果玩家的钱少于每次灵猴最少偷的钱就逃跑
                    if (first != null && first.Data.Money < GameDefine.LingHouMinMoney)
                    {
                        mb.ActionData.ActionType = BattleActionType.RunAway;
                    }
                    else
                    {
                        // 35%的概率会逃跑
                        var r = new Random().Next(0, 100);
                        if (r < 35) mb.ActionData.ActionType = BattleActionType.RunAway;
                    }
                }

                // 神兽降临--宝宝在2轮后逃跑
                if (_battleType == BattleType.ShenShouJiangLin && mb.IsBb && mb.IsMonster && _round > 2)
                {
                    mb.ActionData.ActionType = BattleActionType.RunAway;
                }

                // 不是防御 都要破除隐身状态
                if (mb.ActionData.ActionType != BattleActionType.Skill && mb.ActionData.ActionId != 0 &&
                    mb.ActionData.ActionId != (uint) SkillId.NormalDef)
                {
                    var buff = mb.GetBuffByMagicType(SkillType.YinShen);
                    if (buff != null) mb.RemoveBuff(buff.Id);
                }

                var actionData = new BattleActionData {OnlyId = mb.OnlyId, Type = mb.ActionData.ActionType};
                List<BattleActionData> nextActionDatas = null;
                var before = new BattleActionBefore();
                mb.IsRoundAction = true;

                var runAway = false;
                // 龙族 对自己临时加的buff，计算完成后要删除
                uint buffid2self = 0;
                if (mb.ActionData.ActionType == BattleActionType.RunAway)
                {
                    // 逃跑
                    actionData.ActionId = 0; //0表示逃跑失败
                    var r = new Random().Next(0, 100);
                    if (_battleType == BattleType.ShuiLuDaHui)
                    {
                        // 水路大会不允许逃跑
                        r = 100;
                    }
                    else if (_battleType == BattleType.TianJiangLingHou)
                    {
                        // 天降灵猴逃跑百分百成功
                        r = 0;
                        _lingHouInfo.WinType = 1;
                    }
                    else if (_battleType == BattleType.ShenShouJiangLin)
                    {
                        // 神兽降临不允许逃跑，除了宝宝（100%逃脱成功）
                        r = mb.IsBb && mb.IsMonster ? 0 : 100;
                    }

                    // 有80%的概率能逃跑成功
                    if (r < 80)
                    {
                        runAway = true;
                        actionData.ActionId = 1; //用1表示逃跑成功

                        var winTeam = (byte) (mb.CampId == 1 ? 2 : 1);
                        // 这里前往不要用tempTimer, 因为这个回合走完后就会重置tempTimer
                        _runAwayTimer?.Dispose();
                        _runAwayTimer = RegisterTimer(TeamWinTimeout, winTeam,
                            TimeSpan.FromSeconds(1.8 * (1 + resp.List.Count)), TimeSpan.FromSeconds(1));
                    }
                }
                else if (mb.ActionData.ActionType == BattleActionType.Prop)
                {
                    // 使用道具, 遗忘和混乱状态不能使用
                    if (mb.HasBuff(SkillType.Forget) || mb.HasBuff(SkillType.Chaos) ||
                        mb.HasBuff(SkillType.Seal)) continue;
                    var itemId = mb.ActionData.ActionId;
                    var attacks = OnMemberUseItem(mb, mb.ActionData.Target, itemId);
                    if (attacks.Count > 0)
                    {
                        actionData.Targets.AddRange(attacks);
                        mb.AddBagItem(itemId, -1, true);
                    }
                }
                else if (mb.ActionData.ActionType == BattleActionType.Catch)
                {
                    // 捕获
                    _members.TryGetValue(mb.ActionData.Target, out var targetMb);
                    if (targetMb != null)
                    {
                        var targetAttackData = new BattleAttackData
                        {
                            OnlyId = targetMb.OnlyId,
                            Response = BattleResponseType.NoCatch
                        };

                        // 下面这段用while只是为了方便控制Response, 请仔细理解
                        while (true)
                        {
                            if (!targetMb.IsBb)
                            {
                                targetAttackData.Response = BattleResponseType.NoCatch;
                                break;
                            }

                            // 死了的不能抓
                            if (targetMb.Dead)
                            {
                                targetAttackData.Response = BattleResponseType.CatchFail;
                                mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice {Text = "抓捕失败，死的不能抓"});
                                break;
                            }

                            // 神兽降临 抓捕
                            if (_battleType == BattleType.ShenShouJiangLin)
                            {
                                if (_round > 2)
                                {
                                    targetAttackData.Response = BattleResponseType.CatchFail;
                                    mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "抓捕失败，神兽只能在前2轮可以抓捕" });
                                    break;
                                }
                                // 95%的概率会捕获失败
                                var r = _random.Next(0, 100) + 1;
                                if (r <= 95)
                                {
                                    targetAttackData.Response = BattleResponseType.CatchFail;
                                    mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "抓捕失败，抓了个寂寞" });
                                    // LogDebug($"玩家[{mb.Data.Id}][{mb.Data.Name}]神兽降临 概率没有命中");
                                    break;
                                }
                                var serverId = _startRequest.ServerId;
                                var okay = await RedisService.LockShenShouJiangLinReward(serverId);
                                if (okay)
                                {
                                    var shenShouId = await RedisService.GetShenShouJiangLinReward(serverId);
                                    if (shenShouId > 0)
                                    {
                                        // 记录赢家ID
                                        okay = await RedisService.SetShenShouJiangLinReward(serverId, -(int)mb.Id);
                                        okay = await RedisService.UnLockShenShouJiangLinReward(serverId);
                                        if (!okay)
                                        {
                                            LogError($"玩家[{mb.Data.Id}][{mb.Data.Name}]神兽降临[{serverId}]释放锁失败");
                                        }
                                        if (okay)
                                        {
                                            mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "抓捕成功，抓了个真身" });
                                            var ServerGrain = GrainFactory.GetGrain<IServerGrain>(serverId);
                                            var bytes = Packet.Serialize(GameCmd.S2CSsjlResult, new S2C_SsjlResult
                                            {
                                                Msg = $"<color=#00FF00>恭喜</c><color=#ffbf00>{mb.Data.Name}</c>在<color=#ffef00>神兽降临</c><color=#00FF00>活动中成功抓获神兽真身!</c>",
                                            });
                                            _ = ServerGrain.Broadcast(new Immutable<byte[]>(bytes));
                                        }
                                        else
                                        {
                                            targetAttackData.Response = BattleResponseType.CatchFail;
                                            mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "抓捕失败，抓了个寂寞" });
                                            LogError($"玩家[{mb.Data.Id}][{mb.Data.Name}]神兽降临[{serverId}]设置抓取成功失败");
                                            break;
                                        }
                                    }
                                    else
                                    {
                                        okay = await RedisService.UnLockShenShouJiangLinReward(serverId);
                                        if (!okay)
                                        {
                                            LogError($"玩家[{mb.Data.Id}][{mb.Data.Name}]神兽降临[{serverId}]释放锁失败");
                                        }
                                        targetAttackData.Response = BattleResponseType.CatchFail;
                                        mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "抓捕失败，抓了个寂寞" });
                                        // LogDebug($"玩家[{mb.Data.Id}][{mb.Data.Name}]神兽降临[{serverId}]已经被别队抓了");
                                        break;
                                    }
                                }
                                else
                                {
                                    targetAttackData.Response = BattleResponseType.CatchFail;
                                    mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice { Text = "抓捕失败，抓了个寂寞" });
                                    // LogDebug($"玩家[{mb.Data.Id}][{mb.Data.Name}]神兽降临[{serverId}]获得锁失败");
                                    break;
                                }
                            }
                            else
                            {
                            // 40%的概率会捕获失败
                            var r = _random.Next(0, 100);
                            if (r < 40)
                            {
                                targetAttackData.Response = BattleResponseType.CatchFail;
                                break;
                            }
                            }

                            // 捕获到了宠物, 通知玩家获得宠物
                            ConfigService.Monsters.TryGetValue(targetMb.Data.CfgId, out var cfg);
                            if (cfg is {Pet: > 0})
                            {
                                targetAttackData.Response = BattleResponseType.Catched;
                                mb.CreatePet(cfg.Pet);
                            }

                            break;
                        }

                        targetAttackData.Hp = targetMb.Hp;
                        targetAttackData.Mp = targetMb.Mp;
                        targetAttackData.Dead = targetMb.Dead;
                        targetAttackData.Buffs.AddRange(targetMb.GetBuffsSkillId());
                        actionData.Targets.Add(targetAttackData);
                        if (targetAttackData.Response == BattleResponseType.Catched)
                        {
                            // 被捕获了，需要从战场移除
                            RemoveMember(mb.ActionData.Target);
                        }
                    }
                }
                else if (mb.ActionData.ActionType == BattleActionType.Protect)
                {
                    // 破除隐身的Buff
                    var buff = mb.GetBuffByMagicType(SkillType.YinShen);
                    if (buff != null) mb.RemoveBuff(buff.Id);
                }
                else if (mb.ActionData.ActionType == BattleActionType.Summon)
                {
                    if (mb.HasBuff(SkillType.Forget) || mb.HasBuff(SkillType.Chaos) ||
                        mb.HasBuff(SkillType.Seal)) continue;

                    // 召唤
                    var (oldOnlyId, newOnlyId) = Summon(mb.OnlyId, mb.ActionData.ActionId);
                    // LogInfo($"玩家[{mb.Data.Id}][{mb.Data.Name}]召唤 旧{oldOnlyId} 新{newOnlyId}");

                    // 先召回旧Pet, 如果之前没有，那么oldOnlyId为0
                    actionData.Targets.Add(new BattleAttackData
                    {
                        OnlyId = oldOnlyId,
                        Response = BattleResponseType.SummonBack
                    });

                    // 召唤新Pet
                    var targetAttackData2 = new BattleAttackData();
                    _members.TryGetValue(newOnlyId, out var pet);
                    if (pet == null)
                    {
                        targetAttackData2.Response = BattleResponseType.SummonFail;
                        actionData.Targets.Add(targetAttackData2);
                    }
                    else
                    {
                        targetAttackData2.Response = BattleResponseType.Summon;
                        targetAttackData2.OnlyId = pet.OnlyId;
                        targetAttackData2.Value = pet.Pos;
                        targetAttackData2.Hp = pet.Hp;
                        targetAttackData2.Mp = pet.Mp;
                        targetAttackData2.Dead = pet.Dead;
                        targetAttackData2.Buffs.AddRange(pet.GetBuffsSkillId());

                        var needInsertTurn = false;
                        var enterEffect = LoadPetEnterEffect(pet);
                        if (enterEffect != null)
                        {
                            targetAttackData2.After = new BattleAttackAfter {PetEnter = enterEffect};
                            foreach (var buffItem in enterEffect.Buffs)
                            {
                                if (buffItem.SkillId == SkillId.HenYuFeiFei)
                                {
                                    needInsertTurn = true;

                                    // 立即触发一次法术攻击
                                    pet.ResetRoundData();
                                    pet.ActionData.ActionType = BattleActionType.Skill;
                                    pet.ActionData.Skill = SkillId.HenYuFeiFei;
                                    pet.ActionData.ActionId = (uint) SkillId.HenYuFeiFei;
                                    pet.IsAction = true;
                                }

                                if (buffItem.SkillId == SkillId.JiQiBuYi)
                                {
                                    needInsertTurn = true;
                                    // 击其不意
                                    pet.ActionData.ActionType = BattleActionType.Skill;
                                    pet.ActionData.Skill = SkillId.NormalAtk;
                                    pet.ActionData.ActionId = (uint) SkillId.NormalAtk;
                                }

                                if (buffItem.SkillId == SkillId.DangTouBangHe)
                                {
                                    // 当头棒喝
                                    foreach (var xmb in FindAllTeamMembers(pet.OnlyId))
                                    {
                                        xmb.RemoveDeBuff();
                                    }
                                }
                                _members.TryGetValue(pet.OwnerOnlyId, out var owner);
                                if (owner != null && buffItem.SkillId == SkillId.XianFengDaoGu)
                                {
                                    // 仙风道骨
                                    var addHp = owner.HpMax * 0.7f;
                                    var addMp = owner.MpMax * 0.1f;
                                    if (owner.Dead) {
                                        owner.Attrs.Set(AttrType.Hp, addHp);
                                        owner.Attrs.Set(AttrType.Mp, addMp);
                                        owner.RemoveAllBuff();
                                        owner.Dead = false;
                                        var after1 = new BattleAttackAfter();
                                        after1.NiePan = new BattleNiePanData
                                        {
                                            Hp = owner.Hp,
                                            Mp = owner.Mp,
                                        };
                                        var attackData1 = new BattleAttackData { OnlyId = owner.OnlyId, After = after1 };
                                        actionData.Targets.Add(attackData1);
                                        addTime += 2;
                                    } else {
                                        owner.AddHp(addHp);
                                        owner.AddMp(addMp);
                                    }
                                    // LogInfo("仙风道骨技能生效1");
                                }                             
                            }
                        }

                        actionData.Targets.Add(targetAttackData2);
                        // replaces[oldOnlyId] = pet.OnlyId;

                        if (needInsertTurn)
                        {
                            // 插入turns可以出手攻击
                            var turnItem = new TurnItem
                            {
                                OnlyId = pet.OnlyId,
                                Spd = pet.Spd
                            };
                            if (turnIndex + 1 >= _turns.Count)
                                _turns.Add(turnItem);
                            else
                                _turns.Insert(turnIndex + 1, turnItem);
                        }
                    }
                }
                else if (mb.ActionData.ActionType == BattleActionType.SummonBack)
                {
                    if (mb.HasBuff(SkillType.Forget) || mb.HasBuff(SkillType.Chaos) ||
                        mb.HasBuff(SkillType.Seal)) continue;
                    // 召还
                    var petOnlyId = OnPetLeave(mb.PetOnlyId);
                    if (petOnlyId > 0)
                    {
                        actionData.Targets.Add(new BattleAttackData
                        {
                            OnlyId = petOnlyId,
                            Response = BattleResponseType.SummonBack
                        });
                    }
                }
                else if (mb.ActionData.ActionType == BattleActionType.Skill)
                {
                    // 确认技能
                    var skillId = (SkillId) mb.ActionData.ActionId;
                    if (SkillManager.GetSkill(skillId) == null)
                    {
                        skillId = mb.GetAiSkill();
                        mb.ActionData.ActionId = (uint) skillId;
                    }

                    if (_battleType == BattleType.TianJiangLingHou && mb.IsMonster)
                    {
                        skillId = SkillId.StealMoney;
                    }

                    // 觉醒技 强化泽披八方
                    // 施放天生技能“泽披八方”前有10%/20%/35%/50%概率摆脱一层封混睡忘状态，第6回合起触发概率降低至原有值的10%。
                    if (skillId == SkillId.ZeBeiWanWu && mb.CanUseJxSkill(SkillId.QiangHuaZePiBaFang))
                    {
                        var baseValues = new List<float>() { 100, 200, 350, 500 };
                        var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                        var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                        if (_round >= 6)
                        {
                            calcValue *= 0.1f;
                        }
                        if (_random.Next(1000) < calcValue)
                        {
                            mb.RemoveKongZhiBuff();
                        }
                    }
                    // 觉醒技 强化泽披天下
                    // 施放天生技能“泽披天下”有10%/20%/35%/50%概率不消耗法力值，
                    // 且施放“泽披天下”前有10%/20%/35%/50%概率摆脱一层封混睡忘状态，第6回合起触发概率降低至原有值的10%。
                    if (skillId == SkillId.ZePiTianXia && mb.CanUseJxSkill(SkillId.QiangHuaZePiTianXia))
                    {
                        var baseValues = new List<float>() { 100, 200, 350, 500 };
                        var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                        var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                        if (_round >= 6)
                        {
                            calcValue *= 0.1f;
                        }
                        if (_random.Next(1000) < calcValue)
                        {
                            mb.RemoveKongZhiBuff();
                        }
                    }
                    // 如果中混乱 攻击改为普通攻击
                    if (mb.HasBuff(SkillType.Chaos)) skillId = SkillId.NormalAtk;
                    // 获取技能统计信息
                    mb.Skills.TryGetValue(skillId, out var info);
                    if (skillId != SkillId.NormalAtk && skillId != SkillId.NormalDef)
                    {
                        if (info == null || info.CoolDown > 0)
                        {
                            mb.SendPacket(GameCmd.S2CNotice,
                                new S2C_Notice {Text = $"{SkillManager.GetSkill(skillId).Name}尚未冷却"});
                            skillId = SkillId.NormalAtk;
                        }
                        else if (_isPvp)
                        {
                            // 检查敌方单位是否存在双管齐下
                            var hasShuangGuanQiXia = CheckEnemyHasSkill(mb.OnlyId, SkillId.ShuangGuanQiXia);
                            if (hasShuangGuanQiXia)
                            {
                                if (mb.Hp > 20000)
                                {
                                    mb.AddHp(-20000);
                                }
                                else
                                {
                                    skillId = SkillId.NormalAtk;
                                }
                            }
                        }
                        // 被种了，荼蘼花开
                        var tub = mb.GetBuffTuMiHuaKai();
                        if (tub != null)
                        {
                            mb.AddHp(-tub.EffectData["BaoFaHurt"]);
                            // 觉醒技 花开二度 掉蓝
                            mb.AddMp(-tub.EffectData["BaoFaMHurt"]);
                            mb.RemoveBuffTuMiHuaKai();
                            // LogDebug($"玩家[{mb.Data.Id}][{mb.Data.Name}]爆发荼蘼花开");
                        }
                    }

                    // 获取技能模板信息
                    var skill = SkillManager.GetSkill(skillId);
                    if (skill == null || skillId == SkillId.NormalDef)
                    {
                        continue;
                    }
                    // 荼蘼花开
                    var tuMiHuaKai = skillId == SkillId.NormalAtk ? mb.GetTuMiHuaKai() : null;
                    if (tuMiHuaKai != null)
                    {
                        skillId = SkillId.TuMiHuaKai;
                        mb.ActionData.ActionId = (uint)skillId;
                        skill = SkillManager.GetSkill(skillId);
                        // LogDebug($"玩家[{mb.Data.Id}][{mb.Data.Name}]触发荼蘼花开");
                    }
                    // 幻影离魂
                    var huanYingLiHun = skillId == SkillId.NormalAtk ? mb.GetHuanYingLiHun() : null;
                    if (huanYingLiHun != null)
                    {
                        skillId = SkillId.HuanYingLiHun;
                        mb.ActionData.ActionId = (uint)skillId;
                        skill = SkillManager.GetSkill(skillId);
                        // LogDebug($"玩家[{mb.Data.Id}][{mb.Data.Name}]触发幻影离魂");
                    }
                    // 技能回合限制
                    if (skill.LimitRound > 0 && _round < skill.LimitRound)
                    {
                        mb.SendPacket(GameCmd.S2CNotice,
                            new S2C_Notice {Text = $"{SkillManager.GetSkill(skillId).Name}前{skill.LimitRound}回合不可用"});

                        skillId = SkillId.NormalAtk;
                        skill = SkillManager.GetSkill(skillId);
                        mb.ActionData.ActionId = (uint) skillId;
                    }

                    // 次数限制
                    if (skill.LimitTimes > 0)
                    {
                        if (mb.UsedSkills.GetValueOrDefault(skillId, 0U) >= skill.LimitTimes)
                        {
                            mb.SendPacket(GameCmd.S2CNotice,
                                new S2C_Notice {Text = $"{SkillManager.GetSkill(skillId).Name}超过使用次数"});

                            skillId = SkillId.NormalAtk;
                            skill = SkillManager.GetSkill(skillId);
                            mb.ActionData.ActionId = (uint) skillId;
                        }
                        else
                        {
                            mb.AddLimitSkill(skillId);
                        }
                    }

                    var yinShen = mb.GetBuffByMagicType(SkillType.YinShen);
                    if (yinShen != null) mb.RemoveBuff(yinShen.Id);

                    // 是否可以使用 
                    // 怪物 伙伴 忽略蓝耗
                    // 兵临城下 必须计算蓝耗
                    // 普通战斗不计算蓝耗
                    while (true)
                    {
                        if (mb.IsMonster || mb.IsPartner) break;
                        // 孩子技能--精气爆满--施法前魔法
                        var oldMp = mb.Mp;
                        var canUse = skill.UseSkill(mb, out var error);
                        if (!canUse)
                        {
                            if (string.IsNullOrWhiteSpace(error)) error = "法力不足，无法释放";
                            mb.SendPacket(GameCmd.S2CNotice, new S2C_Notice {Text = error});

                            // 换成普通攻击
                            skillId = SkillId.NormalAtk;
                            skill = SkillManager.GetSkill(skillId);
                            mb.ActionData.ActionId = (uint) skillId;
                        }
                        else
                        {
                            // 孩子技能--精气爆满--施法后魔法减少，有几率补回去
                            var deltaMp = oldMp - mb.Mp;
                            if (deltaMp > 0)
                            {
                                var skillNameJQBM = GameDefine.ChildSkillId2Names[SkillId.JingQiBaoMan];
                                if (mb.ChildSkillTargetNum(SkillId.JingQiBaoMan) > 0)
                                {
                                    mb.AddMp(deltaMp);
                                    var src = _random.Next(56).ToString().PadLeft(3, '0').PadRight(7, '0');
                                    actionData.ChildTalks.Add(new BattleChildTalk()
                                    {
                                        Text = $"看我的<color=#008f00>{skillNameJQBM}</c><img src=\"{src}\"/>",
                                        AniName = GameDefine.ChildAniNameList[_random.Next(GameDefine.ChildAniNameList.Count)],
                                    });
                                    // LogInfo($"玩家[{mb.Data.Id}][{mb.Data.Name}]触发[{skillNameJQBM}]，魔法加[{deltaMp}]");
                                }
                                // else
                                // {
                                //     LogInfo($"玩家[{mb.Data.Id}][{mb.Data.Name}]触发[{skillNameJQBM}]，没有命中");
                                // }

                                // 觉醒技 强化泽披天下
                                // 施放天生技能“泽披天下”有10%/20%/35%/50%概率不消耗法力值，
                                // 且施放“泽披天下”前有10%/20%/35%/50%概率摆脱一层封混睡忘状态，第6回合起触发概率降低至原有值的10%。
                                if (skillId == SkillId.ZePiTianXia && mb.CanUseJxSkill(SkillId.QiangHuaZePiTianXia))
                                {
                                    var baseValues = new List<float>() { 100, 200, 350, 500 };
                                    var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                                    var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                                    if (_round >= 6)
                                    {
                                        calcValue *= 0.1f;
                                    }
                                    if (_random.Next(1000) < calcValue)
                                    {
                                        mb.AddMp(deltaMp);
                                    }
                                }
                            }
                        }

                        break;
                    }

                    actionData.ActionId = (uint) skillId;
                    // 不是不同攻击
                    if (skillId != SkillId.NormalAtk && skillId != SkillId.NormalDef)
                    {
                        if (mb.Data.Weapon != null)
                        {
                            // 有6阶以上的武器
                            var wconfig = ConfigService.Equips.GetValueOrDefault(mb.Data.Weapon.CfgId, null);
                            if (wconfig != null && wconfig.Grade > 5)
                            {
                                // 随机颜色
                                actionData.SkillColor = -500 + _random.Next(1000);
                            }
                        }
                    }

                    // 春回大地
                    if (skillId == SkillId.ChunHuiDaDi)
                    {
                        var members = FindAllTeamMembers(mb.OnlyId, true);
                        foreach (var xmb in members)
                        {
                            // 魅影缠身
                            if (_isPvp) xmb.RemoveDeBuff(_round, _members);
                            else xmb.RemoveDeBuff();
                        }
                    }

                    // 落日融金 血海深仇 技能计算
                    uint deadNum = 0;
                    if (skillId == SkillId.LuoRiRongJin || skillId == SkillId.XueHaiShenChou)
                    {
                        var list = mb.CampId == 1 ? _camp1.Members : _camp2.Members;
                        deadNum = (uint) list.Count(p => (p.IsPlayer || p.IsPartner) && p.Dead);
                    }

                    // 龙族
                    // 技能效果
                    var gedr = new GetEffectDataRequest
                    {
                        Level = mb.Data.Level,
                        Relive = mb.Relive,
                        Intimacy = mb.Data.PetIntimacy,
                        Profic = mb.GetSkillProfic(skillId),
                        Atk = mb.Atk,
                        Deadnum = deadNum,
                        MaxMp = mb.MpMax,
                        Attrs = mb.Attrs,
                        OrnamentSkills = mb.OrnamentSkills,
                        BattleType = _battleType,
                        Member = mb
                    };
                    var effectData = tuMiHuaKai == null ? skill.GetEffectData(gedr) : tuMiHuaKai;
                    // 金翅大鹏
                    if (skill.Id == SkillId.FengJuanCanYunMain)
                    {
                        addTime += 1.3f;
                    }
                    else if (skill.Id == SkillId.FengJuanCanYunLeft)
                    {
                        addTime += 0.8f;
                    }
                    else if (skill.Id == SkillId.FengJuanCanYunRight)
                    {
                        addTime += 0.8f;
                    }
                    else if (skill.Id == SkillId.ZhuTianMieDi)
                    {
                        addTime += 1.8f;
                    }
                    // 幻影离魂
                    if (huanYingLiHun != null)
                    {
                        effectData = huanYingLiHun;
                    }
                    // 龙族 扫击技能
                    if (_isPvp && (skillId == SkillId.FengLeiWanYun || skillId == SkillId.ZhenTianDongDi))
                    {
                        // 劈风斩浪-无价
                        if (mb.OrnamentSkills.ContainsKey(140002))
                        {
                            effectData.Round = 2;
                            effectData.HpMaxPercent = -10;
                            effectData.HpMaxPercent -= (int)MathF.Floor(mb.Attrs.Get(AttrType.LiLiang) / 100.0f) * 1;
                            effectData.HpMaxPercent = Math.Max(-30, effectData.HpMaxPercent);
                        }
                        // 劈风斩浪-珍藏
                        else if (mb.OrnamentSkills.ContainsKey(140001))
                        {
                            effectData.Round = 2;
                            effectData.HpMaxPercent = -10;
                        }
                    }
                    // 龙族 施法效果--对自己（一定是对自己的效果）
                    var effectData2Self = skill.GetEffectData2Self(gedr);
                    if (effectData2Self != null)
                    {
                        mb.AddBuff(new Buff(buffid2self = NextBuffId(), skill, effectData2Self));
                    }
                    // 孩子技能--除了精气爆满和返生香外的其他技能
                    var childTargetNum = mb.ChildSkillTargetNum(skill.Id);
                    var skillName = GameDefine.ChildSkillId2Names.GetValueOrDefault(skill.Id, $"技能ID[{skill.Id}]");
                    if (childTargetNum > 0)
                    {
                        effectData.TargetNum += childTargetNum;
                        var src = _random.Next(56).ToString().PadLeft(3, '0').PadRight(7, '0');
                        actionData.ChildTalks.Add(new BattleChildTalk()
                        {
                            Text = $"看我的<color=#008f00>{skillName}</c><img src=\"{src}\"/>",
                            AniName = GameDefine.ChildAniNameList[_random.Next(GameDefine.ChildAniNameList.Count)],
                        });
                        // LogInfo($"玩家[{mb.Data.Id}][{mb.Data.Name}]触发[{skillName}]，加[{childTargetNum}]个单元");
                    }
                    // else
                    // {
                    //     LogInfo($"玩家[{mb.Data.Id}][{mb.Data.Name}]触发[{skillName}]，没有命中");
                    // }

                    // 技能冷却
                    if (skill.Cooldown > 0)
                    {
                        var stat = mb.Skills[skillId];
                        if (stat != null) stat.CoolDown = skill.Cooldown;
                    }

                    // 卧雪 眠霜卧雪 珍藏、无价
                    if (skill.Type is SkillType.Seal or SkillType.Sleep)
                    {
                        if (mb.OrnamentSkills.ContainsKey(1002) || mb.OrnamentSkills.ContainsKey(1001))
                        {
                            if (_random.Next(0, 100) < 60)
                            {
                                var add = 3.0f;
                                if (mb.OrnamentSkills.ContainsKey(1002))
                                {
                                    // 每500点根骨提升1%
                                    add += MathF.Floor(mb.Attrs.Get(AttrType.GenGu) / 500.0f);
                                }

                                mb.Attrs.Add(AttrType.HdfengYin, add);
                                mb.Attrs.Add(AttrType.HdhunShui, add);
                            }
                        }
                    }
                    // 觉醒技 凤鸣余音
                    // 施放高级五行克技能后，增加主人与所施放技能对应的那一项强力克属性12%/24%/42%/50%，持续2回合。
                    if (FengMingYuYinValidSkills.Contains(skillId) && mb.CanUseJxSkill(SkillId.FengMingYuYin))
                    {
                        _members.TryGetValue(mb.OwnerOnlyId, out var owner);
                        if (owner != null)
                        {
                            AttrType type = AttrType.Unkown;
                            if (effectData.AttrType == AttrType.Jin)
                            {
                                type = AttrType.Qjin;
                            }
                            else if (effectData.AttrType == AttrType.Mu)
                            {
                                type = AttrType.Qmu;
                            }
                            else if (effectData.AttrType == AttrType.Shui)
                            {
                                type = AttrType.Qshui;
                            }
                            else if (effectData.AttrType == AttrType.Huo)
                            {
                                type = AttrType.Qhuo;
                            }
                            else if (effectData.AttrType == AttrType.Tu)
                            {
                                type = AttrType.Qtu;
                            }
                            if (type != AttrType.Unkown && owner.Attrs.Get(type) != 0)
                            {
                                var baseValues = new List<float>() { 120, 240, 420, 500 };
                                var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                                var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                                var buffskill = SkillManager.GetSkill(skillId);
                                var buffeffect = new SkillEffectData()
                                {
                                    Round = 2,
                                    AttrType = type,
                                    AttrValue = owner.Attrs.Get(type) * (1 + (calcValue / 1000.0f))
                                };
                                var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = mb.OnlyId };
                                owner.AddBuff(buff);
                            }
                        }
                    }
                    // 觉醒技 同仇敌忾
                    // 物理攻击时有6%/12%/21%/30%概率召唤友方所有在场召唤兽一起作战，友方其它召唤兽攻击力的一定倍数临时加成至自身。
                    if (mb.CanUseJxSkill(SkillId.TongChouDiKai) && (skillId is SkillId.NormalAtk or SkillId.BingLinChengXia))
                    {
                        var baseValues = new List<float>() { 60, 120, 210, 300 };
                        var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                        var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                        if (_random.Next(1000) < calcValue)
                        {
                            var totalAtk = 0f;
                            foreach (var (id, bm) in _members)
                            {
                                if (bm.CampId == mb.CampId && !bm.Dead && mb.IsPet && bm.OnlyId != mb.OnlyId)
                                {
                                    totalAtk += bm.Attrs.Get(AttrType.Atk);
                                }
                            }
                            effectData.Hurt += 1.2f * totalAtk;
                        }
                    }

                    var targetNum = effectData.TargetNum;
                    // if (mb.HasSkill(SkillId.WanQianHuaShen) && skillId is SkillId.NormalAtk) {
                    //     var skill_wqhs = SkillManager.GetSkill(SkillId.WanQianHuaShen);
                    //     if (skill_wqhs != null) {
                    //         targetNum = skill_wqhs.GetEffectData(gedr).TargetNum;
                    //         // LogInfo($"万千化身技能 目标个数{targetNum}"); 
                    //     }
                    // }
                    var targetList = new List<uint>();
                    // 确定主目标
                    if (!mb.HasBuff(SkillType.Chaos))
                    {
                        _members.TryGetValue(mb.ActionData.Target, out var targetMb);
                        if (targetMb is {Dead: false})
                        {
                            if (targetMb.HasBuff(SkillType.YinShen))
                            {
                                // 霄汉 干霄凌云 珍藏/无价
                                var ret = 0;
                                if (mb.OrnamentSkills.ContainsKey(2042)) ret = 100;
                                else if (mb.OrnamentSkills.ContainsKey(2041)) ret = 50;
                                if (ret > 0 &&
                                    (SkillManager.IsXianFa(skill.Type) || skill.Type == SkillType.GhostFire) &&
                                    _random.Next(0, 100) < ret)
                                {
                                    targetList.Add(mb.ActionData.Target);
                                }
                            }
                            else
                            {
                                targetList.Add(mb.ActionData.Target);
                            }
                        }
                    }

                    // 如果中混乱  改为 全体目标
                    if (mb.HasBuff(SkillType.Chaos))
                    {
                        // 判断混乱后 天罡战气 技能
                        if (mb.HasPassiveSkill(SkillId.TianGangZhanQi))
                        {
                            FindRandomTarget(mb.OnlyId, targetNum, targetList, 1, skill);
                        }
                        else
                        {
                            FindRandomTarget(mb.OnlyId, targetNum, targetList, 3, skill);
                        }
                    }
                    else
                    {
                        // 如果是子虚乌有 就直接放入 目标和自己
                        if (skillId == SkillId.ZiXuWuYou)
                        {
                            targetList.Add(mb.OnlyId);
                        }
                        else
                        {
                            if (SkillManager.IsSelfBuffSkill(skillId))
                            {
                                FindRandomTarget(mb.OnlyId, targetNum, targetList, 2, skill);
                            }
                            else
                            {
                                if (skillId is SkillId.NormalAtk or SkillId.BingLinChengXia)
                                {
                                    if (mb.FenLie())
                                    {
                                        targetNum++;
                                    }

                                    // 幻影-无价, 40%的概率增加1个分裂目标
                                    if (mb.OrnamentSkills.ContainsKey(9002) && _random.Next(0, 100) < 40)
                                    {
                                        targetNum++;
                                    }
                                }

                                FindRandomTarget(mb.OnlyId, targetNum, targetList, 1, skill);
                            }

                            // 利涉大川, 沛雨甘霖
                            if (mb.HasSkill(SkillId.LiSheDaChuan) || mb.HasSkill(SkillId.PeiYuGanLin))
                            {
                                var more = mb.HasSkill(SkillId.LiSheDaChuan) ? 2 : 1;
                                switch (skill.Kind)
                                {
                                    case SkillId.TianMoJieTi:
                                    case SkillId.FenGuangHuaYing:
                                    case SkillId.QingMianLiaoYa:
                                    case SkillId.XiaoLouYeKu:
                                    {
                                        // 选出敌方速度最低的2位目标
                                        var listEnemy = mb.CampId == 1 ? _camp2.Members : _camp1.Members;
                                        listEnemy = listEnemy.Where(p => !p.Dead && p.Pos > 0).OrderBy(p => p.Spd)
                                            .ToList();
                                        var added = 0;
                                        foreach (var enemy in listEnemy)
                                        {
                                            if (added >= more) break;
                                            if (targetList.Contains(enemy.OnlyId)) continue;
                                            targetList.Add(enemy.OnlyId);
                                            added++;
                                        }
                                    }
                                        break;
                                }
                            }
                        }
                    }

                    if (targetList.Count == 0)
                    {
                        continue;
                    }

                    // 魔王窟-红孩儿5-吸星大法
                    if (skillId == SkillId.XiXingDaFa && mb.IsMonster)
                    {
                        effectData.Hurt = mb.Data.CfgId switch
                        {
                            8105 => _random.Next(15000, 20000),
                            8115 => _random.Next(15000, 20000),
                            8125 => _random.Next(30000, 35000),
                            _ => effectData.Hurt
                        };
                    }

                    // 计算悬刃 遗患 等 出手前技能
                    if (skillId != SkillId.NormalAtk && skillId != SkillId.BingLinChengXia &&
                        skillId != SkillId.StealMoney)
                    {
                        var camp = mb.CampId == 1 ? _camp2 : _camp1;

                        // 化无
                        var huawu = HasStageEffect(camp, SkillId.HuaWu);
                        if (huawu > 0)
                        {
                            SetStageEffect(camp, SkillId.HuaWu, 0);
                            before.HuaWu = true;
                            before.Hp = mb.Hp;
                            before.Mp = mb.Mp;
                            before.Dead = mb.Dead;
                            actionData.Before = before;
                            actionData.Targets.Clear();
                            resp.List.Add(actionData);
                            continue;
                        }

                        // 悬刃
                        var xuanrenHurt = HasStageEffect(camp, SkillId.XuanRen);
                        if (xuanrenHurt > 0)
                        {
                            SetStageEffect(camp, SkillId.XuanRen, 0);
                            mb.AddHp(-xuanrenHurt);
                            before.XuanRen = xuanrenHurt;
                        }

                        // 遗患
                        var yihuanHurt = HasStageEffect(camp, SkillId.YiHuan);
                        if (yihuanHurt > 0)
                        {
                            SetStageEffect(camp, SkillId.YiHuan, 0);
                            mb.AddMp(-yihuanHurt);
                            before.YiHuan = yihuanHurt;
                        }

                        if (mb.Dead)
                        {
                            before.Hp = mb.Hp;
                            before.Mp = mb.Mp;
                            before.Dead = mb.Dead;
                            actionData.Before = before;
                            actionData.Targets.Clear();
                            resp.List.Add(actionData);
                            continue;
                        }
                    }

                    // 吸血池
                    var hurtPool = new List<float>();
                    var mpPool = new List<float>();
                    var tgs = new List<BattleAttackData>();

                    var bmingzhong = mb.Attrs.Get(AttrType.PmingZhong) + 80;
                    var fenhuatimes = 0;
                    var fenhuas = new Dictionary<uint, bool>();
                    SkillEffectData tianJiangTuoTu = null;
                    // SkillEffectData huanYingLiHun = null;

                    for (var trindex = 0; trindex < targetList.Count; trindex++)
                    {
                        var troleid = targetList[trindex];
                        _members.TryGetValue(troleid, out var trole);
                        if (trole == null) continue;
                        // 破隐
                        if (skill.Id == SkillId.PoYin)
                        {
                            var buff = trole.GetBuffByMagicType(SkillType.YinShen);
                            if (buff != null) trole.RemoveBuff(buff.Id);
                        }
                        // 套装技能 斩草除根-珍藏/无价 不叠加
                        if (mb.OrnamentSkills.ContainsKey(2011) || mb.OrnamentSkills.ContainsKey(2012))
                        {
                            if (effectData.Hurt > 0 &&
                                (SkillManager.IsXianFa(skill.Type) || skill.Type == SkillType.GhostFire))
                            {
                                if (trole.HasBuff(SkillType.Sleep) || trole.HasBuff(SkillType.Forget))
                                {
                                    var addPercent = mb.OrnamentSkills.ContainsKey(2012) ? 1.6f : 0.8f;
                                    effectData.Hurt = MathF.Floor(effectData.Hurt * (1 + addPercent));
                                }
                            }
                        }

                        var attackData = new BattleAttackData {OnlyId = troleid};
                        // 幻影离魂
                        if (huanYingLiHun != null)
                        {
                            attackData.HuanYingLiHun = true;
                            // LogDebug($"玩家[{mb.Data.Id}][{mb.Data.Name}]前端幻影离魂");
                        }
                        tgs.Add(attackData);
                        var after = new BattleAttackAfter(); // 被攻击者后续
                        attackData.After = after;

                        // 显示分花
                        if (fenhuas.ContainsKey(trole.OnlyId))
                            attackData.FenHuaFuLiu = true;

                        // 封印状态
                        if (trole.HasBuff(SkillType.Seal))
                        {
                            attackData.Hp = trole.Hp;
                            attackData.Mp = trole.Mp;
                            attackData.Dead = trole.Dead;
                            if (skill.Type == SkillType.Seal && effectData.Round > 1)
                            {
                                trole.CheckReplaceBuffRound(skillId, (uint) effectData.Round);
                            }

                            attackData.Buffs.Add(trole.GetBuffsSkillId());
                            continue;
                        }

                        // 移花接木, 交换控制类Buff
                        if (SkillId.YiHuaJieMu == skillId)
                        {
                            var mBuffs = new List<Buff>();
                            foreach (var buff in mb.Buffs)
                            {
                                if (SkillManager.IsDebuffSkill(buff.SkillId))
                                {
                                    mBuffs.Add(buff);
                                }
                            }

                            foreach (var buff in mBuffs)
                            {
                                mb.RemoveBuff(buff.Id);
                            }

                            var tBuffs = new List<Buff>();
                            foreach (var buff in trole.Buffs)
                            {
                                if (SkillManager.IsDebuffSkill(buff.SkillId))
                                {
                                    tBuffs.Add(buff);
                                }
                            }

                            foreach (var buff in tBuffs)
                            {
                                trole.RemoveBuff(buff.Id);
                            }

                            foreach (var buff in mBuffs)
                            {
                                trole.AddBuff(buff);
                            }

                            foreach (var buff in tBuffs)
                            {
                                mb.AddBuff(buff);
                            }

                            attackData.Hp = trole.Hp;
                            attackData.Mp = trole.Mp;
                            attackData.Dead = trole.Dead;
                            attackData.Buffs.Add(trole.GetBuffsSkillId());

                            tgs.Add(new BattleAttackData
                            {
                                OnlyId = mb.OnlyId,
                                Hp = mb.Hp,
                                Mp = mb.Mp,
                                Dead = mb.Dead,
                                Buffs = {mb.GetBuffsSkillId()}
                            });
                            continue;
                        }

                        // 忽视、抗性
                        GameDefine.SkillTypeStrengthen.TryGetValue(skill.Type, out var sattr);
                        GameDefine.SkillTypeKangXing.TryGetValue(skill.Type, out var dattr);

                        var sattrnum = mb.Attrs.Get(sattr);
                        // 天策符 千钧符 气定沧海
                        // 释放冰混睡忘时一定几率增加忽视，血量百分比越低几率越高
                        if (skill.Type == SkillType.Seal
                        || skill.Type == SkillType.Chaos
                        || skill.Type == SkillType.Sleep
                        || skill.Type == SkillType.Forget)
                        {
                            if (mb.IsPlayer && sattrnum > 0)
                            {
                                var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.QiDingCangHai3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.QiDingCangHai2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.QiDingCangHai1);
                                        grade = 1;
                                    }
                                }
                                // 血量百分比越低几率越高
                                if (fskill != null && _random.Next(10000) >= (10000.0 * mb.Hp / mb.HpMax))
                                {
                                    sattrnum *= (1 + (3 * grade + 0.1f * fskill.Addition) * ((float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel) / 100.0f);
                                }
                            }
                        }
                        var dattrnum = trole.Attrs.Get(dattr);
                        // 金翅大鹏 减少抗性
                        if (skill.Type == SkillType.Feng || skill.Type == SkillType.Huo)
                        {
                            // 30%几率减抗性
                            if (mb.IsEagle() && _random.Next(100) < 30)
                            {
                                dattrnum = Math.Max(0, dattrnum - 200);
                            }
                        }
                        var subattrnum = sattrnum - dattrnum;

                        // 判断控制技能闪避
                        if (SkillManager.IsKongZhiSkill(skillId))
                        {
                            var t = (subattrnum + 100) * 100;
                            // 天策符 处理
                            // 千钧符 破釜沉舟
                            // 释放控制法术时一定几率增加强法，己方被控制单位越多几率越高
                            if (mb.IsPlayer)
                            {
                                var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.PoFuChenZhou3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.PoFuChenZhou2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.PoFuChenZhou1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null)
                                {
                                    var BeKongZhiCount = _members.Where(m => m.Value.CampId == mb.CampId && m.Value.HasKongZhiBuff())
                                    .Select(m => m.Key).Count();
                                    if (_random.Next(10000) > (9000 - BeKongZhiCount * 1000))
                                    {
                                        t += (grade * 1000.0f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 50);
                                    }
                                }
                            }
                            var rand = _random.Next(0, 10000);
                            if (t <= rand)
                            {
                                attackData.Response = BattleResponseType.Dodge;
                                attackData.Hp = trole.Hp;
                                attackData.Mp = trole.Mp;
                                attackData.Dead = trole.Dead;
                                attackData.Buffs.AddRange(trole.GetBuffsSkillId());

                                // 无为 以直报怨 珍藏/无价
                                var percent = 0;
                                if (trole.OrnamentSkills.ContainsKey(9042)) percent = 25;
                                else if (trole.OrnamentSkills.ContainsKey(9041)) percent = 15;
                                if (percent > 0 && _random.Next(0, 100) < percent)
                                {
                                    var buffEffect = effectData.Clone();
                                    buffEffect.Round = 2;
                                    var buff = new Buff(NextBuffId(), skill, buffEffect)
                                    {
                                        Source = mb.OnlyId,
                                        Probability = 10000
                                    };
                                    mb.AddBuff(buff);
                                }

                                // 天策符 载物符 金蝉脱壳符
                                // 人物处于被控制时，再次收到相同的控制法术，若未命中，则一定概率立即摆脱控制
                                if (trole.IsPlayer && trole.HasBuffBySkillId(skillId))
                                {
                                    var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.JinChanTuoQiao3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.JinChanTuoQiao2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.JinChanTuoQiao1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null && _random.Next(10000) >= (7500f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                    {
                                        trole.RemoveKongZhiBuff();
                                    }
                                }
                                continue;
                            }
                            // 记录被控制的回合, 套装技能 锋芒毕露-珍藏/无价
                            trole.KongZhiRound = _round;
                        }

                        // 判断闪避命中
                        if (SkillManager.CanShanBi(skillId))
                        {
                            var shanbi = trole.Attrs.Get(AttrType.PshanBi);
                            var rand = _random.Next(0, 10000);
                            if (rand > (bmingzhong - shanbi) * 100)
                            {
                                attackData.Response = BattleResponseType.Dodge;
                                attackData.Hp = trole.Hp;
                                attackData.Mp = trole.Mp;
                                attackData.Dead = trole.Dead;
                                attackData.Buffs.AddRange(trole.GetBuffsSkillId());
                                // Console.WriteLine($"闪避成功 命中【{Convert.ToUInt32(bmingzhong*100)}】闪避【{Convert.ToUInt32(shanbi*100)}】概率【{rand}】");
                                continue;
                            }
                                // Console.WriteLine($"闪避失败 命中【{Convert.ToUInt32(bmingzhong*100)}】闪避【{Convert.ToUInt32(shanbi*100)}】概率【{rand}】");
                        }

                        var respone = BattleResponseType.None;

                        // 开始 天策符--------------------------------------------------------------------------------
                        // 角色
                        if (mb.IsPlayer)
                        {
                            var rand = _random.Next(10000);
                            // 千钧符 飞花溅玉符
                            // 普攻有一定几率3倍暴击，一定几率隔山
                            if (skillId == SkillId.NormalAtk)
                            {
                                var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.FeiHuaJianYv3, null);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.FeiHuaJianYv2, null);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.FeiHuaJianYv1, null);
                                        grade = 1;
                                    }
                                }
                                // 概率 3倍暴击
                                if (fskill != null && rand >= (7500.0f - (grade * 500.0f + fskill.Addition * 50.0f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                {
                                    respone = BattleResponseType.Crits;
                                    var tmpHurt = effectData.Hurt;
                                    effectData.Hurt *= 3;
                                    // 概率 隔山 100%隔山
                                    var tmpRole = FindRandomTeamTarget(trole.OnlyId);
                                    if (tmpRole != null)
                                    {
                                        if (tmpRole.Hp >= 1000 && tmpRole.Skills.ContainsKey(SkillId.PiCaoRouHou))
                                            tmpHurt = (int)MathF.Floor(tmpHurt * 0.5f);
                                        if (tmpHurt > tmpRole.Hp) tmpHurt = (int)tmpRole.Hp;
                                        tmpRole.AddHp(-tmpHurt);
                                        after.GeShan = new BattleGeShanData
                                        {
                                            RoleId = tmpRole.OnlyId,
                                            Response = respone,
                                            Value = -(int)tmpHurt,
                                            Hp = tmpRole.Hp,
                                            Mp = tmpRole.Mp,
                                            Dead = tmpRole.Dead,
                                        };
                                    }
                                }
                            }
                            // 千钧符 陌上开花符
                            // 每次释放师门法术有几率对敌方进行一次普通攻击，目标随机
                            if (mb.double_fashu_hited_round == 0 && skill.TargetType == SkillTargetType.Enemy && skillId != SkillId.NormalAtk && skillId != SkillId.NormalDef)
                            {
                                var grade = 3;
                                BattleTianceSkill fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.MoShangKaiHua3, null);
                                if (fskill == null)
                                {
                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.MoShangKaiHua2, null);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.MoShangKaiHua1, null);
                                        grade = 1;
                                    }
                                }
                                // 概率
                                if (fskill != null && rand >= (7500.0f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                {
                                    // 没有2连击？
                                    if (mb.double_pugong_hited_round == 0)
                                    {
                                        mb.ActionData.ActionId = (uint)SkillId.NormalAtk;
                                        turnIndex--;
                                        mb.double_pugong_hited_round = _round;
                                    }
                                }
                            }
                            // 千钧符 金石为开符
                            // 每施展一次仙法、鬼火、三尸、毒，无视掉被命中目标一定的对应抗性（连续中断两回合，则效果被清空）
                            if (skill.Type == SkillType.Shui
                            || skill.Type == SkillType.Huo
                            || skill.Type == SkillType.Lei
                            || skill.Type == SkillType.Feng
                            || skill.Type == SkillType.Toxin
                            || skill.Type == SkillType.GhostFire
                            || skill.Type == SkillType.ThreeCorpse)
                            {
                                var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.JinShiWeiKai3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.JinShiWeiKai2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.JinShiWeiKai1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null)
                                {
                                    // 第一次使用该类型技能  赋值
                                    if (mb.jswk_count == 0)
                                    {
                                        mb.jswk_type = skill.Type;
                                        mb.jswk_count++;
                                    }
                                    else
                                    {
                                        // 类型符合 次数+1
                                        if (mb.jswk_type == skill.Type)
                                        {
                                            mb.jswk_count++;
                                            effectData.Hurt *= (1 + mb.jswk_count * (grade + 0.1f * fskill.Addition) * ((float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel) / 100.0f);
                                        }
                                        // 使用其他类型的技能
                                        else
                                        {
                                            mb.jswk_count = 0;
                                        }
                                    }
                                }
                            }
                            // 千钧符 堆月符
                            // 魅惑、毒持续施法，可叠加增强效果
                            if (skill.Type == SkillType.Toxin || skill.Type == SkillType.Charm)
                            {
                                var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.DuiYue3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.DuiYue2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.DuiYue1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null)
                                {
                                    uint round_count = 0;
                                    if (skill.Type == SkillType.Toxin)
                                    {
                                        if (mb.duiyue_last_toxin_round == 0)
                                        {
                                            mb.duiyue_last_toxin_round = _round;
                                        }
                                        else
                                        {
                                            round_count = _round - mb.duiyue_last_toxin_round;
                                        }
                                        mb.duiyue_last_charm_round = 0;
                                    }
                                    else
                                    {
                                        if (mb.duiyue_last_charm_round == 0)
                                        {
                                            mb.duiyue_last_charm_round = _round;
                                        }
                                        else
                                        {
                                            round_count = _round - mb.duiyue_last_charm_round;
                                        }
                                        mb.duiyue_last_toxin_round = 0;
                                    }
                                    if (round_count > 0)
                                    {
                                        // 毒
                                        if (skill.Type == SkillType.Toxin)
                                        {
                                            effectData.Hurt *= (1 + round_count * (3 * grade + 0.1f * fskill.Addition) * ((float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel) / 100.0f);
                                        }
                                        // 魅惑
                                        else
                                        {
                                            effectData.KongZhi2 *= (1 + round_count * (3 * grade + 0.1f * fskill.Addition) * ((float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel) / 100.0f);
                                        }
                                    }
                                }
                            } else {
                                mb.duiyue_last_toxin_round = 0;
                                mb.duiyue_last_charm_round = 0;
                            }
                            // 千钧符 安神定魄符
                            // 三尸、鬼火每击中1个召唤兽则恢复自身气血
                            if (trole.IsPet && (skill.Type == SkillType.GhostFire || skill.Type == SkillType.ThreeCorpse))
                            {
                                var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.AnShenDingPo3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.AnShenDingPo2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.AnShenDingPo1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null)
                                {
                                    var addHp = mb.HpMax * (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                    mb.AddHp(addHp);
                                }
                            }
                            // 千钧符 忘尘符
                            // 每次遗忘法术命中对方人物，恢复自身气血(PVP)
                            if (_isPvp && skill.Type == SkillType.Forget)
                            {
                                var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.WangChen3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.WangChen2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.WangChen1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null)
                                {
                                    var addHp = mb.HpMax * (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                    mb.AddHp(addHp);
                                }
                            }
                            // 千钧符 饮血符
                            // (仅PVE生效) 每次施法命中或击倒对面，回复气血。可恢复倒地单位
                            if(!_isPvp)
                            {
                                var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.YinXue3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.YinXue2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.YinXue1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null)
                                {
                                    var addHp = mb.HpMax * (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                    mb.AddHp(addHp);
                                }
                            }
                            // 千钧符 击水三千符
                            // 每次释放仙法、龙法、鬼火、毒攻击时对目标造成当前生命值百分比的伤害（仅PVP）
                            if (_isPvp)
                            {
                                if (skill.Type == SkillType.Shui
                                || skill.Type == SkillType.Huo
                                || skill.Type == SkillType.Lei
                                || skill.Type == SkillType.Feng
                                || skill.Type == SkillType.Toxin
                                || skill.Type == SkillType.GhostFire
                                || skill.Id == SkillId.LingXuYuFeng
                                || skill.Id == SkillId.FeiJuJiuTian
                                || skill.Id == SkillId.WanQianHuaShen
                                || skill.Id == SkillId.FengLeiWanYun
                                || skill.Id == SkillId.ZhenTianDongDi
                                || skill.Id == SkillId.BaiLangTaoTian
                                || skill.Id == SkillId.CangHaiHengLiu)
                                {
                                    var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.JiShuiSanQian3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.JiShuiSanQian2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.JiShuiSanQian1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null)
                                    {
                                        effectData.Hurt += trole.Hp * ((3 * grade + 0.1f * fskill.Addition) * ((float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel) / 100f);
                                    }
                                }
                            }
                            // 载物符 混噩符
                            // 每次混乱法术命中对方人物，恢复自身气血（PVP）
                            if (skill.Type == SkillType.Chaos && _isPvp)
                            {
                                var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.HunE3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.HunE2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.HunE1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null)
                                {
                                    var addHp = mb.HpMax * (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                    mb.AddHp(addHp);
                                }
                            }
                            // PVE
                            if (!_isPvp && effectData.Hurt > 0)
                            {
                                // 千钧符 攻心符
                                // 每逢5的整数倍回合单法伤害提升，PVE常驻提高
                                if (_round % 5 == 0 && effectData.TargetNum == 1 && skill.Id != SkillId.NormalAtk)
                                {
                                    var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.GongXin3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.GongXin2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.GongXin1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null)
                                    {
                                        effectData.Hurt *= 1 + (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                    }
                                }
                                // 千钧符 三尸攻心符
                                // 每逢2的整数倍回合三尸单法伤害提升，PVE常驻提高
                                if (_round % 2 == 0 && effectData.TargetNum == 1 && skill.Type == SkillType.ThreeCorpse)
                                {
                                    var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.SanShiGongXin3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.SanShiGongXin2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.SanShiGongXin1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null)
                                    {
                                        effectData.Hurt *= 1 + (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                    }
                                }
                                // 千钧符 鬼火攻心符
                                // 每逢2的整数倍回合鬼火单法伤害提升，PVE常驻提高
                                if (_round % 2 == 0 && effectData.TargetNum == 1 && skill.Type == SkillType.GhostFire)
                                {
                                    var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.GuiHuoGongXin3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.GuiHuoGongXin2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.GuiHuoGongXin1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null)
                                    {
                                        effectData.Hurt *= 1 + (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                    }
                                }
                                // 千钧符 水系攻心符
                                // 每逢2的整数倍回合水系单法伤害提升，PVE常驻提高
                                if (_round % 2 == 0 && effectData.TargetNum == 1 && skill.Type == SkillType.Shui)
                                {
                                    var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.ShuiXiGongXin3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.ShuiXiGongXin2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.ShuiXiGongXin1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null)
                                    {
                                        effectData.Hurt *= 1 + (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                    }
                                }
                                // 千钧符 火系攻心符
                                // 每逢2的整数倍回合火系单法伤害提升，PVE常驻提高
                                if (_round % 2 == 0 && effectData.TargetNum == 1 && skill.Type == SkillType.Huo)
                                {
                                    var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.HuoXiGongXin3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.HuoXiGongXin2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.HuoXiGongXin1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null)
                                    {
                                        effectData.Hurt *= 1 + (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                    }
                                }
                                // 千钧符 雷系攻心符
                                // 每逢2的整数倍回合雷系单法伤害提升，PVE常驻提高
                                if (_round % 2 == 0 && effectData.TargetNum == 1 && skill.Type == SkillType.Lei)
                                {
                                    var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.LeiXiGongXin3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.LeiXiGongXin2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.LeiXiGongXin1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null)
                                    {
                                        effectData.Hurt *= 1 + (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                    }
                                }
                            }
                        }
                        // 宠物
                        if (mb.IsPet)
                        {
                            var owner = _members.GetValueOrDefault(mb.OwnerOnlyId, null);
                            if (owner != null)
                            {
                                // 御兽符 吸血符
                                // 召唤兽几率吸取自身造成伤害百分比的气血
                                if (effectData.Hurt > 0)
                                {
                                    var fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.XiXue3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.XiXue2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.XiXue1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null && _random.Next(10000) > (7500.0f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                    {
                                        var addHp = effectData.Hurt * (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                        mb.AddHp(addHp);
                                    }
                                }
                                // 御兽符 潜龙在渊符
                                // 召唤兽每存活1回合，物理、仙法、鬼火、天魔、分光、小楼、青面等伤害增加
                                if (skill.Id == SkillId.TianMoJieTi
                                || skill.Id == SkillId.FenGuangHuaYing
                                || skill.Id == SkillId.QingMianLiaoYa
                                || skill.Id == SkillId.XiaoLouYeKu
                                || skill.Id == SkillId.HighTianMoJieTi
                                || skill.Id == SkillId.HighFenGuangHuaYing
                                || skill.Id == SkillId.HighQingMianLiaoYa
                                || skill.Id == SkillId.HighXiaoLouYeKu
                                || skill.Type == SkillType.Huo
                                || skill.Type == SkillType.Physics
                                || skill.Type == SkillType.Shui
                                || skill.Type == SkillType.Feng
                                || skill.Type == SkillType.GhostFire
                                || skill.Type == SkillType.Lei)
                                {
                                    var fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.QianLongZaiYuan3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.QianLongZaiYuan2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.QianLongZaiYuan1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null)
                                    {
                                        effectData.Hurt *= (1 + 0.07f + (grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f) * _round / 100.0f);
                                    }
                                }
                                // 御兽符 淬毒符
                                // 召唤兽物理攻击时，若敌方未处于中毒，则一定几率让敌方中毒
                                if (skill.Type == SkillType.Physics && !trole.HasBuff(SkillType.Toxin))
                                {
                                    var fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.CuiDu3);
                                    var grade = 3;
                                    if (fskill == null)
                                    {
                                        fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.CuiDu2);
                                        grade = 2;
                                        if (fskill == null)
                                        {
                                            fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.CuiDu1);
                                            grade = 1;
                                        }
                                    }
                                    if (fskill != null && _random.Next(10000) >= (7500f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                    {
                                        var buffskill = SkillManager.GetSkill(SkillId.WanDuGongXin);
                                        var buffeffect = buffskill.GetEffectData(new GetEffectDataRequest()
                                        {
                                            Level = mb.Data.Level,
                                            Relive = mb.Relive,
                                            Intimacy = mb.Data.PetIntimacy,
                                            Member = mb,
                                            Profic = mb.GetSkillProfic(buffskill.Id)
                                        });
                                        buffeffect.Hurt = effectData.Hurt / 2;
                                        buffeffect.Round = 3;
                                        var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = mb.OnlyId };
                                        trole.AddBuff(buff);
                                    }
                                }
                            }
                        }
                        // 结束 天策符--------------------------------------------------------------------------------

                        // 打蓝
                        var mpHurt = effectData.MpHurt;

                        if (mpHurt > 0)
                        {
                            var mp = trole.Mp;
                            if (mpHurt > mp) mpHurt = mp;
                            trole.AddMp(-mpHurt);

                            attackData.Hp = trole.Hp;
                            attackData.Mp = trole.Mp;
                            attackData.Dead = trole.Dead;
                            attackData.Type = BattleAttackType.Mp;
                            attackData.Response = respone;
                            attackData.Value = (int) -mpHurt;
                            attackData.Buffs.AddRange(trole.GetBuffsSkillId());
                            continue;
                        }

                        mpHurt = 0;
                        if (effectData.MpPercent2 > 0)
                        {
                            mpHurt = MathF.Floor(trole.Mp * effectData.MpPercent2 / 100);
                        }

                        if (mpHurt > 0)
                        {
                            var percentDZhenShe = 0f; 
                            // 璇玑 颖悟绝伦 无价
                            if (trole.OrnamentSkills.ContainsKey(9022) && skill.Type == SkillType.Frighten)
                            {
                                percentDZhenShe += 0.3f;
                                // mpHurt = MathF.Floor(mpHurt * 0.7f);
                            }
                            //抗震慑属性处理
                            var dZhenShen = trole.Attrs.Get(AttrType.DzhenShe);
                            if (dZhenShen > 0) {
                                percentDZhenShe += dZhenShen / 100f;
                            }
                            if (percentDZhenShe > 1.0f) {
                                percentDZhenShe = 1.0f;
                            } 
                            mpHurt = MathF.Floor(mpHurt * (1.0f - percentDZhenShe));
                            // LogInfo("这里可以处理抗震慑逻辑mp");
                        }

                        // 天策符 载物符 觉醒符
                        // 从被控制状态摆脱后，当前回合内受到伤害减少
                        if (trole.just_not_kongzhi && trole.IsPlayer)
                        {
                            var fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.JueXing3);
                            var grade = 3;
                            if (fskill == null)
                            {
                                fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.JueXing2);
                                grade = 2;
                                if (fskill == null)
                                {
                                    fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.JueXing1);
                                    grade = 1;
                                }
                            }
                            if (fskill != null)
                            {
                                var rate = (1 - (0.2f + grade * 0.1f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.01f));
                                if (effectData.Hurt > 0)
                                {
                                    effectData.Hurt *= rate;
                                }
                                if (effectData.HurtPercent > 0)
                                {
                                    effectData.HurtPercent *= rate;
                                }
                            }
                            trole.just_not_kongzhi = false;
                        }

                        // 伤害
                        var hurt = effectData.Hurt;
                        if (hurt == 0)
                        {
                            var hurtPre = effectData.HurtPercent;
                            if (hurtPre != 0)
                            {
                                hurt = MathF.Floor(trole.Hp * hurtPre / 100);
                            }
                        }

                        // 狂暴率
                        var kbpre = mb.GetKuangBaoPre(skill.Type);
                        // 天策符 处理
                        if (mb.IsPet)
                        {
                            // 御兽符 咆哮符
                            // 增加召唤兽天魔、分光、青面、小楼狂暴率
                            if (skill.Id == SkillId.TianMoJieTi
                            || skill.Id == SkillId.FenGuangHuaYing
                            || skill.Id == SkillId.QingMianLiaoYa
                            || skill.Id == SkillId.XiaoLouYeKu
                            || skill.Id == SkillId.HighTianMoJieTi
                            || skill.Id == SkillId.HighFenGuangHuaYing
                            || skill.Id == SkillId.HighQingMianLiaoYa
                            || skill.Id == SkillId.HighXiaoLouYeKu)
                            {
                                if (kbpre > 0)
                                {
                                    var owner = _members.GetValueOrDefault(mb.OwnerOnlyId, null);
                                    if (owner != null)
                                    {
                                        var fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.PaoXiao3);
                                        var grade = 3;
                                        if (fskill == null)
                                        {
                                            fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.PaoXiao2);
                                            grade = 2;
                                            if (fskill == null)
                                            {
                                                fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.PaoXiao1);
                                                grade = 1;
                                            }
                                        }
                                        if (fskill != null)
                                        {
                                            kbpre *= (1 + 0.2f + grade * 0.1f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.01f);
                                        }
                                    }
                                }
                            }
                            // 御兽符 狂澜符
                            // 百分比增加召唤兽风雷水火鬼火系狂暴率
                            if (skill.Type == SkillType.Huo
                            || skill.Type == SkillType.Shui
                            || skill.Type == SkillType.Feng
                            || skill.Type == SkillType.GhostFire
                            || skill.Type == SkillType.Lei)
                            {
                                // TODO: 暂时去掉，因为宠物没有风雷水火的狂暴，只有物理狂暴，这里直接给狂暴率
                                // if (kbpre > 0)
                                // {
                                    var owner = _members.GetValueOrDefault(mb.OwnerOnlyId, null);
                                    if (owner != null)
                                    {
                                        var fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.KuangLan3);
                                        var grade = 3;
                                        if (fskill == null)
                                        {
                                            fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.KuangLan2);
                                            grade = 2;
                                            if (fskill == null)
                                            {
                                                fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.KuangLan1);
                                                grade = 1;
                                            }
                                        }
                                        if (fskill != null)
                                        {
                                            kbpre += 20 + grade * 10f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 1;
                                        }
                                    }
                                // }
                            }
                            // 觉醒技 威明相济
                            // 增加召唤兽释放天魔、青面技能时狂暴率5%/10%/17.5%/25%。释放分光、小楼夜哭（含高级）后降低目标法力上限6%/12%/21%/30%，持续2回合。
                            if (skill.Id == SkillId.TianMoJieTi
                            || skill.Id == SkillId.QingMianLiaoYa
                            || skill.Id == SkillId.HighTianMoJieTi
                            || skill.Id == SkillId.HighQingMianLiaoYa)
                            {
                                if (kbpre > 0 && mb.CanUseJxSkill(SkillId.WeiMingXiangJi))
                                {
                                    var baseValues = new List<float>() { 50, 100, 175, 250 };
                                    var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                                    var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                                    kbpre *= 1 + (calcValue / 1000f);
                                }
                            }
                        }
                        if (hurt > 0 && kbpre > 0)
                        {
                            var rnd = _random.Next(0, 100);
                            if (rnd < kbpre)
                            {
                                var kbstr = mb.GetKuangBaoStr(skill.Type);
                                hurt = MathF.Floor(hurt * (1.5f + kbstr / 100));
                                respone = BattleResponseType.Crits;
                            }
                        }

                        // 套装技能 锋芒毕露-珍藏/无价
                        var ignoreDSanShi = false;
                        if (skill.Type == SkillType.ThreeCorpse &&
                            respone != BattleResponseType.Crits &&
                            (mb.OrnamentSkills.ContainsKey(4041) || mb.OrnamentSkills.ContainsKey(4042)) &&
                            _round > 1 && _round > mb.KongZhiRound)
                        {
                            // 百分百狂暴
                            hurt = MathF.Floor(hurt * 2.5f);
                            respone = BattleResponseType.Crits;
                            ignoreDSanShi = mb.OrnamentSkills.ContainsKey(4042);
                            // 本次百分百狂暴不进回合统计, 下一回合不能开启百分百狂暴
                            mb.KongZhiRound = _round + 1;
                        }

                        // 没有混乱的时候 攻击自己人 掉1点血 非buff技能
                        if (SkillManager.IsAtkSkill(skillId) && !mb.HasBuff(SkillType.Chaos) &&
                            mb.CampId == trole.CampId)
                        {
                            hurt = 1;
                            mpHurt = 1;
                        }

                        // 天策符 处理
                        if (trole.IsPlayer)
                        {
                            // 天策符 载物符 乘鸾符
                            // 被攻击时，若自身未处于加防状态，则一定几率进入加防状态，加防效果强弱与天策符等级有关
                            if (!trole.HasBuff(SkillType.Defense))
                            {
                                var fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.ChengLuan3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.ChengLuan2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.ChengLuan1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null && _random.Next(10000) >= (7500f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                {
                                    var buffskill = SkillManager.GetSkill(SkillId.HanQingMoMo);
                                    var buffeffect = buffskill.GetEffectData(new GetEffectDataRequest()
                                    {
                                        Level = trole.Data.Level,
                                        Relive = trole.Relive,
                                        Intimacy = trole.Data.PetIntimacy,
                                        Member = trole,
                                        Profic = trole.GetSkillProfic(buffskill.Id)
                                    });
                                    buffeffect.KongZhi = 10 + grade * 5f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.5f;
                                    buffeffect.FaShang = 10 + grade * 5f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.5f;
                                    buffeffect.FangYu = 10 + grade * 5f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.5f;
                                    buffeffect.Round = 2;
                                    var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = trole.OnlyId };
                                    trole.AddBuff(buff);
                                }
                            }
                            // 天策符 载物符 御风符
                            // 被攻击时，若自身未处于加速状态，则一定几率进入加速状态，加速效果强弱与天策符等级有关
                            if (trole.HasBuff(SkillType.Speed))
                            {
                                var fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.YvFeng3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.YvFeng2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.YvFeng1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null && _random.Next(10000) >= (7500f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                {
                                    var buffskill = SkillManager.GetSkill(SkillId.QianKunJieSu);
                                    var buffeffect = buffskill.GetEffectData(new GetEffectDataRequest()
                                    {
                                        Level = trole.Data.Level,
                                        Relive = trole.Relive,
                                        Intimacy = trole.Data.PetIntimacy,
                                        Member = trole,
                                        Profic = trole.GetSkillProfic(buffskill.Id)
                                    });
                                    buffeffect.SpdPercent = 10 + grade * 5f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.5f;
                                    buffeffect.Round = 2;
                                    var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = trole.OnlyId };
                                    trole.AddBuff(buff);
                                }
                            }
                            // 天策符 载物符 冲冠符
                            // 被攻击时，若自身未处于加攻状态，则一定几率进入加攻状态，加攻效果强弱与天策符等级有关
                            if (trole.HasBuff(SkillType.Attack))
                            {
                                var fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.ChongGuan3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.ChongGuan2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.ChongGuan1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null && _random.Next(10000) >= (7500f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                {
                                    var buffskill = SkillManager.GetSkill(SkillId.MoShenFuShen);
                                    var buffeffect = buffskill.GetEffectData(new GetEffectDataRequest()
                                    {
                                        Level = trole.Data.Level,
                                        Relive = trole.Relive,
                                        Intimacy = trole.Data.PetIntimacy,
                                        Member = trole,
                                        Profic = trole.GetSkillProfic(buffskill.Id)
                                    });
                                    buffeffect.AtkPercent = 10f + grade * 5f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.5f;
                                    buffeffect.Hit = 10f + grade * 5f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.5f;
                                    buffeffect.Round = 2;
                                    var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = trole.OnlyId };
                                    trole.AddBuff(buff);
                                }
                            }
                            // 天策符 载物符 承天载物符
                            // 受到伤害时一定几率减伤，己方倒地单位越多，概率越高（每回合最多触发3次）
                            if (trole.ctzw_active_times < 3 && hurt > 0)
                            {
                                var fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.ChengTianZaiWu3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.ChengTianZaiWu2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.ChengTianZaiWu1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null && _random.Next(10000) >= (7500f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                {
                                    var rate = (1 - (0.1f + grade * 0.05f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.005f));
                                    hurt *= rate;
                                    trole.ctzw_active_times++;
                                }
                            }
                        }

                        if (hurt > 0) 
                        {
                            // 龙族 是否能计算破防？
                            var canCalcPoFang = skillId == SkillId.LingXuYuFeng ||
                                                skillId == SkillId.FeiJuJiuTian ||
                                                skillId == SkillId.WanQianHuaShen ||
                                                skillId == SkillId.FengLeiWanYun ||
                                                skillId == SkillId.ZhenTianDongDi ||
                                                skillId == SkillId.BaiLangTaoTian ||
                                                skillId == SkillId.CangHaiHengLiu;
                            // 查看保护
                            var protect = false;
                            protectList.TryGetValue(trole.OnlyId, out var protectId);
                            while (protectId > 0 &&
                                   (skillId == SkillId.NormalAtk || skillId == SkillId.BingLinChengXia || canCalcPoFang))
                            {
                                _members.TryGetValue(protectId, out var protecter);
                                if (protecter == null || protecter.Dead) break;
                                var thurt = hurt;
                                var tmpMpHurt = mpHurt;
                                var pfpre = mb.GetPoFangPre();
                                var randkb = _random.Next(0, 10000);
                                if (randkb < pfpre * 100)
                                {
                                    var pfstr = mb.GetPoFang();
                                    var kwl = protecter.GetKangWuLi();
                                    respone = BattleResponseType.PoFang;
                                    thurt = MathF.Floor(thurt * (1 + (pfstr * 3 - kwl * 2) / 100));
                                }

                                if (thurt <= 0) thurt = 1;

                                // 天策符 处理
                                // 天策符 御兽符 庇佑符
                                // 召唤兽保护我方人物或伙伴，则本回合为其分担一定比例的伤害，且召唤兽分担到的伤害降低一定比例，仅PVP前5回合生效。
                                if (_isPvp && _round <= 5 && protecter.IsPet)
                                {
                                    var owner = _members.GetValueOrDefault(protecter.OwnerOnlyId, null);
                                    if (owner != null)
                                    {
                                        var fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.BiYou3);
                                        var grade = 3;
                                        if (fskill == null)
                                        {
                                            fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.BiYou2);
                                            grade = 2;
                                            if (fskill == null)
                                            {
                                                fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.BiYou1);
                                                grade = 1;
                                            }
                                        }
                                        if (fskill != null)
                                        {
                                            thurt *= (1 - (0.05f + grade * 0.15f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.005f));
                                        }
                                    }
                                }
                                var orgHurt = thurt;
                                if (thurt > protecter.Hp) thurt = protecter.Hp;
                                protecter.AddHp(-thurt);
                                if (tmpMpHurt > protecter.Mp) tmpMpHurt = protecter.Mp;
                                protecter.AddMp(-tmpMpHurt);

                                protect = true;

                                after.Protect = new BattleProtectData
                                {
                                    RoleId = protectId,
                                    Hurt = (int) -hurt,
                                    Dead = protecter.Dead,
                                    Hp = protecter.Hp,
                                    Mp = protecter.Mp,
                                    Respone = respone
                                };

                                if (thurt > 0) hurtPool.Add(orgHurt);
                                if (tmpMpHurt > 0) mpPool.Add(tmpMpHurt);

                                hurt = 0;
                                mpHurt = 0;
                                break;
                            }

                            if (protect)
                            {
                                // 被保护了
                                attackData.Type = BattleAttackType.Hurt;
                                attackData.Response = BattleResponseType.Protect;
                                attackData.Value = (int) hurt;
                            }
                            else
                            {
                                var thurt = hurt;
                                var tmpMpHurt = mpHurt;
                                // 破防
                                if (SkillId.NormalAtk == skillId || SkillId.BingLinChengXia == skillId || canCalcPoFang)
                                {
                                    // 破防
                                    sattrnum += mb.GetPoFang();

                                    // 抗物理(在dattrnum初始值的时候就已经赋值过了) + 物理吸收
                                    dattrnum += trole.Attrs.Get(AttrType.PxiShou);
                                    // 沁肤彻骨-珍藏 无价 TODO: 现在只加了物理，仙法、鬼火未加
                                    if (trole.HasBuff(SkillType.Toxin))
                                    {
                                        var dep = 0.0f;
                                        if (mb.OrnamentSkills.ContainsKey(1022))
                                        {
                                            dep = MathF.Ceiling(20 + 0.02f * mb.Attrs.Get(AttrType.GenGu) / 100.0f);
                                            // if (SkillManager.IsXianFa(skill.Type) || SkillType.GhostFire == skill.Type)
                                            // {
                                            //     dep += MathF.Ceiling(10 + 0.01f * mb.Attrs.Get(AttrType.GenGu) / 100.0f);
                                            // }
                                        }
                                        else if (mb.OrnamentSkills.ContainsKey(1021))
                                        {
                                            dep = 20.0f;
                                            // if (SkillManager.IsXianFa(skill.Type) || SkillType.GhostFire == skill.Type)
                                            // {
                                            //     dep += 10;
                                            // }
                                        }
                                        dattrnum = Math.Max(0, dattrnum - dep);
                                    }

                                    thurt = MathF.Floor(thurt * (1 + (sattrnum * 1f - dattrnum * 1.5f) / 100));
                                }
                                else
                                {
                                    thurt = MathF.Floor(thurt * (1 + (sattrnum * 4 - dattrnum * 3) / 100));
                                }

                                // 五行 震慑不算五行
                                if (skill.Type != SkillType.Frighten)
                                {
                                    var hurtWxPre = 0f;
                                    // 五行相克
                                    foreach (var (k, v) in GameDefine.WuXingStrengThen)
                                    {
                                        var bwx = mb.Attrs.Get(k);
                                        var twx = trole.Attrs.Get(v);
                                        hurtWxPre += (bwx / 100) * (twx / 100) * 0.4f;
                                    }

                                    // 五行强力克, 选取最大强力克数值
                                    var maxQlkValue = 0f;
                                    var maxKzWxKey = AttrType.Unkown;
                                    foreach (var (k, v) in GameDefine.WuXingKeStrengThen)
                                    {
                                        var bwx = mb.Attrs.Get(k);
                                        if (bwx > maxQlkValue)
                                        {
                                            maxKzWxKey = v;
                                            maxQlkValue = bwx;
                                        }
                                    }

                                    if (maxKzWxKey != AttrType.Unkown && maxQlkValue > 0)
                                    {
                                        var twx = trole.Attrs.Get(maxKzWxKey);
                                        hurtWxPre += (maxQlkValue / 100) * (twx / 100) * 0.4f;
                                    }

                                    thurt = MathF.Floor(thurt * (1 + hurtWxPre));
                                }
                                else
                                {
                                    // 张皇失措 珍藏/无价
                                    if (mb.OrnamentSkills.ContainsKey(3071) || mb.OrnamentSkills.ContainsKey(3072))
                                    {
                                        var zhsc = 0.15f;
                                        if (mb.OrnamentSkills.ContainsKey(3072))
                                        {
                                            zhsc += MathF.Floor(mb.Attrs.Get(AttrType.MinJie) / 100.0f) * 0.01f;
                                            if (zhsc > 0.3f) zhsc = 0.3f;
                                        }

                                        trole.ZhangHuangShiCuo = zhsc;
                                    }
                                }

                                if (skill.Type == SkillType.Speed)
                                {
                                    // 气定乾坤
                                    if (mb.OrnamentSkills.ContainsKey(3021) || mb.OrnamentSkills.ContainsKey(3022))
                                    {
                                        var qdqk = 10;
                                        if (mb.OrnamentSkills.ContainsKey(3022))
                                        {
                                            qdqk += (int) MathF.Floor(mb.Attrs.Get(AttrType.MinJie) / 200.0f);
                                        }

                                        trole.QiDingQianKun = qdqk;
                                    }
                                }

                                if (skill.Type == SkillType.Attack)
                                {
                                    // 销神流志
                                    if (mb.OrnamentSkills.ContainsKey(3051) || mb.OrnamentSkills.ContainsKey(3052))
                                    {
                                        trole.XiaoShenLiuZhi1 = 0.2f;
                                        trole.XiaoShenLiuZhi2 = 0.1f;
                                        if (mb.OrnamentSkills.ContainsKey(3052))
                                        {
                                            trole.XiaoShenLiuZhi1 +=
                                                MathF.Floor(mb.Attrs.Get(AttrType.MinJie) / 50.0f) * 0.01f;
                                            trole.XiaoShenLiuZhi2 +=
                                                MathF.Floor(mb.Attrs.Get(AttrType.MinJie) / 50.0f) * 0.003f;
                                        }
                                    }
                                }

                                // 判断防御
                                if (thurt > 0 && trole.ActionData.ActionType == BattleActionType.Skill &&
                                    mb.ActionData.ActionId == (uint) SkillId.NormalAtk &&
                                    trole.ActionData.ActionId == (uint) SkillId.NormalDef &&
                                    !trole.HasBuff(SkillType.Chaos))
                                {
                                    thurt = MathF.Floor(thurt * 0.5f);
                                    attackData.Response = BattleResponseType.Defend;
                                }

                                // 套装技能 五毒俱全-无价
                                if (skill.Type == SkillType.Toxin && mb.OrnamentSkills.ContainsKey(1072))
                                {
                                    var percent = MathF.Floor(mb.Attrs.Get(AttrType.GenGu) / 200) * 0.15f;
                                    if (percent > 0)
                                        thurt = (int) MathF.Round(thurt * (1 + percent));
                                }

                                if (thurt <= 0) thurt = 1;
                                if (thurt > 0)
                                {
                                    // 如果中了睡眠, 被攻击会打断
                                    var sleepBuff = trole.GetBuffByMagicType(SkillType.Sleep);
                                    if (sleepBuff != null)
                                    {
                                        sleepBuff.AtkTimes++;
                                        // 套装技能 醉生梦死-无价
                                        var canBreak = true;
                                        _members.TryGetValue(sleepBuff.Source, out var sender);
                                        if (sender != null && sender.OrnamentSkills.ContainsKey(1032) &&
                                            sleepBuff.AtkTimes <= 1)
                                        {
                                            canBreak = false;
                                        }

                                        if (canBreak)
                                        {
                                            // 清理睡眠
                                            trole.RemoveBuff(SkillType.Sleep);

                                            if (!trole.IsRoundAction)
                                            {
                                                var bindex = _turns.FindIndex(p => p.OnlyId == mb.OnlyId);
                                                var tindex = _turns.FindIndex(p => p.OnlyId == trole.OnlyId);
                                                if (tindex < bindex)
                                                {
                                                    _turns.Insert(bindex + 1, new TurnItem
                                                    {
                                                        OnlyId = trole.OnlyId,
                                                        Spd = trole.Spd
                                                    });
                                                    // _turns.RemoveAt(tindex);
                                                    _turns[tindex].OnlyId = 0;
                                                }
                                            }
                                        }
                                    }
                                }

                                // 能打多少算多少
                                var orgHurt = thurt;
                                // 震慑技能最多只能抽60%
                                if (skill.Type == SkillType.Frighten)
                                {
                                    var hurtPercent = effectData.HurtPercent;
                                    var dZhenShen = trole.Attrs.Get(AttrType.DzhenShe);
                                    if (dZhenShen > 0) {
                                        hurtPercent -= dZhenShen / 100.0f;
                                        if (hurtPercent < 0) {
                                            hurtPercent = 0f;
                                        }
                                        // LogInfo("这里可以处理抗震慑逻辑");
                                    }
                                    var maxHurt = trole.Hp * hurtPercent / 100.0f;
                                    if (thurt > maxHurt)
                                    {
                                        thurt = maxHurt;
                                        orgHurt = thurt;
                                    }
                                }

                                if (thurt > trole.Hp) thurt = trole.Hp;

                                // 蚩尤 套装 张皇失措
                                if (mb.ZhangHuangShiCuo > 0f)
                                {
                                    thurt = MathF.Ceiling(thurt * (1.0f - mb.ZhangHuangShiCuo));
                                }

                                // 戮仙 套装 销神流志
                                if (mb.XiaoShenLiuZhi2 > 0f)
                                {
                                    thurt = MathF.Ceiling(thurt * (1.0f + mb.XiaoShenLiuZhi2));
                                }

                                // 戮仙 套装 销神流志
                                if (mb.XiaoShenLiuZhi1 > 0f && skill.Type == SkillType.Physics)
                                {
                                    tmpMpHurt += MathF.Ceiling(trole.XiaoShenLiuZhi1 * mb.Mp);
                                }

                                // 控制仙法和鬼火的最高输出500w
                                // if (thurt > 0 && (SkillManager.IsXianFa(skill.Type) ||
                                //                   SkillType.GhostFire == skill.Type))
                                // {
                                //     thurt = Math.Min(thurt, 5000000);
                                // }
                                // 龙族 飞举九天和沧海横流 选定目标伤害输出+40%
                                // if(trole.OnlyId == mb.ActionData.Target)
                                // {
                                //     if (skillId is SkillId.CangHaiHengLiu or SkillId.FeiJuJiuTian)
                                //     {
                                //         thurt += (float)(effectData.Hurt * 0.4);
                                //     }
                                // }
                                // trole.AddHp(-thurt);
                                // 如果目标被伤害，则必然会被种上荼蘼花种
                                if (thurt > 0) 
                                {
                                    // 荼蘼花开 100%几率添加BUFF
                                    if (tuMiHuaKai != null)
                                    {
                                        var ed = new SkillEffectData()
                                        {
                                            Round = 2
                                        };
                                        ed["BaoFaHurt"] = tuMiHuaKai["BaoFaHurt"];
                                        // 觉醒技 花开二度
                                        // “荼蘼花开”造成爆发伤害时额外对目标造成8%/16&/28%/40%法力伤害（可以与遗患、步履维艰等叠加）。
                                        if (mb.CanUseJxSkill(SkillId.HuaKaiErDu))
                                        {
                                            var baseValues = new List<float>() { 80, 160, 280, 400 };
                                            var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                                            var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                                            ed["BaoFaMHurt"] = Math.Max(0, trole.Mp) * calcValue / 100f;
                                        }
                                        trole.AddBuff(new Buff(NextBuffId(), skill, ed));
                                        // LogDebug($"玩家/单元[{trole.Data.Id}][{trole.Data.Name}]被添加荼蘼花开BUFF");
                                    }
                                }
                                // 切割
                                // 200-300级  原  切割属性不变 从200级开始每升一级增加0.3%，300级效果是增加30%切割伤害 等于说就是原来整体这件装备的属性 集中在了最后100级。
                                if (!_isPvp && mb.IsPlayer && mb.Data.QiegeLevel > 200 && thurt > 0) {
                                    thurt += trole.HpMax * (ConfigService.QieGeLevelList[mb.Data.QiegeLevel].hp / 1000.0f);
                                }
                                //神之力产生的真实伤害
                                if (mb.IsPlayer && mb.Data.ShenzhiliHurtLevel > 0 && thurt > 0) {
                                    thurt += 100000.0f * mb.Data.ShenzhiliHurtLevel;
                                }

                                // 宠物 有觉醒技？
                                // 觉醒技 正气凛然
                                // 物理攻击后对目标额外造成一次伤害，伤害值为目标当前气血值的5%/10%/17.5%/25%（上限30万，物理连击时仅第一次攻击生效）。
                                float ZhengQiLinRanHurt = 0;
                                if ((skillId is SkillId.NormalAtk or SkillId.BingLinChengXia) && mb.CanUseJxSkill(SkillId.ZhengQiLinRan))
                                {
                                    var baseValues = new List<float>() { 50, 100, 175, 250 };
                                    var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                                    var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                                    ZhengQiLinRanHurt = Math.Min(trole.Hp * calcValue / 100f, 300000f);
                                }
                                // 觉醒技 武圣附身
                                // 物理攻击有14$/28%/49%概率被武圣附身，本次物理攻击附加与力量点数有关的伤害，该伤害无视目标物理抗性，物理连击时仅第一次攻击生效，但如果触发分花、分裂可多次生效。
                                float WuShengFuShenHurt = 0;
                                if ((skillId is SkillId.NormalAtk or SkillId.BingLinChengXia) && mb.CanUseJxSkill(SkillId.WuShengFuShen))
                                {
                                    var baseValues = new List<float>() { 140, 280, 490, 700 };
                                    var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                                    var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                                    if (_random.Next(1000) < calcValue) {
                                        WuShengFuShenHurt = mb.Attrs.Get(AttrType.LiLiang);
                                    }
                                }
                                float thurtBeforeHurt = thurt;
                                thurt = trole.Hurt(thurt + ZhengQiLinRanHurt + WuShengFuShenHurt, _members, out var zptxAttackData);
                                if (zptxAttackData != null) tgs.Add(zptxAttackData);
                                if (tmpMpHurt > trole.Mp) tmpMpHurt = trole.Mp;
                                trole.AddMp(-tmpMpHurt);

                                // 宠物 有觉醒技？
                                if (thurt > 0)
                                {
                                    // 觉醒技 愿者上钩
                                    // 自身受到气血伤害时，有10%/20%/35%/50%概率降低攻击者仙法、鬼火、毒忽视属性及破防程度20%，持续3回合，每场战斗最多触发一次。
                                    if (!trole.IsUsedJxSkill(SkillId.YuanZheShangGou) && trole.CanUseJxSkill(SkillId.YuanZheShangGou))
                                    {
                                        var baseValues = new List<float>() { 1000, 2000, 3500, 5000 };
                                        var rangeValue = baseValues[(int)trole.Data.PetJxGrade] - baseValues[(int)trole.Data.PetJxGrade - 1];
                                        var calcValue = baseValues[(int)trole.Data.PetJxGrade - 1] + rangeValue * trole.Data.PetJxLevel / 6;
                                        if (_random.Next(10000) < calcValue)
                                        {
                                            trole.UseJxSkill(SkillId.YuanZheShangGou);
                                            // buff效果
                                            var buffskill = SkillManager.GetSkill(SkillId.HanQingMoMo);
                                            var buffeffect = new SkillEffectData();
                                            buffeffect.PoFang = -20;
                                            buffeffect.HFaShang = -20;
                                            buffeffect.Round = 3;
                                            var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = trole.OnlyId };
                                            mb.AddBuff(buff);
                                        }
                                    }
                                    // 觉醒技 慧心巧思
                                    // 受到气血伤害时有10%/20%/35%/50%概率吸收伤害，吸收量为自身最大法力值的15%/30%/52.5%/75%，每场战斗最多生效一次。
                                    if (!trole.IsUsedJxSkill(SkillId.HuiXinQiaoSi) && trole.CanUseJxSkill(SkillId.HuiXinQiaoSi))
                                    {
                                        var baseValues = new List<float>() { 1000, 2000, 3500, 5000 };
                                        var rangeValue = baseValues[(int)trole.Data.PetJxGrade] - baseValues[(int)trole.Data.PetJxGrade - 1];
                                        var calcValue = baseValues[(int)trole.Data.PetJxGrade - 1] + rangeValue * trole.Data.PetJxLevel / 6;
                                        if (_random.Next(10000) < calcValue)
                                        {
                                            trole.UseJxSkill(SkillId.HuiXinQiaoSi);
                                            // 加血
                                            var baseValues1 = new List<float>() { 150, 300, 525, 750 };
                                            var rangeValue1 = baseValues1[(int)trole.Data.PetJxGrade + 1] - baseValues1[(int)trole.Data.PetJxGrade];
                                            var calcValue1 = baseValues1[(int)trole.Data.PetJxGrade] + rangeValue1 * trole.Data.PetJxLevel / 6;
                                            trole.AddHp(trole.Attrs.Get(AttrType.MpMax) * calcValue1 / 1000);
                                            tgs.Add(new BattleAttackData
                                            {
                                                OnlyId = trole.OnlyId,
                                                Type = BattleAttackType.Hp,
                                                Value = 0,
                                                Hp = trole.Hp,
                                                Mp = trole.Mp,
                                                Dead = trole.Dead,
                                                Buffs = { trole.GetBuffsSkillId() }
                                            });
                                        }
                                    }
                                    // 觉醒技 投桃报李
                                    // 自身受到气血伤害时，有10%/20%/35%/50%概率将伤害值的30%转化为友方的气血恢复（上限20万）恢复给友方除自身外气血百分比最低的非倒地单位，每回合最多触发1次。
                                    if (!trole.IsUsedJxSkill(SkillId.TouTaoBaoLi) && trole.CanUseJxSkill(SkillId.TouTaoBaoLi))
                                    {
                                        var baseValues = new List<float>() { 1000, 2000, 3500, 5000 };
                                        var rangeValue = baseValues[(int)trole.Data.PetJxGrade] - baseValues[(int)trole.Data.PetJxGrade - 1];
                                        var calcValue = baseValues[(int)trole.Data.PetJxGrade - 1] + rangeValue * trole.Data.PetJxLevel / 6;
                                        if (_random.Next(10000) < calcValue)
                                        {
                                            // 转换
                                            var convertedHurt = Math.Min(thurt * 0.2f, 200000);
                                            BattleMember selected = null;
                                            foreach (var (id, bm) in _members)
                                            {
                                                if (bm.CampId == trole.CampId && !bm.Dead && bm.OnlyId != trole.OnlyId)
                                                {
                                                    if (selected == null || (bm.Hp / bm.HpMax) < (selected.Hp / selected.HpMax))
                                                    {
                                                        selected = bm;
                                                    }
                                                }
                                            }
                                            if (selected != null)
                                            {
                                                trole.UseJxSkill(SkillId.TouTaoBaoLi);
                                                trole.AddHp(convertedHurt);
                                                selected.AddHp(convertedHurt);

                                                tgs.Add(new BattleAttackData
                                                {
                                                    OnlyId = selected.OnlyId,
                                                    Type = BattleAttackType.Hp,
                                                    Value = 0,
                                                    Hp = selected.Hp,
                                                    Mp = selected.Mp,
                                                    Dead = selected.Dead,
                                                    Buffs = { selected.GetBuffsSkillId() }
                                                });
                                            }
                                        }
                                    }

                                    // 觉醒技 威明相济
                                    // 增加召唤兽释放天魔、青面技能时狂暴率5%/10%/17.5%/25%。释放分光、小楼夜哭（含高级）后降低目标法力上限6%/12%/21%/30%，持续2回合。
                                    if (skill.Id == SkillId.FenGuangHuaYing
                                    || skill.Id == SkillId.XiaoLouYeKu
                                    || skill.Id == SkillId.HighFenGuangHuaYing
                                    || skill.Id == SkillId.HighXiaoLouYeKu)
                                    {
                                        var baseValues = new List<float>() { 6, 12, 21, 30 };
                                        var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                                        var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                                        // buff效果
                                        var buffskill = SkillManager.GetSkill(SkillId.FenGuangHuaYing);
                                        var buffeffect = new SkillEffectData();
                                        buffeffect.AttrType = AttrType.Mp;
                                        buffeffect.AttrValue = -calcValue;
                                        buffeffect.Round = 2;
                                        var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = trole.OnlyId };
                                        trole.AddBuff(buff);
                                    }
                                }

                                // 天策符 处理
                                // 千钧符 浩气凌霄
                                // 释放仙法、鬼火、三尸、毒有一定几率二连击
                                if (mb.IsPlayer)
                                {
                                    if (skill.Type == SkillType.Shui
                                    || skill.Type == SkillType.Huo
                                    || skill.Type == SkillType.Lei
                                    || skill.Type == SkillType.Feng
                                    || skill.Type == SkillType.Toxin
                                    || skill.Type == SkillType.GhostFire
                                    || skill.Type == SkillType.ThreeCorpse)
                                    {
                                        // 没有2连击？
                                        if (mb.double_fashu_hited_round == 0 && mb.double_pugong_hited_round == 0)
                                        {
                                            var fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.HaoQiLingXiao3);
                                            var grade = 3;
                                            if (fskill == null)
                                            {
                                                fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.HaoQiLingXiao2);
                                                grade = 2;
                                                if (fskill == null)
                                                {
                                                    fskill = mb.TianceFuSkills.GetValueOrDefault(SkillId.HaoQiLingXiao1);
                                                    grade = 1;
                                                }
                                            }
                                            if (fskill != null)
                                            {
                                                var rand = _random.Next(10000);
                                                if (rand >= (7500.0f - (grade * 500.0f + fskill.Addition * 50.0f) * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                                {
                                                    turnIndex--;
                                                    mb.double_fashu_hited_round = _round;
                                                }
                                            }
                                        }
                                    }
                                }

                                if (skillId is SkillId.NormalAtk or SkillId.BingLinChengXia && thurt > 0)
                                {
                                    // 连击
                                    var lianji = mb.GetLianJi();
                                    if (lianji > 0)
                                    {
                                        after.LianJi = new BattleLianJiData();
                                        after.LianJi.Hurts.Add(0 - (int) thurt);
                                        var lianjihurt = thurt;
                                        for (var x = 0; x < lianji; x++)
                                        {
                                            if (trole.Dead) break;
                                            // 衰减70%
                                            lianjihurt = MathF.Ceiling(lianjihurt * 0.7f);

                                            var realHurt = lianjihurt;
                                            if (trole.Hp >= 1000 && trole.Skills.ContainsKey(SkillId.PiCaoRouHou)) realHurt *= 0.5f;
                                            if (realHurt >= trole.Hp) realHurt = trole.Hp;
                                            trole.AddHp(-realHurt);
                                            after.LianJi.Hurts.Add(0 - (int) realHurt);
                                            // 正气凛然/武圣附身 加成去除
                                            lianjihurt = thurtBeforeHurt * 0.7f;
                                            ZhengQiLinRanHurt = 0;
                                            WuShengFuShenHurt = 0;
                                        }

                                        addTime += lianji * 0.16f;
                                    }

                                    // 隔山打牛
                                    var geshan = mb.GetGeShan();
                                    if (geshan > 0)
                                    {
                                        var tmpRole = FindRandomTeamTarget(trole.OnlyId);
                                        if (tmpRole != null)
                                        {
                                            var tmpHurt = (int) MathF.Floor(orgHurt * geshan / 100f);
                                            if (tmpRole.Hp >= 1000 && tmpRole.Skills.ContainsKey(SkillId.PiCaoRouHou))
                                                tmpHurt = (int) MathF.Floor(tmpHurt * 0.5f);
                                            if (tmpHurt > tmpRole.Hp) tmpHurt = (int) tmpRole.Hp;
                                            tmpRole.AddHp(-tmpHurt);
                                            after.GeShan = new BattleGeShanData
                                            {
                                                RoleId = tmpRole.OnlyId,
                                                Response = respone,
                                                Value = -tmpHurt,
                                                Hp = tmpRole.Hp,
                                                Mp = tmpRole.Mp,
                                                Dead = tmpRole.Dead,
                                            };
                                        }
                                    }

                                    // 天降脱兔
                                    tianJiangTuoTu = mb.GetTianJiangTuoTu();

                                    // // 幻影离魂
                                    // huanYingLiHun = mb.GetHuanYingLiHun();
                                }

                                attackData.Type = BattleAttackType.Hurt;
                                attackData.Response = respone;
                                attackData.Value = (int) -thurt;

                                if (thurt > 0)
                                {
                                    hurtPool.Add(orgHurt);
                                }

                                if (tmpMpHurt > 0)
                                {
                                    mpPool.Add(tmpMpHurt);
                                }

                                if (skillId == SkillId.HenYuFeiFei && thurt > 0 && mb.ZhuiJiNum < 2)
                                {
                                    if (trole.Dead || (trole.Hp * 1.0f / trole.HpMax <= 0.9f))
                                    {
                                        // 追击下一个单位(速度最高的敌方单位)
                                        var members = mb.CampId == 1 ? _camp2.Members : _camp1.Members;
                                        members = members.Where(p => !p.Dead && p.Pos > 0).ToList();
                                        members.Sort((a, b) => b.Spd - a.Spd);
                                        if (members.Count > 0)
                                        {
                                            mb.ResetRoundData(false);
                                            mb.ActionData.ActionType = BattleActionType.Skill;
                                            mb.ActionData.ActionId = (uint) SkillId.HenYuFeiFei;
                                            mb.IsAction = true;

                                            // 叠加10%水系狂暴率
                                            mb.AddRoundAttr(AttrType.KshuiLv, 10);
                                            mb.ZhuiJiNum++;

                                            var turnItem = new TurnItem
                                            {
                                                OnlyId = mb.OnlyId,
                                                Spd = mb.Spd
                                            };
                                            if (turnIndex + 1 >= _turns.Count)
                                                _turns.Add(turnItem);
                                            else
                                                _turns.Insert(turnIndex + 1, turnItem);
                                        }
                                    }
                                }

                                // 天降灵猴
                                if (skillId == SkillId.StealMoney)
                                {
                                    var xmb = GetFirstPlayer();
                                    if (xmb != null)
                                    {
                                        var stealMoney = (uint) effectData.Money;
                                        if (stealMoney > xmb.Data.Money) stealMoney = xmb.Data.Money;
                                        xmb.Data.Money -= stealMoney;
                                        // 注意这里不是+=, 而是等于, 让玩家挑适当的最大值, 奖励的时候翻倍
                                        _lingHouInfo.StealMoney = stealMoney;
                                        xmb.AddMoney(MoneyType.Jade, 0 - (int) _lingHouInfo.StealMoney, "灵猴偷走");
                                        xmb.SendPacket(GameCmd.S2CNotice,
                                            new S2C_Notice {Text = $"灵猴偷走了你{stealMoney}仙玉"});
                                    }
                                }

                                hurt = thurt;
                            }
                        }

                        // 龙族 治疗
                        if (skillId != SkillId.PeiRanMoYu && skillId != SkillId.ZeBeiWanWu) {
                        var hp = effectData.Hp;
                        if (hp > 0)
                        {
                            hp = (int) trole.AddHp(hp, _members);
                            attackData.Type = BattleAttackType.Hp;
                            attackData.Value = hp;
                        }
                        }

                        // 龙族 加血百分比
                        if (skillId != SkillId.PeiRanMoYu && skillId != SkillId.ZeBeiWanWu) {
                        var hpper = effectData.HpPercent;
                        if (hpper > 0)
                        {
                            var addHp = (int) MathF.Ceiling(trole.HpMax * hpper / 100);
                            addHp = (int) trole.AddHp(addHp, _members);
                            attackData.Type = BattleAttackType.Hp;
                            attackData.Value = addHp;
                        }
                        }

                        // 加蓝百分比
                        var mpper = effectData.MpPercent;
                        if (mpper > 0)
                        {
                            var addmp = (int) MathF.Ceiling(trole.MpMax * mpper / 100);
                            trole.AddMp(addmp);
                        }

                        // 龙族 治愈法术增强
                        if (skillId == SkillId.PeiRanMoYu || skillId == SkillId.ZeBeiWanWu)
                        {
                            var addMpRate = 0f;
                            var addHpRate = 0f;
                            // 安适如常-无价
                            if (mb.OrnamentSkills.ContainsKey(110002))
                            {
                                addMpRate = 0.1f;
                                addMpRate += MathF.Floor(mb.Attrs.Get(AttrType.GenGu) / 100.0f) * 0.01f;
                            }
                            // 安适如常-珍藏
                            else if (mb.OrnamentSkills.ContainsKey(110001))
                            {
                                addMpRate = 0.1f;
                            }
                            // 万古长春-无价
                            if (mb.OrnamentSkills.ContainsKey(120002))
                            {
                                addHpRate = 0.04f;
                                addHpRate += MathF.Floor(mb.Attrs.Get(AttrType.GenGu) / 300.0f) * 0.04f;
                            }
                            // 万古长春-珍藏 
                            else if (mb.OrnamentSkills.ContainsKey(120002))
                            {
                                addHpRate = 0.04f;
                            }
                            if (addMpRate > 0)
                            {
                                var addMp = MathF.Floor(trole.MpMax * addMpRate);
                                trole.AddMp(addMp, _members);
                            }
                            if (addHpRate > 0)
                            {
                                var addHp = MathF.Floor(trole.HpMax * addHpRate);
                                trole.AddHp(addHp, _members);
                            }
                        }

                        // 扣蓝百分比
                        // var smpper = effectData.MpPercent2;
                        // if (smpper > 0)
                        // {
                        //     var addmp = -(int) MathF.Ceiling(trole.MpMax * smpper / 100);
                        //     trole.AddMp(addmp);
                        //     attackData.Type = BattleAttackType.Mp;
                        //     attackData.Value = addmp;
                        // }

                        // 处理buff
                        if (effectData.Round > 0)
                        {
                            // 命中
                            var mingzhong = 100;
                            // 回合
                            var round = effectData.Round;
                            // 抗性计算
                            if (SkillManager.IsAtkSkill(skillId))
                            {
                                GameDefine.SkillTypeStrengthen.TryGetValue(skill.Type, out sattr);
                                GameDefine.SkillTypeKangXing.TryGetValue(skill.Type, out dattr);
                                sattrnum = mb.Attrs.Get(sattr);
                                var tattrnum = trole.Attrs.Get(dattr);

                                // 套装技能 五毒俱全-珍藏
                                if (skill.Type == SkillType.Toxin && mb.OrnamentSkills.ContainsKey(1071) &&
                                    _random.Next(0, 100) < 50)
                                {
                                    tattrnum = 0;
                                }

                                // 套装技能 锋芒毕露-无价
                                if (skill.Type == SkillType.ThreeCorpse && ignoreDSanShi)
                                {
                                    tattrnum = 0;
                                }

                                var attrnum = sattrnum - tattrnum + 100;
                                if (mb.IsPlayer && SkillManager.IsKongZhiSkill(skillId))
                                {
                                    // 男人：忽视 * 1.5 - 抗 + 100
                                    if (mb.Data.Race == Race.Ren && mb.Data.Sex == Sex.Male)
                                    {
                                        attrnum = sattrnum * 1.5f - tattrnum + 100;
                                    }
                                    else if (mb.Data.Race != Race.Ren)
                                    {
                                        // 仙，鬼，魔：忽视 * 1.35 - 抗 + 100
                                        attrnum = sattrnum * 1.35f - tattrnum + 100;
                                    }
                                }

                                var r = _random.Next(0, 100);
                                if (r < attrnum)
                                {
                                    mingzhong = (int) attrnum;
                                }
                                else
                                {
                                    mingzhong = 0;
                                }

                                // 衰减回合数(注意分母不能为0), 毒的回合数不衰减
                                if (mingzhong > 0 && tattrnum > sattrnum &&
                                    skill.Type == SkillType.Chaos || skill.Type == SkillType.Seal ||
                                    skill.Type == SkillType.Sleep || skill.Type == SkillType.Forget)
                                {
                                    // var delRound = (int) MathF.Ceiling((tattrnum - sattrnum) / sattrnum * round);
                                    // round -= delRound;
                                    // if (round < 1) round = 1;

                                    var fixRound = 0;

                                    if (trole.Data.Race == Race.Ren)
                                    {
                                        // 人 -> 人
                                        var delta = tattrnum - sattrnum;
                                        if (delta >= 80)
                                        {
                                            fixRound = _random.Next(1, 4);
                                        }
                                        else if (delta >= 60)
                                        {
                                            fixRound = _random.Next(2, 5);
                                        }
                                        else if (delta >= 40)
                                        {
                                            fixRound = _random.Next(3, 6);
                                        }
                                    }
                                    else
                                    {
                                        // 人 -> 仙、魔、鬼
                                        var delta = tattrnum - sattrnum;
                                        if (delta >= 50)
                                        {
                                            fixRound = _random.Next(1, 4);
                                        }
                                        else if (delta >= 45)
                                        {
                                            fixRound = _random.Next(2, 5);
                                        }
                                        else if (delta >= 40)
                                        {
                                            fixRound = _random.Next(3, 6);
                                        }
                                    }

                                    if (fixRound > 0)
                                    {
                                        if (fixRound > round) fixRound = round;
                                        round = fixRound;
                                    }

                                    if (round < 1) round = 1;
                                }
                            }

                            // 龙族 震击BUFF 100%命中
                            var isLongZhenJiBuff =
                                 skillId == SkillId.CangHaiHengLiu
                              || skillId == SkillId.BaiLangTaoTian
                              || skillId == SkillId.FeiJuJiuTian
                              || skillId == SkillId.WanQianHuaShen
                              || skillId == SkillId.LingXuYuFeng;
                            if ((mingzhong > 0 && round > 0) || isLongZhenJiBuff)
                            {
                                while (true)
                                {
                                    // 天外飞魔和乾坤借速对负敏单位无效
                                    if ((skillId is SkillId.TianWaiFeiMo or SkillId.QianKunJieSu) &&
                                        trole.Spd < 0)
                                    {
                                        break;
                                    }

                                    var buffEffect = effectData.Clone();
                                    // 龙族 物理伤害只有1次
                                    buffEffect.Hurt = isLongZhenJiBuff ? 0 : hurt;
                                    buffEffect.Round = round;
                                    var buff = new Buff(NextBuffId(), skill, buffEffect)
                                    {
                                        Source = mb.OnlyId,
                                        Probability = (uint) (mingzhong * 100),
                                        Percent = effectData.Percent
                                    };
                                    trole.AddBuff(buff);
                                    break;
                                }
                            }
                        }

                        // 天策符 回风落雁符 上次触发回合
                        // 被击倒或控制命中时，几率让敌方速度降低，持续3个回合，同一队伍两次触发至少间隔5个回合
                        if (SkillManager.IsKongZhiSkill(skillId) || trole.Dead)
                        {
                            var check_hfly = (trole.CampId == 1 && (_hfly_last_round_camp1 == 0 || (_round - _hfly_last_round_camp1) >= 5))
                            || (trole.CampId == 2 && (_hfly_last_round_camp2 == 0 || (_round - _hfly_last_round_camp2) >= 5));
                            if (trole.IsPlayer && check_hfly)
                            {
                                var fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.HuiFengLuoYan3);
                                var grade = 3;
                                if (fskill == null)
                                {
                                    fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.HuiFengLuoYan2);
                                    grade = 2;
                                    if (fskill == null)
                                    {
                                        fskill = trole.TianceFuSkills.GetValueOrDefault(SkillId.HuiFengLuoYan1);
                                        grade = 1;
                                    }
                                }
                                if (fskill != null && _random.Next(10000) >= (7500f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                                {
                                    var buffskill = SkillManager.GetSkill(SkillId.QinSiBingWu);
                                    var buffeffect = buffskill.GetEffectData(new GetEffectDataRequest()
                                    {
                                        Level = trole.Data.Level,
                                        Relive = trole.Relive,
                                        Intimacy = trole.Data.PetIntimacy,
                                        Member = trole,
                                        Profic = trole.GetSkillProfic(buffskill.Id)
                                    });
                                    buffeffect.SpdPercent = -(int)Math.Ceiling(20 + grade * 5f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.5f);
                                    buffeffect.Round = 3;
                                    var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = trole.OnlyId };
                                    mb.AddBuff(buff);
                                    if (trole.CampId == 1)
                                    {
                                        _hfly_last_round_camp1 = _round;
                                    }
                                    else
                                    {
                                        _hfly_last_round_camp2 = _round;
                                    }
                                }
                            }
                        }
                        // 觉醒技 黄泉一笑
                        // 物理攻击击倒目标时，有20%/40%/70%/100%概率为友方气血百分比最低的单位回复60%气血，可复活倒地单位，每回合最多触发2次。
                        if ((skillId is SkillId.NormalAtk or SkillId.BingLinChengXia) &&
                             trole.Dead &&
                             mb.CanUseJxSkill(SkillId.HuangQuanYiXiao) &&
                             mb.huang_quan_yi_xiao_times < 2)
                        {
                            var baseValues = new List<float>() { 200, 400, 700, 1000 };
                            var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                            var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                            if (_random.Next(1000) < calcValue)
                            {
                                BattleMember selected = null;
                                foreach (var (id, bm) in _members)
                                {
                                    if (bm.CampId == mb.CampId && bm.OnlyId != mb.OnlyId)
                                    {
                                        if (selected == null || (bm.Hp / bm.HpMax) < (selected.Hp / selected.HpMax))
                                        {
                                            selected = bm;
                                        }
                                    }
                                }
                                if (selected != null)
                                {
                                    mb.huang_quan_yi_xiao_times++;
                                    var hpAdd = selected.HpMax * 0.6f;
                                    if (selected.Dead)
                                    {
                                        selected.Dead = false;
                                        selected.RemoveAllBuff();
                                        selected.AddHp(hpAdd);
                                        tgs.Add(new BattleAttackData
                                        {
                                            OnlyId = selected.OnlyId,
                                            After = new BattleAttackAfter()
                                            {
                                                NiePan = new BattleNiePanData()
                                                {
                                                    Hp = selected.Hp,
                                                    Mp = selected.Mp,
                                                }
                                            }
                                        });
                                        addTime += 2;
                                    }
                                    else
                                    {
                                        selected.AddHp(hpAdd);
                                        tgs.Add(new BattleAttackData
                                        {
                                            OnlyId = selected.OnlyId,
                                            Type = BattleAttackType.Hp,
                                            Value = 0,
                                            Hp = selected.Hp,
                                            Mp = selected.Mp,
                                            Dead = selected.Dead,
                                            Buffs = { selected.GetBuffsSkillId() }
                                        });
                                    }
                                }
                            }
                        }
                        // 觉醒技 嫣然一笑
                        // 物理攻击击倒目标，有6%/12%/21%/30%概率为友方回复60%气血，可复活倒地单位。
                        if ((skillId is SkillId.NormalAtk or SkillId.BingLinChengXia) &&
                             trole.Dead &&
                             mb.CanUseJxSkill(SkillId.YanRanYiXiao))
                        {
                            var baseValues = new List<float>() { 60, 120, 210, 300 };
                            var rangeValue = baseValues[(int)mb.Data.PetJxGrade] - baseValues[(int)mb.Data.PetJxGrade - 1];
                            var calcValue = baseValues[(int)mb.Data.PetJxGrade - 1] + rangeValue * mb.Data.PetJxLevel / 6;
                            if (_random.Next(1000) < calcValue)
                            {
                                List<BattleMember> list = new();
                                foreach (var (id, bm) in _members)
                                {
                                    if (bm.CampId != mb.CampId || (bm.Hp / bm.HpMax) > 0.6) continue;
                                    list.Add(bm);
                                }
                                if (list.Count > 0) {
                                    var selected = list[list.Count > 1 ? _random.Next(list.Count) : 0];
                                    var hpAdd = selected.HpMax * 0.6f;
                                    if (selected.Dead)
                                    {
                                        selected.Dead = false;
                                        selected.RemoveAllBuff();
                                        selected.AddHp(hpAdd);
                                        tgs.Add(new BattleAttackData
                                        {
                                            OnlyId = selected.OnlyId,
                                            After = new BattleAttackAfter()
                                            {
                                                NiePan = new BattleNiePanData()
                                                {
                                                    Hp = selected.Hp,
                                                    Mp = selected.Mp,
                                                }
                                            }
                                        });
                                        addTime += 2;
                                    }
                                    else
                                    {
                                        selected.AddHp(hpAdd);
                                        tgs.Add(new BattleAttackData
                                        {
                                            OnlyId = selected.OnlyId,
                                            Type = BattleAttackType.Hp,
                                            Value = 0,
                                            Hp = selected.Hp,
                                            Mp = selected.Mp,
                                            Dead = selected.Dead,
                                            Buffs = { selected.GetBuffsSkillId() }
                                        });
                                    }
                                }
                            }
                        }

                        if (!trole.Dead)
                        {
                            // 修正普通攻击属性   混乱 封印
                            var fixAtkSkill = SkillId.Unkown;
                            if (skillId is SkillId.NormalAtk or SkillId.BingLinChengXia)
                            {
                                BaseSkill fixSkill = null;
                                if (mb.HasPassiveSkill(SkillId.HunLuan))
                                    fixSkill = SkillManager.GetSkill(SkillId.HunLuan);
                                if (mb.HasPassiveSkill(SkillId.FengYin))
                                    fixSkill = SkillManager.GetSkill(SkillId.FengYin);
                                if (fixSkill != null)
                                {
                                    var fixEffect = fixSkill.GetEffectData(new GetEffectDataRequest
                                    {
                                        Level = mb.Data.Level,
                                        Relive = mb.Relive,
                                        Intimacy = mb.Data.PetIntimacy,
                                        Member = mb
                                    });
                                    fixAtkSkill = fixEffect.SkillId;
                                }
                            }

                            if (fixAtkSkill != SkillId.Unkown)
                            {
                                var buffSkill = SkillManager.GetSkill(fixAtkSkill);
                                var buffskilleffect = buffSkill.GetEffectData(new GetEffectDataRequest
                                {
                                    Level = mb.Data.Level,
                                    Relive = mb.Relive,
                                    Intimacy = mb.Data.PetIntimacy,
                                    Member = mb,
                                });
                                buffskilleffect.Round = 1;
                                var buff = new Buff(NextBuffId(), buffSkill, buffskilleffect)
                                {
                                    Source = mb.OnlyId,
                                    Probability = 10000
                                };
                                trole.AddBuff(buff);
                            }

                            // 幻影-珍藏, 普通攻击时有10%的概率附带混乱效果, 对己方目标无效
                            if (skillId == SkillId.NormalAtk && trole.CampId != mb.CampId)
                            {
                                if (mb.OrnamentSkills.ContainsKey(9001) && !trole.HasBuff(SkillType.Chaos))
                                {
                                    if (_random.Next(0, 100) < 10)
                                    {
                                        var fixSkill = SkillManager.GetSkill(SkillId.HunLuan);
                                        var fixEffect = fixSkill.GetEffectData(new GetEffectDataRequest
                                        {
                                            Level = mb.Data.Level,
                                            Relive = mb.Relive,
                                            Intimacy = mb.Data.PetIntimacy,
                                            Member = mb,
                                        });
                                        fixEffect.Round = 1;
                                        var buff = new Buff(NextBuffId(), fixSkill, fixEffect)
                                        {
                                            Source = mb.OnlyId,
                                            Probability = 10000
                                        };
                                        trole.AddBuff(buff);
                                    }
                                }
                            }
                        }

                        var isdead = trole.Dead;
                        if (isdead)
                        {
                            // LogInfo($"吉人天相 检测是否生效 _round={_round}");
                            if (trole.JiRenTianXiang(_round))
                            {
                                // LogInfo("吉人天相 技能生效");
                                isdead = false;
                            }
                        }

                        attackData.Hp = trole.Hp;
                        attackData.Mp = trole.Mp;
                        attackData.Dead = isdead;
                        attackData.Buffs.AddRange(trole.GetBuffsSkillId());
                        if (isdead)
                        {
                            // 分花拂柳, 物理攻击时才有效
                            if (skillId is SkillId.NormalAtk or SkillId.BingLinChengXia &&
                                mb.FenHuaFuLiu())
                            {
                                var trole2 = FindRandomTeamTarget(trole.OnlyId);
                                if (trole2 != null && trindex == targetList.Count - 1 && fenhuatimes < 3)
                                {
                                    targetList.Add(trole2.OnlyId);
                                    fenhuatimes++;

                                    fenhuas[trole2.OnlyId] = true;
                                }
                            }

                            if (trole.IsPlayer)
                            {
                                // 孩子技能--返生香--这个孩子说话只有特殊处理，玩家涅槃时显示孩子说话
                                do {
                                var canRevive = trole.ChildSkillTargetNum(SkillId.FanShengXiang) > 0;
                                var skillNameFSX = GameDefine.ChildSkillId2Names[SkillId.FanShengXiang];
                                if (canRevive && !trole.UsedSkills.ContainsKey(SkillId.NiePan)) {
                                    // LogInfo($"玩家[{trole.Data.Id}][{trole.Data.Name}]触发[{skillNameFSX}]，复活一次");
                                    trole.Attrs.Set(AttrType.Hp, trole.HpMax);
                                    trole.Attrs.Set(AttrType.Mp, trole.MpMax);
                                    trole.RemoveAllBuff();
                                    trole.Dead = false;
                                    trole.UsedSkills.Add(SkillId.NiePan, 1);
                                    after.NiePan = new BattleNiePanData
                                    {
                                        Hp = trole.Hp,
                                        Mp = trole.Mp,
                                    };
                                    addTime += 2;
                                    break;
                                }
                                // 龙族 治愈技能--沛然莫御、泽被万物
                                if (trole.Dead)
                                {
                                    var longBuff = trole.GetLongBuffs();
                                    if (longBuff.Count > 0)
                                    {
                                        int addHp = 0;
                                        after.NiePan = new BattleNiePanData();
                                        foreach (var lb in longBuff)
                                        {
                                            addHp += (int)MathF.Ceiling(trole.HpMax * lb.EffectData.HpPercent / 100) + lb.EffectData.Hp;
                                            after.NiePan.Buffs.Add((uint)lb.SkillId);
                                        }
                                        trole.AddHp(Math.Min(addHp, trole.HpMax), _members);
                                        trole.RemoveAllBuff();
                                        trole.Dead = false;
                                        after.NiePan.Hp = trole.Hp;
                                        addTime += 2;
                                        break;
                                    }
                                }
                                // else
                                // {
                                //     LogInfo($"玩家[{trole.Data.Id}][{trole.Data.Name}]触发[{skillNameFSX}]，没有命中");
                                // }
                                if (_isPvp)
                                {
                                    // 作鸟兽散
                                    _members.TryGetValue(trole.PetOnlyId, out var pet);
                                    if (pet is {Dead: false} && pet.HasSkill(SkillId.ZuoNiaoShouSan))
                                    {
                                        var listmb = trole.CampId == 1 ? _camp1.Members : _camp2.Members;
                                        var petCount = listmb.Count(p => !p.Dead && p.Type == LivingThingType.Pet);
                                        if (petCount == 1)
                                        {
                                            // 玩家回血回蓝
                                            var zuoNiaoShouSan = new BattleZuoNiaoShouSanData();
                                            foreach (var xxmb in listmb)
                                            {
                                                if (xxmb.Type != LivingThingType.Player) continue;
                                                var addHp = MathF.Floor(xxmb.HpMax * 0.5f);
                                                var addMp = MathF.Floor(xxmb.MpMax * 0.5f);
                                                addHp = xxmb.AddHp(addHp, _members);
                                                addMp = xxmb.AddMp(addMp, _members);

                                                var item = new BattleZuoNiaoShouSanItem
                                                {
                                                    OnlyId = xxmb.OnlyId,
                                                    AddHp = addHp,
                                                    Hp = xxmb.Hp,
                                                    AddMp = (uint) addMp,
                                                    Mp = xxmb.Mp
                                                };
                                                zuoNiaoShouSan.List.Add(item);
                                            }

                                            // pet离场
                                            pet.RemoveAllBuff();
                                            OnPetLeave(pet.OnlyId);
                                            zuoNiaoShouSan.PetOnlyId = pet.OnlyId;
                                            after.ZuoNiaoShouSan = zuoNiaoShouSan;
                                        }
                                    }
                                }

                                // 安行疾斗, 主人第一次死亡后，立即清除福犬的异常状态
                                if (trole.Dead && trole.DeadTimes == 1)
                                {
                                    _members.TryGetValue(trole.PetOnlyId, out var pet);
                                    if (pet is {Dead: false, Pos: > 0} && pet.HasSkill(SkillId.AnXingJiDou))
                                    {
                                        pet.RemoveDeBuff();
                                        tgs.Add(new BattleAttackData
                                        {
                                            OnlyId = pet.OnlyId,
                                            Type = BattleAttackType.Hp,
                                            Value = 0,
                                            Hp = pet.Hp,
                                            Mp = pet.Mp,
                                            Dead = pet.Dead,
                                            Buffs = {pet.GetBuffsSkillId()}
                                        });
                                    }
                                }
                                break;
                                } while(true);
                            }
                            else if (trole.IsPet)
                            {
                                if (trole.NiePan())
                                {
                                    after.NiePan = new BattleNiePanData
                                    {
                                        Hp = trole.Hp,
                                        Mp = trole.Mp,
                                    };
                                    addTime += 2;
                                }
                                else
                                {
                                    // 设置 死亡宠物不可被召唤
                                    var ppos = trole.Pos;
                                    _members.TryGetValue(trole.OwnerOnlyId, out var owner);
                                    // 天策符 处理
                                    if (owner != null)
                                    {
                                        // 天策符 御兽符 护主符
                                        // 召唤兽离场恢复主人气血
                                        if (!owner.Dead)
                                        {
                                            var fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.HuZhu3);
                                            var grade = 3;
                                            if (fskill == null)
                                            {
                                                fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.HuZhu2);
                                                grade = 2;
                                                if (fskill == null)
                                                {
                                                    fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.HuZhu1);
                                                    grade = 1;
                                                }
                                            }
                                            if (fskill != null)
                                            {
                                                var addHp = owner.HpMax * (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                                owner.AddHp(addHp);
                                            }

                                            //遗产 召唤兽离场，恢复自己主人70%的蓝量
                                            if (trole.HasSkill(SkillId.YiChan))
                                            {
                                                var addMp = owner.MpMax * 0.7f;
                                                owner.AddMp(addMp);
                                                // LogInfo("遗产技能生效");
                                            }
                                        }
                                        // 天策符 御兽符 生机符
                                        // 召唤兽受到伤害被击杀离场时有概率释放一次低血量单体回血效果，可复活倒地单位，仅PVP前3回合生效
                                        if (_round <= 3 && _isPvp)
                                        {
                                            var fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.ShengJi3);
                                            var grade = 3;
                                            if (fskill == null)
                                            {
                                                fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.ShengJi2);
                                                grade = 2;
                                                if (fskill == null)
                                                {
                                                    fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.ShengJi1);
                                                    grade = 1;
                                                }
                                            }
                                            if (fskill != null && (_random.Next(10000) >= (7500f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel)))
                                            {
                                                var partener = FindRandomTeamTarget(trole.OnlyId, 0, true);
                                                if (partener != null)
                                                {
                                                    var pdead = partener.Dead;
                                                    var addHp = partener.HpMax * (0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f);
                                                    partener.AddHp(Math.Min(addHp, partener.HpMax));
#if false
                                                    partener.RemoveAllBuff();
                                                    partener.Dead = false;
#else
                                                    // 如果对象已经死亡，则使用涅槃效果拉起
                                                    // FIXME: 拉不起死亡单元，暂时在FindRandomTeamTarget里去掉到底单元
                                                    if (pdead)
                                                    {
                                                        partener.RemoveAllBuff();
                                                        partener.Dead = false;
                                                        var after1 = new BattleAttackAfter();
                                                        after1.NiePan = new BattleNiePanData
                                                        {
                                                            Hp = partener.Hp,
                                                            Mp = partener.Mp,
                                                        };
                                                        var attackData1 = new BattleAttackData { OnlyId = partener.OnlyId, After = after1 };
                                                        tgs.Add(attackData1);
                                                        addTime += 2;
                                                    }
#endif
                                                }
                                            }
                                        }
                                    }
                                    OnPetLeave(trole.OnlyId);

                                    // 寻找闪现宠物, 根据闪现
                                    if (owner != null)
                                    {
                                        // 找出属于owner的其他未上场未死亡的宠物集合
                                        var otherPets = _pets.Values.Where(p =>
                                            !p.Dead && !p.BeCache && p.Id != trole.OnlyId && p.Pos >= 0 &&
                                            p.OwnerOnlyId == owner.OnlyId).ToList();
                                        if (owner.Data.ShanXianOrdered)
                                        {
                                            // 闪现支援，排序
                                            otherPets.Sort((a, b) => (int) a.Data.SxOrder - (int) b.Data.SxOrder);
                                        }

                                        foreach (var v in otherPets)
                                        {
                                            var shanxianRet = v.ShanXian();
                                            // 没闪现，继续找闪现宠物
                                            if (shanxianRet == 1) continue;
                                            // 有闪现没出来，跳出闪现逻辑
                                            if (shanxianRet == 2) break;
                                            // 成功闪现
                                            if (shanxianRet == 0)
                                            {
                                                var petId = OnPetEnter(v.OnlyId, ppos);
                                                if (petId > 0)
                                                {
                                                    var enterEffect = LoadPetEnterEffect(v);
                                                    after.ShanXian = new BattleShanXianData
                                                    {
                                                        OnlyId = petId,
                                                        Hp = v.Hp,
                                                        Mp = v.Mp,
                                                        Pos = ppos,
                                                        Buffs = {v.GetBuffsSkillId()}
                                                    };
                                                    after.PetEnter = enterEffect;

                                                    // 本轮是否需要出手
                                                    var needInsertTurn = false;

                                                    foreach (var buffItem in enterEffect.Buffs)
                                                    {
                                                        if (buffItem.SkillId == SkillId.HenYuFeiFei)
                                                        {
                                                            needInsertTurn = true;

                                                            // 立即触发一次法术攻击
                                                            v.ResetRoundData();
                                                            v.ActionData.ActionType = BattleActionType.Skill;
                                                            v.ActionData.ActionId = (uint) SkillId.HenYuFeiFei;
                                                            v.IsAction = true;
                                                        }
                                                        else if (buffItem.SkillId == SkillId.JiQiBuYi)
                                                        {
                                                            needInsertTurn = true;

                                                            // 击其不意
                                                            v.ResetRoundData();
                                                            v.ActionData.ActionType = BattleActionType.Skill;
                                                            v.ActionData.ActionId = (uint) SkillId.NormalAtk;
                                                            v.IsAction = true;
                                                        }

                                                        if (buffItem.SkillId == SkillId.DangTouBangHe)
                                                        {
                                                            // 当头棒喝
                                                            foreach (var xmb in FindAllTeamMembers(petId))
                                                            {
                                                                xmb.RemoveDeBuff();
                                                            }
                                                        }
                                                        if (buffItem.SkillId == SkillId.XianFengDaoGu)
                                                        {
                                                            // 仙风道骨
                                                            var addHp = owner.HpMax * 0.7f;
                                                            var addMp = owner.MpMax * 0.1f;
                                                            if (owner.Dead) {
                                                                owner.Attrs.Set(AttrType.Hp, addHp);
                                                                owner.Attrs.Set(AttrType.Mp, addMp);
                                                                owner.RemoveAllBuff();
                                                                owner.Dead = false;
                                                                after.NiePan1 = new BattleNiePanData
                                                                {
                                                                    Hp = owner.Hp,
                                                                    Mp = owner.Mp,
                                                                };
                                                                addTime += 2;
                                                                // LogInfo("仙风道骨技能生效 owner.Dead -> false");
                                                            } else {
                                                                owner.AddHp(addHp);
                                                                owner.AddMp(addMp);
                                                            }
                                                            // LogInfo("仙风道骨技能生效 4680 ");
                                                        }
                                                    }

                                                    if (needInsertTurn)
                                                    {
                                                        // 插入turns可以出手攻击
                                                        var turnItem = new TurnItem
                                                        {
                                                            OnlyId = v.OnlyId,
                                                            Spd = v.Spd
                                                        };
                                                        if (turnIndex + 1 >= _turns.Count)
                                                            _turns.Add(turnItem);
                                                        else
                                                            _turns.Insert(turnIndex + 1, turnItem);
                                                    }

                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    // 套装技能 偷天换日-珍藏/无价
                    if (_isPvp && skill.Type == SkillType.Frighten &&
                        (mb.OrnamentSkills.ContainsKey(3002) || mb.OrnamentSkills.ContainsKey(3001)))
                    {
                        var hpPre = 0.01f;
                        var mpPre = 0.05f;
                        if (mb.OrnamentSkills.ContainsKey(3002))
                        {
                            hpPre = 0.02f;
                            mpPre = 0.1f;
                        }

                        var suckHp = MathF.Round(hurtPool.Sum() * hpPre);
                        var suckMp = MathF.Round(mpPool.Sum() * mpPre);
                        if (suckHp > 0 || suckMp > 0)
                        {
                            suckHp = mb.AddHp(suckHp, _members);
                            mb.AddMp(suckMp, _members);

                            tgs.Add(new BattleAttackData
                            {
                                OnlyId = mb.OnlyId,
                                Type = BattleAttackType.HpMp,
                                Value = (int) suckHp,
                                Hp = mb.Hp,
                                Mp = mb.Mp,
                                Dead = mb.Dead,
                                Buffs = {mb.GetBuffsSkillId()}
                            });
                        }
                    }

                    // 吸血, 血煞之蛊、吸星大法
                    if (skill.Type == SkillType.ThreeCorpse)
                    {
                        // 套装技能 起死回生-珍藏/无价
                        var suckRate = 3.0f; //原始回血倍率
                        if (mb.OrnamentSkills.ContainsKey(4001) || mb.OrnamentSkills.ContainsKey(4002))
                        {
                            var addRate = 1.0f;
                            if (mb.OrnamentSkills.ContainsKey(4002))
                            {
                                addRate += MathF.Floor(mb.Attrs.Get(AttrType.GenGu) / 100) * 0.1f;
                            }

                            suckRate += addRate;
                        }

                        // 魔王窟-红孩儿5-吸星大法
                        if (skillId == SkillId.XiXingDaFa && mb.IsMonster)
                        {
                            // 红孩儿强行修改吸血效果、骷髅王-嬴鱼
                            var xixue = mb.Data.CfgId switch
                            {
                                8105 => _random.Next(148000, 152000),
                                8115 => _random.Next(148000, 152000),
                                8125 => _random.Next(298000, 302000),
                                6702 => 100000,
                                _ => 0
                            };

                            if (xixue > 0)
                            {
                                suckRate = 1.0f;
                                for (var i = 0; i < hurtPool.Count; i++)
                                {
                                    hurtPool[i] = xixue;
                                }
                            }
                        }

                        // 智能加血，给队伍中血量最少的 几个人 加血。
                        var listmb = mb.CampId == 1 ? _camp1.Members : _camp2.Members;
                        listmb = listmb.Where(p => p.Pos > 0).ToList();
                        listmb.Sort((a, b) =>
                        {
                            var p1 = a.Hp * 1.0f / a.HpMax;
                            var p2 = b.Hp * 1.0f / b.HpMax;
                            var x = p1 - p2;
                            if (x > 0) return 1;
                            if (x == 0) return 0;
                            return -1;
                        });
                        hurtPool.Sort((a, b) =>
                        {
                            var x = b - a;
                            if (x > 0) return 1;
                            if (x == 0) return 0;
                            return -1;
                        });

                        // 地藏 反本修古 珍藏/无价
                        var addMpRate = 0f;
                        if (mb.OrnamentSkills.ContainsKey(4012))
                        {
                            addMpRate = 0.1f;
                            addMpRate += MathF.Floor(mb.Attrs.Get(AttrType.GenGu) / 100.0f) * 0.01f;
                        }
                        else if (mb.OrnamentSkills.ContainsKey(4011))
                        {
                            addMpRate = 0.1f;
                        }

                        var idx = 0;
                        foreach (var tmp in listmb)
                        {
                            if (tmp.IsPet && tmp.Dead) continue;
                            if (idx >= hurtPool.Count) break;
                            // 3倍吸血
                            var addHp = MathF.Floor(hurtPool[idx] * suckRate);
                            addHp = tmp.AddHp(addHp, _members);
                            var addMp = MathF.Floor(tmp.MpMax * addMpRate);
                            tmp.AddMp(addMp, _members);

                            idx++;

                            tgs.Add(new BattleAttackData
                            {
                                OnlyId = tmp.OnlyId,
                                Type = BattleAttackType.Suck,
                                Value = (int) addHp,
                                Hp = tmp.Hp,
                                Mp = tmp.Mp,
                                Dead = tmp.Dead,
                                Buffs = {tmp.GetBuffsSkillId()}
                            });
                        }
                    }

                    actionData.Targets.AddRange(tgs);

                    // 天降脱兔，不要在for(var trindex = 0)循环体内处理, 因为天降脱兔会降低目标血量，会影响真实normalatk攻击后的怪物血量
                    if (tianJiangTuoTu != null)
                    {
                        var tmpTargets = new List<uint>(tianJiangTuoTu.TargetNum);
                        FindRandomTarget(mb.OnlyId, tianJiangTuoTu.TargetNum, tmpTargets, 1, skill);
                        var xxtAttacks = new List<BattleAttackData>(tmpTargets.Count);
                        foreach (var xxt in tmpTargets)
                        {
                            _members.TryGetValue(xxt, out var xxtmb);
                            if (xxtmb is {Dead: false})
                            {
                                var tmpHurt = (int) MathF.Floor(tianJiangTuoTu.Hurt);
                                if (xxtmb.Hp >= 1000 && xxtmb.Skills.ContainsKey(SkillId.PiCaoRouHou))
                                    tmpHurt = (int) MathF.Floor(tmpHurt * 0.5f);
                                if (tmpHurt > xxtmb.Hp) tmpHurt = (int) xxtmb.Hp;
                                xxtmb.AddHp(-tmpHurt);

                                xxtAttacks.Add(new BattleAttackData
                                {
                                    OnlyId = xxtmb.OnlyId,
                                    Type = BattleAttackType.Hp,
                                    Value = -tmpHurt,
                                    Response = BattleResponseType.None,
                                    Dead = xxtmb.Dead,
                                    Hp = xxtmb.Hp,
                                    Mp = xxtmb.Mp,
                                    TianJiangTuoTu = true,
                                    Buffs = {xxtmb.GetBuffsSkillId()}
                                });
                            }
                        }

                        if (xxtAttacks.Count > 0)
                        {
                            var tmpActionData = new BattleActionData
                            {
                                OnlyId = mb.OnlyId,
                                Type = BattleActionType.Skill,
                                ActionId = (uint) SkillId.TianJiangTuoTu,
                                Before = new BattleActionBefore
                                {
                                    Hp = mb.Hp,
                                    Mp = mb.Mp,
                                    Dead = mb.Dead
                                },
                                Targets = {xxtAttacks},
                                Buffs = {mb.GetBuffsSkillId()}
                            };
                            nextActionDatas ??= new List<BattleActionData>();
                            nextActionDatas.Add(tmpActionData);
                        }
                    }
                    /*
                    // 幻影离魂
                    if (huanYingLiHun != null)
                    {
                        var tmpTargets = new List<uint>(huanYingLiHun.TargetNum);
                        FindRandomTarget(mb.OnlyId, huanYingLiHun.TargetNum, tmpTargets, 1, skill);
                        var xxtAttacks = new List<BattleAttackData>(tmpTargets.Count);
                        foreach (var xxt in tmpTargets)
                        {
                            _members.TryGetValue(xxt, out var xxtmb);
                            if (xxtmb is {Dead: false})
                            {
                                var tmpHurt = (int) MathF.Floor(huanYingLiHun.Hurt);
                                if (xxtmb.Hp >= 1000 && xxtmb.Skills.ContainsKey(SkillId.PiCaoRouHou))
                                    tmpHurt = (int) MathF.Floor(tmpHurt * 0.5f);
                                if (tmpHurt > xxtmb.Hp) tmpHurt = (int) xxtmb.Hp;
                                xxtmb.AddHp(-tmpHurt);

                                xxtAttacks.Add(new BattleAttackData
                                {
                                    OnlyId = xxtmb.OnlyId,
                                    Type = BattleAttackType.Hp,
                                    Value = -tmpHurt,
                                    Response = BattleResponseType.None,
                                    Dead = xxtmb.Dead,
                                    Hp = xxtmb.Hp,
                                    Mp = xxtmb.Mp,
                                    HuanYingLiHun = true,
                                    Buffs = {xxtmb.GetBuffsSkillId()}
                                });
                            }
                        }

                        if (xxtAttacks.Count > 0)
                        {
                            var tmpActionData = new BattleActionData
                            {
                                OnlyId = mb.OnlyId,
                                Type = BattleActionType.Skill,
                                ActionId = (uint) SkillId.HuanYingLiHun,
                                Before = new BattleActionBefore
                                {
                                    Hp = mb.Hp,
                                    Mp = mb.Mp,
                                    Dead = mb.Dead
                                },
                                Targets = {xxtAttacks},
                                Buffs = {mb.GetBuffsSkillId()}
                            };
                            nextActionDatas ??= new List<BattleActionData>();
                            nextActionDatas.Add(tmpActionData);
                        }
                    }
                    */
                }

                before.Hp = mb.Hp;
                before.Mp = mb.Mp;
                before.Dead = mb.Dead;
                actionData.Before = before;
                // 龙族
                if (buffid2self > 0)
                {
                    mb.RemoveBuff(buffid2self);
                }
                actionData.Buffs.AddRange(mb.GetBuffsSkillId());

                resp.List.Add(actionData);
                if (nextActionDatas is {Count: > 0})
                {
                    resp.List.AddRange(nextActionDatas);
                }

                if (runAway) break;
            }

            // 给一点时间用来播放这一回合的操作
            var dueTime = TimeSpan.FromSeconds(resp.List.Count * 1.9f + addTime + 1);
            _tempTimer?.Dispose();
            _tempTimer = RegisterTimer(EndRoundWrap, null, dueTime, TimeSpan.FromSeconds(1));

            Broadcast(GameCmd.S2CBattleRoundEnd, resp);
            await Task.CompletedTask;
        }

        private async Task EndRoundWrap(object _)
        {
            try
            {
                _tempTimer?.Dispose();
                _tempTimer = null;

                EndRound();
            }
            catch (Exception ex)
            {
                LogError($"结束回合出错[{ex.Message}][{ex.StackTrace}]");
                Draw();
            }

            await Task.CompletedTask;
        }

        private void EndRound()
        {
            foreach (var t in _turns)
            {
                _members.TryGetValue(t.OnlyId, out var mb);
                if (mb == null) continue;

                // 技能冷却减少
                foreach (var v in mb.Skills.Values)
                {
                    if (v.CoolDown > 0) v.CoolDown--;
                }
                // 标记删除
                var toRemove = new List<uint>();
                for (var i = mb.Buffs.Count - 1; i >= 0; i--)
                {
                    var buff = mb.Buffs[i];
                    buff.NextRound();

                    // 混乱、昏睡、封印如果概率不为10000，就有可能在结束回合的时候自动消失
                    if (buff.SkillType == SkillType.Chaos ||
                        buff.SkillType == SkillType.Sleep ||
                        buff.SkillType == SkillType.Seal)
                    {
                        if (buff.Probability != 10000)
                        {
                            var r = _random.Next(0, 10000);
                            if (buff.Probability < r)
                            {
                                // mb.RemoveBuff(buff.Id);
                                toRemove.Add(buff.Id);
                                continue;
                            }
                        }
                    }
                    // 龙族 治愈技能BUFF，应该在回合开始后删除
                    var isLongZhiYu = buff.SkillId == SkillId.PeiRanMoYu || buff.SkillId == SkillId.ZeBeiWanWu;
                    if (buff.IsEnd() && !isLongZhiYu)
                    {
                        // mb.RemoveBuff(buff.Id);
                         toRemove.Add(buff.Id);
                    }
                }
                foreach (var id in toRemove)
                {
                    mb.RemoveBuff(id);
                }

                // 重置Round数据
                mb.ResetRoundData();
            }

            // 开启下一回合
            StartRound();
        }

        // 根据Spd排序，spd越大越先出手, 由于宠物可以召唤，所以这里需要重新构建turn
        private void GenTurnList()
        {
            _turns.Clear();
            foreach (var mb in _members.Values)
            {
                _turns.Add(new TurnItem
                {
                    OnlyId = mb.OnlyId,
                    Spd = mb.Spd
                });
            }

            _turns.Sort((a, b) => b.Spd - a.Spd);
        }

        // 宠物进场特效
        private BattlePetEnterData LoadPetEnterEffect(BattleMember member)
        {
            var effect = new BattlePetEnterData();
            foreach (var (skId, _) in member.Skills)
            {
                // 如虎添翼
                if (skId == SkillId.RuHuTianYi)
                {
                    var item = effect.Buffs.FirstOrDefault(p => p.SkillId == skId);
                    if (item == null)
                    {
                        item = new BattlePetEnterBuffData {SkillId = skId};
                        effect.Buffs.Add(item);
                    }

                    var skill = SkillManager.GetSkill(skId);
                    var effectData = skill.GetEffectData(new GetEffectDataRequest
                    {
                        Level = member.Data.Level,
                        Relive = member.Relive,
                        Intimacy = member.Data.PetIntimacy,
                        Member = member,
                    });
                    var buff = new Buff(NextBuffId(), skill, effectData)
                        {Source = member.OnlyId, Probability = 10000};
                    member.AddBuff(buff);
                    item.Ids.Add(member.OnlyId);

                    _members.TryGetValue(member.OwnerOnlyId, out var owner);
                    if (owner is {Dead: false})
                    {
                        buff = new Buff(NextBuffId(), skill, effectData) {Probability = 10000};
                        owner.AddBuff(buff);
                        item.Ids.Add(owner.OnlyId);
                    }
                }

                if (_round != 0)
                {
                    if (skId == SkillId.HenYuFeiFei)
                    {
                        var item = effect.Buffs.FirstOrDefault(p => p.SkillId == skId);
                        if (item == null)
                        {
                            item = new BattlePetEnterBuffData {SkillId = skId};
                            effect.Buffs.Add(item);
                        }
                    }
                    else if (skId == SkillId.JiQiBuYi)
                    {
                        var item = effect.Buffs.FirstOrDefault(p => p.SkillId == skId);
                        if (item == null)
                        {
                            item = new BattlePetEnterBuffData {SkillId = skId};
                            effect.Buffs.Add(item);
                        }
                    }
                    else if (skId == SkillId.YinShen)
                    {
                        var item = effect.Buffs.FirstOrDefault(p => p.SkillId == skId);
                        if (item == null)
                        {
                            item = new BattlePetEnterBuffData {SkillId = skId};
                            effect.Buffs.Add(item);
                        }

                        var skill = SkillManager.GetSkill(skId);
                        var effectData = skill.GetEffectData(null);
                        var buff = new Buff(NextBuffId(), skill, effectData)
                            {Source = member.OnlyId, Probability = 10000};
                        member.AddBuff(buff);
                        item.Ids.Add(member.OnlyId);
                    }

                    if (skId == SkillId.DangTouBangHe)
                    {
                        var item = effect.Buffs.FirstOrDefault(p => p.SkillId == skId);
                        if (item == null)
                        {
                            item = new BattlePetEnterBuffData {SkillId = skId};
                            effect.Buffs.Add(item);
                        }
                    }

                    //仙风道骨
                    if (skId == SkillId.XianFengDaoGu)
                    {
                        var item = effect.Buffs.FirstOrDefault(p => p.SkillId == skId);
                        if (item == null)
                        {
                            item = new BattlePetEnterBuffData {SkillId = skId};
                            effect.Buffs.Add(item);
                        }
                    }
                }
            }

            return effect;
        }

        private byte CheckWin()
        {
            if (CheckTeamAllDie(_camp1.Members)) return 2;
            if (CheckTeamAllDie(_camp2.Members)) return 1;
            return 0;
        }

        private static bool CheckTeamAllDie(IEnumerable<BattleMember> members)
        {
            return members.All(mb => mb.Pos <= 0 || mb.Dead);
        }

        private Task TeamWinTimeout(object winTeam)
        {
            _tempTimer?.Dispose();
            _tempTimer = null;
            _runAwayTimer?.Dispose();
            _runAwayTimer = null;

            TeamWin((byte) winTeam);
            return Task.CompletedTask;
        }

        // 胜利
        private void TeamWin(byte camp)
        {
            foreach (var v in _players.Values)
            {
                if (v.IsPlayer)
                {
                    var win = v.CampId == camp ? 1 : 2;
                    v.ExitBattle(new ExitBattleRequest
                    {
                        Id = _battleId,
                        Type = _battleType,
                        Source = _startRequest.Source,
                        MonsterGroup = _startRequest.MonsterGroup,
                        Win = win,
                        StarLevel = _startRequest.StarLevel
                    });
                }
            }

            if (_battleType == BattleType.TianJiangLingHou)
            {
                var money = _lingHouInfo.StealMoney;
                if (_lingHouInfo.WinType == 1)
                {
                    // 猴子跑了
                    money = 0;
                    if (_round == 1) money = GameDefine.LingHouRetMoney;
                }
                else if (_lingHouInfo.WinType == 0)
                {
                    // 抓住了猴子
                    money *= 2;
                }

                var player = GetFirstPlayer();
                if (money > 0)
                {
                    player.AddMoney(MoneyType.Silver, (int) money, "教训灵猴奖励");
                    player.SendPacket(GameCmd.S2CNotice, new S2C_Notice
                    {
                        Text = $"你教训了灵猴，获得{money}银两"
                    });

                    if (money > 500000)
                    {
                        player.Broadcast(GameCmd.S2CChat, new S2C_Chat
                        {
                            Msg = new ChatMessage
                            {
                                Type = ChatMessageType.System,
                                Msg = $"{player.Data.Name} 教训灵猴，获得了 {money} 银两"
                            }
                        });
                    }
                }
                else if (_lingHouInfo.WinType == 1)
                {
                    player.SendPacket(GameCmd.S2CNotice, new S2C_Notice
                    {
                        Text = "灵猴跑了，啥都没捞着"
                    });
                }
            }

            ShutDown();
        }

        // 流局, 算输
        private void Draw()
        {
            if (_battleType is BattleType.SectWarArena or BattleType.SectWarCannon or BattleType.SectWarDoor or
                BattleType.SectWarFreePk or
                BattleType.SinglePk)
            {
                // 计算剩余血量比例最高者
                var totalHpMax1 = _camp1.Members.Sum(p => p.HpMax);
                var hp1 = _camp1.Members.Sum(p => p.Hp);
                var percent1 = hp1 * 1.0f / totalHpMax1;

                var totalHpMax2 = _camp2.Members.Sum(p => p.HpMax);
                var hp2 = _camp2.Members.Sum(p => p.Hp);
                var percent2 = hp2 * 1.0f / totalHpMax2;

                if (percent1 >= percent2)
                {
                    TeamWin(1);
                }
                else
                {
                    TeamWin(2);
                }

                return;
            }

            LogDebug("战斗流局");
            foreach (var v in _players.Values)
            {
                if (v.IsPlayer)
                {
                    v.ExitBattle(new ExitBattleRequest
                    {
                        Id = _battleId,
                        Type = _battleType,
                        Source = _startRequest.Source,
                        MonsterGroup = _startRequest.MonsterGroup,
                        Win = 0,
                        StarLevel = _startRequest.StarLevel
                    });
                }
            }

            ShutDown();
        }

        private void CheckStageEffect(BattleCamp camp)
        {
            if (!camp.Effects.ContainsKey(SkillId.HuaWu))
            {
                camp.Effects[SkillId.HuaWu] = new BattleStageEffect {Skill = SkillId.HuaWu};
            }

            if (!camp.Effects.ContainsKey(SkillId.XuanRen))
            {
                camp.Effects[SkillId.XuanRen] = new BattleStageEffect {Skill = SkillId.XuanRen};
            }

            if (!camp.Effects.ContainsKey(SkillId.YiHuan))
            {
                camp.Effects[SkillId.YiHuan] = new BattleStageEffect {Skill = SkillId.YiHuan};
            }

            foreach (var mb in camp.Members)
            {
                if (mb.Pos > 0)
                {
                    // 化无 仅PVP生效
                    if (_isPvp && mb.HasPassiveSkill(SkillId.HuaWu) && !camp.Effects[SkillId.HuaWu].Roles.Contains(mb.OnlyId))
                    {
                        camp.Effects[SkillId.HuaWu].Roles.Add(mb.OnlyId);
                        camp.Effects[SkillId.HuaWu].Hurt = 1;
                        camp.Effects[SkillId.HuaWu].Role = mb.OnlyId;

                        camp.Effects[SkillId.XuanRen].Roles.Add(mb.OnlyId);
                        camp.Effects[SkillId.YiHuan].Roles.Add(mb.OnlyId);
                    }

                    // 如果存在化无 就没有 遗患或者悬刃
                    if (camp.Effects[SkillId.HuaWu].Hurt == 0)
                    {
                        if ((mb.HasPassiveSkill(SkillId.QiangHuaXuanRen) || mb.HasPassiveSkill(SkillId.XuanRen)) &&
                            (camp.Effects[SkillId.XuanRen] == null ||
                             !camp.Effects[SkillId.XuanRen].Roles.Contains(mb.OnlyId)))
                        {
                            camp.Effects[SkillId.XuanRen].Roles.Add(mb.OnlyId);

                            var hurtBase = 100;
                            if (mb.HasPassiveSkill(SkillId.QiangHuaXuanRen)) hurtBase = 150;
                            // 等级 x 100
                            var hurt = mb.Data.Level * hurtBase;
                            if (hurt > camp.Effects[SkillId.XuanRen].Hurt)
                            {
                                camp.Effects[SkillId.XuanRen].Hurt = hurt;
                                camp.Effects[SkillId.XuanRen].Role = mb.OnlyId;
                            }
                        }

                        if ((mb.HasPassiveSkill(SkillId.QiangHuaYiHuan) || mb.HasPassiveSkill(SkillId.YiHuan)) &&
                            (camp.Effects[SkillId.YiHuan] == null ||
                             !camp.Effects[SkillId.YiHuan].Roles.Contains(mb.OnlyId)))
                        {
                            camp.Effects[SkillId.YiHuan].Roles.Add(mb.OnlyId);

                            var hurtbase = 100;
                            if (mb.HasPassiveSkill(SkillId.QiangHuaYiHuan)) hurtbase = 150;
                            // 等级 x 100
                            var hurt = mb.Data.Level * hurtbase;
                            if (hurt > camp.Effects[SkillId.YiHuan].Hurt)
                            {
                                camp.Effects[SkillId.YiHuan].Hurt = hurt;
                                camp.Effects[SkillId.YiHuan].Role = mb.OnlyId;
                            }
                        }
                    }
                }
            }
        }

        private IEnumerable<BattleStageEffect> GetStageEffect()
        {
            var list = new List<BattleStageEffect>();
            foreach (var (k, v) in _camp1.Effects)
            {
                if (v.Hurt > 0 && v.Role > 0)
                {
                    list.Add(new BattleStageEffect {Role = v.Role, Skill = k});
                }
            }

            foreach (var (k, v) in _camp2.Effects)
            {
                if (v.Hurt > 0 && v.Role > 0)
                {
                    list.Add(new BattleStageEffect {Role = v.Role, Skill = k});
                }
            }

            return list;
        }

        private static float HasStageEffect(BattleCamp camp, SkillId id)
        {
            if (camp.Effects.ContainsKey(id)) return camp.Effects[id].Hurt;
            return 0;
        }

        public static void SetStageEffect(BattleCamp camp, SkillId id, uint value)
        {
            camp.Effects.TryGetValue(id, out var eff);
            if (eff != null)
            {
                eff.Hurt = value;
                eff.Role = value;
            }
        }

        private static void AddStageEffect(BattleCamp camp, SkillId id, uint role, float value)
        {
            camp.Effects.TryGetValue(id, out var effect);
            if (effect == null)
            {
                effect = new BattleStageEffect {Skill = id};
                camp.Effects[id] = effect;
            }

            if (value > effect.Hurt)
            {
                effect.Role = role;
                if (!effect.Roles.Contains(role)) effect.Roles.Add(role);
                effect.Hurt = value;
            }
        }

        /// <summary>
        /// 尝试反转camp的StageEffect给对手
        /// </summary>
        private bool TryReverseStageEffect(BattleCamp camp)
        {
            var ret = false;

            var enemyCamp = _camp1 == camp ? _camp2 : _camp1;
            // 检查camp中是否有成员有扭转乾坤技能
            var fengBo =
                camp.Members.FirstOrDefault(p => !p.Dead && p.Pos > 0 && p.Skills.ContainsKey(SkillId.NiuZhuanQianKun));
            // 检查敌方camp是否有扭转乾坤
            var fengBo2 = enemyCamp.Members.FirstOrDefault(p =>
                !p.Dead && p.Pos > 0 && p.Skills.ContainsKey(SkillId.NiuZhuanQianKun));

            // 两方都有风伯的前提下, 不反转
            if (fengBo == null && fengBo2 == null || fengBo != null && fengBo2 != null) return false;

            // 第一轮或者宠物上场后的下一轮开始生效
            if (fengBo != null && (_round == 1 || _round - fengBo.EnterRound == 1))
            {
                // 检查对手camp是否有化无、悬刃、遗患
                var effectValue = HasStageEffect(enemyCamp, SkillId.HuaWu);
                if (effectValue > 0)
                {
                    // 反转
                    SetStageEffect(enemyCamp, SkillId.HuaWu, 0);
                    AddStageEffect(camp, SkillId.HuaWu, fengBo.OnlyId, effectValue);
                    ret = true;
                }

                effectValue = HasStageEffect(enemyCamp, SkillId.XuanRen);
                if (effectValue > 0)
                {
                    SetStageEffect(enemyCamp, SkillId.XuanRen, 0);
                    AddStageEffect(camp, SkillId.XuanRen, fengBo.OnlyId, effectValue);
                    ret = true;
                }

                effectValue = HasStageEffect(enemyCamp, SkillId.YiHuan);
                if (effectValue > 0)
                {
                    SetStageEffect(enemyCamp, SkillId.YiHuan, 0);
                    AddStageEffect(camp, SkillId.YiHuan, fengBo.OnlyId, effectValue);
                    ret = true;
                }
            }
            // 觉醒技 强化扭转乾坤
            // “扭转乾坤”触发时，有5%/10%/17.5%/25%概率进入隐身状态，持续2回合。
            if (ret && fengBo.CanUseJxSkill(SkillId.QiangHuaMuRuQingFeng))
            {
                var baseValues = new List<float>() { 50, 100, 175, 250 };
                var rangeValue = baseValues[(int)fengBo.Data.PetJxGrade] - baseValues[(int)fengBo.Data.PetJxGrade - 1];
                var calcValue = baseValues[(int)fengBo.Data.PetJxGrade - 1] + rangeValue * fengBo.Data.PetJxLevel / 6;
                if (_random.Next(1000) < calcValue)
                {
                    var skill = SkillManager.GetSkill(SkillId.YinShen);
                    var effect = skill.GetEffectData(null);
                    effect.Round = 2;
                    var buff = new Buff(NextBuffId(), skill, effect)
                    {
                        Source = fengBo.OnlyId,
                        Probability = 10000
                    };
                    fengBo.AddBuff(buff);
                }
            }

            return ret;
        }

        // 检测是否所有的单位都行动完毕
        private bool CheckIfAllAttacked()
        {
            foreach (var mb in _members.Values)
            {
                // 如果有任何上阵在线未死的单位还没有出手，就不能算完成
                if ((mb.IsPlayer || mb.IsPet) && mb.Pos > 0 && !mb.Dead && mb.Online && !mb.IsAction)
                {
                    // 机器人要自动出手
                    if (mb.IsPlayer && mb.Data.RoleType != 2)
                    {
                        return false;
                    }

                    if (mb.IsPet)
                    {
                        _members.TryGetValue(mb.OwnerOnlyId, out var owner);
                        if (owner != null && owner.Data.RoleType != 2) return false;
                    }
                }
            }

            return true;
        }

        private bool HasBb()
        {
            return _members.Values.Any(p => p.IsBb && !p.Dead);
        }

        private BattleMember GetFirstPlayer()
        {
            return _members.Values.FirstOrDefault(p => p.IsPlayer);
        }

        // 使用道具
        private List<BattleAttackData> OnMemberUseItem(BattleMember useMb, uint targetId, uint itemId)
        {
            var list = new List<BattleAttackData>();

            var itemEffect = ConfigService.GetMedicineEffect(itemId);
            _members.TryGetValue(targetId, out var targetMb);
            if (targetMb == null) return list;

            var attack = new BattleAttackData {OnlyId = targetId, Response = (BattleResponseType) itemId};
            var attackType = BattleAttackType.Unkown;
            var num = 0;

            if (targetMb.IsPet && targetMb.Dead) return list;

            var isHpOrMp = false;
            // 天策符 载物符 莲华符
            // 人物和召唤兽使用药品的治疗效果提高
            if (itemEffect.AddHp > 0
            || itemEffect.AddMp > 0
            || itemEffect.MulHp > 0
            || itemEffect.MulMp > 0)
            {
                isHpOrMp = true;
                var fskill = useMb.TianceFuSkills.GetValueOrDefault(SkillId.LianHua3, null);
                var grade = 3;
                if (fskill == null)
                {
                    fskill = useMb.TianceFuSkills.GetValueOrDefault(SkillId.LianHua2, null);
                    grade = 2;
                    if (fskill == null)
                    {
                        grade = 1;
                        fskill = useMb.TianceFuSkills.GetValueOrDefault(SkillId.LianHua1, null);
                    }
                }
                if (fskill != null)
                {
                    var addon = 0.07f + grade * 0.02f * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel + fskill.Addition * 0.001f;
                    itemEffect.AddHp = (int)(itemEffect.AddHp * (1 + addon));
                    itemEffect.AddMp = (int)(itemEffect.AddMp * (1 + addon));
                    itemEffect.MulHp = (int)(itemEffect.MulHp * (1 + addon));
                    itemEffect.MulMp = (int)(itemEffect.MulMp * (1 + addon));
                }
            }
            // 隐身药
            if (itemEffect.YinShen)
            {
                var skill = SkillManager.GetSkill(SkillId.YinShen);
                var effectData = skill.GetEffectData(null);
                var buff = new Buff(NextBuffId(), skill, effectData)
                {
                    Source = useMb.OnlyId,
                    Probability = 10000
                };
                targetMb.AddBuff(buff);
                var atkData = new BattleAttackData
                {
                    OnlyId = targetMb.OnlyId,
                    Dead = targetMb.Dead,
                    Hp = targetMb.Hp,
                    Mp = targetMb.Mp,
                    Buffs = {targetMb.GetBuffsSkillId()}
                };
                list.Add(atkData);
            }

            // 火眼真金, 破除隐身
            if (itemEffect.DYinShen)
            {
                // var team = targetMb.CampId == 1 ? _camp2.Members : _camp1.Members;
                // foreach (var tmb in team)
                // {
                //     var buff = tmb.GetBuffByMagicType(SkillType.YinShen);
                //     if (buff != null)
                //     {
                //         tmb.RemoveBuff(buff.Id);
                //         var atkData = new BattleAttackData
                //         {
                //             OnlyId = tmb.OnlyId,
                //             Dead = tmb.Dead,
                //             Hp = tmb.Hp,
                //             Mp = tmb.Mp,
                //             Buffs = {tmb.GetBuffsSkillId()}
                //         };
                //         list.Add(atkData);
                //     }
                // }
                // 火眼真金 只对选定目标作用
                if (targetMb.CampId != useMb.CampId)
                {
                    var buff = targetMb.GetBuffByMagicType(SkillType.YinShen);
                    if (buff != null)
                    {
                        targetMb.RemoveBuff(buff.Id);
                        var atkData = new BattleAttackData
                        {
                            OnlyId = targetMb.OnlyId,
                            Dead = targetMb.Dead,
                            Hp = targetMb.Hp,
                            Mp = targetMb.Mp,
                            Buffs = { targetMb.GetBuffsSkillId() }
                        };
                        list.Add(atkData);
                    }
                } else {
                    useMb.SendPacket(GameCmd.S2CNotice, new S2C_Notice
                    {
                        Text = "你想对队友干嘛？"
                    });
                }
            }

            if (itemEffect.AddHp > 0)
            {
                num = (int) targetMb.AddHp(itemEffect.AddHp, _members);
            }

            if (itemEffect.AddMp > 0)
            {
                var tmp = targetMb.AddMp(itemEffect.AddMp);
                if (num == 0) num = (int) tmp;
            }

            if (itemEffect.MulHp > 0)
            {
                var baseHp = targetMb.HpMax;
                var addHp = (int) MathF.Ceiling(baseHp * itemEffect.MulHp / 100f);
                // 能加多少算多少
                var oldHp = targetMb.Hp;
                targetMb.AddHp(addHp);
                num = (int) (targetMb.Hp - oldHp);
            }

            if (itemEffect.MulMp > 0)
            {
                var baseMp = targetMb.MpMax;
                var addMp = (int) MathF.Ceiling(baseMp * itemEffect.MulMp / 100f);
                // 能加多少算多少
                var oldMp = targetMb.Mp;
                targetMb.AddMp(addMp);
                if (num == 0) num = (int) (targetMb.Mp - oldMp);
            }

            if (itemEffect.AddHp > 0 || itemEffect.MulHp > 0)
            {
                attackType = BattleAttackType.Hp;
                if (targetMb.Dead) targetMb.Dead = false;
            }

            if (itemEffect.AddMp > 0 || itemEffect.MulMp > 0)
            {
                attackType = attackType == BattleAttackType.Unkown ? BattleAttackType.Mp : BattleAttackType.HpMp;
            }

            attack.Value = num;
            attack.Type = attackType;

            attack.Hp = targetMb.Hp;
            attack.Mp = targetMb.Mp;
            attack.Dead = targetMb.Dead;
            attack.Response = BattleResponseType.None;
            attack.Buffs.AddRange(targetMb.GetBuffsSkillId());

            list.Insert(0, attack);

            // 天策符 载物符 拈花符
            // 人物或召唤兽受到药品恢复时，一定几率让主人召唤兽同时受到相同恢复
            if (isHpOrMp && (targetMb.IsPlayer || targetMb.IsPet))
            {
                var fskill = useMb.TianceFuSkills.GetValueOrDefault(SkillId.NianHua3, null);
                var grade = 3;
                if (fskill == null)
                {
                    fskill = useMb.TianceFuSkills.GetValueOrDefault(SkillId.NianHua2, null);
                    grade = 2;
                    if (fskill == null)
                    {
                        grade = 1;
                        fskill = useMb.TianceFuSkills.GetValueOrDefault(SkillId.NianHua1, null);
                    }
                }
                if (fskill != null && _random.Next(10000) > (7500f - (grade * 500f + fskill.Addition * 50f) * (float)fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                {
                    BattleMember another = _members.GetValueOrDefault(targetMb.IsPlayer ? targetMb.PetOnlyId : targetMb.OwnerOnlyId, null);
                    if (another != null)
                    {
                        var attackAnother = new BattleAttackData { OnlyId = another.OnlyId, Response = (BattleResponseType)itemId };
                        var attackTypeAnother = BattleAttackType.Unkown;
                        var numAnother = 0;

                        if (itemEffect.AddHp > 0)
                        {
                            numAnother = (int)another.AddHp(itemEffect.AddHp, _members);
                        }

                        if (itemEffect.AddMp > 0)
                        {
                            var tmp = another.AddMp(itemEffect.AddMp);
                            if (numAnother == 0) numAnother = (int)tmp;
                        }

                        if (itemEffect.MulHp > 0)
                        {
                            var baseHp = another.HpMax;
                            var addHp = (int)MathF.Ceiling(baseHp * itemEffect.MulHp / 100f);
                            // 能加多少算多少
                            var oldHp = another.Hp;
                            another.AddHp(addHp);
                            numAnother = (int)(another.Hp - oldHp);
                        }

                        if (itemEffect.MulMp > 0)
                        {
                            var baseMp = another.MpMax;
                            var addMp = (int)MathF.Ceiling(baseMp * itemEffect.MulMp / 100f);
                            // 能加多少算多少
                            var oldMp = another.Mp;
                            another.AddMp(addMp);
                            if (numAnother == 0) numAnother = (int)(another.Mp - oldMp);
                        }

                        if (itemEffect.AddHp > 0 || itemEffect.MulHp > 0)
                        {
                            attackTypeAnother = BattleAttackType.Hp;
                            if (another.Dead) another.Dead = false;
                        }

                        if (itemEffect.AddMp > 0 || itemEffect.MulMp > 0)
                        {
                            attackTypeAnother = attackTypeAnother == BattleAttackType.Unkown ? BattleAttackType.Mp : BattleAttackType.HpMp;
                        }

                        attackAnother.Value = numAnother;
                        attackAnother.Type = attackTypeAnother;
                        attackAnother.Hp = another.Hp;
                        attackAnother.Mp = another.Mp;
                        attackAnother.Dead = another.Dead;
                        attackAnother.Response = BattleResponseType.None;
                        attackAnother.Buffs.AddRange(another.GetBuffsSkillId());

                        list.Add(attackAnother);
                    }
                }
            }

            return list;
        }

        private void RemoveMember(uint onlyId)
        {
            _members.Remove(onlyId, out var mb);
            if (mb != null)
            {
                _camp1.Members.Remove(mb);
                _camp2.Members.Remove(mb);
                var idx = _turns.FindIndex(p => p.OnlyId == onlyId);
                if (idx >= 0)
                {
                    // _turns.RemoveAt(idx);
                    _turns[idx].OnlyId = 0;
                }
            }
        }

        /// <summary>
        /// 召唤宠物上场
        /// </summary>
        private Tuple<uint, uint> Summon(uint ownerOnlyId, uint petOnlyId)
        {
            _members.TryGetValue(ownerOnlyId, out var owner);
            if (owner == null) return new Tuple<uint, uint>(0, 0);
            // 旧Pet离场
            var oldPetOnlyId = OnPetLeave(owner.PetOnlyId);
            // 新Pet进场
            var newPetOnlyId = OnPetEnter(petOnlyId, owner.Pos + 5);
            return new Tuple<uint, uint>(oldPetOnlyId, newPetOnlyId);
        }

        /// <summary>
        /// pet上场, 如果上场失败则返回0, 每个宠物只能上场一次
        /// </summary>
        private uint OnPetEnter(uint onlyId, int pos)
        {
            _pets.TryGetValue(onlyId, out var pet);
            if (pet == null || !pet.IsPet || pet.Dead || pet.BeCache) return 0;

            // 宠物上场
            pet.BeCache = true;
            pet.Pos = pos;
            pet.IsRoundAction = true;
            pet.EnterRound = _round;
            _pets.Remove(onlyId);
            _members.Add(onlyId, pet);

            _members.TryGetValue(pet.OwnerOnlyId, out var owner);
            if (owner != null)
            {
                owner.PetOnlyId = onlyId;
                pet.Online = owner.Online; // 同步Online

                // 天策符 御兽符 四海承风符
                // 新出场的召唤兽（不含首发）一定几率携带，加防、加速或加攻状态
                var fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.SiHaiChengFeng3, null);
                var grade = 3;
                if (fskill == null)
                {
                    fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.SiHaiChengFeng2, null);
                    grade = 2;
                    if (fskill == null)
                    {
                        grade = 1;
                        fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.SiHaiChengFeng1, null);
                    }
                }
                if (fskill != null)
                {
                    var rand = _random.Next(10000);
                    var skilllist = new List<SkillId>() { SkillId.MoShenHuTi, SkillId.TianWaiFeiMo, SkillId.ShouWangShenLi };
                    var buffskill = SkillManager.GetSkill(skilllist[_random.Next(skilllist.Count)]);
                    var buffeffect = buffskill.GetEffectData(new GetEffectDataRequest()
                    {
                        Level = pet.Data.Level,
                        Relive = pet.Relive,
                        Intimacy = pet.Data.PetIntimacy,
                        Member = pet,
                        Profic = pet.GetSkillProfic(buffskill.Id)
                    });
                    var baseAdd = (grade * 5.0 * fskill.Addition / GameDefine.TianCeFuMaxLevel) * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel;
                    var addPercent = (grade * 0.5 * fskill.Addition / GameDefine.TianCeFuMaxLevel) * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel;
                    buffeffect.FangYu = (float)Math.Floor((20 + baseAdd) * (pet.Data.Level * (0.35 + addPercent) * 3 / 100 + 1));
                    buffeffect.Spd = (int)Math.Floor((20 + baseAdd) * (pet.Data.Level * (0.35 + addPercent) * 3 / 100 + 1));
                    buffeffect.Atk = (int)Math.Floor((50 + baseAdd) * (pet.Data.Level * (0.35 + addPercent) * 3 / 100 + 1));
                    buffeffect.Round = 3;
                    var buff = new Buff(NextBuffId(), buffskill, buffeffect);
                    buff.Source = pet.OnlyId;
                    if (rand >= (5000 - grade * 1000 - 1000.0 * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel))
                    {
                        pet.AddBuff(buff);
                    }
                }
                // 第5回合后出战
                if (_round > 5)
                {
                    // 天策符 御兽符 怒击符
                    // 召唤兽蓄势待发，第N回合出战则攻击力增加
                    fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.NuJi3, null);
                    grade = 3;
                    if (fskill == null)
                    {
                        fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.NuJi2, null);
                        grade = 2;
                        if (fskill == null)
                        {
                            grade = 1;
                            fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.NuJi1, null);
                        }
                    }
                    if (fskill != null)
                    {
                        var skilllist = new List<SkillId>() { SkillId.MoShenHuTi, SkillId.TianWaiFeiMo, SkillId.ShouWangShenLi };
                        var buffskill = SkillManager.GetSkill(skilllist[_random.Next(skilllist.Count)]);
                        var buffeffect = buffskill.GetEffectData(new GetEffectDataRequest()
                        {
                            Level = pet.Data.Level,
                            Relive = pet.Relive,
                            Intimacy = pet.Data.PetIntimacy,
                            Member = pet,
                            Profic = pet.GetSkillProfic(buffskill.Id)
                        });
                        var baseAdd = (grade * (50.0 + (_round - 5) * 10) * fskill.Addition / GameDefine.TianCeFuMaxLevel) * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel;
                        var addPercent = (grade * 0.5 * fskill.Addition / GameDefine.TianCeFuMaxLevel) * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel;
                        buffeffect.Atk = (int)Math.Floor((500 + (_round - 5) * 100 + baseAdd) * (pet.Data.Level * (0.35 + addPercent) * 3 / 100 + 1));
                        buffeffect.Round = grade;
                        var buff = new Buff(NextBuffId(), buffskill, buffeffect);
                        buff.Source = pet.OnlyId;
                        pet.AddBuff(buff);
                    }
                    // 天策符 御兽符 气集符
                    // 召唤兽蓄势待发，第N回合出战则法力值增加
                    fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.QiJi3, null);
                    grade = 3;
                    if (fskill == null)
                    {
                        fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.QiJi2, null);
                        grade = 2;
                        if (fskill == null)
                        {
                            grade = 1;
                            fskill = owner.TianceFuSkills.GetValueOrDefault(SkillId.QiJi1, null);
                        }
                    }
                    if (fskill != null)
                    {
                        var skilllist = new List<SkillId>() { SkillId.MoShenHuTi, SkillId.TianWaiFeiMo, SkillId.ShouWangShenLi };
                        var buffskill = SkillManager.GetSkill(skilllist[_random.Next(skilllist.Count)]);
                        var buffeffect = buffskill.GetEffectData(new GetEffectDataRequest()
                        {
                            Level = pet.Data.Level,
                            Relive = pet.Relive,
                            Intimacy = pet.Data.PetIntimacy,
                            Member = pet,
                            Profic = pet.GetSkillProfic(buffskill.Id)
                        });
                        var baseAdd = (grade * (100.0 + (_round-5)*20) * fskill.Addition / GameDefine.TianCeFuMaxLevel) * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel;
                        var addPercent = (grade * 0.5 * fskill.Addition / GameDefine.TianCeFuMaxLevel) * fskill.TianYanCeLevel / GameDefine.TianYanCeMaxLevel;
                        buffeffect.Mp = (int)Math.Floor((10000 + (_round - 5) * 1000 + baseAdd) * (pet.Data.Level * (0.35 + addPercent) * 3 / 100 + 1));
                        buffeffect.Round = grade;
                        var buff = new Buff(NextBuffId(), buffskill, buffeffect);
                        buff.Source = pet.OnlyId;
                        pet.AddBuff(buff);
                    }
                }
            }

            // 觉醒技 拔刀相助
            // 召唤或闪现进场时为友方除自身外所有召唤兽增加命中，持续2回合，增加值等于自身命中率的3%/6%/10.5%/15%（上限30%命中率）。
            if (!pet.IsUsedJxSkill(SkillId.BaDaoXiangZhu) && pet.CanUseJxSkill(SkillId.BaDaoXiangZhu))
            {
                pet.UseJxSkill(SkillId.YuanZheShangGou);
                var baseValues = new List<float>() { 30, 60, 105, 150 };
                var rangeValue = baseValues[(int)pet.Data.PetJxGrade] - baseValues[(int)pet.Data.PetJxGrade - 1];
                var calcValue = baseValues[(int)pet.Data.PetJxGrade - 1] + rangeValue * pet.Data.PetJxLevel / 6;
                var addon = Math.Min(30, pet.Attrs.Get(AttrType.PmingZhong) * calcValue / 1000f);
                foreach (var (id, mb) in _members)
                {
                    if (!mb.IsPet || mb.Dead || mb.CampId != pet.CampId) continue;
                    if (mb.OnlyId == pet.OnlyId) continue;
                    // buff效果
                    var buffskill = SkillManager.GetSkill(SkillId.HanQingMoMo);
                    var buffeffect = new SkillEffectData();
                    buffeffect.MingZhong = addon;
                    buffeffect.Round = 2;
                    var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = pet.OnlyId };
                    mb.AddBuff(buff);
                }
            }
            // 觉醒技 点睛之笔
            // 召唤或闪现进场时，有5%/10%/17.5%/25%概率在友方除自身外随机召唤兽身上点下一滴墨点，持续2回合。
            // 该召唤兽行动前摆脱一层封混睡忘状态，并清除墨点效果。
            if (!pet.IsUsedJxSkill(SkillId.DianJingZhiBi) && pet.CanUseJxSkill(SkillId.DianJingZhiBi))
            {
                pet.UseJxSkill(SkillId.DianJingZhiBi);
                var baseValues = new List<float>() { 50, 100, 175, 250 };
                var rangeValue = baseValues[(int)pet.Data.PetJxGrade] - baseValues[(int)pet.Data.PetJxGrade - 1];
                var calcValue = baseValues[(int)pet.Data.PetJxGrade - 1] + rangeValue * pet.Data.PetJxLevel / 6;
                if (_random.Next(1000) < calcValue)
                {
                    List<BattleMember> list = new();
                    foreach (var (id, mb) in _members)
                    {
                        if (!mb.IsPet || mb.Dead || mb.CampId != pet.CampId || !mb.HasKongZhiBuff()) continue;
                        if (mb.OnlyId == pet.OnlyId) continue;
                        list.Add(mb);
                    }
                    // 随机一个召唤兽
                    if (list.Count > 0)
                    {
                        var mb = list[_random.Next(list.Count)];
                        mb.RemoveKongZhiBuff();
                    }
                }
            }
            // 觉醒技 空木葬花
            // 召唤或闪现进场时，有8%/16%/28%/40%概率清除我方一个未倒地人物单位的封混睡忘状态，
            // 有8%/16%/28%/40%概率清除敌方一个未倒地人物单位加防和加攻/加速状态中的随机一个。
            if (!pet.IsUsedJxSkill(SkillId.KongMuZangHua) && pet.CanUseJxSkill(SkillId.KongMuZangHua))
            {
                pet.UseJxSkill(SkillId.KongMuZangHua);
                var baseValues = new List<float>() { 80, 160, 280, 400 };
                var rangeValue = baseValues[(int)pet.Data.PetJxGrade] - baseValues[(int)pet.Data.PetJxGrade - 1];
                var calcValue = baseValues[(int)pet.Data.PetJxGrade - 1] + rangeValue * pet.Data.PetJxLevel / 6;
                // 有8%/16%/28%/40%概率清除我方一个未倒地人物单位的封混睡忘状态
                if (_random.Next(1000) < calcValue)
                {
                    List<BattleMember> list = new();
                    foreach (var (id, mb) in _members)
                    {
                        if (!mb.IsPet || mb.Dead || mb.CampId != pet.CampId || !mb.HasKongZhiBuff()) continue;
                        if (mb.OnlyId == pet.OnlyId) continue;
                        list.Add(mb);
                    }
                    // 随机一个召唤兽
                    if (list.Count > 0)
                    {
                        var mb = list[_random.Next(list.Count)];
                        mb.RemoveKongZhiBuff();
                    }
                }
                // 有8%/16%/28%/40%概率清除敌方一个未倒地人物单位加防和加攻/加速状态中的随机一个
                if (_random.Next(1000) < calcValue)
                {
                    List<BattleMember> list = new();
                    foreach (var (id, mb) in _members)
                    {
                        if (!mb.IsPet || mb.Dead || mb.CampId == pet.CampId || !mb.HasFGSBuff()) continue;
                        if (mb.OnlyId == pet.OnlyId) continue;
                        list.Add(mb);
                    }
                    // 随机一个召唤兽
                    if (list.Count > 0)
                    {
                        var mb = list[_random.Next(list.Count)];
                        mb.RemoveFGSBuff();
                    }
                }
            }
            // 觉醒技 八面玲珑
            // 增加召唤兽速度。召唤或闪现进场时有20%/40%/70%/100%概率增加自身最高一项属性点获得对应加成状态，持续3回合；
            // 最高敏捷:加速；最高力量:加攻；最高根骨或灵性:加防。
            if (pet.CanUseJxSkill(SkillId.BaMianLingLong))
            {
                pet.UseJxSkill(SkillId.KongMuZangHua);
                var baseValues = new List<float>() { 200, 400, 700, 1000 };
                var rangeValue = baseValues[(int)pet.Data.PetJxGrade] - baseValues[(int)pet.Data.PetJxGrade - 1];
                var calcValue = baseValues[(int)pet.Data.PetJxGrade - 1] + rangeValue * pet.Data.PetJxLevel / 6;
                if (_random.Next(1000) < calcValue)
                {
                    AttrType type = AttrType.Unkown;
                    float value = 0;
                    List<AttrType> valid = new() { AttrType.MinJie, AttrType.LiLiang, AttrType.GenGu, AttrType.LingXing };
                    foreach (var (k, v) in pet.Attrs)
                    {
                        if (valid.Contains(k) && v > value)
                        {
                            type = k;
                            value = v;
                        }
                    }
                    if (type != AttrType.Unkown)
                    {
                        // 加速
                        if (type == AttrType.MinJie)
                        {
                            var buffskill = SkillManager.GetSkill(SkillId.QianKunJieSu);
                            var buffeffect = new SkillEffectData();
                            buffeffect.SpdPercent = 20;
                            buffeffect.Round = 3;
                            var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = pet.OnlyId };
                            pet.AddBuff(buff);
                        }
                        // 加攻
                        if (type == AttrType.LiLiang)
                        {
                            var buffskill = SkillManager.GetSkill(SkillId.ShouWangShenLi);
                            var buffeffect = new SkillEffectData();
                            buffeffect.AtkPercent = 20;
                            buffeffect.Round = 3;
                            var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = pet.OnlyId };
                            pet.AddBuff(buff);
                        }
                        // 加防
                        if (type == AttrType.GenGu || type == AttrType.LingXing)
                        {
                            var buffskill = SkillManager.GetSkill(SkillId.HanQingMoMo);
                            var buffeffect = new SkillEffectData();
                            buffeffect.FangYu = 20;
                            buffeffect.Round = 3;
                            var buff = new Buff(NextBuffId(), buffskill, buffeffect) { Source = pet.OnlyId };
                            pet.AddBuff(buff);
                        }
                    }
                }
            }
            return onlyId;
        }

        /// <summary>
        /// pet离场, 如果离场失败则返回0
        /// </summary>
        private uint OnPetLeave(uint onlyId)
        {
            _members.TryGetValue(onlyId, out var pet);
            if (pet == null || !pet.IsPet) {
                // LogInfo($"没找到宠物[{onlyId}]");
                return 0;
            }

            // 将死
            if (pet.HasSkill(SkillId.JiangSi))
            {
                var teams = FindAllTeamMembers(onlyId);
                foreach (var xbm in teams)
                {
                    xbm.RemoveDeBuff();
                }
            }

            // 从turns中移除
            var idx = _turns.FindIndex(p => p.OnlyId == onlyId);
            if (idx >= 0)
            {
                // 不能直接remove, 否则有可能会导致被删除的索引位置无法
                // _turns.RemoveAt(idx);
                _turns[idx].OnlyId = 0;
            }

            pet.Pos = -1;
            _pets.Add(onlyId, pet);
            _members.Remove(onlyId);

            _members.TryGetValue(pet.OwnerOnlyId, out var owner);
            if (owner != null) owner.PetOnlyId = 0;

            return onlyId;
        }

        // mod == 1 敌人  2 自己人 3 全体
        private void FindRandomTarget(uint onlyId, int needNum, IList<uint> list, byte mod,
            BaseSkill skill = null)
        {
            if (list.Count == needNum) return;
            _members.TryGetValue(onlyId, out var role);
            if (role == null) return;
            var tid = role.CampId;

            var team = new List<BattleMember>();
            var enemyTeam = new List<BattleMember>();
            var selfTeam = new List<BattleMember>();

            if (tid == 1)
            {
                enemyTeam = _camp2.Members;
                selfTeam = _camp1.Members;
            }
            else if (tid == 2)
            {
                enemyTeam = _camp1.Members;
                selfTeam = _camp2.Members;
            }

            if (mod == 1) team = enemyTeam;
            else if (mod == 2) team = selfTeam;
            else if (mod == 3)
            {
                team.AddRange(enemyTeam);
                team.AddRange(selfTeam);
            }

            var tmpList = new List<BattleMember>();
            foreach (var xmb in team)
            {
                if (xmb.Pos <= 0) continue;
                if (mod == 1 || mod == 3)
                {
                    // 不能选择自己为目标
                    if (xmb.OnlyId == onlyId) continue;
                    // 过滤已死的
                    if (xmb.Dead) continue;
                    // 过滤 隐身的
                    if (xmb.HasBuff(SkillType.YinShen) && (skill == null || skill.Id != SkillId.PoYin))
                    {
                        var needContinue = true;
                        // 霄汉 干霄凌云 珍藏/无价
                        if (skill != null && (SkillManager.IsXianFa(skill.Type) || skill.Type == SkillType.GhostFire))
                        {
                            var ret = 0;
                            if (role.OrnamentSkills.ContainsKey(2042)) ret = 100;
                            else if (role.OrnamentSkills.ContainsKey(2041)) ret = 50;
                            if (ret > 0 && _random.Next(0, 100) < ret)
                            {
                                needContinue = false;
                            }
                        }

                        if (needContinue) continue;
                    }
                }

                if (mod == 2)
                {
                    if (skill != null && skill.Type != SkillType.Resume)
                    {
                        if (xmb.Dead) continue;
                    }
                    // 龙族 治愈技能 不对倒地目标生效
                    if (skill.Id == SkillId.PeiRanMoYu || skill.Id == SkillId.ZeBeiWanWu)
                    {
                        if (xmb.Dead) continue;
                    }
                }
                if (!list.Contains(xmb.OnlyId))
                {
                    tmpList.Add(xmb);
                }
            }

            if (tmpList.Count > 0)
            {
                if (mod == 2)
                {
                    if (tmpList.Count > 1) tmpList.Sort((a, b) => a.Spd - b.Spd);
                }
                else
                {
                    if (tmpList.Count > 1)
                    {
                        // 打乱顺序
                        for (var i = 0; i < tmpList.Count; i++)
                        {
                            var idx = _random.Next(0, tmpList.Count);
                            if (idx != i)
                            {
                                var tmp = tmpList[i];
                                tmpList[i] = tmpList[idx];
                                tmpList[idx] = tmp;
                            }
                        }

                        // tmpList.Sort((a, b) => _random.Next(0, 100) > 40 ? -1 : 1);
                    }
                }

                // 优先选择没有中 技能的人。
                foreach (var xmb in tmpList)
                {
                    if (xmb.HasBuff(SkillType.Seal)) continue;
                    if (skill != null && skill.Type != SkillType.Physics)
                    {
                        if (xmb.HasBuff(skill.Type)) continue;
                    }

                    if (list.Count >= needNum) break;
                    if (!list.Contains(xmb.OnlyId))
                    {
                    list.Add(xmb.OnlyId);
                    }
                }

                // 补选人数
                if (list.Count < needNum)
                {
                    foreach (var xmb in tmpList)
                    {
                        if (xmb.HasBuff(SkillType.Seal)) continue;
                        if (list.Count >= needNum) break;
                        if (!list.Contains(xmb.OnlyId))
                        {
                            list.Add(xmb.OnlyId);
                        }
                    }
                }
            }
        }

        // findtype = 0 找同队中 非自己的 随机一个人
        // findtype = 1 找敌队中 随机一个人
        // findShenJi=true 检查生机符
        private BattleMember FindRandomTeamTarget(uint onlyId, byte findType = 0, bool findShenJi = false)
        {
            _members.TryGetValue(onlyId, out var role);
            if (role == null) return null;
            var team = role.CampId == 1 ? _camp1.Members : _camp2.Members;
            if (team.Count <= 1) return null;

            bool hasShengji = false;
            if (findShenJi && role.IsPet)
            {
                var owner = _members.GetValueOrDefault(role.OwnerOnlyId, null);
                hasShengji = owner != null && (owner.TianceFuSkills.ContainsKey(SkillId.ShengJi1)
                           || owner.TianceFuSkills.ContainsKey(SkillId.ShengJi2)
                           || owner.TianceFuSkills.ContainsKey(SkillId.ShengJi3));
            }

            var tmpTeam = new List<BattleMember>();
            foreach (var mb in team)
            {
                if (findType == 0 && mb.OnlyId == onlyId) continue;
                // 天策符 生机符 御兽符
                // 召唤兽受到伤害被击杀离场时有概率释放一次低血量单体回血效果，可复活倒地单位，仅PVP前3回合生效
                if (hasShengji && mb.IsPlayer && !mb.Dead && !mb.HasBuff(SkillType.Seal))
                {
                    tmpTeam.Add(mb);
                    continue;
                }
                if (mb.Dead || mb.Pos <= 0 || mb.HasBuff(SkillType.Seal) || mb.HasBuff(SkillType.YinShen)) continue;
                tmpTeam.Add(mb);
            }

            if (tmpTeam.Count <= 0) return null;
            var idx = _random.Next(0, tmpTeam.Count);
            return tmpTeam[idx];
        }

        private List<BattleMember> FindAllTeamMembers(uint onlyId, bool includeSelf = false)
        {
            var res = new List<BattleMember>();

            _members.TryGetValue(onlyId, out var role);
            if (role == null) return res;

            var team = role.CampId == 1 ? _camp1.Members : _camp2.Members;
            foreach (var mb in team)
            {
                if (mb.Dead) continue;
                if (includeSelf || mb.OnlyId != onlyId) res.Add(mb);
            }

            return res;
        }

        private bool CheckEnemyHasSkill(uint onlyId, SkillId skid)
        {
            var res = false;

            _members.TryGetValue(onlyId, out var role);
            if (role == null) return false;

            var team = role.CampId == 1 ? _camp2.Members : _camp1.Members;
            foreach (var xmb in team)
            {
                if (!xmb.Dead && xmb.HasSkill(skid))
                {
                    res = true;
                    break;
                }
            }

            return res;
        }

        // 发送战斗开始的数据
        private void SendBattleStart(uint onlyId)
        {
            _members.TryGetValue(onlyId, out var mb);
            if (mb == null || !mb.IsPlayer) return;

            var resp = new S2C_BattleStart
            {
                BattleId = _battleId,
                OnlyId = onlyId,
                Team1 = new BattleTeamData(),
                Team2 = new BattleTeamData()
            };
            if (mb.CampId == 1)
            {
                resp.Team1.Camp = 1;
                foreach (var x in _camp1.Members)
                {
                    resp.Team1.List.Add(x.BuildObjectData());
                }
                // 标记1队是否有星阵
                if (_xingzhen1 != null)
                {
                    resp.Team1.XingzhenId = _xingzhen1.Id;
                    resp.Team1.XingzhenLevel = _xingzhen1.Level;
                }

                resp.Team2.Camp = 2;
                foreach (var x in _camp2.Members)
                {
                    resp.Team2.List.Add(x.BuildObjectData());
                }
                // 标记2队是否有星阵
                if (_xingzhen2 != null)
                {
                    resp.Team2.XingzhenId = _xingzhen2.Id;
                    resp.Team2.XingzhenLevel = _xingzhen2.Level;
                }
            }
            else
            {
                resp.Team1.Camp = 1;
                foreach (var x in _camp2.Members)
                {
                    resp.Team1.List.Add(x.BuildObjectData());
                }
                // 标记1队是否有星阵
                if (_xingzhen2 != null)
                {
                    resp.Team1.XingzhenId = _xingzhen2.Id;
                    resp.Team1.XingzhenLevel = _xingzhen2.Level;
                }

                resp.Team2.Camp = 2;
                foreach (var x in _camp1.Members)
                {
                    resp.Team2.List.Add(x.BuildObjectData());
                }
                // 标记2队是否有星阵
                if (_xingzhen1 != null)
                {
                    resp.Team2.XingzhenId = _xingzhen1.Id;
                    resp.Team2.XingzhenLevel = _xingzhen1.Level;
                }
            }

            mb.SendPacket(GameCmd.S2CBattleStart, resp);
        }

        // 发送战斗开始的数据
        private void SendBattleStart2Watcher(uint roleId)
        {
            _players.TryGetValue(roleId, out var mb);
            if (mb == null || !mb.IsPlayer) return;

            var resp = new S2C_BattleStart
            {
                BattleId = _battleId,
                OnlyId = 0,
                Team1 = new BattleTeamData(),
                Team2 = new BattleTeamData()
            };
            if (mb.CampId == 1)
            {
                resp.Team1.Camp = 1;
                foreach (var x in _camp1.Members)
                {
                    resp.Team1.List.Add(x.BuildObjectData());
                }
                // 标记1队是否有星阵
                if (_xingzhen1 != null)
                {
                    resp.Team1.XingzhenId = _xingzhen1.Id;
                    resp.Team1.XingzhenLevel = _xingzhen1.Level;
                }

                resp.Team2.Camp = 2;
                foreach (var x in _camp2.Members)
                {
                    resp.Team2.List.Add(x.BuildObjectData());
                }
                // 标记2队是否有星阵
                if (_xingzhen2 != null)
                {
                    resp.Team2.XingzhenId = _xingzhen2.Id;
                    resp.Team2.XingzhenLevel = _xingzhen2.Level;
                }
            }
            else
            {
                resp.Team1.Camp = 1;
                foreach (var x in _camp2.Members)
                {
                    resp.Team1.List.Add(x.BuildObjectData());
                }
                // 标记1队是否有星阵
                if (_xingzhen2 != null)
                {
                    resp.Team1.XingzhenId = _xingzhen2.Id;
                    resp.Team1.XingzhenLevel = _xingzhen2.Level;
                }

                resp.Team2.Camp = 2;
                foreach (var x in _camp1.Members)
                {
                    resp.Team2.List.Add(x.BuildObjectData());
                }
                // 标记2队是否有星阵
                if (_xingzhen1 != null)
                {
                    resp.Team2.XingzhenId = _xingzhen1.Id;
                    resp.Team2.XingzhenLevel = _xingzhen1.Level;
                }
            }

            mb.SendPacket(GameCmd.S2CBattleStart, resp);
        }

        // 发送给OnlyId所在阵营的所有Player
        private void BroadcastCamp(uint onlyId, GameCmd command, IMessage msg)
        {
            // 通过onlyId查找campId
            _members.TryGetValue(onlyId, out var mb);
            if (mb == null) return;
            var campId = mb.CampId;

            foreach (var v in _players.Values)
            {
                if (v.IsPlayer && v.CampId == campId)
                {
                    v.SendPacket(command, msg);
                }
            }
        }

        // 广播给所有上场的Player
        private void Broadcast(GameCmd command, IMessage msg)
        {
            foreach (var v in _players.Values)
            {
                v.SendPacket(command, msg);
            }
        }

        // 广播给所有上场的Player
        private void Broadcast(Immutable<byte[]> bytes)
        {
            foreach (var v in _players.Values)
            {
                v.SendPacket(bytes);
            }
        }

        private void LogInfo(string msg)
        {
            _logger?.LogInformation($"战斗[{_battleId}]:{msg}");
        }

        private void LogDebug(string msg)
        {
            _logger?.LogDebug($"战斗[{_battleId}]:{msg}");
        }

        private void LogError(string msg)
        {
            _logger?.LogError($"战斗[{_battleId}]:{msg}");
        }

        private uint NextOnlyId() => ++_onlyId;

        private uint NextBuffId() => ++_buffId;
    }

    public class BattleCamp
    {
        // 1 强化悬刃 2 强化遗患
        public Dictionary<SkillId, BattleStageEffect> Effects = new Dictionary<SkillId, BattleStageEffect>();

        // 阵营内所有单位，包含当前未上阵的宠物
        public List<BattleMember> Members = new List<BattleMember>();
    }

    public class TurnItem
    {
        public uint OnlyId;
        public int Spd;
    }

    public class LingHouInfo
    {
        public uint StealMoney;
        public byte WinType; // 0-猴子死了，1-猴子跑了
    }
}