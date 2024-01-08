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

namespace Ddxy.GameServer.Logic.Pet
{
    public class PetManager
    {
        private PlayerGrain _player;
        public List<Pet> All { get; private set; }

        /// <summary>
        /// 当前参战的宠物
        /// </summary>
        public Pet Pet { get; private set; }

        public PetManager(PlayerGrain player)
        {
            _player = player;
            All = new List<Pet>(5);
        }

        public async Task Init()
        {
            var entities = await DbService.QueryPets(_player.RoleId);
            foreach (var entity in entities)
            {
                var data = new Pet(_player, entity, false);
                All.Add(data);

                if (data.Active)
                {
                    if (Pet == null) Pet = data;
                    else data.Active = false;
                }
            }
        }

        public Task Start()
        {
            foreach (var pet in All)
            {
                pet.Start();
            }

            return Task.CompletedTask;
        }

        public async Task Destroy()
        {
            var tasks = from p in All select p.Destroy();
            await Task.WhenAll(tasks);

            All.Clear();
            All = null;

            _player = null;
        }

        public Task SaveData()
        {
            var tasks = from p in All select p.SaveData();
            return Task.WhenAll(tasks);
        }

        public async Task<bool> SendList()
        {
            var resp = new S2C_PetList();
            foreach (var data in All)
            {
                resp.List.Add(data.BuildPbData());
            }

            // 下发宠物列表
            await _player.SendPacket(GameCmd.S2CPetList, resp);

            if (!_player.GetFlag(FlagType.AdoptPet))
            {
                // 提示领取宠物
                await _player.SendPacket(GameCmd.S2CPetAdopt);
                // FIXME: 这里使用宠物领取下发作为绑定推广ID标志，所以这里只要下发一次，就设置标志
                _player.SetFlag(FlagType.AdoptPet, true);
                // 防止自动入库失败，这里先把flag强行入库
                await DbService.Sql.Update<RoleEntity>()
                    .Where(it => it.Id == _player.RoleId)
                    .Set(it => it.Flags, _player.Entity.Flags)
                    .ExecuteAffrowsAsync();
                return true;
            }
            return false;
        }

        // 重新计算宠物属性并下发列表
        public async Task RecalculateAttrsAndSendList() {
            var resp = new S2C_PetList();
            foreach (var data in All)
            {
                data.CalculateAttrs();
                resp.List.Add(data.BuildPbData());
            }
            await _player.SendPacket(GameCmd.S2CPetList, resp);
        }

        public async Task SendShanXianOrderList()
        {
            var list = new List<PetShanXianOrderItem>();
            foreach (var pet in All)
            {
                if (pet.HasShanXian())
                {
                    var item = new PetShanXianOrderItem
                    {
                        Id = pet.Id,
                        Order = pet.SxOrder
                    };
                    list.Add(item);
                }
            }

            // Order升序
            list.Sort((a, b) => (int) a.Order - (int) b.Order);

            // 先把所有的都置为0
            foreach (var pet in All)
            {
                pet.SxOrder = 0;
            }

            // 应用新的排序
            for (var i = 0; i < list.Count; i++)
            {
                var pet = All.FirstOrDefault(p => p.Id == list[i].Id);
                if (pet == null) continue;
                var order = (uint) (i + 1);
                pet.SxOrder = order;
                list[i].Order = order;
            }

            await _player.SendPacket(GameCmd.S2CPetShanXianOrderList, new S2C_PetShanXianOrderList
            {
                List = {list}
            });
        }

        public async Task ChangeShanXianOrder(uint petId, uint order)
        {
            var pet = All.FirstOrDefault(p => p.Id == petId);
            if (pet == null || !pet.HasShanXian()) return;

            pet.SxOrder = order;
            foreach (var p in All)
            {
                if (p.Id == petId || !p.HasShanXian()) continue;
                // 插队, 后面的往后挪
                if (p.SxOrder >= order)
                {
                    p.SxOrder += 1;
                }
            }

            await SendShanXianOrderList();
        }

