using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Grains;
using Ddxy.Protocol;

namespace Ddxy.GameServer.Logic.Mail
{
    public class MailManager
    {
        private PlayerGrain _player;

        private List<Mail> _myMails;
        private List<MailData> _sysMails;

        public MailManager(PlayerGrain player)
        {
            _player = player;
            _myMails = new List<Mail>(3);
            _sysMails = new List<MailData>(3);
        }

        public async Task Init()
        {
            var now = TimeUtil.TimeStamp;
            var entities = await DbService.Sql.Queryable<MailEntity>()
                .Where(it =>
                    it.ServerId == _player.Entity.ServerId && it.Recver == _player.RoleId && it.DeleteTime == 0)
                .ToListAsync();
            foreach (var entity in entities)
            {
                if (now >= entity.ExpireTime)
                {
                    await DbService.DeleteEntity<MailEntity>(entity.Id);
                    continue;
                }

                if (entity.DeleteTime > 0) continue;

                _myMails.Add(new Mail(entity));
            }
        }

        public void Destroy()
        {
            foreach (var mail in _myMails)
            {
                mail.Dispose();
            }

            _myMails.Clear();
            _myMails = null;
            _sysMails.Clear();
            _sysMails = null;

            _player = null;
        }

        public async Task SendList()
        {
            var resp = new S2C_MailList();
            var now = TimeUtil.TimeStamp;

            for (var i = _sysMails.Count - 1; i >= 0; i--)
            {
                var mail = _sysMails[i];
                if (now >= mail.ExpireTime)
                {
                    _sysMails.RemoveAt(i);
                    continue;
                }

                resp.List.Add(mail);
            }

            for (var i = _myMails.Count - 1; i >= 0; i--)
            {
                var mail = _myMails[i];
                if (now >= mail.Entity.ExpireTime)
                {
                    await DbService.DeleteEntity<MailEntity>(mail.Id);
                    mail.Dispose();
                    _myMails.RemoveAt(i);
                    continue;
                }

                resp.List.Add(mail.BuildPbData());
            }

            await _player.SendPacket(GameCmd.S2CMailList, resp);
        }

        // 玩家enter server的时候查询一次系统邮件
        public async Task Reload()
        {
            _sysMails.Clear();

            // 先获取当前本区所有的邮件(包含我已经删除了的、领取的)
            var bytes = await _player.ServerGrain.QueryMails();
            if (bytes.Value != null)
            {
                var resp = S2C_MailList.Parser.ParseFrom(bytes.Value);
                _sysMails.AddRange(resp.List);
            }

            var needSync = false;
            foreach (var k in _player.Mails.Keys.ToList())
            {
                // 如果我已删除/已领取的邮件id，已经被Server删除记录, 防止记录堆积，这里也对记录进行删除
                if (!_sysMails.Exists(p => p.Id == k))
                {
                    _player.Mails.Remove(k);
                    needSync = true;
                }
            }

            if (needSync) _player.SyncMails();

            // 检查系统邮件是否已过期
            var now = TimeUtil.TimeStamp;
            for (var i = _sysMails.Count - 1; i >= 0; i--)
            {
                var mail = _sysMails[i];
                // 已过期
                if (now >= mail.ExpireTime)
                {
                    _sysMails.RemoveAt(i);
                    continue;
                }

                // 检查是否高于领取的最高等级, 如果是就不显示
                if (mail.MaxLevel != 0)
                {
                    if (_player.Entity.Relive > mail.MaxRelive || _player.Entity.Relive == mail.MaxRelive &&
                        _player.Entity.Level > mail.MaxLevel)
                    {
                        _sysMails.RemoveAt(i);
                        continue;
                    }
                }

                // 已操作
                if (_player.Mails.TryGetValue(mail.Id, out var flag))
                {
                    if (flag == 0)
                    {
                        // 已领取
                        mail.Picked = true;
                    }
                    else
                    {
                        // 已删除
                        _sysMails.RemoveAt(i);
                    }
                }
            }

            // 检查私人邮件是否已过期
            for (var i = _myMails.Count - 1; i >= 0; i--)
            {
                var mail = _myMails[i];
                if (mail.Expire)
                {
                    await DbService.DeleteEntity<MailEntity>(mail.Id);
                    mail.Dispose();
                    _myMails.RemoveAt(i);
                    continue;
                }

                // 检查是否高于领取的最高等级, 如果是就不显示
                if (mail.Entity.MaxLevel > 0)
                {
                    if (_player.Entity.Relive > mail.Entity.MaxRelive ||
                        _player.Entity.Relive == mail.Entity.MaxRelive &&
                        _player.Entity.Level > mail.Entity.MaxLevel)
                    {
                        await DbService.DeleteEntity<MailEntity>(mail.Id);
                        mail.Dispose();
                        _myMails.RemoveAt(i);
                        continue;
                    }
                }

                if (mail.Entity.DeleteTime > 0)
                {
                    _myMails.RemoveAt(i);
                    mail.Dispose();
                }
            }
        }

