using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Core;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Config;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;
using Orleans.Concurrency;

namespace Ddxy.GameServer.Logic.XTask
{
    public class XTaskManager
    {
        private TaskEntity _entity;
        private TaskEntity _lastEntity;

        private Dictionary<uint, byte> _complets; //已经完成的剧情任务id集合
        private Dictionary<uint, uint> _states; //已接受的任务id及当前的step
        private Dictionary<uint, byte> _dailyStart; //日常任务必须手动接， 接了之后就可以自动寻找相同group的任务来做满次数
        private Dictionary<uint, uint> _dailyCnt; //日常任务计数, key是task group, value是完成的数量
        private Dictionary<uint, uint> _instanceCnt; //副本任务, key是task id，value是step
        private Dictionary<uint, float> _activeScore; //任务积分
        private uint[] _beenTake; //今日任务积分奖励领取情况

        private Dictionary<uint, XTask> _tasks;
        private PlayerGrain _player;
        private uint _tickCnt;

        public uint StarNum
        {
            get => _entity.StarNum;
            set => _entity.StarNum = value;
        }

        public uint MonkeyNum
        {
            get => _entity.MonkeyNum;
            set => _entity.MonkeyNum = value;
        }

        public uint JinChanSongBaoNum
        {
            get => _entity.JinChanSongBaoNum;
            set => _entity.JinChanSongBaoNum = value;
        }

        public uint EagleNum
        {
            get => _entity.EagleNum;
            set => _entity.EagleNum = value;
        }

        // 是否能自动触发KillNpc
        public bool CanAutoFight { get; set; }

        public XTaskManager(PlayerGrain player)
        {
            _player = player;
            _tasks = new Dictionary<uint, XTask>(5);

            _complets = new Dictionary<uint, byte>(5);
            _states = new Dictionary<uint, uint>(5);
            _dailyStart = new Dictionary<uint, byte>(10);
            _dailyCnt = new Dictionary<uint, uint>(10);
            _instanceCnt = new Dictionary<uint, uint>(10);
            _activeScore = new Dictionary<uint, float>(10);
            _beenTake = new uint[GameDefine.TaskActivePrizes.Count];

            _tickCnt = 0;
        }

        public async Task Init()
        {
            _entity = await DbService.QueryTask(_player.RoleId);
            _lastEntity = new TaskEntity();
            _lastEntity.CopyFrom(_entity);

            // 解析数据
            InitComplets();
            InitStates();
            InitDailyStart();
            InitDailyCnt();
            InitInstanceCnt();
            InitActiveScore();
            InitBeenTake();

            // 装载States
            foreach (var taskId in _states.Keys.ToList())
            {
                ConfigService.Tasks.TryGetValue(taskId, out var cfg);
                if (cfg == null)
                {
                    _states.Remove(taskId);
                    continue;
                }

                var taskStep = _states[taskId];
                if (IsTeamTask(taskId) && !_player.InTeam)
                {
                    _states.Remove(taskId);
                    continue;
                }

                if (IsSectDaily(cfg.Group) && !_player.InSect)
                {
                    _states.Remove(taskId);
                    continue;
                }

                await AddXTask(taskId, (byte) taskStep, false);
            }

            // 删除已完成的任务
            await DeleteCompletedTask();

            // 检测是否需要清空日常和副本任务的计数
            if (CheckIsNewDay()) await OnNewDay(false);
        }

        public async Task Destroy()
        {
            await SaveData();

            foreach (var t in _tasks.Values)
            {
                t.Destroy();
            }

            _tasks.Clear();
            _tasks = null;
            _complets.Clear();
            _complets = null;
            _states.Clear();
            _states = null;
            _dailyStart.Clear();
            _dailyStart = null;
            _dailyCnt.Clear();
            _dailyCnt = null;
            _instanceCnt.Clear();
            _instanceCnt = null;
            _beenTake = null;
            _activeScore.Clear();
            _activeScore = null;
            _entity = null;
            _lastEntity = null;
            _player = null;
        }

        public async Task SaveData()
        {
            SyncComplets();
            SyncStates();
            SyncDailyStart();
            SyncDailyCnt();
            SyncInstanceCnt();
            SyncActiveScore();
            SyncBeenTake();

            if (!_entity.Equals(_lastEntity))
            {
                var ret = await DbService.UpdateEntity(_lastEntity, _entity);
                if (ret) _lastEntity.CopyFrom(_entity);
            }

            await Task.CompletedTask;
        }

