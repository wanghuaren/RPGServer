using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Data.Vo;
using Ddxy.GameServer.Grains;
using Ddxy.GameServer.Util;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Mount
{
    public class MountManager
    {
        private PlayerGrain _player;

        /// <summary>
        /// 所有坐骑
        /// </summary>
        public List<Mount> All { get; private set; }

        /// <summary>
        /// 当前乘坐的坐骑
        /// </summary>
        public Mount Mount { get; private set; }

        public uint ActiveMountCfgId => Mount?.CfgId ?? 0;

        public MountManager(PlayerGrain player)
        {
            _player = player;
            All = new List<Mount>(5);
        }

        public async Task Init()
        {
            var entities = await DbService.QueryMounts(_player.RoleId);
            foreach (var entity in entities)
            {
                var mount = new Mount(_player, entity);
                All.Add(mount);
                if (mount.Active)
                {
                    if (Mount == null) Mount = mount;
                    else mount.Active = false;
                }
            }
            // 全属性坐骑12345
            if (entities != null && entities.Count > 0)
            {
                if (All.Find(m => m.CfgId == 12345) == null)
                {
                    await CreateMount(12345);
                }
            }
        }

        public async Task Destroy()
        {
            var tasks = from p in All select p.Destroy();
            await Task.WhenAll(tasks);

            Mount = null;
            All.Clear();
            All = null;

            _player = null;
        }

        public Task SaveData()
        {
            var tasks = from p in All select p.SaveData();
            return Task.WhenAll(tasks);
        }

        public async Task SendList()
        {
            if (All.Count == 0)
            {
                ConfigService.MountGroups.TryGetValue(_player.Entity.Race, out var list);
                if (list != null && list.Count > 0)
                {
                    foreach (var cfg in list)
                    {
                        await CreateMount(cfg.Id);
                    }
                }
                // 全属性坐骑12345
                await CreateMount(12345);
            }

            var resp = new S2C_MountList();
            foreach (var data in All)
            {
                resp.List.Add(data.BuildPbData());
            }

            // 下发宠物列表
            await _player.SendPacket(GameCmd.S2CMountList, resp);
        }

        /// <summary>
        /// 转生或修改种族之后重新生成4个坐骑, 在这之前要先修改PlayerEntity.Race
        /// </summary>
        public void OnRaceChanged()
        {
            ConfigService.MountGroups.TryGetValue(_player.Entity.Race, out var list);
            if (list != null && list.Count > 0)
            {
                for (var i = 0; i < list.Count; i++)
                {
                    var cfg = list[i];
                    All[i].UpdateCfg(cfg.Id, cfg.Name);
                }
            }
        }

        public async Task<Mount> CreateMount(uint cfgId)
        {
            ConfigService.Mounts.TryGetValue(cfgId, out var cfg);
            if (cfg == null || (cfg.Race != 0 && cfg.Race != _player.Entity.Race)) return null;

            // 洗练一次
            var washData = RandomWashData(cfgId);
            var skills = washData.Skills.Select(p => new MountSkillVo(p)).ToList();
            // 构建实体
            var entity = new MountEntity
            {
                RoleId = _player.RoleId,
                CfgId = cfgId,
                Name = cfg.Name,
                Level = 0,
                Exp = 0,
                Hp = 10000,
                Spd = washData.Spd,
                Rate = washData.Rate,
                Skills = Json.Serialize(skills),
                Pets = "",
                WashData = "",
                Active = false,
                Locked = (cfgId == 12345),
                CreateTime = TimeUtil.TimeStamp
            };

            // 插入数据，得到主键id
            await DbService.InsertEntity(entity);
            if (entity.Id == 0) return null;

            var mount = new Mount(_player, entity);
            All.Add(mount);

            return mount;
        }

        public async Task SetActive(uint id, bool active)
        {
            if (_player.Entity.Relive == 0)
            {
                _player.SendNotice("1转后开放此功能");
                return;
            }

            var mount = FindMount(id);
            if (mount == null || mount.Active == active) return;

            if (mount.Locked)
            {
                _player.SendNotice("请先解锁");
                return;
            }

            // var oldMount = Mount;
            if (Mount != null)
            {
                Mount.Active = false;
                Mount = null;
            }

            mount.Active = active;
            if (active) Mount = mount;

            await _player.SendPacket(GameCmd.S2CMountActive, new S2C_MountActive
            {
                Id = id,
                Active = active
            });

            await _player.RefreshMount();

            // if (oldMount != null)
            // {
            //     foreach (var pid in oldMount.Pets)
            //     {
            //         var pet = _player.PetMgr.FindPet(pid);
            //         pet?.RefreshAttrs();
            //     }
            // }
            //
            // foreach (var pid in mount.Pets)
            // {
            //     var pet = _player.PetMgr.FindPet(pid);
            //     pet?.RefreshAttrs();
            // }
        }

        public async Task Unlock(uint id)
        {
            if (_player.Entity.Relive == 0)
            {
                _player.SendNotice("1转后开放此功能");
                return;
            }
            var mount = FindMount(id);
            if (mount == null)
            {
                _player.SendNotice("没找到坐骑");
                return;
            }
            if (!mount.Locked)
            {
                _player.SendNotice("已经解锁，无需再次解锁");
                return;
            }
            uint itemId = 9914;
            var itemNeed = 1;
            if (_player.GetBagItemNum(itemId) < itemNeed)
            {
                _player.SendNotice($"{ConfigService.Items[itemId].Name}不足");
                return;
            }
            var ret = await _player.AddBagItem(itemId, -itemNeed, tag: "解锁天马");
            if (!ret)
            {
                _player.SendNotice($"{ConfigService.Items[itemId].Name}消耗失败");
                return;
            }
            mount.Locked = false;
            await mount.SendInfo();
        }

        public Mount FindMount(uint id)
        {
            return All.FirstOrDefault(p => p.Id == id);
        }

        public async ValueTask<bool> AddMountExp(uint id, uint exp)
        {
            var mount = FindMount(id);
            if (mount == null) return false;
            var ret = await mount.AddExp(exp);
            return ret;
        }

        public async Task DingZhi(uint id, List<uint> skills)
        {
            var mount = FindMount(id);
            if (mount == null) return;
            await mount.Dingzhi(skills);
        }

        public async Task WashMount(uint id)
        {
            var mount = FindMount(id);
            if (mount == null) return;
            await mount.Wash();
        }

        public async Task SaveWash(uint id)
        {
            var mount = FindMount(id);
            if (mount == null) return;
            await mount.SaveWash();
        }

        public async Task ControlPet(uint id, uint petId, bool add)
        {
            var mount = FindMount(id);
            if (mount == null) return;
            await mount.ControlPet(petId, add);
        }

        public async Task UpgradeSkill(uint id, int grid)
        {
            var mount = FindMount(id);
            if (mount == null) return;
            await mount.UpgradeSkill(grid);
        }

        public Mount FindWhoControlPet(uint petId)
        {
            return All.FirstOrDefault(p => p.Pets.Contains(petId));
        }

        public static MountWashData RandomWashData(uint cfgId)
        {
            ConfigService.Mounts.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return null;

            var washData = new MountWashData();

            var rnd = new Random();
            // 成长率要扩大到1w倍
            var rateMin = (int) MathF.Floor(cfg.Rate[0] * 10000);
            var rateMax = (int) MathF.Floor(cfg.Rate[1] * 10000);
            // 有5%的概率获取最大成长率
            if (rnd.Next(0, 100) < 5)
            {
                washData.Rate = (uint) rateMax;
            }
            else
            {
                washData.Rate = (uint) rnd.Next(rateMin, rateMax);
            }

            // 移动速度
            washData.Spd = rnd.Next(cfg.Spd[0], cfg.Spd[1] + 1);

            // 洗技能, 3个
            ConfigService.MountGroupedSkills.TryGetValue(cfg.Type, out var skills);
            if (skills != null || cfgId == 12345)
            {
                for (var i = 1; i <= 3; i++)
                {
                    List<Data.Config.MountSkillConfig> list = null;
                    if (cfgId == 12345)
                    {
                        ConfigService.MountGroupedPSkills.TryGetValue(i, out var list1);
                        list = list1;
                    }
                    else
                    {
                        skills.TryGetValue(i, out var list1);
                        list = list1;
                    }
                    if (list == null || list.Count == 0) continue;
                    var skCfg = list[rnd.Next(0, list.Count)];
                    // 构建技能, 熟练度在这里先不赋值
                    var msk = new MountSkillData
                    {
                        CfgId = skCfg.Id,
                        Exp = 0,
                        ExpMax = 2000,
                        Level = 0
                    };
                    washData.Skills.Add(msk);
                }

                // 10%的概率出1个高级, 5%的概率出2个高级，1%的概率出3个高级, 0,2,4坐骑几率减半
                var highLevelNum = 0;
                var x = rnd.Next(0, 1000);
                if (cfg.Type is 2 or 4 or 0)
                {
                    if (x < 5) highLevelNum = 3;
                    else if (x < 25) highLevelNum = 2;
                    else if (x < 50) highLevelNum = 1;
                }
                else
                {
                    if (x < 10) highLevelNum = 3;
                    else if (x < 50) highLevelNum = 2;
                    else if (x < 100) highLevelNum = 1;
                }

                if (highLevelNum > 0)
                {
                    var idsx = new List<int>();
                    for (var i = 0; i < washData.Skills.Count; i++) idsx.Add(i);
                    for (var i = 0; i < highLevelNum; i++)
                    {
                        if (idsx.Count == 0) break;
                        // 随机一个位置设置为高级
                        var t = rnd.Next(0, idsx.Count);
                        washData.Skills[idsx[t]].Level = 1;
                        idsx.RemoveAt(t);
                    }
                }
            }

            return washData;
        }

        // 获取宠物最大成长率
        public static uint GetMaxRate(uint cfgId)
        {
            ConfigService.Mounts.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return 0;
            return (uint) MathF.Floor(cfg.Rate[1] * 10000);
        }

        public const byte MaxLevel = 100;
    }
}