        public async Task Pick(IList<uint> ids)
        {
            if (ids == null || ids.Count == 0) return;
            var needSync = false;
            var resp = new S2C_MailPick();
            foreach (var id in ids)
            {
                // 检查是否在个人邮件中
                var idx = _myMails.FindIndex(p => p.Id == id);
                if (idx >= 0)
                {
                    var mail = _myMails[idx];
                    // 过期了
                    if (mail.Expire)
                    {
                        _player.SendNotice("邮件已过期");
                        _myMails.RemoveAt(idx);
                        continue;
                    }

                    // 已经领取过了
                    if (mail.Entity.PickedTime > 0) continue;

                    // 检查等级是否符合最低等级要求
                    if (mail.Entity.MinLevel != 0)
                    {
                        if (_player.Entity.Relive < mail.Entity.MinRelive ||
                            _player.Entity.Relive == mail.Entity.MinRelive &&
                            _player.Entity.Level < mail.Entity.MinLevel)
                        {
                            _player.SendNotice($"请先升级至{mail.Entity.MinRelive}转{mail.Entity.MinLevel}级");
                            continue;
                        }
                    }

                    // 检查等级是否符合最高等级要求
                    if (mail.Entity.MaxLevel != 0)
                    {
                        if (_player.Entity.Relive > mail.Entity.MaxRelive ||
                            _player.Entity.Relive == mail.Entity.MaxRelive &&
                            _player.Entity.Level > mail.Entity.MaxLevel)
                        {
                            _player.SendNotice($"您的等级高于{mail.Entity.MaxRelive}转{mail.Entity.MaxLevel}级，无法领取");
                            continue;
                        }
                    }

                    // 分析是否溢出, 停止领取
                    if (mail.Items is {Count: > 0} && _player.CheckIsBagOverflow((uint) mail.Items.Count))
                    {
                        _player.SendNotice("背包空间不足");
                        break;
                    }

                    // 及时入库
                    await DbService.Sql.Update<MailEntity>()
                        .Where(it => it.Id == id)
                        .Set(it => it.PickedTime, TimeUtil.TimeStamp)
                        .ExecuteAffrowsAsync();
                    mail.Entity.PickedTime = TimeUtil.TimeStamp;

                    // 发放奖励
                    if (mail.Items is {Count: > 0})
                    {
                        foreach (var idata in mail.Items)
                        {
                            await _player.AddItem(idata.Id, (int) idata.Num, true, "领取邮件");
                        }
                    }

                    resp.Ids.Add(id);
                    continue;
                }

                // 检查是否在全服邮件中
                idx = _sysMails.FindIndex(p => p.Id == id);
                if (idx >= 0)
                {
                    var mail = _sysMails[idx];
                    // 过期了
                    if (TimeUtil.TimeStamp >= mail.ExpireTime)
                    {
                        _player.SendNotice("邮件已过期");
                        _sysMails.RemoveAt(idx);
                        continue;
                    }

                    // 我已经删除/领取了该邮件
                    if (mail.Picked) continue;
                    if (_player.Mails.ContainsKey(mail.Id)) continue;
                    // 去server校验一下
                    var ret = await _player.ServerGrain.CheckMail(mail.Id);
                    if (!ret)
                    {
                        _player.SendNotice("邮件已过期");
                        _sysMails.RemoveAt(idx);
                        continue;
                    }

                    // 检查等级是否符合最低等级要求
                    if (mail.MinLevel != 0)
                    {
                        if (_player.Entity.Relive < mail.MinRelive ||
                            _player.Entity.Relive == mail.MinRelive &&
                            _player.Entity.Level < mail.MinLevel)
                        {
                            _player.SendNotice($"请先升级至{mail.MinRelive}转{mail.MinLevel}级");
                            continue;
                        }
                    }

                    // 检查等级是否符合最高等级要求
                    if (mail.MaxLevel != 0)
                    {
                        if (_player.Entity.Relive > mail.MaxRelive ||
                            _player.Entity.Relive == mail.MaxRelive &&
                            _player.Entity.Level > mail.MaxLevel)
                        {
                            _player.SendNotice($"您的等级高于{mail.MaxRelive}转{mail.MaxLevel}级，无法领取");
                            continue;
                        }
                    }

                    // 分析是否溢出, 停止领取
                    if (mail.Items is {Count: > 0} && _player.CheckIsBagOverflow((uint) mail.Items.Count))
                    {
                        _player.SendNotice("背包空间不足");
                        break;
                    }

                    // 记录我已经领取
                    mail.Picked = true;
                    _player.Mails[mail.Id] = 0;
                    needSync = true;

                    // 发放奖励
                    if (mail.Items is {Count: > 0})
                    {
                        foreach (var idata in mail.Items)
                        {
                            await _player.AddItem(idata.Id, (int) idata.Num, true, "领取邮件");
                        }
                    }

                    resp.Ids.Add(id);
                }
            }

            if (needSync) _player.SyncMails();
            await _player.SendPacket(GameCmd.S2CMailPick, resp);
        }