        // 每1s调用一次
        public async Task Tick(uint ts)
        {
            _tickCnt++;

            if (_tickCnt % 2 == 0)
            {
                await SubmitTaskEvent(TaskEventType.ArriveArea, new SubmitTaskEventData
                {
                    MapId = _player.Entity.MapId,
                    MapX = _player.Entity.MapX,
                    MapY = _player.Entity.MapY
                });
            }

            if (_tickCnt % 5 == 0)
            {
                await FailEvent(TaskEventType.FailEventTimeout, new SubmitTaskEventData {Time = TimeUtil.TimeStamp});
            }

            // 每5s 检测是否需要重置任务计数, 防止调度过于频繁
            if (_tickCnt % 5 == 0 && CheckIsNewDay())
            {
                await OnNewDay(true);
            }
        }

        /// <summary>
        /// 发送任务数据给客户端
        /// </summary>
        public async Task SendList()
        {
            if (!_player.IsEnterServer) return;

            var resp = new S2C_TaskList();

            // 下发正在进行的任务极其状态数据
            foreach (var xtask in _tasks.Values)
            {
                resp.List.Add(xtask.BuildPbData());
            }

            // 日常任务的group及其计数
            foreach (var (k, v) in _dailyCnt)
            {
                resp.DailyCnt.Add(new TaskPair {Id = k, Num = v});
            }

            // 副本任务的id及其计数
            foreach (var (k, v) in _instanceCnt)
            {
                resp.InstanceCnt.Add(new TaskPair {Id = k, Num = v});
            }

            await _player.SendPacket(GameCmd.S2CTaskList, resp);

            // 同步给队伍, 只同步TeamTask类型
            if (_player.IsTeamLeader)
            {
                var req = new UpdateTeamTasksRequest();
                foreach (var (k, v) in _tasks)
                {
                    if (IsTeamTask(k)) req.List.Add(v.BuildXTaskData());
                }

                _ = _player.TeamGrain.UpdateTasks(new Immutable<byte[]>(Packet.Serialize(req)));
            }
        }

        /// <summary>
        /// 判断日常任务是否已完成
        /// </summary>
        public bool IsDailyTaskCompleted(uint group)
        {
            // 判断次数
            _dailyCnt.TryGetValue(group, out var num);
            return num >= GetDailyMaxCnt(group);
        }

        /// <summary>
        /// 接受日常任务
        /// </summary>
        public async Task InceptDailyTask(uint group, uint onlyId)
        {
            var cfgId = await _player.ServerGrain.FindCfgIdWithNpcOnlyId(onlyId);
            if (cfgId == 0) return;

            // 是否已经领取过该任务
            if (IsAlreadyHasDailyTask(group))
            {
                await _player.SendNpcNotice(cfgId, "你已经领过这个任务了");
                return;
            }

            // 判断是否是组队任务
            var isTeamTask = IsTeamDaily(group);
            if (isTeamTask && !_player.IsTeamLeader)
            {
                await _player.SendNpcNotice(cfgId, "只有队长才能接这个任务");
                return;
            }

            // 帮派任务必须加入帮派
            if (IsSectDaily(group) && !_player.InSect)
            {
                await _player.SendNpcNotice(cfgId, "加入帮派才能接这个任务");
                return;
            }

            // 多人任务
            // 检查多人日常任务是否有队员已完成
            if (isTeamTask)
            {
                var slist = await _player.CheckTeamDailyTaskCompleted(group);
                if (slist.Length > 0) {
                    await _player.SendNpcNotice(cfgId, $"{slist}，今日次数已满");
                    return;
                }
            }
            else
            {
                // 判断次数
                var maxNum = GetDailyMaxCnt(group);
                _dailyCnt.TryGetValue(group, out var num);
                if (num >= maxNum)
                {
                    await _player.SendNpcNotice(cfgId, "今日次数已满");
                    return;
                }
            }
            // 检查是否符合
            if (ConfigService.GroupedDailyTasks.TryGetValue(group, out var list) && list is {Count: > 0})
            {
                var limits = list[0].Limits;
                if (limits is {Relive: { }} && limits.Relive > _player.Entity.Relive)
                {
                    await _player.SendNpcNotice(cfgId, "转生等级不足");
                    return;
                }
                // if (limits is {Level: { }} && limits.Level > _player.Entity.Level)
                // {
                //     await _player.SendNpcNotice(cfgId, "等级不足");
                //     return;
                // }
            }

            // 记录已经开始这个group的任务
            _dailyStart[group] = 0;

            // 下面自动寻找该group的任务
            await CheckAndInceptTask();
            await SendList();
        }