        public List<BattleMemberData> BuildBattleTeamData(int pos)
        {
            var list = new List<BattleMemberData>();
            foreach (var pet in All)
            {
                var mbData = pet.BuildBattleMemberData();
                list.Add(mbData);
                if (Pet != null && Pet.Id == pet.Id)
                {
                    mbData.Pos = pos;
                }
                else
                {
                    mbData.Pos = 0;
                }
            }

            return list;
        }

        // 地摊下架货物
        public async Task<Pet> AddPet(PetEntity entity)
        {
            var pet = new Pet(_player, entity);
            All.Add(pet);
            await pet.SendInfo();

            return pet;
        }

        public async Task CreatePet(uint cfgId)
        {
            ConfigService.Pets.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return;

            // 洗练
            var washData = RandomWashData(cfgId);
            var entity = new PetEntity
            {
                RoleId = _player.RoleId,
                CfgId = cfgId,
                Name = cfg.Name,
                Relive = 0,
                Level = 0,
                Exp = 0,
                Intimacy = 0,
                Hp = washData.Hp,
                Mp = washData.Mp,
                Atk = washData.Atk,
                Spd = washData.Spd,
                Rate = washData.Rate,
                Quality = (byte) washData.Quality,
                Keel = 0,
                Unlock = 0,
                Skills = "",
                SsSkill = 0,
                JxLevel = 0,
                ApAttrs = "",
                Elements = "",
                RefineLevel = 0,
                RefineExp = 0,
                RefineAttrs = "",
                Fly = 0,
                Color = 0,
                SxOrder = 0,
                AutoSkill = 0,
                WashData = "",
                Active = Pet == null,
                CreateTime = TimeUtil.TimeStamp
            };

            // 技能
            if (cfg.Skill > 0)
            {
                var list = new List<PetSkillEntity>
                {
                    new PetSkillEntity {Id = cfg.Skill, Lock = 0, UnLock = 1}
                };
                entity.Skills = Json.Serialize(list);
            }

            // 插入数据，得到主键id
            await DbService.InsertEntity(entity);
            if (entity.Id == 0) return;

            var pet = new Pet(_player, entity);
            All.Add(pet);
            // 设为当前使用
            Pet ??= pet;

            // 下发
            await pet.SendInfo();

            _player.SendNotice($"恭喜你获得召唤兽{pet.Name}");

            _player.LogInformation($"获得宠物: id:{pet.Id} cfgId:{pet.CfgId}");
        }

        // 放生
        public async ValueTask<bool> DelPet(uint id)
        {
            if (Pet != null && Pet.Id == id)
            {
                _player.SendNotice("参战的宠物不能放生");
                return false;
            }

            var idx = All.FindIndex(p => p.Id == id);
            if (idx < 0) return false;
            var pet = All[idx];
            await DbService.DeleteEntity<PetEntity>(id);
            All.RemoveAt(idx);

            var resp = new S2C_PetDel {Id = id, Active = Pet?.Id ?? 0};
            await _player.SendPacket(GameCmd.S2CPetDel, resp);

            // 如果被坐骑托管， 需要自动取消管制, 而且要在All.RemoveAt之前
            var mount = _player.MountMgr.FindWhoControlPet(id);
            if (mount != null)
            {
                await mount.ControlPet(id, false);
            }

            if (pet != null)
            {
                if (pet.HasShanXian()) await SendShanXianOrderList();
                _player.LogInformation($"放生宠物: id:{pet.Id} cfgId:{pet.CfgId}");
            }

            return true;
        }

        public async Task ActivePet(uint id)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return;
            if (Pet != null)
            {
                Pet.Active = false;
                Pet = null;
            }

            Pet = pet;
            Pet.Active = true;
            await _player.SendPacket(GameCmd.S2CPetActive, new S2C_PetActive {Id = id});
            await Task.CompletedTask;
        }

