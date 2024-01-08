using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Title
{
    public class TitleManager
    {
        private PlayerGrain _player;
        public List<Title> All { get; private set; }
        public Title Title { get; private set; }

        public TitleManager(PlayerGrain player)
        {
            _player = player;
            All = new List<Title>(10);
        }

        public async Task Init()
        {
            var deletes = new List<uint>();
            var now = TimeUtil.TimeStamp;

            var entities = await DbService.QueryTitles(_player.RoleId);
            foreach (var entity in entities)
            {
                // 过期的自动删除
                if (entity.ExpireTime > 0 && now >= entity.ExpireTime)
                {
                    deletes.Add(entity.Id);
                    continue;
                }

                var data = new Title(_player, entity);
                All.Add(data);
            }

            foreach (var tid in deletes)
            {
                await DelTitle(tid);
            }
        }

        public async Task Destroy()
        {
            var tasks = from p in All select p.Destroy();
            await Task.WhenAll(tasks);

            All.Clear();
            All = null;
            Title = null;

            _player = null;
        }

        public Task SaveData()
        {
            var tasks = from p in All select p.SaveData();
            return Task.WhenAll(tasks);
        }

        public async Task SendList()
        {
            var resp = new S2C_TitleList();
            foreach (var data in All)
            {
                resp.List.Add(data.BuildPbData());
            }

            // 下发宠物列表
            await _player.SendPacket(GameCmd.S2CTitleList, resp);
        }

        public async Task<Title> AddTitle(uint cfgId, string text = "", uint seconds = 0,
            bool send = true)
        {
            var exists = All.Exists(p => p.CfgId == cfgId);
            if (exists) return null;
            var now = TimeUtil.TimeStamp;
            var expire = seconds == 0 ? 0 : now + seconds;
            var entity = new TitleEntity
            {
                RoleId = _player.RoleId,
                CfgId = cfgId,
                Text = text,
                Active = false,
                CreateTime = now,
                ExpireTime = expire
            };
            await DbService.InsertEntity(entity);
            if (entity.Id == 0) return null;

            var title = new Title(_player, entity);
            All.Add(title);
            if (send) await _player.SendPacket(GameCmd.S2CTitleAdd, new S2C_TitleAdd {Data = title.BuildPbData()});
            return title;
        }

        public async ValueTask<bool> DelTitle(uint id, bool send = true)
        {
            var ret = await DbService.DeleteEntity<TitleEntity>(id);
            if (!ret) return false;

            var idx = All.FindIndex(p => p.Id == id);
            if (idx >= 0) All.RemoveAt(idx);
            if (Title != null && Title.Id == id)
            {
                _player.RefreshTitle();
                Title.Active = false;
                Title = null;
            }

            if (send) await _player.SendPacket(GameCmd.S2CTitleDel, new S2C_TitleDel {Id = id});
            return true;
        }

        public async Task AddSectTitle(SectMemberType type, string sectName)
        {
            // 移除帮派称号
            await DelSectTitles();
            var cfgId = type switch
            {
                SectMemberType.BangZhu => TitleId.BangZhu,
                SectMemberType.FuBangZhu => TitleId.FuBangZhu,
                SectMemberType.ZuoHuFa => TitleId.ZuoHuFa,
                SectMemberType.YouHuFa => TitleId.YouHuFa,
                SectMemberType.ZhangLao => TitleId.ZhangLao,
                SectMemberType.TangZhu => TitleId.TangZhu,
                SectMemberType.TuanZhang => TitleId.TuanZhang,
                _ => TitleId.BangZhong
            };
            await AddTitle((uint) cfgId, sectName);
        }

        public async Task DelSectTitles(bool send = true)
        {
            var deletes = All.Where(p => IsSectTitle(p.CfgId)).ToList();
            foreach (var t in deletes)
            {
                await DelTitle(t.Id);
            }
        }

        public async ValueTask<bool> ActiveTitle(uint id, bool active = true)
        {
            var newTitle = All.FirstOrDefault(p => p.Id == id);
            if (newTitle == null || newTitle.Active == active) return false;

            if (active)
            {
                if (Title != null)
                {
                    if (Title.Id == id) return false;
                    // 将当前Title取消激活
                    await _player.SendPacket(GameCmd.S2CTitleChange, new S2C_TitleChange
                    {
                        Id = Title.Id,
                        Active = false
                    });
                    Title.Active = false;
                    Title = null;
                }

                newTitle.Active = true;
                Title = newTitle;
            }
            else
            {
                newTitle.Active = false;
                if (Title != null && Title.Id == id)
                {
                    Title = null;
                }
            }

            await _player.SendPacket(GameCmd.S2CTitleChange, new S2C_TitleChange
            {
                Id = id,
                Active = active
            });

            _player.RefreshTitle();

            return true;
        }

        public static bool IsSectTitle(uint cfgId)
        {
            return cfgId is >= (uint) TitleId.BangZhong and <= (uint) TitleId.BangZhu;
        }
    }
}