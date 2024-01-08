using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Config;
using Ddxy.GameServer.Grains;
using Ddxy.GameServer.Logic.Aoi;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.XTask
{
    public class XTask
    {
        public uint CfgId { get; set; }

        public uint Step { get; set; }

        public TaskConfig Config { get; set; }

        public TaskKind Kind => (TaskKind) Config.Kind;

        public uint Group => Config.Group;

        public List<TaskEventData> Events { get; set; }

        public List<TaskFailEventData> FailEvents { get; set; }

        public TaskState State { get; set; }

        private PlayerGrain _player;

        public XTask(PlayerGrain player)
        {
            _player = player;
        }

        public XTask(PlayerGrain player, uint cfgId, uint step)
        {
            _player = player;
            CfgId = cfgId;
            Step = step;

            Config = ConfigService.Tasks[cfgId];
            State = TaskState.Unkown;

            Events = new List<TaskEventData>(10);
            for (var i = 0; i < Config.Events.Length; i++)
            {
                var eventCfg = Config.Events[i];
                var eventData = new TaskEventData
                {
                    Type = (TaskEventType) eventCfg.Type,
                    State = i < Step ? TaskState.Done : TaskState.Unkown
                };

                // 记录目的地
                if (eventData.Type == TaskEventType.ArriveArea)
                {
                    eventData.MapId = eventCfg.Map.GetValueOrDefault();
                    eventData.MapX = eventCfg.X.GetValueOrDefault();
                    eventData.MapY = eventCfg.Y.GetValueOrDefault();
                }

                Events.Add(eventData);
            }

            FailEvents = new List<TaskFailEventData>();
            foreach (var cfg in Config.FailEvents)
            {
                var type = (TaskEventType) cfg.Type;
                if (TaskEventType.FailEventDead == type)
                {
                    FailEvents.Add(new TaskFailEventData {Type = type, DeadNum = 0});
                }
                else if (TaskEventType.FailEventTimeout == type)
                {
                    FailEvents.Add(new TaskFailEventData {Type = type, StartTime = TimeUtil.TimeStamp});
                }
            }

            if (Step >= Config.Events.Length)
            {
                State = TaskState.Done;
            }
        }

        public async Task Refresh()
        {
            for (var i = 0; i < Events.Count; i++)
            {
                var evt = Events[i];

                if (evt.State == TaskState.Done) continue;
                if (evt.State == TaskState.Doing)
                {
                    // 说明该任务有正在执行的步骤, 所以直接break循环
                    break;
                }

                // 标记为开始
                evt.State = TaskState.Doing;
                Step = (byte) i;

                // 队员不允许创建Npc
                if (_player.InTeam && !_player.IsTeamLeader && !_player._teamLeave) return;

                var stepCfg = ConfigService.GetEventStep(CfgId, (uint) i);
                if (stepCfg.CreateNpc != null)
                {
                    var owner = new NpcOwner();
                    // 队伍
                    if (XTaskManager.IsTeamTask(CfgId))
                    {
                        owner.Type = NpcOwnerType.Team;
                        owner.Value = _player.TeamId;
                    }
                    else
                    {
                        owner.Type = NpcOwnerType.Player;
                        owner.Value = _player.Entity.Id;
                    }

                    // 由于任务需要, 在Server中创建一个动态Npc
                    var onlyId = await _player.CreateNpc(new CreateNpcRequest
                    {
                        MapId = stepCfg.CreateNpc.Map,
                        MapX = stepCfg.CreateNpc.X,
                        MapY = stepCfg.CreateNpc.Y,
                        CfgId = stepCfg.CreateNpc.Npc,
                        Owner = owner
                    });
                    evt.Npcs.Add(new DynamicNpcData
                    {
                        OnlyId = onlyId,
                        CfgId = stepCfg.CreateNpc.Npc
                    });
                }
                else if (stepCfg.Npc.HasValue)
                {
                    var onlyId = await _player.ServerGrain.FindOnlyIdWithNpcCfgId(stepCfg.Npc.Value);
                    if (onlyId == 0)
                    {
                        // 这里就说明系统Npc并没有伴随地图AutoCreate, 有问题，但是也得return,否则会产生很多个Doing状态的Event
                        return;
                    }

                    evt.Npcs.Add(new DynamicNpcData
                    {
                        OnlyId = onlyId,
                        CfgId = stepCfg.Npc.Value
                    });
                }

                if (stepCfg.Type == (byte) TaskEventType.KillNpc && stepCfg.AutoTrigle.GetValueOrDefault() &&
                    evt.Npcs.Count > 0 && _player.TaskMgr.CanAutoFight)
                {
                    var ret = await _player.ServerGrain.ExistsNpc(evt.Npcs[0].OnlyId);
                    if (!ret) return;

                    ConfigService.Npcs.TryGetValue(evt.Npcs[0].CfgId, out var npcCfg);
                    if (npcCfg == null) return;

                    // 自动战斗
                    _ = _player.StartPve(evt.Npcs[0].OnlyId, npcCfg.MonsterGroup);
                }

                // 只需要创建一条即可, 这里立马break, 终止循环
                break;
            }
        }

        public void Destroy()
        {
            Events.Clear();
            Events = null;
            FailEvents.Clear();
            FailEvents = null;
            Config = null;
            _player = null;
        }

        public TaskData BuildPbData()
        {
            var res = new TaskData {Id = CfgId};
            res.Events.AddRange(Events);
            return res;
        }

        public XTaskData BuildXTaskData()
        {
            var resp = new XTaskData
            {
                Id = CfgId,
                Step = Step,
                State = State,
                Events = {Events},
                Fails = {FailEvents}
            };
            return resp;
        }

        public bool CheckAndFinish()
        {
            var finish = Events.All(evt => evt.State == TaskState.Done);
            if (finish) State = TaskState.Done;
            return finish;
        }

        public async Task<bool> SubmitEvent(TaskEventType type, SubmitTaskEventData req)
        {
            var changed = false;
            for (var i = 0; i < Events.Count; i++)
            {
                var stepData = Events[i];
                if (stepData.Type != type || stepData.State != TaskState.Doing) continue;
                switch (stepData.Type)
                {
                    case TaskEventType.TalkNpc:
                    {
                        if (req.TaskId == CfgId && req.TaskStep == i)
                        {
                            await OnEventDone((byte) i);
                            changed = true;
                        }
                    }
                        break;
                    case TaskEventType.GatherNpc:
                    {
                        for (var j = 0; j < stepData.Npcs.Count; j++)
                        {
                            if (stepData.Npcs[j].OnlyId == req.OnlyId)
                            {
                                stepData.Npcs.RemoveAt(j);
                                DeleteNpc(req.OnlyId.GetValueOrDefault());
                                changed = true;
                                break;
                            }
                        }

                        // 检测是否所有Npc都抓完了
                        if (stepData.Npcs.Count == 0)
                        {
                            await OnEventDone((byte) i);
                            changed = true;
                        }
                    }
                        break;
                    case TaskEventType.DoAction:
                    {
                        await OnEventDone((byte) i);
                        changed = true;
                    }
                        break;
                    case TaskEventType.ArriveArea:
                    {
                        if (req.MapId == stepData.MapId && AoiNode.IsVeryClose(req.MapX.GetValueOrDefault(),
                            req.MapY.GetValueOrDefault(), stepData.MapX, stepData.MapY))
                        {
                            await OnEventDone((byte) i);
                            changed = true;
                        }
                    }
                        break;
                    case TaskEventType.GiveNpcItem:
                    {
                        await OnEventDone((byte) i);
                        changed = true;
                    }
                        break;
                    case TaskEventType.KillNpc:
                    {
                        for (var j = 0; j < stepData.Npcs.Count; j++)
                        {
                            if (stepData.Npcs[j].OnlyId == req.OnlyId)
                            {
                                stepData.Npcs.RemoveAt(j);
                                DeleteNpc(req.OnlyId.GetValueOrDefault());
                                changed = true;
                                break;
                            }
                        }

                        // 检测是否所有Npc都击杀了
                        if (stepData.Npcs.Count == 0)
                        {
                            await OnEventDone((byte) i);
                            changed = true;
                        }
                    }
                        break;
                }
            }

            if (!changed) return false;

            _player.TaskMgr.CanAutoFight = true;
            await Refresh();
            CheckAndFinish();
            return true;
        }

        public bool SubmitFailEvent(TaskEventType type, SubmitTaskEventData req)
        {
            for (var i = 0; i < FailEvents.Count; i++)
            {
                var evt = FailEvents[i];
                if (evt == null || evt.Type != type) continue;
                var cfg = ConfigService.GetTaskFailEventConfig(CfgId, i);
                if (cfg == null) continue;

                if (evt.Type == TaskEventType.FailEventDead)
                {
                    evt.DeadNum++;
                    if (evt.DeadNum >= cfg.DeadNum)
                    {
                        State = TaskState.Done;
                        return true;
                    }
                }

                if (evt.Type == TaskEventType.FailEventTimeout)
                {
                    if (req.Time.GetValueOrDefault() - evt.StartTime > cfg.Duration)
                    {
                        State = TaskState.Done;
                        return true;
                    }
                }
            }

            return false;
        }

        // 能否使用双倍经验？
        public bool CanUseX2Exp()
        {
            // 钟馗抓鬼
            var isZhongKuiZhuGui = Config.Id >= 601 && Config.Id <= 632;
            // 天庭降妖
            var isTianTingXiangYao = Config.Id >= 701 && Config.Id <= 704;
            // 击杀修罗
            var isJiShaXiuLuo = Config.Id >= 801 && Config.Id <= 804;
            return isZhongKuiZhuGui || isTianTingXiangYao || isJiShaXiuLuo;
        }

        public async Task OnEventDone(uint step)
        {
            if (step >= Config.Events.Length) return;

            while (true)
            {
                if (Kind == TaskKind.Instance)
                {
                    var cnt = _player.TaskMgr.GetInstanceTaskCnt(CfgId);
                    if (cnt > step)
                    {
                        _player.SendNotice("此关卡已完成，无法再次获得奖励");
                        break;
                    }
                }
                else if (Kind == TaskKind.Daily)
                {
                    var cnt = _player.TaskMgr.GetDailyTaskCnt(Config.Group);
                    if (cnt >= XTaskManager.GetDailyMaxCnt(Config.Group))
                    {
                        _player.SendNotice("你的次数已满，无法再次获得奖励");
                        break;
                    }
                }

                // 双倍经验
                uint x2expleft = 0;
                ulong multi = 1;
                if (CanUseX2Exp())
                {
                    x2expleft = await RedisService.GetRoleX2ExpLeft(_player.RoleId);
                    multi = (ulong)(x2expleft > 0 ? 2 : 1);
                }
                bool x2expused = false;
                // 计算奖励
                var prize = Config.Events[step].Prizes;
                foreach (var property in prize.EnumerateObject())
                {
                    if (property.NameEquals("exp"))
                    {
                        // 满员队伍队长有10%的经验加成
                        var exp = property.Value.GetUInt64();
                        if (exp > 0)
                        {
                            var expAdd = 0U;
                            if (_player.IsTeamLeader && _player.TeamMemberCount >= 5)
                            {
                                expAdd = (uint) MathF.Ceiling(exp * 0.1f);
                            }
                            await _player.AddExp((exp + expAdd) * multi);
                            if (expAdd > 0)
                            {
                                _player.SendNotice($"队长额外获得: {expAdd * multi}角色经验");
                            }
                            x2expused = true;
                        }
                    }
                    else if (property.NameEquals("petExp"))
                    {
                        if (_player.PetMgr?.Pet != null)
                        {
                            await _player.PetMgr.Pet.AddExp(property.Value.GetUInt64() * multi);
                            x2expused = true;
                        }
                    }
                    else if (property.NameEquals("money"))
                    {
                        if (Kind == TaskKind.Daily && Group == 2)
                        {
                            // 帮派任务奖励帮贡
                            await _player.AddMoney(MoneyType.Contrib, property.Value.GetInt32(), "任务奖励");
                        }
                        else
                        {
                            // 五环给仙玉
                            var mt = MoneyType.Silver;
                            if (Kind == TaskKind.Daily && Group == 5) mt = MoneyType.Jade;
                            await _player.AddMoney(mt, property.Value.GetInt32(), "任务奖励");
                        }
                    }
                    else if (property.NameEquals("active"))
                    {
                        _player.TaskMgr.AddActive(ScoreKind, property.Value.GetSingle());
                    }
                    else
                    {
                        // 道具奖励
                        uint.TryParse(property.Name, out var itemCfgId);
                        if (itemCfgId > 0)
                        {
                            await _player.AddItem(itemCfgId, property.Value.GetInt32(), true, "任务奖励");
                        }
                    }
                }

                // 标记完成
                var evtData = Events[(int) step];
                evtData.State = TaskState.Done;
                // 清空Npc, 要区别是否为队伍的Npc
                foreach (var npc in evtData.Npcs)
                {
                    DeleteNpc(npc.OnlyId);
                    evtData.Npcs.Clear();
                }

                // 副本任务计数
                if (Kind == TaskKind.Instance)
                {
                    var cnt = _player.TaskMgr.GetInstanceTaskCnt(CfgId);
                    _player.TaskMgr.SetInstanceTaskCnt(CfgId, Math.Max(cnt, step + 1));
                }
                // 双倍经验更新
                if (x2expused && multi > 1)
                {
                    await RedisService.SetRoleX2ExpLeft(_player.RoleId, Math.Max(0, x2expleft - 1));
                }

                break;
            }

            // 通知队伍
            if (_player.IsTeamLeader && XTaskManager.IsTeamTask(CfgId))
            {
                await _player.TeamGrain.FinishTaskEvent(CfgId, step);
            }
        }

        public void DeleteNpc(uint onlyId)
        {
            if (XTaskManager.IsTeamTask(CfgId))
            {
                if (_player.IsTeamLeader)
                {
                    _player.DeleteTeamNpc(onlyId);
                }
            }
            else
            {
                _player.DeleteNpc(onlyId);
            }
        }

        public uint ScoreKind
        {
            get
            {
                if (Kind == TaskKind.Daily) return Group;
                if (Kind == TaskKind.Instance) return CfgId;
                return 0;
            }
        }

        public static bool IsSectMap(uint map) => map == 3002;
    }

    public class SubmitTaskEventData
    {
        public uint? TaskId;
        public uint? TaskStep;
        public uint? OnlyId;
        public uint? MapId;
        public int? MapX;
        public int? MapY;
        public uint? Time;
    }
}