        public async Task Delete(IList<uint> ids)
        {
            if (ids == null || ids.Count == 0) return;
            var resp = new S2C_MailDel();
            var needSync = false;
            foreach (var id in ids)
            {
                // 检查是否在个人邮件中
                var idx = _myMails.FindIndex(p => p.Id == id);
                if (idx >= 0)
                {
                    var mail = _myMails[idx];
                    _myMails.RemoveAt(idx);
                    // 我的邮件可以标记为删除, 待过期之后再交给系统删除数据记录
                    await DbService.Sql.Update<MailEntity>()
                        .Where(it => it.Id == id)
                        .Set(it => it.DeleteTime, TimeUtil.TimeStamp)
                        .ExecuteAffrowsAsync();
                    mail.Dispose();
                    resp.Ids.Add(id);

                    _player.LogDebug($"删除邮件{id}");
                    continue;
                }

                // 检查是否在全服邮件中
                idx = _sysMails.FindIndex(p => p.Id == id);
                if (idx >= 0)
                {
                    var mail = _sysMails[idx];
                    _sysMails.RemoveAt(idx);
                    // 记录我删除的全服邮件
                    _player.Mails[mail.Id] = 1;
                    needSync = true;
                    resp.Ids.Add(id);

                    _player.LogDebug($"删除邮件{id}");
                }

                if (!resp.Ids.Contains(id))
                {
                    resp.Ids.Add(id);
                }
            }

            if (needSync) _player.SyncMails();
            await _player.SendPacket(GameCmd.S2CMailDel, resp);
        }

        public async Task RecvMail(uint id)
        {
            if (id == 0) return;

            var entity = await DbService.Sql.Queryable<MailEntity>().Where(it => it.Id == id).FirstAsync();
            if (entity == null) return;
            var md = new Mail(entity);
            _myMails.Add(md);

            if (_player.IsEnterServer)
            {
                await _player.SendPacket(GameCmd.S2CMailAdd, new S2C_MailAdd {Data = md.BuildPbData()});
            }
        }

        public async Task DelMail(uint id)
        {
            if (id == 0) return;
            var idx = _myMails.FindIndex(p => p.Id == id);
            if (idx < 0) return;
            _myMails.RemoveAt(idx);

            if (_player.IsEnterServer)
            {
                await _player.SendPacket(GameCmd.S2CMailDel, new S2C_MailDel {Ids = {id}});
            }
        }

        public async Task RecvMail(MailData md)
        {
            if (md == null) return;
            _sysMails.Add(md);
            if (_player.IsEnterServer)
            {
                await _player.SendPacket(GameCmd.S2CMailAdd, new S2C_MailAdd {Data = md});
            }
        }

        public async Task DelMail(MailData md)
        {
            if (md == null) return;
            var idx = _sysMails.FindIndex(p => p.Id == md.Id);
            if (idx < 0) return;
            var mail = _sysMails[idx];
            _sysMails.RemoveAt(idx);

            if (_player.IsEnterServer)
            {
                await _player.SendPacket(GameCmd.S2CMailDel, new S2C_MailDel {Ids = {mail.Id}});
            }
        }
    }
}