        /// <summary>
        /// 判断副本任务是否已完成
        /// </summary>
        public bool IsInstanceTaskCompleted(uint taskId)
        {
            ConfigService.Tasks.TryGetValue(taskId, out var taskCfg);
            if (taskCfg == null || taskCfg.Kind != (byte) TaskKind.Instance) return true;
            if (_instanceCnt.TryGetValue(taskId, out var step) && step >= taskCfg.Events.Length)
            {
                // 魔王窟不限次数
                if (taskId == 2000 || taskId == 2001 || taskId == 2002)
                {
                    step = 0;
                    _instanceCnt[taskId] = step;
                }
                else
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// 接受副本任务
        /// </summary>
        public async Task InceptInstanceTask(uint taskId, uint onlyId)
        {
            ConfigService.Tasks.TryGetValue(taskId, out var taskCfg);
            if (taskCfg == null || taskCfg.Kind != (byte) TaskKind.Instance) return;

            var cfgId = await _player.ServerGrain.FindCfgIdWithNpcOnlyId(onlyId);
            if (cfgId == 0) return;

            if (_tasks.ContainsKey(taskId))
            {
                await _player.SendNpcNotice(cfgId, "你已经领过这个任务了");
                return;
            }

            if (!_player.IsTeamLeader)
            {
                await _player.SendNpcNotice(cfgId, "只有队长才能接这个任务");
                return;
            }

            var step = _instanceCnt.GetValueOrDefault(taskId, (uint)0);

            // 检查多人副本任务是否有队员已完成
            var slist = await _player.CheckTeamInstanceTaskCompleted(taskId);
            if (slist.Length > 0)
            {
                await _player.SendNpcNotice(cfgId, $"{slist}，今日次数已满");
                return;
            }
            else
            {
                if (step >= taskCfg.Events.Length)
                {
                    // 魔王窟不限次数
                    if (taskId == 2000 || taskId == 2001 || taskId == 2002)
                    {
                        step = 0;
                        _instanceCnt[taskId] = step;
                    }
                    else
                    {
                        await _player.SendNpcNotice(cfgId, "今日次数已满");
                        return;
                    }
                }
            }

            // 方便GM测试
            if (!_player.AppOptions.TeamUnLimit)
            {
                var tc = TeamMemberNeed(taskId);
                if (tc > 1 && _player.TeamMemberCount < tc)
                {
                    await _player.SendNpcNotice(cfgId, $"队伍人数不足{tc}人");
                    return;
                }
            }

            if (!CheckTaskLimit(taskCfg))
            {
                await _player.SendNpcNotice(cfgId, "不满足条件");
                return;
            }

            await AddXTask(taskId, step);
            await SendList();
        }

        public async Task SubmitTalkNpcTask(uint taskId, uint taskStep, uint npcOnlyId, uint npcCfgId)
        {
            if (_player.InTeam && !_player.IsTeamLeader && !_player._teamLeave) return;
            var stepCfg = ConfigService.GetEventStep(taskId, taskStep);
            if (stepCfg == null) return;
            var stepData = GetTaskEventData(taskId, taskStep);
            if (stepData == null) return;

            switch ((TaskEventType) stepCfg.Type)
            {
                case TaskEventType.TalkNpc:
                {
                    var req = new SubmitTaskEventData {TaskId = taskId, TaskStep = taskStep};
                    await SubmitTaskEvent(TaskEventType.TalkNpc, req);
                }
                    break;
                case TaskEventType.KillNpc:
                {
                    var found = false;
                    foreach (var dnd in stepData.Npcs)
                    {
                        if (dnd.OnlyId != npcOnlyId) continue;
                        ConfigService.Npcs.TryGetValue(dnd.CfgId, out var npcCfg);
                        if (npcCfg == null) continue;
                        // 再次判断是否有队员已完成
                        _tasks.TryGetValue(taskId, out var taskData);
                        if (taskData != null)
                        {
                            if (taskData.Kind == TaskKind.Daily)
                            {
                                // 多人任务
                                // 检查多人日常任务是否有队员已完成
                                if (IsTeamDaily(taskData.Group))
                                {
                                    var slist = await _player.CheckTeamDailyTaskCompleted(taskData.Group);
                                    if (slist.Length > 0)
                                    {
                                        await _player.SendNpcNotice(dnd.CfgId, $"{slist}，今日次数已满");
                                        return;
                                    }
                                }
                            }
                            if (taskData.Kind == TaskKind.Instance)
                            {
                                // 检查多人副本任务是否有队员已完成
                                var slist = await _player.CheckTeamInstanceTaskCompleted(taskId);
                                if (slist.Length > 0)
                                {
                                    await _player.SendNpcNotice(dnd.CfgId, $"{slist}，今日次数已满");
                                    return;
                                }
                            }
                        }
                        var group = npcCfg.MonsterGroup;
                        await _player.StartPve(npcOnlyId, group);
                        found = true;
                        break;
                    }
                    if (!found)
                    {
                        _player.SendNotice("当前任务被取消，请重新接受任务！");
                        await this.AbortTask(taskId);
                    }
                }
                    break;
                case TaskEventType.GiveNpcItem:
                {
                    if (_player.GetBagItemNum(stepCfg.Item.GetValueOrDefault()) < stepCfg.ItemNum) return;
                    await _player.AddBagItem(stepCfg.Item.GetValueOrDefault(),
                        0 - (int) stepCfg.ItemNum.GetValueOrDefault(),
                        true,
                        "任务上交");
                    await SubmitTaskEvent(TaskEventType.GiveNpcItem, new SubmitTaskEventData());
                }
                    break;
            }
        }

        /// <summary>
        /// 收集Npc后, 提交任务
        /// </summary>
        public async Task SubmitGatherNpcTask(uint npcOnlyId)
        {
            var cfgId = await _player.ServerGrain.FindCfgIdWithNpcOnlyId(npcOnlyId);
            ConfigService.Npcs.TryGetValue(cfgId, out var npcCfg);
            if (npcCfg == null) return;
            await SubmitTaskEvent(TaskEventType.GatherNpc, new SubmitTaskEventData {OnlyId = npcOnlyId});
        }

        /// <summary>
        /// 收集Npc后, 提交任务
        /// </summary>
        public Task SubmitDoActionTask(uint mapId, int mapX, int mapY)
        {
            return SubmitTaskEvent(TaskEventType.DoAction,
                new SubmitTaskEventData {MapId = mapId, MapX = mapX, MapY = mapY});
        }

        public async Task SubmitKillNpcTask(uint npcOnlyId, NpcConfig npcCfg)
        {
            if (npcCfg == null) return;
            await SubmitTaskEvent(TaskEventType.KillNpc, new SubmitTaskEventData {OnlyId = npcOnlyId});
        }

        /// <summary>
        /// 触发Npc战斗
        /// </summary>
        public async Task TriggerNpcBoomb(uint npcOnlyId, uint npcCfgId)
        {
            if (_player.InTeam && !_player.IsTeamLeader && !_player._teamLeave) return;
            ConfigService.Npcs.TryGetValue(npcCfgId, out var npcCfg);
            if (npcCfg == null) return;
            await _player.StartPve(npcOnlyId, npcCfg.MonsterGroup);
        }

        /// <summary>
        /// 放弃任务
        /// </summary>
        public async Task AbortTask(uint id)
        {
            _tasks.TryGetValue(id, out var task);
            if (task == null) return;

            if (task.Kind == TaskKind.Story)
            {
                _player.SendNotice("剧情任务无法取消");
                return;
            }

            if (task.Kind == TaskKind.Daily)
            {
                if (IsTeamDaily(task.Group) && !_player.IsTeamLeader)
                {
                    _player.SendNotice("队员无法放弃组队任务");
                    return;
                }

                // 防止再次自动接收
                _dailyStart.Remove(task.Group);
            }

            // 标记任务失败
            task.State = TaskState.Faild;
            await DeleteCompletedTask();
            await SendList();
        }

        /// <summary>
        /// 退出队伍的时候，放弃所有组队任务
        /// </summary>
        public async Task AbortAllTeamTask()
        {
            if (_player.InTeam) return;
            var ret = false;
            foreach (var id in _tasks.Keys)
            {
                if (IsTeamTask(id))
                {
                    _tasks.TryGetValue(id, out var task);
                    if (task == null) continue;
                    task.State = TaskState.Faild;
                    _dailyStart.Remove(task.Group);
                    ret = true;
                }
            }

            if (ret)
            {
                await DeleteCompletedTask();
                await SendList();
            }
        }

        // 队长更新了组队任务, 作为队员需要更新
        public async Task OnTeamTasksChanged(IEnumerable<XTaskData> tasks)
        {
            if (!_player.InTeam || _player.IsTeamLeader || _player._teamLeave) return;

            var ret = false;
            foreach (var xtd in tasks)
            {
                if (!IsTeamTask(xtd.Id)) continue;
                // 尝试移除旧的task, 这里不用考虑入库
                _tasks.Remove(xtd.Id, out var oldTask);
                oldTask?.Destroy();
                // 构建新的task
                ConfigService.Tasks.TryGetValue(xtd.Id, out var taskCfg);
                if (taskCfg == null) continue;

                // 复制数据构建XTask
                var xTask = new XTask(_player)
                {
                    CfgId = xtd.Id,
                    Config = taskCfg,
                    Step = xtd.Step,
                    State = xtd.State,
                    Events = new List<TaskEventData>(xtd.Events),
                    FailEvents = new List<TaskFailEventData>(xtd.Fails)
                };
                _tasks[xTask.CfgId] = xTask;

                ret = true;
            }
            // 如果队长已经清空任务，则应该清空队员团队任务
            if (tasks.Count() == 0)
            {
                List<uint> toRemoveIdList = new();
                foreach (var xtask in _tasks.Values)
                {
                    if (IsTeamTask(xtask.CfgId))
                    {
                        toRemoveIdList.Add(xtask.CfgId);
                    }
                }
                foreach (var id in toRemoveIdList)
                {
                    // 尝试移除旧的task, 这里不用考虑入库
                    _tasks.Remove(id, out var oldTask);
                    oldTask?.Destroy();
                }
            }

            if (ret || tasks.Count() == 0) await SendList();
        }

        // 队长提交了任务事件, 同步任务事件结束给队友
        public async Task OnTeamTaskEventDone(uint taskId, uint step)
        {
            if (!_player.InTeam || _player.IsTeamLeader || _player._teamLeave) return;

            _tasks.TryGetValue(taskId, out var xTask);
            if (xTask == null) return;
            await xTask.OnEventDone(step);

            // await xTask.Refresh();
            // xTask.CheckAndFinish();

            // await DeleteCompletedTask(false);
            // await SendList();
        }

        // 队长完成了任务
        public async Task OnTeamTaskFinish(uint taskId, bool success)
        {
            if (!_player.InTeam || _player.IsTeamLeader || _player._teamLeave) return;
            _tasks.TryGetValue(taskId, out var xTask);
            if (xTask == null) return;
            // 更新计数
            await OnTaskFinish(xTask, success);

            _tasks.Remove(taskId);
            _states.Remove(taskId);
        }

        public async Task SendDailyData()
        {
            var resp = new S2C_TaskDailyData();
            foreach (var (k, v) in _dailyCnt)
            {
                resp.DailyCnt.Add(new UintPair {Key = k, Value = v});
            }

            foreach (var (k, v) in _instanceCnt)
            {
                resp.InstanceCnt.Add(new UintPair {Key = k, Value = v});
            }

            resp.DailyStarted.AddRange(_dailyStart.Keys);

            foreach (var (k, v) in _activeScore)
            {
                resp.ActiveScore.Add(new FloatPair {Key = k, Value = v});
            }

            resp.BeenTake.AddRange(_beenTake);
            resp.StarNum = StarNum;
            resp.MonkeyNum = MonkeyNum;
            resp.JinChanSongBaoNum = JinChanSongBaoNum;
            resp.EagleNum = EagleNum;

            await _player.SendPacket(GameCmd.S2CTaskDailyData, resp);
        }

        public async Task GetActivePrize(int index)
        {
            if (index >= GameDefine.TaskActivePrizes.Count) return;
            // 检查该步骤是否领取过
            if (_beenTake[index] > 0)
            {
                _player.SendNotice("已经领取过该奖励");
                return;
            }

            // 每35分一个段
            var totalScore = (int) MathF.Floor(_activeScore.Values.Sum());
            var needScore = (index + 1) * 36;
            if (totalScore < needScore)
            {
                _player.SendNotice("积分不足");
                return;
            }

            var (itemId, itemNum) = GameDefine.TaskActivePrizes[index];
            await _player.AddItem(itemId, (int) itemNum, tag: "领取任务活跃度奖励");
            _beenTake[index] = 1;

            // 再次下发DailyData
            await SendDailyData();
        }

        // 提交任务事件
        private async Task SubmitTaskEvent(TaskEventType type, SubmitTaskEventData req)
        {
            // 队长才能提交
            if (_player.InTeam && !_player.IsTeamLeader && !_player._teamLeave) return;

            var changed = false;
            foreach (var task in _tasks.Values)
            {
                var tmp = await task.SubmitEvent(type, req);
                if (tmp) changed = true;
            }

            if (changed)
            {
                await DeleteCompletedTask();
                await SendList();
            }
        }

        /// <summary>
        /// 任务失败
        /// </summary>
        public async Task FailEvent(TaskEventType type, SubmitTaskEventData data)
        {
            var changed = false;
            foreach (var task in _tasks.Values)
            {
                if (task.SubmitFailEvent(type, data)) changed = true;
            }

            if (changed)
            {
                await DeleteCompletedTask();
                await SendList();
            }
        }

        public TaskEventData GetTaskEventData(uint taskId, uint taskStep)
        {
            _tasks.TryGetValue(taskId, out var taskData);
            if (taskData == null) return null;
            if (taskStep >= taskData.Events.Count) return null;
            return taskData.Events[(int) taskStep];
        }

        private async Task AddXTask(uint cfgId, uint step, bool save = true)
        {
            ConfigService.Tasks.TryGetValue(cfgId, out var taskCfg);
            if (taskCfg == null) return;
            if (IsTeamTask(cfgId) && !_player.InTeam) return;

            var xTask = new XTask(_player, cfgId, step);
            await xTask.Refresh();
            _tasks[cfgId] = xTask;

            if (save)
            {
                _states[cfgId] = xTask.Step;
            }
        }

        public void AddActive(uint kind, float value)
        {
            _activeScore.TryGetValue(kind, out var oldVal);
            // 最大不超过140
            var newValue = MathF.Min(oldVal + value, 140);
            _activeScore[kind] = newValue;

            _player.LogDebug($"获得任务积分{value} 当前总积分{newValue}");
        }

        public bool IsAlreadyDone(uint taskId)
        {
            if (taskId == 0) return true;
            return _complets.ContainsKey(taskId);
        }

        public bool IsAlreadyHasDailyTask(uint taskGroup)
        {
            return _tasks.Values.Any(p => p.Group == taskGroup);
        }

        public uint GetInstanceTaskCnt(uint taskId)
        {
            _instanceCnt.TryGetValue(taskId, out var cnt);
            return cnt;
        }

        public void SetInstanceTaskCnt(uint taskId, uint cnt)
        {
            _instanceCnt[taskId] = cnt;
        }

        public uint GetDailyTaskCnt(uint taskGroup)
        {
            _dailyCnt.TryGetValue(taskGroup, out var cnt);
            return cnt;
        }

        public uint AddDailyTaskCnt(uint taskGroup, uint delta = 1)
        {
            _dailyCnt.TryGetValue(taskGroup, out var cnt);
            cnt = Math.Min(cnt + delta, GetDailyMaxCnt(taskGroup));
            _dailyCnt[taskGroup] = cnt;
            return cnt;
        }

        // 接受剧情和日常任务
        private async Task CheckAndInceptTask()
        {
            // 检查剧情任务, 同时只能有1个剧情任务
            if (_tasks.Values.Any(p => p.Kind == TaskKind.Story))
            {
            }
            else
            {
                ConfigService.TypedTasks.TryGetValue((byte) TaskKind.Story, out var list);
                if (list != null)
                {
                    foreach (var cfg in list)
                    {
                        if (IsAlreadyDone(cfg.Id)) continue;
                        // 检查是否已经已经接受了
                        if (_tasks.ContainsKey(cfg.Id)) continue;
                        // 检查是否符合条件
                        if (!CheckTaskLimit(cfg)) continue;
                        // 添加任务
                        await AddXTask(cfg.Id, 0);

                        // 每次只接受1个剧情任务
                        break;
                    }
                }
            }

            // 检查日常任务
            foreach (var (group, cfgList) in ConfigService.GroupedDailyTasks)
            {
                // 必须手动接过了
                if (!_dailyStart.ContainsKey(group)) continue;
                // 检查该group的任务是否已经接了
                if (IsAlreadyHasDailyTask(group)) continue;

                // 组队的任务必须队长才能接
                if (IsTeamDaily(group) && !_player.IsTeamLeader) continue;
                // 帮派任务
                if (IsSectDaily(group) && !_player.InSect) continue;

                // 对cfgList进行Limit过滤, 并随机选择一个
                var filtedList = cfgList.Where(CheckTaskLimit).ToList();
                if (filtedList.Count == 0) continue;
                var cfg = filtedList[_player.Random.Next(0, filtedList.Count)];

                // 检查该类型的日常任务是否次数做完了
                _dailyCnt.TryGetValue(group, out var cnt);
                if (cnt >= cfg.Daily) continue;

                await AddXTask(cfg.Id, 0);
            }
        }

        private bool CheckTaskLimit(TaskConfig cfg)
        {
            if (cfg == null) return false;
            if (cfg.Limits == null) return true;
            if (cfg.Limits.Level != null) return _player.Entity.Level >= cfg.Limits.Level;
            if (cfg.Limits.Race != null) return _player.Entity.Race == cfg.Limits.Race;
            if (cfg.Limits.Sect.GetValueOrDefault()) return _player.Entity.SectId > 0;
            if (cfg.Limits.PreTask.GetValueOrDefault() > 0)
            {
                return IsAlreadyDone(cfg.Limits.PreTask.GetValueOrDefault());
            }

            return true;
        }

        private async Task DeleteCompletedTask(bool autoIncept = true)
        {
            foreach (var key in _tasks.Keys.ToList())
            {
                var value = _tasks[key];

                // 剧情任务要更新步骤
                _states[value.CfgId] = value.Step;

                if (value.State == TaskState.Done)
                {
                    await OnTaskFinish(value, true);
                    _tasks.Remove(key);
                    _states.Remove(key);
                }
                else if (value.State == TaskState.Faild)
                {
                    await OnTaskFinish(value, false);
                    _tasks.Remove(key);
                    _states.Remove(key);
                }
            }

            // 自动检测并接收新任务
            if (autoIncept) await CheckAndInceptTask();
        }

        private async Task OnTaskFinish(XTask data, bool success)
        {
            // 计算下一个step
            var newStep = data.Events.Count;
            for (var i = 0; i < data.Events.Count; i++)
            {
                var evtData = data.Events[i];
                if (evtData.State != TaskState.Doing) continue;
                newStep = i;
                // 清空所有的动态Npc
                if (evtData.Npcs == null) continue;
                foreach (var npc in evtData.Npcs)
                {
                    if (IsTeamTask(data.CfgId))
                    {
                        _player.DeleteTeamNpc(npc.OnlyId);
                    }
                    else
                    {
                        _player.DeleteNpc(npc.OnlyId);
                    }
                }
            }

            if (success)
            {
                if (data.Kind == TaskKind.Story)
                {
                    _complets.Add(data.CfgId, 0);
                }
                else if (data.Kind == TaskKind.Daily)
                {
                    AddDailyTaskCnt(data.Group);
                }
                else if (data.Kind == TaskKind.Instance)
                {
                    _instanceCnt.TryGetValue(data.CfgId, out var step);
                    _instanceCnt[data.CfgId] = Math.Max(step, (byte) newStep);
                }
            }

            // 通知Player
            _player.SendNotice(data.Config.Name + "  " + (success ? "完成" : "失败"));

            // 队长要通知所有的队员
            if (IsTeamTask(data.CfgId) && _player.IsTeamLeader)
            {
                await _player.TeamGrain.FinishTask(data.CfgId, success);
            }
        }

        private void InitComplets()
        {
            if (_entity != null && !string.IsNullOrWhiteSpace(_entity.Complets))
            {
                var list = Json.Deserialize<List<uint>>(_entity.Complets);
                foreach (var taskId in list)
                {
                    _complets.Add(taskId, 0);
                }
            }
        }

        private void SyncComplets()
        {
            if (_complets == null || _complets.Count == 0)
            {
                _entity.Complets = string.Empty;
                return;
            }

            _entity.Complets = Json.Serialize(_complets.Keys.ToList());
        }

        private void InitStates()
        {
            if (_entity != null && !string.IsNullOrWhiteSpace(_entity.States))
            {
                _states = Json.SafeDeserialize<Dictionary<uint, uint>>(_entity.States);
            }
        }

        private void SyncStates()
        {
            if (_states == null || _states.Count == 0)
            {
                _entity.States = string.Empty;
                return;
            }

            _entity.States = Json.SafeSerialize(_states);
        }

        private void InitDailyStart()
        {
            if (_entity != null && !string.IsNullOrWhiteSpace(_entity.DailyStart))
            {
                var list = Json.Deserialize<List<uint>>(_entity.DailyStart);
                if (list != null)
                {
                    foreach (var group in list)
                    {
                        _dailyStart[group] = 0;
                    }
                }
            }
        }

        private void SyncDailyStart()
        {
            if (_dailyStart.Count == 0)
            {
                _entity.DailyStart = string.Empty;
                return;
            }

            _entity.DailyStart = Json.Serialize(_dailyStart.Keys.ToList());
        }

        private void InitDailyCnt()
        {
            if (_entity != null && !string.IsNullOrWhiteSpace(_entity.DailyCnt))
            {
                _dailyCnt = Json.SafeDeserialize<Dictionary<uint, uint>>(_entity.DailyCnt);
            }
        }

        private void SyncDailyCnt()
        {
            if (_dailyCnt == null || _dailyCnt.Count == 0)
            {
                _entity.DailyCnt = string.Empty;
                return;
            }

            _entity.DailyCnt = Json.SafeSerialize(_dailyCnt);
        }

        private void InitInstanceCnt()
        {
            if (_entity != null && !string.IsNullOrWhiteSpace(_entity.InstanceCnt))
            {
                _instanceCnt = Json.SafeDeserialize<Dictionary<uint, uint>>(_entity.InstanceCnt);
            }
        }

        private void SyncInstanceCnt()
        {
            if (_instanceCnt == null || _instanceCnt.Count == 0)
            {
                _entity.InstanceCnt = string.Empty;
                return;
            }

            _entity.InstanceCnt = Json.SafeSerialize(_instanceCnt);
        }

        private void InitActiveScore()
        {
            if (_entity != null && !string.IsNullOrWhiteSpace(_entity.ActiveScore))
            {
                _activeScore = Json.SafeDeserialize<Dictionary<uint, float>>(_entity.ActiveScore);
            }
        }

        private void SyncActiveScore()
        {
            if (_activeScore == null || _activeScore.Count == 0)
            {
                _entity.ActiveScore = string.Empty;
                return;
            }

            _entity.ActiveScore = Json.SafeSerialize(_activeScore);
        }

        private void InitBeenTake()
        {
            if (_entity != null && !string.IsNullOrWhiteSpace(_entity.BeenTake))
            {
                var arr = Json.Deserialize<uint[]>(_entity.BeenTake);
                if (arr != null)
                {
                    for (var i = 0; i < arr.Length; i++)
                    {
                        if (i >= _beenTake.Length) break;
                        if (arr[i] > 0) _beenTake[i] = arr[i];
                    }
                }
            }
        }

        private void SyncBeenTake()
        {
            if (_beenTake.All(p => p == 0))
            {
                _entity.BeenTake = string.Empty;
                return;
            }

            _entity.BeenTake = Json.Serialize(_beenTake);
        }

        private bool CheckIsNewDay()
        {
            var ret = false;
            var now = DateTimeOffset.Now;
            var last = DateTimeOffset.FromUnixTimeSeconds(_entity.UpdateTime).AddHours(8);

            // 相差时间超过1天，必定立即刷新
            if (now.Subtract(last).TotalDays >= 1) return true;

            // 现在超过5点, 上次更新时间小于今天5点
            if (now.Hour >= 5)
            {
                // 记得转换为东八区时间
                var todayHour5Ts = new DateTimeOffset(now.Year, now.Month, now.Day, 5, 0, 0, TimeSpan.FromHours(8))
                    .ToUnixTimeSeconds();
                ret = _entity.UpdateTime < todayHour5Ts;
            }

            return ret;
        }

        private async Task OnNewDay(bool send)
        {
            var resetTasks = _tasks.Values.Where(p => p.Kind == TaskKind.Daily || p.Kind == TaskKind.Instance);
            foreach (var task in resetTasks)
            {
                // 放弃该任务
                task.State = TaskState.Faild;
            }

            await DeleteCompletedTask(false);

            // 重置日常和副本计数、活动积分及领取奖励的记录
            _dailyStart.Clear();
            SyncDailyStart();
            _dailyCnt.Clear();
            SyncDailyCnt();
            _instanceCnt.Clear();
            SyncInstanceCnt();
            _activeScore.Clear();
            SyncActiveScore();
            _beenTake = new uint[GameDefine.TaskActivePrizes.Count];
            SyncBeenTake();
            StarNum = 0;
            JinChanSongBaoNum = 0;
            EagleNum = 0;
            // 记录本次更新时间
            _entity.UpdateTime = TimeUtil.TimeStamp;

            // 自动接受新任务
            await CheckAndInceptTask();

            // 更新任务给客户端
            if (send) await SendList();
        }

        public static bool IsTeamTask(uint taskId)
        {
            return taskId >= 500;
        }

        public static int TeamMemberNeed(uint taskId)
        {
            // 大雁塔、寻芳、地宫、魔王窟 必须要3人以上的队伍
            if (taskId is 1001 or 1002 or 1004 or 1005 or 1006 or 2000 or 2001 or 2002)
            {
                return 3;
            }

            return taskId >= 500 ? 1 : 0;
        }

        public static bool IsTeamDaily(uint group)
        {
            return group >= 5;
            // return group == 5 || group == 6 || group == 7 || group == 8 || group == 9;
        }

        public static bool IsSectDaily(uint group)
        {
            return group == 2;
        }

        public static uint GetDailyMaxCnt(uint group)
        {
            uint cnt = 0;
            switch (group)
            {
                case 2:
                case 3:
                    cnt = 20;
                    break;
                case 4:
                    cnt = 15;
                    break;
                case 5:
                    cnt = 5;
                    break;
                case 6:
                    cnt = 9999;
                    break;
                case 7:
                    // 天庭降妖
                    cnt = 120;
                    break;
                case 8:
                    // 击杀修罗
                    cnt = 300;
                    break;
                case 9:
                    cnt = 999;
                    break;
                case 10:
                case 11:
                case 12:
                    cnt = 10;
                    break;
            }

            return cnt;
        }
    }
}