        public async ValueTask<bool> AddPetExp(uint id, ulong exp)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return false;
            var ret = await pet.AddExp(exp);
            return ret;
        }

        public bool AddPetKeel(uint id)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return false;
            return pet.AddKeel();
        }

        public async ValueTask<bool> UseNingHunDan(uint id, uint exp)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return false;
            var ret = await pet.UseNingHunDan(exp);
            return ret;
        }

        public async ValueTask<bool> UseExp2HunPo(uint id, int expNum)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return false;
            var ret = await pet.UseExp2HunPo(expNum);
            return ret;
        }

        public bool UseYuanQiDan(uint id, uint enableCfgId, float rate)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return false;
            if (enableCfgId > 0 && pet.CfgId != enableCfgId)
            {
                _player.SendNotice("该元气丹不能被当前宠物使用!");
                return false;
            }

            return pet.UseYuanQiDan(rate);
        }

        public bool UseQinMiDan(uint id, int intimacy)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return false;
            if (intimacy <= 0) return false;
            return pet.AddIntimacy((uint) intimacy);
        }

        public async Task WashPet(uint id)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return;
            await pet.Wash();
        }

        public async Task SaveWash(uint id)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return;
            await pet.SaveWash();
        }

        public async Task RelivePet(uint id)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return;
            await pet.Relive();
        }

        public async Task CombinePet(uint cfgId)
        {
            // if (cfgId == 1061)
            // {
            //     _player.SendNotice("该神兽即将开放");
            //     return;
            // }

            ConfigService.Pets.TryGetValue(cfgId, out var cfg);
            if (cfg == null)
            {
                _player.SendNotice("宠物不存在");
                return;
            }

            // 检查能否合成
            var ret = CheckPetCanCombine(cfg, out var dic);
            if (!ret)
            {
                _player.SendNotice("超过最大数量或物品不足");
                return;
            }

            // 扣除物品
            foreach (var (k, v) in dic)
            {
                await _player.AddBagItem(k, -(int) v);
            }

            // 创建宠物
            await CreatePet(cfgId);
        }

        public async Task UnlockSkill(uint id)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return;
            await pet.UnlockSkill();
        }

        public async Task LearnSkill(uint id, int index, uint itemId)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return;
            await pet.LearnSkill(index, itemId);
        }

        public async Task ForgetSkill(uint id, SkillId skId)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return;
            await pet.ForgetSkill(skId);
        }

        public async Task LockSkill(uint id, SkillId skId, bool beLock)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return;
            await pet.LockSkill(skId, beLock);
        }

        public async Task ChangeSsSkill(uint id, SkillId skId)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return;
            await pet.ChangeSsSkill(skId);
        }

        public async Task Fly(uint id, uint type)
        {
            var pet = All.FirstOrDefault(p => p.Id == id);
            if (pet == null) return;
            await pet.Fly(type);
        }

        public Pet FindPet(uint id)
        {
            return All.FirstOrDefault(p => p.Id == id);
        }

        private bool CheckPetCanCombine(PetConfig cfg, out Dictionary<uint, uint> dic)
        {
            dic = null;
            if (cfg == null) return false;
            if (All.Count >= GameDefine.MaxPetNum) return false;
            // 统计所需材料
            dic = new Dictionary<uint, uint>();
            foreach (var itemId in cfg.NeedItem)
            {
                if (dic.ContainsKey(itemId))
                    dic[itemId] += 1;
                else
                    dic[itemId] = 1;
            }

            // 能合成的宠物一定是需要材料的
            if (dic.Count == 0) return false;

            // 检查材料是否足够
            foreach (var (k, v) in dic)
            {
                if (_player.GetBagItemNum(k) < v)
                {
                    dic = null;
                    return false;
                }
            }

            return true;
        }

        public static PetWashData RandomWashData(uint cfgId)
        {
            var washData = new PetWashData();

            ConfigService.Pets.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return washData;
            var rnd = new Random();

            // 成长率要扩大到1w倍
            var rateMin = (int) MathF.Floor(cfg.Rate[0] * 10000);
            var rateMax = (int) MathF.Floor(cfg.Rate[1] * 10000);

            // 有20%的概率获取最大成长率
            if (rnd.Next(0, 100) < 20)
            {
                washData.Rate = (uint) rateMax;
            }
            else
            {
                washData.Rate = (uint) rnd.Next(rateMin, rateMax);
            }

            // 气血、发力、攻击、速度 都是从min和max之间随机
            washData.Hp = (uint) rnd.Next((int) cfg.Hp[0], (int) (cfg.Hp[1] + 1));
            washData.HpMax = cfg.Hp[1];
            washData.Mp = (uint) rnd.Next((int) cfg.Mp[0], (int) (cfg.Mp[1] + 1));
            washData.MpMax = cfg.Mp[1];
            washData.Atk = (uint) rnd.Next((int) cfg.Atk[0], (int) (cfg.Atk[1] + 1));
            washData.AtkMax = cfg.Atk[1];
            washData.Spd = rnd.Next(cfg.Spd[0], cfg.Spd[1] + 1);
            washData.SpdMax = cfg.Spd[1];

            // 计算资质，根据洗出属性的最大值百分比,注意要防止最大值为0的情况
            var percents = new List<float>();
            if (washData.Rate == rateMax) percents.Add(1);
            else percents.Add(washData.Rate * 1.0f / rateMax);
            if (washData.Hp == cfg.Hp[1]) percents.Add(1);
            else percents.Add(washData.Hp * 1.0f / cfg.Hp[1]);
            if (washData.Mp == cfg.Mp[1]) percents.Add(1);
            else percents.Add(washData.Mp * 1.0f / cfg.Mp[1]);
            if (washData.Atk == cfg.Atk[1]) percents.Add(1);
            else percents.Add(washData.Atk * 1.0f / cfg.Atk[1]);
            if (washData.Spd == cfg.Spd[1]) percents.Add(1);
            else percents.Add(washData.Spd * 1.0f / cfg.Spd[1]);

            var fullNum = percents.Count(p => p == 1f); //点满的个数
            var low90Num = percents.Count(p => p < 0.9f); //低于90%的个数
            washData.Quality = PetQuality.ZiZhiPingPing;
            if (percents[0] == 1f)
            {
                if (fullNum >= 4 && low90Num == 0)
                {
                    // 4项属性中，3个满，1个不低于90%
                    washData.Quality = PetQuality.WanZhongWuYi;
                }
                else if (fullNum >= 3 && low90Num < 2)
                {
                    // 4项属性中，2个满，2个不低于90%
                    washData.Quality = PetQuality.MiaoLingTianJi;
                }
                else
                {
                    // 成长率能满最次都是一个出类拔萃
                    washData.Quality = PetQuality.ChuLeiBaCui;
                }
            }
            else if (fullNum >= 4)
            {
                // 如果能点满4个
                washData.Quality = PetQuality.MiaoLingTianJi;
            }
            else if (low90Num <= 2)
            {
                washData.Quality = PetQuality.ChuLeiBaCui;
            }

            return washData;
        }

        // 获取宠物最大技能
        public static uint GetMaxSkillNum(uint cfgId)
        {
            ConfigService.Pets.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return 4;
            if (cfg.MaxSkillCnt > 0) return cfg.MaxSkillCnt;
            return 4;
        }

        // 获取宠物最大成长率
        public static uint GetMaxRate(uint cfgId)
        {
            ConfigService.Pets.TryGetValue(cfgId, out var cfg);
            if (cfg == null) return 0;
            return (uint) MathF.Floor(cfg.Rate[1] * 10000);
        }

        public static uint GetMaxKeel(byte relive)
        {
            if (relive == 0) return 2;
            if (relive == 1) return 4;
            if (relive == 2) return 7;
            if (relive == 3) return 12;
            return 12;
        }
    }
}