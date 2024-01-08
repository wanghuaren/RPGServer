using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Ddxy.Common.Jwt;
using Ddxy.Common.Model;
using Ddxy.Common.Model.Admin;
using Ddxy.Common.Utils;
using Ddxy.GameServer.Data;
using Ddxy.GameServer.Data.Entity;
using Ddxy.GameServer.Data.Vo;
using Ddxy.GameServer.Gate;
using Ddxy.GameServer.Http;
using Ddxy.GameServer.Util;
using Ddxy.GrainInterfaces;
using Ddxy.GrainInterfaces.Gate;
using Ddxy.Protocol;
using FreeSql;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Orleans;
using Orleans.Concurrency;
using Orleans.Runtime;

namespace Ddxy.GameServer.Gate
{
    [StatelessWorker(1000)]
    [Reentrant]
    public class AdminGateGrain : Grain, IAdminGateGrain
    {
        private readonly ILogger<AdminGateGrain> _logger;
        private readonly JwtOptions _jwtOptions;
        private readonly XinPayOptions _xinPayOptions;

        public AdminGateGrain(ILogger<AdminGateGrain> logger, IOptions<JwtOptions> jwtOptions, IOptions<XinPayOptions> xinPayOptions)
        {
            _logger = logger;
            _jwtOptions = jwtOptions.Value;
            _xinPayOptions = xinPayOptions.Value;
        }

        /// <summary>
        /// 登录
        /// </summary>
        public async Task<Immutable<byte[]>> SignIn(string ip, string username, string password)
        {
            // 先通过用户名查找记录
            var entity = await DbService.Sql.Queryable<AdminEntity>().Where(it => it.UserName == username).FirstAsync();
            if (entity == null)
                return JsonResp.Error("账号不存在").Serialize();
            // 判断密码是否匹配
            var pass = PasswordUtil.Encode(password, entity.PassSalt);
            if (!string.Equals(pass, entity.Password))
                return JsonResp.Error("密码错误").Serialize();
            // 判断是否被封禁
            if (entity.Status != AdminStatus.Normal)
                return JsonResp.Error("账号被冻结").Serialize();
            if (entity.ParentId > 0)
            {
                var obj = await DbService.Sql.Queryable<AdminEntity>()
                    .Where(it => it.Id == entity.ParentId)
                    .FirstAsync(it => new {it.NickName, it.InvitCode});
                if (obj != null)
                {
                    entity.FatherName = obj.NickName;
                    entity.FatherInvitCode = obj.InvitCode;
                }
            }

            // if (entity.Category <= AdminCategory.Admin) entity.LoginTime = 0;
            // else entity.LoginTime = TimeUtil.TimeStamp;
            entity.LoginTime = TimeUtil.TimeStamp;
            entity.LoginIp = ip;
            await DbService.Sql.Update<AdminEntity>()
                .Where(it => it.Id == entity.Id)
                .Set(it => it.LoginTime, entity.LoginTime)
                .Set(it => it.LoginIp, entity.LoginIp)
                .ExecuteAffrowsAsync();
            // 构建token
            var token = TokenUtil.GenToken(_jwtOptions, new[]
            {
                new Claim(ClaimTypes.Sid, entity.Id.ToString()),
                new Claim(ClaimTypes.Role, ((byte) entity.Category).ToString())
            });
            // 缓存Admin的信息, 如果需要用户重新登录就删除该信息
            await RedisService.SetAdminInfo(entity.Id, entity.Category);
            await RedisService.SetAdminAgencyInfo(entity.Id, entity.Agency);
            return JsonResp.Ok(entity, token).Serialize();
        }

        /// <summary>
        /// 处理登录后的所有操作
        /// </summary>
        public async Task<Immutable<byte[]>> Handle(uint opUid, string ip, string method, Immutable<byte[]> payload)
        {
            var category = await RedisService.GetAdminInfo(opUid);
            if (category < AdminCategory.System || category > AdminCategory.Agency)
                return JsonResp.Unauthorized().Serialize();
            var agency = await RedisService.GetAdminAgencyInfo(opUid);
            JsonResp resp = null;
            switch (method)
            {
                case "profile":
                    resp = await Profile(opUid);
                    break;
                case "signout":
                    resp = await SignOut(opUid);
                    break;
                case "save_profile":
                {
                    var req = ParsePayload<SaveProfileReq>(payload);
                    resp = await SaveProfile(opUid, category, req);
                }
                    break;
                case "list_admin":
                {
                    var req = ParsePayload<ListPageReq>(payload);
                    resp = await ListAdmin(req);
                }
                    break;
                case "add_admin":
                {
                    var req = ParsePayload<AddAdminReq>(payload);
                    resp = await AddAdmin(req);
                }
                    break;
                case "del_admin":
                {
                    var req = ParsePayload<DelAdminReq>(payload);
                    resp = await DelAdmin(req.Id.GetValueOrDefault());
                }
                    break;
                case "edit_admin":
                {
                    var req = ParsePayload<EditAdminReq>(payload);
                    resp = await EditAdmin(req);
                }
                    break;
                case "froze_admin":
                {
                    var req = ParsePayload<FrozeAdminReq>(payload);
                    resp = await FrozeAdmin(req);
                }
                    break;
                case "list_agency":
                {
                    var req = ParsePayload<ListAgencyReq>(payload);
                    resp = await ListAgency(opUid, category, req);
                }
                    break;
                case "add_agency":
                {
                    var req = ParsePayload<AddAdminReq>(payload);
                    resp = await AddAgency(opUid, category, req);
                }
                    break;
                case "edit_agency":
                {
                    var req = ParsePayload<EditAdminReq>(payload);
                    resp = await EditAgency(opUid, category, req);
                }
                    break;
                case "froze_agency":
                {
                    var req = ParsePayload<FrozeAdminReq>(payload);
                    resp = await FrozeAgency(opUid, category, req);
                }
                    break;
                case "get_notice":
                {
                    resp = await GetNotice();
                }
                    break;
                case "set_notice":
                {
                    var req = ParsePayload<SetNoticeReq>(payload);
                    resp = await SetNotice(req.Text);
                }
                    break;
                case "get_pay_enable":
                {
                    resp = await GetPayEnable();
                }
                    break;
                case "set_pay_enable":
                {
                    var req = ParsePayload<SetPayEnableReq>(payload);
                    resp = await SetPayEnable(req.Enable);
                }
                    break;
                case "get_pay_rate":
                {
                    resp = await GetPayRate();
                }
                    break;
                case "set_pay_rate":
                {
                    var req = ParsePayload<SetPayRateReq>(payload);
                    resp = await SetPayRate(req.Rate.GetValueOrDefault(), req.BindRate.GetValueOrDefault());
                }
                    break;
                case "list_mail":
                {
                    var req = ParsePayload<ListMailReq>(payload);
                    resp = await ListMail(req);
                }
                    break;
                case "add_mail":
                {
                    var req = ParsePayload<AddMailReq>(payload);
                    resp = await AddMail(opUid, req);
                }
                    break;
                case "del_mail":
                {
                    var req = ParsePayload<DelMailReq>(payload);
                    resp = await DelMail(req);
                }
                    break;
                case "get_res_version":
                {
                    resp = await GetResVerion();
                }
                    break;
                case "set_res_version":
                {
                    var req = ParsePayload<SetResVersionReq>(payload);
                    resp = await SetResVerion(req);
                }
                    break;
                case "reload_config":
                {
                    resp = await ReloadConfig();
                }
                    break;
                case "list_server":
                {
                    var req = ParsePayload<ListPageReq>(payload);
                    resp = await ListServer(req);
                }
                    break;
                case "list_server1":
                {
                    resp = await ListServer1();
                }
                    break;
                case "add_server":
                {
                    var req = ParsePayload<AddServerReq>(payload);
                    resp = await AddServer(req);
                }
                    break;
                case "edit_server":
                {
                    var req = ParsePayload<EditServerReq>(payload);
                    resp = await EditServer(req);
                }
                    break;
                case "change_server_status":
                {
                    var req = ParsePayload<ChangeServerStatusReq>(payload);
                    resp = await ChangeServerStatus(req);
                }
                    break;
                case "start_server":
                {
                    var req = ParsePayload<StartServerReq>(payload);
                    resp = await StartServer(req.Id);
                }
                    break;
                case "stop_server":
                {
                    var req = ParsePayload<StartServerReq>(payload);
                    resp = await StopServer(req.Id);
                }
                    break;
                case "open_activity":
                {
                    var req = ParsePayload<OpenActivityReq>(payload);
                    resp = await OpenActivity(opUid, req);
                }
                    break;
                case "query_combine_server":
                {
                    resp = await QueryCombineServer();
                }
                    break;
                case "combine_server":
                {
                    var req = ParsePayload<CombineServerReq>(payload);
                    resp = await CombineServer(req);
                }
                    break;
                case "list_user":
                {
                    var req = ParsePayload<ListUserReq>(payload);
                    resp = await ListUser(opUid, category, req);
                }
                    break;
                case "edit_user":
                {
                    var req = ParsePayload<EditUserReq>(payload);
                    resp = await EditUser(opUid, category, req);
                }
                    break;
                case "froze_user":
                {
                    var req = ParsePayload<FrozeUserReq>(payload);
                    resp = await FrozeUser(opUid, category, req);
                }
                    break;
                case "list_role":
                {
                    var req = ParsePayload<ListRoleReq>(payload);
                    resp = await ListRole(opUid, category, req);
                }
                    break;
                case "froze_role":
                {
                    var req = ParsePayload<FrozeRoleReq>(payload);
                    resp = await FrozeRole(opUid, category, agency, req);
                }
                    break;
                case "change_role_online":
                {
                    var req = ParsePayload<ChangeRoleOnlineReq>(payload);
                    resp = await ChangeRoleOnline(opUid, category, agency, req);
                }
                    break;
                case "get_role_detail":
                {
                    var req = ParsePayload<GetRoleDetailReq>(payload);
                    resp = await GetRoleDetail(opUid, category, req);
                }
                    break;
                case "get_role_equips":
                {
                    var req = ParsePayload<GetRoleEquipsReq>(payload);
                    resp = await GetRoleEquips(opUid, category, req);
                }
                    break;
                case "set_equip_refine":
                {
                    var req = ParsePayload<SetEquipRefineReq>(payload);
                    resp = await SetEquipRefine(opUid, category, req);
                }
                    break;
                case "get_role_ornaments":
                {
                    var req = ParsePayload<GetRoleEquipsReq>(payload);
                    resp = await GetRoleOrnaments(opUid, category, req);
                }
                    break;
                case "get_role_mounts":
                {
                    var req = ParsePayload<GetRoleEquipsReq>(payload);
                    resp = await GetRoleMounts(opUid, category, req);
                }
                    break;
                case "set_mount_skill":
                {
                    var req = ParsePayload<SetMountSkillReq>(payload);
                    resp = await SetMountSkill(opUid, category, req);
                }
                    break;
                case "change_role_level":
                {
                    var req = ParsePayload<ChangeRoleLevelReq>(payload);
                    resp = await ChangeRoleLevel(opUid, category, req);
                }
                    break;
                // case "change_role_money":
                // {
                //     var req = ParsePayload<ChangeRoleMoneyReq>(payload);
                //     resp = await ChangeRoleMoney(opUid, category, req);
                // }
                //     break;
                case "change_role_item":
                {
                    var req = ParsePayload<ChangeRoleItemReq>(payload);
                    resp = await ChangeRoleItem(opUid, category, req);
                }
                    break;
                case "change_role_star":
                {
                    var req = ParsePayload<ChangeRoleStarReq>(payload);
                    resp = await ChangeRoleStar(opUid, req);
                }
                    break;
                case "change_role_totalpay":
                {
                    var req = ParsePayload<ChangeRoleTotalPayReq>(payload);
                    resp = await ChangeRoleTotalPay(opUid, req);
                }
                    break;
                case "add_role_skillexp":
                {
                    var req = ParsePayload<GetRoleDetailReq>(payload);
                    resp = await AddRoleSkillExp(opUid, req);
                }
                    break;
                case "add_role_equip":
                {
                    var req = ParsePayload<AddRoleEquipReq>(payload);
                    resp = await AddRoleEquip(opUid, req);
                }
                    break;
                case "add_role_ornament":
                {
                    var req = ParsePayload<AddRoleOrnamentReq>(payload);
                    resp = await AddRoleOrnament(opUid, req);
                }
                    break;
                case "add_role_wing":
                {
                    var req = ParsePayload<AddRoleWingReq>(payload);
                    resp = await AddRoleWing(opUid, req);
                }
                    break;
                case "add_role_title":
                {
                    var req = ParsePayload<AddRoleTitleReq>(payload);
                    resp = await AddRoleTitle(opUid, req);
                }
                    break;
                case "del_role_title":
                {
                    var req = ParsePayload<DelRoleTitleReq>(payload);
                    resp = await DelRoleTitle(opUid, req);
                }
                    break;
                case "del_role_shane":
                {
                    var req = ParsePayload<DelRoleShaneReq>(payload);
                    resp = await DelRoleShane(opUid, req);
                }
                    break;
                case "set_role_type":
                {
                    var req = ParsePayload<SetRoleTypeReq>(payload);
                    resp = await SetRoleType(req);
                }
                    break;
                case "set_role_flag":
                {
                    var req = ParsePayload<SetRoleFlagReq>(payload);
                    resp = await SetRoleFlag(opUid, category, req);
                }
                    break;
                case "recharge":
                {
                    var req = ParsePayload<RechargeReq>(payload);
                    resp = await Recharge(opUid, category, req);
                }
                    break;
                case "recharge_role":
                {
                    var req = ParsePayload<RechargeRoleReq>(payload);
                    resp = await RechargeRole(opUid, category, req);
                }
                    break;
                case "list_recharge":
                {
                    var req = ParsePayload<ListRechargeReq>(payload);
                    resp = await ListRecharge(opUid, category, req);
                }
                    break;
                case "del_recharge":
                {
                    var req = ParsePayload<DelRecordsReq>(payload);
                    resp = await DelRecharge(opUid, category, req);
                }
                    break;
                case "list_recharge_role":
                {
                    var req = ParsePayload<ListRechargeRoleReq>(payload);
                    resp = await ListRechargeRole(opUid, category, req);
                }
                    break;
                case "del_recharge_role":
                {
                    var req = ParsePayload<DelRecordsReq>(payload);
                    resp = await DelRechargeRole(opUid, category, req);
                }
                    break;
                case "list_pay":
                {
                    var req = ParsePayload<ListPayReq>(payload);
                    resp = await ListPay(opUid, category, req);
                }
                    break;
                case "del_pay":
                {
                    var req = ParsePayload<DelRecordsReq>(payload);
                    resp = await DelPay(opUid, category, req);
                }
                    break;
                // case "refresh_order":
                // {
                //     var req = ParsePayload<RefreshOrderReq>(payload);
                //     resp = await RefreshOrder(opUid, category, req);
                // }
                //     break;
                case "list_pay_records":
                {
                    var req = ParsePayload<ListPayRecordsReq>(payload);
                    resp = await ListPayRecords(opUid, category, req);
                }
                    break;
                case "list_rank_level":
                {
                    var req = ParsePayload<ListRankReq>(payload);
                    resp = await ListRankLevel(req);
                }
                    break;
                case "list_rank_jade":
                {
                    var req = ParsePayload<ListRankReq>(payload);
                    resp = await ListRankJade(req);
                }
                    break;
                case "list_rank_pay":
                {
                    var req = ParsePayload<ListRankReq>(payload);
                    resp = await ListRankPay(req);
                }
                    break;
                case "list_rank_sldh":
                {
                    var req = ParsePayload<ListRankReq>(payload);
                    resp = await ListRankSldh(req);
                }
                    break;
                case "list_rank_sect":
                {
                    var req = ParsePayload<ListRankReq>(payload);
                    resp = await ListRankSect(req);
                }
                    break;
                case "dismiss_sect":
                {
                    var req = ParsePayload<DissmissSectReq>(payload);
                    resp = await DismissSect(opUid, req);
                }
                    break;
                case "reload_sects":
                {
                    var req = ParsePayload<ReloadSectsReq>(payload);
                    resp = await ReloadSects(req);
                }
                    break;
                case "list_rank_single_pk":
                {
                    var req = ParsePayload<ListRankReq>(payload);
                    resp = await ListRankSinglePk(req);
                }
                    break;
                case "list_log_op":
                {
                    var req = ParsePayload<ListOpLogReq>(payload);
                    resp = await ListOpLog(req);
                }
                    break;
                case "list_log_bug":
                {
                    var req = ParsePayload<ListBugLogReq>(payload);
                    resp = await ListBugLog(req);
                }
                    break;
                case "del_log_bug":
                {
                    var req = ParsePayload<DelRecordsReq>(payload);
                    resp = await DelBugLog(opUid, category, req);
                }
                    break;
                case "send_palace_notice":
                {
                    var req = ParsePayload<SendPalaceNoticeReq>(payload);
                    resp = await SendPalaceNotice(req.Id, req.Msg, req.Times);
                }
                    break;
                case "send_role_gift":
                {
                    var req = ParsePayload<SendRoleGiftReq>(payload);
                    resp = await SendRoleGift(req.Id);
                }
                    break;
                //case "send_set_limit_charge_rank":
                //{
                //    var req = ParsePayload<SendSetLimitChargeRankReq>(payload);
                //    resp = await SendSetLimitChargeRank(req.Server, req.Start, req.End, req.Cleanup);
                //}
                //    break;
                case "send_get_limit_charge_rank":
                {
                    var req = ParsePayload<SendGetLimitChargeRankReq>(payload);
                    resp = await SendGetLimitChargeRank(req.Server);
                }
                    break;
                case "list_limit_charge_rank":
                {
                    var req = ParsePayload<ListLimitRankReq>(payload);
                    resp = await ListLimitRank(req);
                }
                    break;
                case "list_chat_log":
                {
                    var req = ParsePayload<ListChatLogReq>(payload);
                    resp = await ListChatLog(opUid, category, req);
                }
                    break;
                case "del_chat_log":
                {
                    var req = ParsePayload<DelRecordsReq>(payload);
                    resp = await DelChatLog(opUid, category, req);
                }
                    break;
                // case "reset_first_pay":
                // {
                //     resp = await ResetFirstPay(opUid);
                //     break;
                // }
            }

            resp ??= JsonResp.BadOperation();
            return resp.Serialize();
        }

        private async Task<JsonResp> Profile(uint uid)
        {
            var entity = await DbService.Sql.Queryable<AdminEntity>().Where(it => it.Id == uid).FirstAsync();
            if (entity == null)
                return JsonResp.Error("账号不存在");
            // 判断是否被封禁
            if (entity.Status != AdminStatus.Normal)
                return JsonResp.Error("账号被冻结");
            if (entity.ParentId > 0)
            {
                var obj = await DbService.Sql.Queryable<AdminEntity>()
                    .Where(it => it.Id == entity.ParentId)
                    .FirstAsync(it => new {it.NickName, it.InvitCode});
                if (obj != null)
                {
                    entity.FatherName = obj.NickName;
                    entity.FatherInvitCode = obj.InvitCode;
                }
            }

            return JsonResp.Ok(entity);
        }

        private async Task<JsonResp> SignOut(uint uid)
        {
            await RedisService.DelUserToken(uid);
            return JsonResp.Ok();
        }

        private async Task<JsonResp> SaveProfile(uint userId, AdminCategory opCategory, SaveProfileReq req)
        {
            string safePwd = null;
            string pwdSalt = null;

            if (!string.IsNullOrWhiteSpace(req.Password))
            {
                safePwd = PasswordUtil.Encode(req.Password, out pwdSalt);
            }

            var er = await DbService.Sql.Update<AdminEntity>()
                .Where(it => it.Id == userId)
                // 代理不能修改昵称
                .SetIf(opCategory <= AdminCategory.Admin && !string.IsNullOrWhiteSpace(req.NickName), it => it.NickName,
                    req.NickName)
                .SetIf(!string.IsNullOrWhiteSpace(safePwd), it => it.Password, safePwd)
                .SetIf(!string.IsNullOrWhiteSpace(pwdSalt), it => it.PassSalt, pwdSalt)
                .ExecuteAffrowsAsync();
            if (er == 0)
            {
                return JsonResp.DbError("更新数据失败");
            }

            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListAdmin(ListPageReq req)
        {
            req.Build();
            uint.TryParse(req.Search, out var searchId);

            var repo = DbService.Sql.GetRepository<AdminEntity>();
            var rows = await repo
                .Where(it => it.Category == AdminCategory.Admin)
                .WhereIf(searchId > 0, it => it.Id == searchId)
                .WhereIf(!string.IsNullOrWhiteSpace(req.Search) && searchId == 0,
                    it => it.UserName.Contains(req.Search) || it.NickName.Contains(req.Search))
                .Count(out var total)
                .Page(req.PageIndex, req.PageSize)
                .ToListAsync(it => new
                {
                    it.Id,
                    it.UserName,
                    it.NickName,
                    it.Status,
                    it.InvitCode,
                    it.LoginIp,
                    it.LoginTime,
                    it.CreateTime
                });

            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> AddAdmin(AddAdminReq req)
        {
            var repo = DbService.Sql.GetRepository<AdminEntity>();
            // 检查用户名或昵称
            var ret = await repo.Where(it => it.UserName == req.UserName).CountAsync();
            if (ret > 0) return JsonResp.Error("账号已存在");
            ret = await repo.Where(it => it.NickName == req.NickName).CountAsync();
            if (ret > 0) return JsonResp.Error("昵称已存在");

            // 先查询除所有的邀请码
            var invitCodes = await DbService.Sql.Queryable<AdminEntity>()
                .ToListAsync(it => it.InvitCode);
            var invitCode = string.Empty;
            // 最多尝试30次
            for (var i = 0; i < 30; i++)
            {
                var code = StringUtil.Random(4);
                if (!invitCodes.Contains(code))
                {
                    invitCode = code;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(invitCode))
            {
                return JsonResp.Error("生成邀请码失败, 请重试");
            }

            // 插入数据
            var entity = new AdminEntity
            {
                UserName = req.UserName,
                Password = PasswordUtil.Encode(req.Password, out var passSalt),
                PassSalt = passSalt,
                NickName = req.NickName,
                Status = AdminStatus.Normal,
                Category = AdminCategory.Admin,
                ParentId = 0,
                Agency = 0,
                Money = 0,
                TotalPay = 0,
                InvitCode = invitCode,
                LoginIp = "",
                LoginTime = 0,
                CreateTime = TimeUtil.TimeStamp
            };
            await repo.InsertAsync(entity);
            if (entity.Id == 0)
                return JsonResp.DbError();
            return JsonResp.Ok();
        }

        private async Task<JsonResp> DelAdmin(uint id)
        {
            var er = await DbService.Sql.Delete<AdminEntity>()
                .Where(it => it.Id == id)
                .ExecuteAffrowsAsync();
            if (er == 0) return JsonResp.Error("管理员不存在");
            return JsonResp.Ok();
        }

        private async Task<JsonResp> EditAdmin(EditAdminReq req)
        {
            string safePwd = null;
            string pwdSalt = null;

            if (!string.IsNullOrWhiteSpace(req.Password))
                safePwd = PasswordUtil.Encode(req.Password, out pwdSalt);
            var er = await DbService.Sql.Update<AdminEntity>()
                .Where(it => it.Id == req.Id && it.Category == AdminCategory.Admin)
                .SetIf(!string.IsNullOrWhiteSpace(req.NickName), it => it.NickName, req.NickName)
                .SetIf(!string.IsNullOrWhiteSpace(safePwd), it => it.Password, safePwd)
                .SetIf(!string.IsNullOrWhiteSpace(pwdSalt), it => it.PassSalt, pwdSalt)
                .ExecuteAffrowsAsync();
            if (er == 0)
                return JsonResp.Error("理员不存在");
            return JsonResp.Ok();
        }

        private async Task<JsonResp> FrozeAdmin(FrozeAdminReq req)
        {
            var status = req.Status == 0 ? AdminStatus.Normal : AdminStatus.Frozen;
            var er = await DbService.Sql.Update<AdminEntity>()
                .Where(it => it.Id == req.Id)
                .Set(it => it.Status, status)
                .ExecuteAffrowsAsync();
            if (er == 0)
                return JsonResp.Error("管理员不存在");
            // 冻结该账号后立即清理他的缓存, 这样他下一次操作就必须重新登录, 从而进行Status校验
            if (status == AdminStatus.Frozen)
            {
                await RedisService.DelAdminInfo(req.Id);
                await RedisService.DelAdminAgencyInfo(req.Id);
            }
            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListAgency(uint opUid, AdminCategory opCategory, ListAgencyReq req)
        {
            req.Build();

            var hasSearchParent = !string.IsNullOrWhiteSpace(req.SearchParent);
            var hasSearchText = !string.IsNullOrWhiteSpace(req.Search);

            ISelect<AdminEntity, Admin1Entity> selector;
            if (opCategory <= AdminCategory.Admin)
            {
                if (hasSearchParent)
                {
                    // 先定位parent
                    var parentId = await DbService.Sql.Select<AdminEntity>()
                        .Where(it => it.Category == AdminCategory.Agency)
                        .Where(it => it.NickName.Contains(req.SearchParent) || it.InvitCode.Contains(req.SearchParent))
                        .FirstAsync(it => it.Id);
                    if (parentId == 0) return JsonResp.Ok();

                    selector = DbService.Sql.Select<AdminEntity>()
                        .Where(it => it.ParentId == parentId)
                        .AsTreeCte()
                        .From<Admin1Entity>((_, b) => _);
                }
                else
                {
                    selector = DbService.Sql.Select<AdminEntity, Admin1Entity>()
                        .Where((a, b) => a.Category == AdminCategory.Agency);
                }
            }
            else
            {
                // 代理只能查看自己下线代理
                var viewId = opUid;
                if (hasSearchParent)
                {
                    viewId = await DbService.Sql.Select<AdminEntity>()
                        .Where(it => it.Id == opUid)
                        .AsTreeCte()
                        .Where(it => it.NickName.Contains(req.SearchParent) || it.InvitCode.Contains(req.SearchParent))
                        .FirstAsync(it => it.Id);
                    if (viewId == 0) return JsonResp.Ok();
                }

                selector = DbService.Sql.Select<AdminEntity, Admin1Entity>()
                    .Where((a, b) => a.ParentId == viewId);
                    //.AsTreeCte()
                    //.From<Admin1Entity>((_, b) => _);
            }

            selector
                .LeftJoin((a, b) => a.ParentId == b.Id)
                .WhereIf(req.Agency.HasValue, (a, b) => a.Agency == req.Agency.Value)
                .WhereIf(hasSearchText,
                    (a, b) => a.NickName.Contains(req.Search) || a.InvitCode.Contains(req.Search))
                .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                    (a, b) => a.CreateTime >= req.StartTime && a.CreateTime < req.EndTime);

            var total = await selector.CountAsync();

            if (req.Order == 1) selector.OrderByDescending((a, b) => a.Money);
            else if (req.Order == 2) selector.OrderByDescending((a, b) => a.TotalPay);
            else selector.OrderBy((a, b) => a.Agency);

            var rows = await selector
                .OrderByDescending((a, b) => a.Id)
                .Page(req.PageIndex, req.PageSize)
                .ToListAsync((a, b) => new
                {
                    a.Id,
                    a.UserName,
                    a.NickName,
                    a.Status,
                    a.Category,
                    a.Money,
                    a.TotalPay,
                    a.InvitCode,
                    a.ParentId,
                    a.Agency,
                    a.LoginIp,
                    a.LoginTime,
                    a.CreateTime,
                    ParentName = b.NickName,
                    ParentInvitCode = b.InvitCode
                });

            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> AddAgency(uint opUid, AdminCategory opCategory, AddAdminReq req)
        {
            var repo = DbService.Sql.GetRepository<AdminEntity>();

            uint opAgency = 0;
            if (opCategory >= AdminCategory.Agency)
            {
                var oper = await repo.Where(it => it.Id == opUid).FirstAsync(it => new {it.Agency});
                if (oper == null)
                    return JsonResp.BadOperation();
                // 最多支持3级代理
                if (oper.Agency >= 3)
                    return JsonResp.Error("3级代理不能增加下级代理");
                opAgency = oper.Agency;
            }

            // 检查用户名或昵称
            var ret = await repo.Where(it => it.UserName == req.UserName).CountAsync();
            if (ret > 0) return JsonResp.Error("用户名已存在");
            ret = await repo.Where(it => it.NickName == req.NickName).CountAsync();
            if (ret > 0) return JsonResp.Error("昵称已存在");

            // 先查询除所有的邀请码
            var invitCodes = await DbService.Sql.Queryable<AdminEntity>().ToListAsync(it => it.InvitCode);
            var invitCode = string.Empty;
            // 最多尝试30次
            for (var i = 0; i < 30; i++)
            {
                var code = StringUtil.Random(4);
                if (!invitCodes.Contains(code))
                {
                    invitCode = code;
                    break;
                }
            }

            if (string.IsNullOrWhiteSpace(invitCode))
            {
                return JsonResp.Error("生成邀请码失败, 请重试");
            }

            // 插入数据, 1级代理商的parentId是0
            var parentId = opCategory <= AdminCategory.Admin ? 0 : opUid;
            var entity = new AdminEntity
            {
                UserName = req.UserName,
                Password = PasswordUtil.Encode(req.Password, out var passSalt),
                PassSalt = passSalt,
                NickName = req.NickName,
                Status = AdminStatus.Normal,
                Category = AdminCategory.Agency,
                ParentId = parentId,
                Agency = opAgency + 1,
                Money = 0,
                TotalPay = 0,
                InvitCode = invitCode,
                LoginIp = "",
                LoginTime = 0,
                CreateTime = TimeUtil.TimeStamp
            };
            await repo.InsertAsync(entity);
            if (entity.Id == 0) return JsonResp.DbError();
            return JsonResp.Ok();
        }

        public async Task<JsonResp> EditAgency(uint opUid, AdminCategory opCategory, EditAdminReq req)
        {
            var repo = DbService.Sql.GetRepository<AdminEntity>();

            var entity = await repo.Where(it => it.Id == req.Id)
                .FirstAsync(it => new {it.Category, it.ParentId, it.Agency});
            if (entity == null)
                return JsonResp.Error("代理不存在");
            if (opCategory == AdminCategory.Agency && entity.ParentId != opUid)
            {
                // 代理只能修改自己的下级代理
                return JsonResp.NoPermission();
            }

            string safePwd = null;
            string pwdSalt = null;

            if (!string.IsNullOrWhiteSpace(req.Password))
                safePwd = PasswordUtil.Encode(req.Password, out pwdSalt);

            var er = await DbService.Sql.Update<AdminEntity>()
                .Where(it => it.Id == req.Id)
                .SetIf(!string.IsNullOrWhiteSpace(req.NickName), it => it.NickName, req.NickName)
                .SetIf(!string.IsNullOrWhiteSpace(safePwd), it => it.Password, safePwd)
                .SetIf(!string.IsNullOrWhiteSpace(pwdSalt), it => it.PassSalt, pwdSalt)
                .ExecuteAffrowsAsync();
            if (er == 0)
                return JsonResp.Error("代理不存在");
            return JsonResp.Ok();
        }

        private async Task<JsonResp> FrozeAgency(uint opUid, AdminCategory opCategory, FrozeAdminReq req)
        {
            var repo = DbService.Sql.GetRepository<AdminEntity>();

            var entity = await repo.Where(it => it.Id == req.Id)
                .FirstAsync(it => new {it.Category, it.ParentId, it.Agency});
            if (entity == null)
                return JsonResp.Error("代理不存在");
            if (opCategory == AdminCategory.Agency && entity.ParentId != opUid)
            {
                // 代理只能修改自己的下级代理
                return JsonResp.NoPermission();
            }

            var status = req.Status == 0 ? AdminStatus.Normal : AdminStatus.Frozen;
            var er = await DbService.Sql.Update<AdminEntity>()
                .Where(it => it.Id == req.Id)
                .Set(it => it.Status, status)
                .ExecuteAffrowsAsync();
            if (er == 0)
                return JsonResp.Error("代理不存在");
            // 冻结该账号后立即清理他的缓存, 这样他下一次操作就必须重新登录, 从而进行Status校验
            if (status == AdminStatus.Frozen)
            {
                await RedisService.DelAdminInfo(req.Id);
                await RedisService.DelAdminAgencyInfo(req.Id);
            }
            return JsonResp.Ok();
        }

        private async Task<JsonResp> GetNotice()
        {
            var notice = await RedisService.GetNotice();
            return JsonResp.Ok(notice);
        }

        private async Task<JsonResp> SetNotice(string notice)
        {
            notice ??= "";
            var ret = await RedisService.SetNotice(notice);
            if (!ret) return JsonResp.CacheError();
            return JsonResp.Ok();
        }

        private async Task<JsonResp> GetPayEnable()
        {
            var payEnable = await RedisService.GetPayEnable();
            return JsonResp.Ok(payEnable);
        }

        private async Task<JsonResp> SetPayEnable(bool enable)
        {
            await RedisService.SetPayEnable(enable);
            return JsonResp.Ok();
        }

        private async Task<JsonResp> GetPayRate()
        {
            var rate = await RedisService.GetPayRateJade();
            var bindrate = await RedisService.GetPayRateBindJade();
            return JsonResp.Ok(new { rate = rate, bindrate = bindrate });
        }

        private async Task<JsonResp> SetPayRate(uint rate, uint bindRate)
        {
            await RedisService.SetPayRateJade(rate);
            await RedisService.SetPayRateBindJade(bindRate);
            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListMail(ListMailReq req)
        {
            req.Build();
            var hasTextSearch = !string.IsNullOrWhiteSpace(req.Search);
            uint.TryParse(req.Search, out var searchId);

            var rows = await DbService.Sql.Select<MailEntity, ServerEntity, AdminEntity>()
                .LeftJoin((a, b, c) => a.ServerId == b.Id)
                .LeftJoin((a, b, c) => a.Admin == c.Id)
                .WhereIf(searchId > 0, (a, b, c) => a.Id == searchId)
                .WhereIf(searchId == 0 && hasTextSearch,
                    (a, b, c) => a.Text.Contains(req.Search))
                .WhereIf(searchId == 0 && req.Server.HasValue, (a, b, c) => a.ServerId == req.Server.Value)
                .WhereIf(searchId == 0 && req.Type.HasValue, (a, b, c) => a.Type == req.Type.Value)
                .WhereIf(searchId == 0 && req.Picked.HasValue && req.Picked.Value, (a, b, c) => a.PickedTime > 0)
                .WhereIf(searchId == 0 && req.Picked.HasValue && !req.Picked.Value, (a, b, c) => a.PickedTime == 0)
                .WhereIf(searchId == 0 && req.Delete.HasValue && req.Delete.Value, (a, b, c) => a.DeleteTime > 0)
                .WhereIf(searchId == 0 && req.Delete.HasValue && !req.Delete.Value, (a, b, c) => a.DeleteTime == 0)
                .WhereIf(searchId == 0 && req.StartTime.HasValue && req.EndTime.HasValue,
                    (a, b, c) => a.CreateTime >= req.StartTime && a.CreateTime < req.EndTime)
                .Count(out var total)
                .OrderByDescending((a, b, c) => a.CreateTime)
                .Page(req.PageIndex, req.PageSize)
                .ToListAsync((a, b, c) => new
                {
                    id = a.Id,
                    sid = a.ServerId,
                    sname = b.Name,
                    sender = a.Sender,
                    recver = a.Recver,
                    admin = c.NickName,
                    type = a.Type,
                    text = a.Text,
                    items = a.Items,
                    minRelive = a.MinRelive,
                    minLevel = a.MinLevel,
                    maxRelive = a.MaxRelive,
                    maxLevel = a.MaxLevel,
                    remark = a.Remark,
                    createTime = a.CreateTime,
                    pickedTime = a.PickedTime,
                    deleteTime = a.DeleteTime,
                    expireTime = a.ExpireTime
                });

            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> AddMail(uint opUid, AddMailReq req)
        {
            var servers = new Dictionary<uint, uint>();

            // 检查最低等级
            var minLevel = (uint) ConfigService.GetRoleMinLevel(req.MinRelive.GetValueOrDefault());
            var maxLevel = (uint) ConfigService.GetRoleMaxLevel(req.MinRelive.GetValueOrDefault());
            if (req.MinLevel < minLevel || req.MinLevel > maxLevel)
                return JsonResp.Error("请检查最低等级是否合理");
            // 检查最高等级
            minLevel = ConfigService.GetRoleMinLevel(req.MaxRelive.GetValueOrDefault());
            maxLevel = ConfigService.GetRoleMaxLevel(req.MaxRelive.GetValueOrDefault());
            if (req.MaxLevel < minLevel || req.MaxLevel > maxLevel)
                return JsonResp.Error("请检查最高等级是否合理");
            if (req.MinRelive > req.MaxRelive) return JsonResp.Error("请确保最低转生等级不高于最高转生等级");
            if (req.MinRelive == req.MaxRelive && req.MinLevel > req.MaxLevel) return JsonResp.Error("请确保最低等级不高于最高等级");
            // 检查过期时间
            if (req.Expire == 0) req.Expire = (uint) DateTimeOffset.Now.AddDays(30).ToUnixTimeSeconds();
            if (req.Expire < TimeUtil.TimeStamp)
                return JsonResp.Error("请检查过期时间是否合理");

            // 检查接收人是否存在
            if (req.Recv > 0)
            {
                var role = await DbService.Sql.Queryable<RoleEntity>()
                    .Where(it => it.Id == req.Recv)
                    .FirstAsync(it => new {it.Status, it.ServerId});
                if (role == null)
                    return JsonResp.Error("指定的角色不存在");
                if (role.Status != RoleStatus.Normal)
                    return JsonResp.Error("指定的角色已被冻结");

                servers.Add(role.ServerId, 0);
            }
            else
            {
                if (req.Sid == 0)
                {
                    // 所有服推送
                    var rows = await DbService.Sql.Queryable<ServerEntity>()
                        .Where(it => it.Status == ServerStatus.Normal)
                        .ToListAsync(it => it.Id);
                    foreach (var sid in rows)
                    {
                        servers.Add(sid, 0);
                    }
                }
                else
                {
                    // 检查区服id是否存在
                    var svr = await DbService.Sql.Queryable<ServerEntity>()
                        .Where(it => it.Id == req.Sid)
                        .FirstAsync(it => new {it.Status});
                    if (svr == null)
                        return JsonResp.Error("指定的区服不存在");
                    if (svr.Status != ServerStatus.Normal)
                        return JsonResp.Error("指定的区服非正常状态");
                    servers.Add(req.Sid, 0);
                }
            }

            using (var uow = DbService.Sql.CreateUnitOfWork())
            {
                var repo = uow.GetRepository<MailEntity>();

                // 30天过期
                var now = DateTimeOffset.Now;
                var createTime = (uint) now.ToUnixTimeSeconds();
                var expireTime = req.Expire;

                // 分析items
                var items = string.Empty;
                if (req.Items != null && req.Items.Length > 0)
                {
                    var dic = new Dictionary<uint, int>();
                    foreach (var pair in req.Items)
                    {
                        if (ConfigService.Items.ContainsKey(pair.Id) && pair.Num > 0)
                        {
                            if (dic.ContainsKey(pair.Id))
                                dic[pair.Id] += pair.Num;
                            else
                                dic[pair.Id] = pair.Num;
                        }
                    }

                    if (dic.Count > 0)
                    {
                        items = Json.SafeSerialize(dic);
                    }
                }

                foreach (var sid in servers.Keys.ToList())
                {
                    // 后台发送的邮件, 用admin记录操作者
                    var entity = new MailEntity
                    {
                        ServerId = sid,
                        Sender = 0,
                        Recver = req.Recv,
                        Admin = opUid,
                        Type = 0,
                        Text = req.Text,
                        Items = items,
                        MinRelive = req.MinRelive.GetValueOrDefault(),
                        MinLevel = req.MinLevel.GetValueOrDefault(),
                        MaxRelive = req.MaxRelive.GetValueOrDefault(),
                        MaxLevel = req.MaxLevel.GetValueOrDefault(),
                        Remark = req.Remark ?? "",
                        CreateTime = createTime,
                        PickedTime = 0,
                        DeleteTime = 0,
                        ExpireTime = expireTime
                    };

                    await repo.InsertAsync(entity);
                    if (entity.Id == 0)
                    {
                        uow.Rollback();
                        return JsonResp.Error("操作出错");
                    }

                    // 记录该区服新插入的mail id
                    servers[sid] = entity.Id;
                }

                uow.Commit();
            }

            if (req.Recv == 0)
            {
                var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
                foreach (var (k, v) in servers)
                {
                    if (v == 0) continue;
                    // 通知
                    var serverGrain = GrainFactory.GetGrain<IServerGrain>(k);
                    // 检查区服是否已激活
                    var isActive = serverGrain != null && await serverGrain.CheckActive();
                    if (isActive)
                    {
                        _ = serverGrain.OnMailAdd(v);
                    }
                }
            }
            else if (servers.Count > 0)
            {
                var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
                var active = await globalGrain.CheckPlayer(req.Recv);
                if (active)
                {
                    var id = servers.Values.FirstOrDefault();
                    var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(req.Recv);
                    if (playerGrain != null)
                    {
                        if (await playerGrain.StartUp())
                        {
                            _ = playerGrain.OnRecvMail(id);
                        }
                    }
                }
            }

            return JsonResp.Ok();
        }

        private async Task<JsonResp> DelMail(DelMailReq req)
        {
            var id = req.Id.GetValueOrDefault();
            if (id == 0) return JsonResp.BadRequest();

            var mail = await DbService.Sql.Queryable<MailEntity>()
                .Where(it => it.Id == id)
                .FirstAsync();
            if (mail == null) return JsonResp.BadRequest();

            var ret = await DbService.DeleteEntity<MailEntity>(id);
            if (!ret) return JsonResp.DbError();

            if (mail.Recver == 0)
            {
                var serverGrain = GrainFactory.GetGrain<IServerGrain>(mail.ServerId);
                // 检查区服是否已激活
                var isActive = serverGrain != null && await serverGrain.CheckActive();
                if (isActive)
                {
                    await serverGrain.OnMailDel(id);
                }
            }
            else
            {
                var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
                ret = await globalGrain.CheckPlayer(mail.Recver);
                if (ret)
                {
                    var playerGrain = GrainFactory.GetGrain<IPlayerGrain>(mail.Recver);
                    await playerGrain.OnDelMail(id);
                }
            }

            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListServer(ListPageReq req)
        {
            req.Build();
            uint.TryParse(req.Search, out var searchId);

            var repo = DbService.Sql.GetRepository<ServerEntity>();
            var rows = await repo
                .Where(it => true)
                .WhereIf(searchId > 0, it => it.Id == searchId)
                .WhereIf(!string.IsNullOrWhiteSpace(req.Search) && searchId == 0,
                    it => it.Name.Contains(req.Search))
                .Count(out var total)
                .OrderBy(it => it.Rank)
                .Page(req.PageIndex, req.PageSize)
                .ToListAsync(it => new ServerEntity
                {
                    Id = it.Id,
                    Name = it.Name,
                    Status = it.Status,
                    Recom = it.Recom,
                    Rank = it.Rank,
                    Addr = it.Addr,
                    CreateTime = it.CreateTime,
                    RegRoleNum = (uint) DbService.Sql.Queryable<RoleEntity>().Where(itx => itx.ServerId == it.Id)
                        .Count()
                });

            var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
            foreach (var entity in rows)
            {
                var serverGrain = GrainFactory.GetGrain<IServerGrain>(entity.Id);
                if (await serverGrain.CheckActive())
                {
                    entity.OnlineNum = await serverGrain.GetOnlineNum();

                    var sldhGrain = GrainFactory.GetGrain<IShuiLuDaHuiGrain>(entity.Id);
                    var sldhInfoBytes = await sldhGrain.GetActivityInfo();
                    if (sldhInfoBytes.Value != null)
                    {
                        var sldhInfo = SldhActivityInfo.Parser.ParseFrom(sldhInfoBytes.Value);
                        entity.SldhInfo = Json.Serialize(sldhInfo);
                    }

                    var wzzzGrain = GrainFactory.GetGrain<IWangZheZhiZhanGrain>(entity.Id);
                    var wzzzInfoBytes = await wzzzGrain.GetActivityInfo();
                    if (wzzzInfoBytes.Value != null)
                    {
                        var wzzzInfo = WzzzActivityInfo.Parser.ParseFrom(wzzzInfoBytes.Value);
                        entity.WzzzInfo = Json.Serialize(wzzzInfo);
                    }

                    var ssjlGrain = GrainFactory.GetGrain<IShenShouJiangLinGrain>(entity.Id);
                    var ssjlInfoBytes = await ssjlGrain.GetActivityInfo();
                    if (ssjlInfoBytes.Value != null)
                    {
                        var ssjlInfo = SldhActivityInfo.Parser.ParseFrom(ssjlInfoBytes.Value);
                        entity.SsjlInfo = Json.Serialize(ssjlInfo);
                    }

                    var sectWarGrain = GrainFactory.GetGrain<ISectWarGrain>(entity.Id);
                    var sectWarInfoBytes = await sectWarGrain.GetActivityInfo();
                    if (sectWarInfoBytes.Value != null)
                    {
                        var sectWarInfo = SectWarActivityInfo.Parser.ParseFrom(sectWarInfoBytes.Value);
                        entity.SectWarInfo = Json.Serialize(sectWarInfo);
                    }

                    var singlePkGrain = GrainFactory.GetGrain<ISinglePkGrain>(entity.Id);
                    var singlePkBytes = await singlePkGrain.GetActivityInfo();
                    if (singlePkBytes.Value != null)
                    {
                        var singlePkInfo = SinglePkActivityInfo.Parser.ParseFrom(singlePkBytes.Value);
                        entity.SinglePkInfo = Json.Serialize(singlePkInfo);
                    }
                }
            }

            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> GetResVerion()
        {
            var grain = GrainFactory.GetGrain<IGlobalGrain>(0);
            var bytes = await grain.GetResVersion();
            if (bytes.Value == null) return JsonResp.Ok();
            var vo = Json.Deserialize<ResVersionVo>(bytes.Value);
            return JsonResp.Ok(vo);
        }

        private async Task<JsonResp> SetResVerion(SetResVersionReq req)
        {
            var grain = GrainFactory.GetGrain<IGlobalGrain>(0);
            await grain.SetResVersion(req.Version, req.Force);
            return JsonResp.Ok();
        }

        private async Task<JsonResp> ReloadConfig()
        {
            await ConfigService.Reload();
            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListServer1()
        {
            var repo = DbService.Sql.GetRepository<ServerEntity>();
            var rows = await repo
                .Where(it => it.Status == ServerStatus.Normal)
                .ToListAsync(it => new
                {
                    it.Id,
                    it.Name
                });

            return JsonResp.Ok(rows);
        }

        private async Task<JsonResp> AddServer(AddServerReq req)
        {
            var repo = DbService.Sql.GetRepository<ServerEntity>();
            var ret = await repo.Where(it => it.Name == req.Name).CountAsync();
            if (ret > 0) return JsonResp.Error("区服名已存在");

            // 新开服默认是临时维护的状态
            var entity = new ServerEntity
            {
                Name = req.Name,
                Status = ServerStatus.Stop,
                Recom = false,
                Rank = 0,
                Addr = req.Addr,
                CreateTime = TimeUtil.TimeStamp,
            };
            await repo.InsertAsync(entity);
            if (entity.Id == 0)
                return JsonResp.Error(ErrCode.DbError);
            return JsonResp.Ok();
        }

        private async Task<JsonResp> EditServer(EditServerReq req)
        {
            var entity = await DbService.Sql.Queryable<ServerEntity>().Where(it => it.Id == req.Id)
                .FirstAsync(it => new {it.Status});
            if (entity == null) return JsonResp.Error("区服不存在");

            var er = await DbService.Sql.Update<ServerEntity>()
                .Where(it => it.Id == req.Id)
                .SetIf(!string.IsNullOrWhiteSpace(req.Name), it => it.Name, req.Name)
                .SetIf(!string.IsNullOrWhiteSpace(req.Addr), it => it.Addr, req.Addr)
                .SetIf(req.Recom.HasValue, it => it.Recom, req.Recom.GetValueOrDefault())
                .ExecuteAffrowsAsync();
            if (er == 0)
                return JsonResp.Error("区服不存在");
            return JsonResp.Ok();
        }

        private async Task<JsonResp> ChangeServerStatus(ChangeServerStatusReq req)
        {
            var entity = await DbService.Sql.Queryable<ServerEntity>().Where(it => it.Id == req.Id)
                .FirstAsync(it => new {it.Status});
            if (entity == null) return JsonResp.Error("区服不存在");
            if ((byte) entity.Status == req.Status) return JsonResp.Error("重复的操作,请刷新页面");

            switch ((ServerStatus) req.Status)
            {
                case ServerStatus.Normal:
                {
                    await DbService.Sql.Update<ServerEntity>()
                        .Where(it => it.Id == req.Id)
                        .Set(it => it.Status, ServerStatus.Normal)
                        .ExecuteAffrowsAsync();
                    // 激活Grain
                    var grain = GrainFactory.GetGrain<IServerGrain>(req.Id);
                    await grain.Startup();
                }
                    break;
                case ServerStatus.Stop:
                {
                    if (entity.Status != ServerStatus.Normal) return JsonResp.Error("重复的操作,请刷新页面");
                    // 修改状态，防止新的用户进入区服
                    await DbService.Sql.Update<ServerEntity>()
                        .Where(it => it.Id == req.Id)
                        .Set(it => it.Status, ServerStatus.Stop)
                        .ExecuteAffrowsAsync();
                    // 先执行逻辑停服
                    var grain = GrainFactory.GetGrain<IServerGrain>(req.Id);
                    await grain.Shutdown();
                }
                    break;
                case ServerStatus.Dead:
                {
                    // 必须先停服维护
                    // if (entity.Status != ServerStatus.Stop)
                    //     return JsonResp.Error("请先停服维护");
                    // // 检查是否在运行
                    // var onlineNum = await GrainFactory.GetGrain<IGlobalGrain>(0).CheckServer(req.Id);
                    // if (onlineNum >= 0) return JsonResp.Error("该区服在运行中, 请先停服");
                    //
                    // // 检查该分区下是否还有角色, 如果有不能这么操作
                    // var num = await DbService.Sql.Queryable<RoleEntity>().Where(it => it.ServerId == req.Id)
                    //     .CountAsync();
                    // if (num > 0) return JsonResp.Error("该区服下还有角色, 不能直接废弃");
                    return JsonResp.BadOperation();
                }
            }

            return JsonResp.Ok();
        }

        private async Task<JsonResp> StartServer(uint serverId)
        {
            var entity = await DbService.Sql.Queryable<ServerEntity>().Where(it => it.Id == serverId)
                .FirstAsync(it => new {it.Status});
            if (entity == null) return JsonResp.Error("区服不存在");
            if (entity.Status != ServerStatus.Normal) return JsonResp.Error("区服非正常状态, 不能启动");

            var grain = GrainFactory.GetGrain<IServerGrain>(serverId);
            await grain.Startup();
            return JsonResp.Ok();
        }

        private async Task<JsonResp> StopServer(uint serverId)
        {
            var entity = await DbService.Sql.Queryable<ServerEntity>().Where(it => it.Id == serverId)
                .FirstAsync(it => new {it.Status});
            if (entity == null) return JsonResp.Error("区服不存在");

            // if (entity.Status != ServerStatus.Stop) return JsonResp.Error("区服非维护状态, 不能停止");

            if (entity.Status != ServerStatus.Stop)
            {
                await DbService.Sql.Update<ServerEntity>()
                    .Where(it => it.Id == serverId)
                    .Set(it => it.Status, ServerStatus.Stop)
                    .ExecuteAffrowsAsync();
            }

            // 检查区服是否已激活
            var serverGrain = GrainFactory.GetGrain<IServerGrain>(serverId);
            var isActive = serverGrain != null && await serverGrain.CheckActive();
            if (!isActive)
            {
                return JsonResp.Error("该分区尚未开启");
            }
            // 这里可能会耗时非常长, 所以不要await
            await serverGrain.Shutdown();

            return JsonResp.Ok();
        }

        private async Task<JsonResp> OpenActivity(uint opUid, OpenActivityReq req)
        {
            var sid = req.Sid.GetValueOrDefault();
            var aid = (ActivityId) req.Aid.GetValueOrDefault();

            // 检查区服是否已激活
            var serverGrain = GrainFactory.GetGrain<IServerGrain>(sid);
            var isActive = serverGrain != null && await serverGrain.CheckActive();
            if (!isActive)
            {
                return JsonResp.Error("该分区尚未开启");
            }

            switch (aid)
            {
                case ActivityId.ShuiLuDaHui:
                {
                    var grain = GrainFactory.GetGrain<IShuiLuDaHuiGrain>(sid);
                    var error = await grain.GmOpen(req.Open, opUid);
                    if (!string.IsNullOrWhiteSpace(error)) return JsonResp.Error(error);
                    return JsonResp.Ok();
                }
                case ActivityId.WangZheZhiZhan:
                {
                    var grain = GrainFactory.GetGrain<IWangZheZhiZhanGrain>(sid);
                    var error = await grain.GmOpen(req.Open, opUid);
                    if (!string.IsNullOrWhiteSpace(error)) return JsonResp.Error(error);
                    return JsonResp.Ok();
                }
                case ActivityId.SectWar:
                {
                    var grain = GrainFactory.GetGrain<ISectWarGrain>(sid);
                    var error = await grain.GmOpen(req.Open, opUid);
                    if (!string.IsNullOrWhiteSpace(error)) return JsonResp.Error(error);
                    return JsonResp.Ok();
                }
                case ActivityId.SinglePk:
                {
                    var grain = GrainFactory.GetGrain<ISinglePkGrain>(sid);
                    var error = await grain.GmOpen(req.Open, opUid);
                    if (!string.IsNullOrWhiteSpace(error)) return JsonResp.Error(error);
                    return JsonResp.Ok();
                }
                case ActivityId.ShenShouJiangLin:
                {
                    var grain = GrainFactory.GetGrain<IShenShouJiangLinGrain>(sid);
                    var error = await grain.GmOpen(req.Open, opUid);
                    if (!string.IsNullOrWhiteSpace(error)) return JsonResp.Error(error);
                    return JsonResp.Ok();
                }
                default:
                    return JsonResp.BadRequest();
            }
        }

        private async Task<JsonResp> QueryCombineServer()
        {
            var list = await DbService.Sql.Queryable<ServerEntity>()
                .Where(it => it.Status == ServerStatus.Stop || it.Status == ServerStatus.Normal)
                .ToListAsync(it => new {it.Id, it.Name, it.Status});
            var normals = list.Where(it => it.Status == ServerStatus.Normal).ToList();
            var stops = list.Where(it => it.Status == ServerStatus.Stop).ToList();
            // 确保维护状态的区服都是停服的
            var grain = GrainFactory.GetGrain<IGlobalGrain>(0);
            for (var i = stops.Count - 1; i >= 0; i--)
            {
                var serverGrain = GrainFactory.GetGrain<IServerGrain>(stops[i].Id);
                if (await serverGrain.CheckActive())
                {
                    stops.RemoveAt(i);
                }
            }

            var dic = new Dictionary<string, object>
            {
                ["stops"] = stops,
                ["normals"] = normals
            };

            return JsonResp.Ok(dic);
        }

        private async Task<JsonResp> CombineServer(CombineServerReq req)
        {
            if (req.Target.GetValueOrDefault() == 0) return JsonResp.Error("请选择合并后的区服");
            if (req.From == null || req.From.Length == 0) return JsonResp.Error("请选择待合并的区服");
            // 检查Target是否在From中
            if (req.From.Contains(req.Target.GetValueOrDefault())) return JsonResp.Error("待合并的区服中不能包含合并后的区服");

            var grain = GrainFactory.GetGrain<IGlobalGrain>(0);
            // 先检查from中是否有开启的区服
            foreach (var sid in req.From)
            {
                var serverGrain = GrainFactory.GetGrain<IServerGrain>(sid);
                if (await serverGrain.CheckActive())
                {
                    return JsonResp.Error("待合并的区服请确保已停止运行");
                }
            }

            var targetEntity = await DbService.Sql.Queryable<ServerEntity>()
                .Where(it => it.Id == req.Target.GetValueOrDefault())
                .FirstAsync();
            if (targetEntity.Status == ServerStatus.Stop)
                JsonResp.Error("合并后的区服不能是永久停服状态");

            foreach (var sid in req.From)
            {
                var entity = await DbService.Sql.Queryable<ServerEntity>()
                    .Where(it => it.Id == sid)
                    .FirstAsync();
                if (entity == null) return JsonResp.Error("待合并的区服不存在");
            }

            // 事务执行，确保安全
            using (var uow = DbService.Sql.CreateUnitOfWork())
            {
                try
                {
                    var tsid = req.Target.GetValueOrDefault();

                    // 删除新区全服邮件 
                    await uow.Orm.Delete<MailEntity>()
                        .Where(it => it.ServerId == tsid && it.Recver == 0)
                        .ExecuteAffrowsAsync();

                    // 统计待合并的分区的水陆战神rid
                    var slzsDic = new Dictionary<uint, bool>();
                    var wzzsDic = new Dictionary<uint, bool>();
                    var pkzsDic = new Dictionary<uint, bool>();

                    foreach (var sid in req.From)
                    {
                        // 更新角色表
                        await uow.Orm.Update<RoleEntity>()
                            .Where(it => it.ServerId == sid)
                            .Set(it => it.ServerId, tsid)
                            .ExecuteAffrowsAsync();
                        // 更新帮派表-合并
                        await uow.Orm.Update<SectEntity>()
                            .Where(it => it.ServerId == sid)
                            .Set(it => it.ServerId, tsid)
                            .ExecuteAffrowsAsync();
                        // 更新摆摊-合并
                        await uow.Orm.Update<MallEntity>()
                            .Where(it => it.ServerId == sid)
                            .Set(it => it.ServerId, tsid)
                            .ExecuteAffrowsAsync();

                        // 水陆大会就跟随目标表, 将记录删除, 要合并水陆战神记录
                        {
                            var oldSlzs = await uow.Orm.Queryable<SldhEntity>()
                                .Where(it => it.ServerId == sid)
                                .FirstAsync(it => it.Slzs);
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(oldSlzs))
                                {
                                    var tmpList = Json.Deserialize<List<uint>>(oldSlzs);
                                    if (tmpList is { Count: > 0 })
                                    {
                                        foreach (var rid in tmpList)
                                        {
                                            slzsDic[rid] = true;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "合并水陆战神出错:{Msg}", ex.Message);
                            }
                        }

                        await uow.Orm.Delete<SldhEntity>()
                            .Where(it => it.ServerId == sid)
                            .ExecuteAffrowsAsync();

                        // 王者之战就跟随目标表, 将记录删除, 要合并水陆战神记录
                        {
                            var oldWzzs = await uow.Orm.Queryable<WzzzEntity>()
                                .Where(it => it.ServerId == sid)
                                .FirstAsync(it => it.Slzs);
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(oldWzzs))
                                {
                                    var tmpList = Json.Deserialize<List<uint>>(oldWzzs);
                                    if (tmpList is { Count: > 0 })
                                    {
                                        foreach (var rid in tmpList)
                                        {
                                            wzzsDic[rid] = true;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "合并王者战神出错:{Msg}", ex.Message);
                            }
                        }

                        await uow.Orm.Delete<WzzzEntity>()
                            .Where(it => it.ServerId == sid)
                            .ExecuteAffrowsAsync();

                        // 单人PK就跟随目标表, 将记录删除, 要合并PK战神记录
                        {
                            var oldPkzs = await uow.Orm.Queryable<SinglePkEntity>()
                                .Where(it => it.ServerId == sid)
                                .FirstAsync(it => it.Pkzs);
                            try
                            {
                                if (!string.IsNullOrWhiteSpace(oldPkzs))
                                {
                                    var tmpList = Json.Deserialize<List<uint>>(oldPkzs);
                                    if (tmpList is { Count: > 0 })
                                    {
                                        foreach (var rid in tmpList)
                                        {
                                            pkzsDic[rid] = true;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "合并PK战神出错:{Msg}", ex.Message);
                            }
                        }

                        await uow.Orm.Delete<SinglePkEntity>()
                            .Where(it => it.ServerId == sid)
                            .ExecuteAffrowsAsync();

                        // 帮战就跟随目标表, 将记录删除
                        await uow.Orm.Delete<SectWarEntity>()
                            .Where(it => it.ServerId == sid)
                            .ExecuteAffrowsAsync();
                        // 删除老区全服邮件 
                        await uow.Orm.Delete<MailEntity>()
                            .Where(it => it.ServerId == sid && it.Recver == 0)
                            .ExecuteAffrowsAsync();
                        // 更新个人邮件到新的区服
                        await uow.Orm.Update<MailEntity>()
                            .Where(it => it.ServerId == sid && it.Recver > 0)
                            .Set(it => it.ServerId, req.Target.GetValueOrDefault())
                            .ExecuteAffrowsAsync();
                        // 删除这个区服
                        await uow.Orm.Delete<ServerEntity>()
                            .Where(it => it.Id == sid)
                            .ExecuteAffrowsAsync();

                        // 清除排行榜缓存
                        await RedisService.DelRoleLevelRank(sid);
                        await RedisService.DelRoleJadeRank(sid);
                        await RedisService.DelRolePayRank(sid);
                        await RedisService.DelRoleSldhRank(sid);
                        await RedisService.DelRoleWzzzRank(sid);
                        await RedisService.DelRoleCszlLayerRank(sid);
                    }

                    // 合并水陆战神
                    {
                        var slzs = await uow.Orm.Queryable<SldhEntity>()
                            .Where(it => it.ServerId == tsid)
                            .FirstAsync(it => it.Slzs);
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(slzs))
                            {
                                var tmpList = Json.Deserialize<List<uint>>(slzs);
                                if (tmpList is { Count: > 0 })
                                {
                                    foreach (var rid in tmpList)
                                    {
                                        slzsDic[rid] = true;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignore
                        }

                        // 执行合并
                        slzs = Json.Serialize(slzsDic.Keys.ToList());
                        await uow.Orm.Update<SldhEntity>()
                            .Where(it => it.ServerId == tsid)
                            .Set(it => it.Slzs, slzs)
                            .ExecuteAffrowsAsync();
                    }

                    // 合并王者之战战神
                    {
                        var slzs = await uow.Orm.Queryable<WzzzEntity>()
                            .Where(it => it.ServerId == tsid)
                            .FirstAsync(it => it.Slzs);
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(slzs))
                            {
                                var tmpList = Json.Deserialize<List<uint>>(slzs);
                                if (tmpList is { Count: > 0 })
                                {
                                    foreach (var rid in tmpList)
                                    {
                                        wzzsDic[rid] = true;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignore
                        }

                        // 执行合并
                        slzs = Json.Serialize(wzzsDic.Keys.ToList());
                        await uow.Orm.Update<WzzzEntity>()
                            .Where(it => it.ServerId == tsid)
                            .Set(it => it.Slzs, slzs)
                            .ExecuteAffrowsAsync();
                    }

                    // 合并PK战神
                    {
                        var pkzs = await uow.Orm.Queryable<SinglePkEntity>()
                            .Where(it => it.ServerId == tsid)
                            .FirstAsync(it => it.Pkzs);
                        try
                        {
                            if (!string.IsNullOrWhiteSpace(pkzs))
                            {
                                var tmpList = Json.Deserialize<List<uint>>(pkzs);
                                if (tmpList is { Count: > 0 })
                                {
                                    foreach (var rid in tmpList)
                                    {
                                        pkzsDic[rid] = true;
                                    }
                                }
                            }
                        }
                        catch
                        {
                            // ignore
                        }

                        // 执行合并
                        pkzs = Json.Serialize(pkzsDic.Keys.ToList());
                        await uow.Orm.Update<SinglePkEntity>()
                            .Where(it => it.ServerId == tsid)
                            .Set(it => it.Pkzs, pkzs)
                            .ExecuteAffrowsAsync();
                    }

                    uow.Commit();
                }
                catch (Exception ex)
                {
                    uow.Rollback();
                    _logger.LogError(ex, "合并区服出错:{Msg}", ex.Message);
                }
            }

            // 如果target区服在线，重新加载帮派数据
            {
                var serverGrain = GrainFactory.GetGrain<IServerGrain>(req.Target.GetValueOrDefault());
                if (await serverGrain.CheckActive())
                {
                    await serverGrain.Reload();
                }

                var mallGrain = GrainFactory.GetGrain<IMallGrain>(req.Target.GetValueOrDefault());
                if (await mallGrain.CheckActive())
                {
                    await mallGrain.Reload();
                }
            }

            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListUser(uint opUid, AdminCategory opCategory, ListUserReq req)
        {
            req.Build();

            var hasTextSearch = !string.IsNullOrWhiteSpace(req.Search);
            uint.TryParse(req.Search, out var searchId);

            if (opCategory <= AdminCategory.Admin)
            {
                var selector = DbService.Sql.Select<UserEntity, AdminEntity>()
                    .LeftJoin((a, b) => a.ParentId == b.Id)
                    .WhereIf(searchId > 0, (a, b) => a.Id == searchId)
                    .WhereIf(searchId == 0 && hasTextSearch,
                        (a, b) => a.UserName.Contains(req.Search))
                    .WhereIf(searchId == 0 && req.Status.HasValue, (a, b) => a.Status == (UserStatus) req.Status)
                    .WhereIf(searchId == 0 && req.StartTime.HasValue && req.EndTime.HasValue,
                        (a, b) => a.CreateTime >= req.StartTime && a.CreateTime < req.EndTime);
                if (searchId == 0)
                {
                    if (req.Type == (byte) UserType.Gm) selector.Where((a, b) => a.Type == UserType.Gm);
                    if (req.Type == (byte) UserType.Robot) selector.Where((a, b) => a.Type == UserType.Robot);
                    else selector.Where((a, b) => a.Type <= UserType.Gm);
                }


                var total = await selector.CountAsync();
                var rows = await selector
                    .OrderByDescending((a, b) => a.Id)
                    .Page(req.PageIndex, req.PageSize)
                    .ToListAsync((a, b) => new
                    {
                        a.Id,
                        a.UserName,
                        a.Type,
                        a.Status,
                        a.CreateTime,
                        a.LastLoginIp,
                        a.LastLoginTime,
                        a.ParentId,
                        ParentName = b.NickName,
                        ParentInvitCode = b.InvitCode
                    });

                return JsonResp.Ok(new ListPageResp
                {
                    Total = total,
                    Rows = rows
                });
            }
            else
            {
                // 代理查询自己和自己的代理发展的所有用户
                var selector = DbService.Sql.Select<AdminEntity, UserEntity>()
                    .Where((a, b) => a.Id == opUid)
                    // .AsTreeCte()
                    // .From<UserEntity>((_, b) => _)
                    .LeftJoin((a, b) => b.ParentId == a.Id)
                    .Where((a, b) => b.Id > 0 && b.Type <= UserType.Gm) //防止左表没有用户
                    .WhereIf(searchId > 0, (a, b) => b.Id == searchId)
                    .WhereIf(hasTextSearch && searchId == 0,
                        (a, b) => b.UserName.Contains(req.Search))
                    .WhereIf(searchId == 0 && req.Status.HasValue, (a, b) => b.Status == (UserStatus) req.Status)
                    .WhereIf(searchId == 0 && req.StartTime.HasValue && req.EndTime.HasValue,
                        (a, b) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime);
                var total = await selector.CountAsync();
                var rows = await selector.OrderByDescending((a, b) => b.Id)
                    .Page(req.PageIndex, req.PageSize)
                    .ToListAsync((a, b) => new
                    {
                        b.Id,
                        b.UserName,
                        b.Type,
                        b.Status,
                        b.CreateTime,
                        b.LastLoginIp,
                        b.LastLoginTime,
                        b.ParentId,
                        ParentName = a.NickName,
                        ParentInvitCode = a.InvitCode
                    });

                return JsonResp.Ok(new ListPageResp
                {
                    Total = total,
                    Rows = rows
                });
            }
        }

        private async Task<JsonResp> EditUser(uint opUid, AdminCategory opCategory, EditUserReq req)
        {
            if (req.Id.GetValueOrDefault() == 0 || string.IsNullOrWhiteSpace(req.Password))
                return JsonResp.Error(ErrCode.BadRequest);
            // 获取用户信息
            var user = await DbService.Sql.Queryable<UserEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new
                {
                    it.ParentId, it.Status
                });
            if (user == null) return JsonResp.Error("用户不存在");
            // 代理只能修改自己名下的用户
            if (opCategory >= AdminCategory.Agency && user.ParentId != opUid)
                return JsonResp.Error(ErrCode.NoPermission);
            // 更新密码
            var password = PasswordUtil.Encode(req.Password, out var passSalt);
            var er = await DbService.Sql.Update<UserEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .Set(it => it.Password, password)
                .Set(it => it.PassSalt, passSalt)
                .ExecuteAffrowsAsync();
            if (er == 0)
                return JsonResp.Error("用户不存在");
            return JsonResp.Ok();
        }

        private async Task<JsonResp> FrozeUser(uint opUid, AdminCategory opCategory, FrozeUserReq req)
        {
            if (req.Id.GetValueOrDefault() == 0 || !req.Status.HasValue)
                return JsonResp.Error(ErrCode.BadRequest);
            // 获取用户信息
            var user = await DbService.Sql.Queryable<UserEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new
                {
                    it.ParentId, it.Status, it.LastUseRoleId
                });
            if (user == null) return JsonResp.Error("用户不存在");
            // 代理只能修改自己名下的用户
            if (opCategory >= AdminCategory.Agency && user.ParentId != opUid)
                return JsonResp.Error(ErrCode.NoPermission);
            if ((byte) user.Status == req.Status)
                return JsonResp.Ok();
            var status = req.Status.GetValueOrDefault() == 0 ? UserStatus.Normal : UserStatus.Frozen;
            // 更新状态
            var er = await DbService.Sql.Update<UserEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .Set(it => it.Status, status)
                .ExecuteAffrowsAsync();
            if (er == 0)
                return JsonResp.Error("用户不存在");

            // 冻结用户, 把用户下所有的在线角色踢下线
            if (status == UserStatus.Frozen)
            {
                // 删除token
                await RedisService.DelUserToken(req.Id.GetValueOrDefault());

                var roles = await DbService.QueryRoles(req.Id.GetValueOrDefault());
                if (roles != null && roles.Count > 0)
                {
                    var grain = GrainFactory.GetGrain<IGlobalGrain>(0);
                    foreach (var rid in roles)
                    {
                        var ret = await grain.CheckPlayer(rid);
                        if (ret)
                        {
                            _ = GrainFactory.GetGrain<IPlayerGrain>(rid).Shutdown();
                        }
                    }
                }
            }

            return JsonResp.Ok();
        }


        private async Task<JsonResp> ListRole(uint opUid, AdminCategory opCategory, ListRoleReq req)
        {
            req.Build();
            var hasTextSearch = !string.IsNullOrWhiteSpace(req.Search);
            uint.TryParse(req.Search, out var searchId);

            if (opCategory <= AdminCategory.Admin)
            {
                var selector = DbService.Sql.Select<RoleEntity, UserEntity, ServerEntity, AdminEntity>()
                    .LeftJoin((a, b, c, d) => a.UserId == b.Id)
                    .LeftJoin((a, b, c, d) => a.ServerId == c.Id)
                    .LeftJoin((a, b, c, d) => a.ParentId == d.Id)
                    .WhereIf(searchId > 0 && req.SearchTextType == 0, (a, b, c, d) => a.Id == searchId)
                    .WhereIf(searchId > 0 && req.SearchTextType == 1, (a, b, c, d) => b.Id == searchId)
                    .WhereIf(hasTextSearch && searchId == 0 && req.SearchTextType == 0,
                        (a, b, c, d) => a.NickName.Contains(req.Search))
                    .WhereIf(hasTextSearch && searchId == 0 && req.SearchTextType == 1,
                        (a, b, c, d) => b.UserName.Contains(req.Search))
                    .WhereIf(searchId == 0 && req.Server.HasValue, (a, b, c, d) => a.ServerId == req.Server.Value)
                    .WhereIf(searchId == 0 && req.Status != null, (a, b, c, d) => a.Status == (RoleStatus) req.Status)
                    .WhereIf(searchId == 0 && req.Sex != null, (a, b, c, d) => a.Sex == req.Sex)
                    .WhereIf(searchId == 0 && req.Race != null, (a, b, c, d) => a.Race == req.Race)
                    .WhereIf(searchId == 0 && req.Online != null, (a, b, c, d) => a.Online == req.Online)
                    .WhereIf(searchId == 0 && req.StartTime.HasValue && req.EndTime.HasValue,
                        (a, b, c, d) => a.CreateTime >= req.StartTime && a.CreateTime < req.EndTime);

                if (searchId == 0)
                {
                    if (req.Type == (byte) UserType.Gm) selector.Where((a, b, c, d) => a.Type == UserType.Gm);
                    if (req.Type == (byte) UserType.Robot) selector.Where((a, b, c, d) => a.Type == UserType.Robot);
                    else selector.Where((a, b, c, d) => a.Type <= UserType.Gm);
                }

                var total = await selector.CountAsync();
                var rows = await selector
                    .OrderByDescending((a, b, c, d) => a.Id)
                    .Page(req.PageIndex, req.PageSize)
                    .ToListAsync((a, b, c, d) => new
                    {
                        a.Id,
                        a.UserId,
                        b.UserName,
                        a.ServerId,
                        ServerName = c.Name,
                        a.Type,
                        a.Status,
                        a.NickName,
                        a.Race,
                        a.Sex,
                        a.Relive,
                        a.Level,
                        a.Jade,
                        a.TotalPayBS,
                        a.Online,
                        a.OnlineTime,
                        a.CreateTime,
                        a.Flags,
                        parentId = a.ParentId,
                        parentName = d.NickName,
                        ParentInvitCode = d.InvitCode
                    });

                return JsonResp.Ok(new ListPageResp
                {
                    Total = total,
                    Rows = rows
                });
            }
            else
            {
                var selector = DbService.Sql.Select<AdminEntity, RoleEntity, UserEntity, ServerEntity>()
                    .Where((a, b, c, d) => a.Id == opUid)
                    // .AsTreeCte()
                    // .From<RoleEntity, UserEntity, ServerEntity>((a, b, c, d) => a)
                    .LeftJoin((a, b, c, d) => b.ParentId == a.Id)
                    .LeftJoin((a, b, c, d) => b.UserId == c.Id)
                    .LeftJoin((a, b, c, d) => b.ServerId == d.Id)
                    .Where((a, b, c, d) => b.Id > 0 && b.Type <= UserType.Gm)
                    .WhereIf(searchId > 0 && req.SearchTextType == 0, (a, b, c, d) => b.Id == searchId)
                    .WhereIf(searchId > 0 && req.SearchTextType == 1, (a, b, c, d) => c.Id == searchId)
                    .WhereIf(searchId == 0 && hasTextSearch && req.SearchTextType == 0,
                        (a, b, c, d) => b.NickName.Contains(req.Search))
                    .WhereIf(searchId == 0 && hasTextSearch && req.SearchTextType == 1,
                        (a, b, c, d) => c.UserName.Contains(req.Search))
                    .WhereIf(searchId == 0 && req.Server.HasValue, (a, b, c, d) => b.ServerId == req.Server.Value)
                    .WhereIf(searchId == 0 && req.Status != null, (a, b, c, d) => b.Status == (RoleStatus) req.Status)
                    .WhereIf(searchId == 0 && req.Sex != null, (a, b, c, d) => b.Sex == req.Sex)
                    .WhereIf(searchId == 0 && req.Race != null, (a, b, c, d) => b.Race == req.Race)
                    .WhereIf(searchId == 0 && req.Online != null, (a, b, c, d) => b.Online == req.Online)
                    .WhereIf(searchId == 0 && req.StartTime.HasValue && req.EndTime.HasValue,
                        (a, b, c, d) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime);
                var total = await selector.CountAsync();
                var rows = await selector.OrderByDescending((a, b, c, d) => b.Id)
                    .Page(req.PageIndex, req.PageSize)
                    .ToListAsync((a, b, c, d) => new
                    {
                        b.Id,
                        b.UserId,
                        c.UserName,
                        b.ServerId,
                        ServerName = d.Name,
                        b.Type,
                        b.Status,
                        b.NickName,
                        b.Race,
                        b.Sex,
                        b.Relive,
                        b.Level,
                        b.Jade,
                        b.TotalPayBS,
                        b.Online,
                        b.OnlineTime,
                        b.CreateTime,
                        b.Flags,
                        parentId = b.ParentId,
                        parentName = a.NickName,
                        ParentInvitCode = a.InvitCode
                    });

                return JsonResp.Ok(new ListPageResp
                {
                    Total = total,
                    Rows = rows
                });
            }
        }

        private async Task<JsonResp> FrozeRole(uint opUid, AdminCategory category, uint agency, FrozeRoleReq req)
        {
            if (req.Id.GetValueOrDefault() == 0 || !req.Status.HasValue)
                return JsonResp.Error(ErrCode.BadRequest);
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new
                {
                    it.UserId, it.ParentId, it.Status
                });
            if (role == null) return JsonResp.Error("角色不存在");
            // // 代理只能修改自己名下的角色
            // 必现是代理以上，并且至少是1级代理
            if (category > AdminCategory.Agency || agency > 1)
                return JsonResp.Error(ErrCode.NoPermission);
            if ((byte) role.Status == req.Status)
                return JsonResp.Ok();
            var status = req.Status.GetValueOrDefault() == 0 ? RoleStatus.Normal : RoleStatus.Frozen;
            // 更新状态
            var er = await DbService.Sql.Update<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .Set(it => it.Status, status)
                .ExecuteAffrowsAsync();
            if (er == 0)
                return JsonResp.Error("角色不存在");

            // 冻结角色, 踢下线
            if (status == RoleStatus.Frozen)
            {
                // 删除用户的token
                await RedisService.DelUserToken(role.UserId);

                var grain = GrainFactory.GetGrain<IGlobalGrain>(0);
                var ret = await grain.CheckPlayer(req.Id.GetValueOrDefault());
                if (ret)
                {
                    await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault()).Shutdown();
                }
                else
                {
                    // 直接更新数据库
                    await DbService.Sql.Update<RoleEntity>()
                        .Where(it => it.Id == req.Id.GetValueOrDefault())
                        .Set(it => it.Online, false)
                        .ExecuteAffrowsAsync();
                }
            }

            return JsonResp.Ok();
        }

        private async Task<JsonResp> ChangeRoleOnline(uint opUid, AdminCategory category, uint agency, ChangeRoleOnlineReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new
                {
                    it.UserId, it.ParentId, it.Online
                });
            if (role == null) return JsonResp.Error("角色不存在");
            // // 代理只能修改自己名下的角色
            // 必现是代理以上，并且至少是1级代理
            if (category > AdminCategory.Agency || agency > 1)
                return JsonResp.Error(ErrCode.NoPermission);
            if (role.Online == req.Online)
                return JsonResp.Ok();

            var grain = GrainFactory.GetGrain<IGlobalGrain>(0);
            var online = await grain.CheckPlayer(req.Id.GetValueOrDefault());
            if (online == req.Online) return JsonResp.Ok();

            if (req.Online.GetValueOrDefault())
            {
                await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault()).StartUp();
            }
            else
            {
                // 踢下线
                await RedisService.DelUserToken(role.UserId);
                // 直接更新数据库
                await DbService.Sql.Update<RoleEntity>()
                    .Where(it => it.Id == req.Id.GetValueOrDefault())
                    .Set(it => it.Online, false)
                    .ExecuteAffrowsAsync();

                _ = GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault()).Shutdown();
            }

            return JsonResp.Ok();
        }

        private async Task<JsonResp> GetRoleDetail(uint opUid, AdminCategory opCategory, GetRoleDetailReq req)
        {
            var roleId = req.Id.GetValueOrDefault();
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == roleId)
                .FirstAsync();
            if (role == null) return JsonResp.Error("角色不存在");
            if (opCategory >= AdminCategory.Agency)
            {
                var exists = await DbService.Sql.Select<AdminEntity>()
                    .Where(it => it.Id == opUid)
                    .AsTreeCte()
                    .From<RoleEntity>((a, b) => a)
                    .LeftJoin((a, b) => b.ParentId == a.Id)
                    .Where((a, b) => b.Id == req.Id)
                    .AnyAsync();
                if (!exists) return JsonResp.NoPermission();

                // var list = await DbService.Sql.Select<AdminEntity>()
                //     .Where(it => it.Id == opUid)
                //     .AsTreeCte()
                //     .ToListAsync(it => it.Id);
                // if (!list.Contains(role.ParentId))
                //     return JsonResp.NoPermission();
            }

            var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
            var isOnline = await globalGrain.CheckPlayer(roleId);
            if (isOnline)
            {
                // 获取热数据
                var roleGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                var hotData = await roleGrain.Dump();
                role = Json.Deserialize<RoleEntity>(hotData.Value);
            }

            if (role.ParentId > 0)
            {
                role.AgencyInvitCode = await DbService.Sql.Queryable<AdminEntity>()
                    .Where(it => it.Id == role.ParentId)
                    .FirstAsync(it => it.InvitCode);
            }

            if (role.Spread > 0)
            {
                var rEntity = await RedisService.GetRoleInfo(role.Spread);
                if (rEntity != null) role.SpreadName = rEntity.NickName;
            }

            return JsonResp.Ok(role);
        }

        private async Task<JsonResp> GetRoleEquips(uint opUid, AdminCategory category, GetRoleEquipsReq req)
        {
            var roleId = req.Id.GetValueOrDefault();

            // 权限
            var obj = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == roleId)
                .FirstAsync(it => new {it.ParentId});
            if (obj == null) return JsonResp.Error("角色不存在");
            if (category >= AdminCategory.Agency)
            {
                var exists = await DbService.Sql.Select<AdminEntity>()
                    .Where(it => it.Id == opUid)
                    .AsTreeCte()
                    .From<RoleEntity>((a, b) => a)
                    .LeftJoin((a, b) => b.ParentId == a.Id)
                    .Where((a, b) => b.Id == req.Id)
                    .AnyAsync();
                if (!exists) return JsonResp.NoPermission();
            }

            var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
            var isOnline = await globalGrain.CheckPlayer(roleId);
            if (isOnline)
            {
                var roleGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                var hotData = await roleGrain.DumpEquips();
                var equips = Json.Deserialize<List<EquipEntity>>(hotData.Value);
                // 把名字携带上
                foreach (var entity in equips)
                {
                    ConfigService.Equips.TryGetValue(entity.CfgId, out var cfg);
                    if (cfg != null)
                    {
                        entity.Name = cfg.Name;
                        entity.Pos = cfg.Index;
                    }
                }

                return JsonResp.Ok(equips);
            }

            // 获取角色信息
            var rows = await DbService.Sql.Queryable<EquipEntity>()
                .Where(it => it.RoleId == roleId)
                .ToListAsync();
            // 把名字携带上
            foreach (var entity in rows)
            {
                ConfigService.Equips.TryGetValue(entity.CfgId, out var cfg);
                if (cfg != null)
                {
                    entity.Name = cfg.Name;
                    entity.Pos = cfg.Index;
                }
            }

            return JsonResp.Ok(rows);
        }

        private async Task<JsonResp> SetEquipRefine(uint opUid, AdminCategory category, SetEquipRefineReq req)
        {
            if (req.Pairs == null || req.Pairs.Length > 5) return JsonResp.BadRequest();
            // 权限
            var obj = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Rid)
                .FirstAsync(it => new {it.ParentId});
            if (obj == null) return JsonResp.Error("角色不存在");

            var args = new List<Tuple<byte, float>>(5);
            foreach (var pair in req.Pairs)
            {
                if (pair.Key == 0 || pair.Value == 0) continue;
                args.Add(new Tuple<byte, float>((byte) pair.Key, pair.Value));
            }

            var grain = GrainFactory.GetGrain<IPlayerGrain>(req.Rid.GetValueOrDefault());
            var ret = await grain.GmRefineEquip(req.Id.GetValueOrDefault(), args);
            if (!ret) return JsonResp.BadRequest();
            return JsonResp.Ok();
        }

        private async Task<JsonResp> GetRoleOrnaments(uint opUid, AdminCategory category, GetRoleEquipsReq req)
        {
            var roleId = req.Id.GetValueOrDefault();

            // 权限
            var obj = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == roleId)
                .FirstAsync(it => new {it.ParentId});
            if (obj == null) return JsonResp.Error("角色不存在");
            if (category >= AdminCategory.Agency)
            {
                var exists = await DbService.Sql.Select<AdminEntity>()
                    .Where(it => it.Id == opUid)
                    .AsTreeCte()
                    .From<RoleEntity>((a, b) => a)
                    .LeftJoin((a, b) => b.ParentId == a.Id)
                    .Where((a, b) => b.Id == req.Id)
                    .AnyAsync();
                if (!exists) return JsonResp.NoPermission();
            }

            var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
            var isOnline = await globalGrain.CheckPlayer(roleId);
            if (isOnline)
            {
                var roleGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                var hotData = await roleGrain.DumpOrnaments();
                var ornaments = Json.Deserialize<List<OrnamentEntity>>(hotData.Value);
                // 把名字携带上
                foreach (var entity in ornaments)
                {
                    ConfigService.Ornaments.TryGetValue(entity.CfgId, out var cfg);
                    if (cfg != null) entity.Name = cfg.Name;
                }

                return JsonResp.Ok(ornaments);
            }

            // 获取角色信息
            var rows = await DbService.Sql.Queryable<OrnamentEntity>()
                .Where(it => it.RoleId == roleId)
                .ToListAsync();
            // 把名字携带上
            foreach (var entity in rows)
            {
                ConfigService.Ornaments.TryGetValue(entity.CfgId, out var cfg);
                if (cfg != null) entity.Name = cfg.Name;
            }

            return JsonResp.Ok(rows);
        }

        private async Task<JsonResp> GetRoleMounts(uint opUid, AdminCategory category, GetRoleEquipsReq req)
        {
            var roleId = req.Id.GetValueOrDefault();

            // 权限
            var obj = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == roleId)
                .FirstAsync(it => new {it.ParentId});
            if (obj == null) return JsonResp.Error("角色不存在");
            if (category >= AdminCategory.Agency)
            {
                var exists = await DbService.Sql.Select<AdminEntity>()
                    .Where(it => it.Id == opUid)
                    .AsTreeCte()
                    .From<RoleEntity>((a, b) => a)
                    .LeftJoin((a, b) => b.ParentId == a.Id)
                    .Where((a, b) => b.Id == req.Id)
                    .AnyAsync();
                if (!exists) return JsonResp.NoPermission();
            }

            var globalGrain = GrainFactory.GetGrain<IGlobalGrain>(0);
            var isOnline = await globalGrain.CheckPlayer(roleId);
            if (isOnline)
            {
                var roleGrain = GrainFactory.GetGrain<IPlayerGrain>(roleId);
                var hotData = await roleGrain.DumpMounts();
                var mounts = Json.Deserialize<List<MountEntity>>(hotData.Value);
                return JsonResp.Ok(mounts);
            }

            // 获取角色信息
            var rows = await DbService.Sql.Queryable<MountEntity>()
                .Where(it => it.RoleId == roleId)
                .ToListAsync();
            return JsonResp.Ok(rows);
        }

        private async Task<JsonResp> SetMountSkill(uint opUid, AdminCategory category, SetMountSkillReq req)
        {
            if (category >= AdminCategory.Agency) return JsonResp.Error("权限不足");
            // 权限
            var obj = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Rid)
                .FirstAsync(it => new {it.ParentId});
            if (obj == null) return JsonResp.Error("角色不存在");

            // 检查坐骑
            var mObj = await DbService.Sql.Queryable<MountEntity>()
                .Where(it => it.Id == req.Mid)
                .FirstAsync(it => new {it.RoleId});
            if (mObj == null) return JsonResp.Error("坐骑不存在");
            if (mObj.RoleId != req.Rid) return JsonResp.Error("该坐骑不属于该角色");
            if (!ConfigService.MountSkills.ContainsKey(req.SkCfgId.GetValueOrDefault()))
                return JsonResp.Error("技能不存在");

            var grain = GrainFactory.GetGrain<IPlayerGrain>(req.Rid.GetValueOrDefault());
            var ret = await grain.GmSetMountSkill(req.Mid.GetValueOrDefault(), req.SkIdx.GetValueOrDefault(),
                req.SkCfgId.GetValueOrDefault(), req.SkLevel.GetValueOrDefault(), req.SkExp.GetValueOrDefault());
            if (!ret) return JsonResp.BadRequest();

            return JsonResp.Ok();
        }

        private async Task<JsonResp> ChangeRoleLevel(uint opUid, AdminCategory category, ChangeRoleLevelReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new
                {
                    it.ParentId
                });
            if (role == null) return JsonResp.Error("角色不存在");
            // 代理只能修改自己名下的用户
            if (category >= AdminCategory.Agency && role.ParentId != opUid)
                return JsonResp.Error(ErrCode.NoPermission);

            // 如果离线修改数据，担心漏掉大量的因登记变化带来的其他数据变化, 所以就直接激活该Grain进行等级修改
            await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmSetLevel(req.Level.GetValueOrDefault());

            return JsonResp.Ok();
        }

        // private async Task<JsonResp> ChangeRoleMoney(uint opUid, AdminCategory opCategory, ChangeRoleMoneyReq req)
        // {
        //     // 获取角色信息
        //     var tempTarget = await DbService.Sql.Queryable<RoleEntity>()
        //         .Where(it => it.Id == req.Id.GetValueOrDefault())
        //         .FirstAsync(it => new
        //         {
        //             it.ServerId, it.ParentId, it.Spread, it.Status, it.NickName
        //         });
        //     if (tempTarget == null)
        //         return JsonResp.Error("角色不存在");
        //     if (tempTarget.Status != RoleStatus.Normal)
        //         return JsonResp.Error("角色已被冻结");
        //     if (opCategory >= AdminCategory.Agency)
        //         return JsonResp.Error(ErrCode.NoPermission);
        //
        //     var grain = GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault());
        //     if (req.Silver.GetValueOrDefault() != 0)
        //     {
        //         await grain.AddMoney((byte) MoneyType.Silver, req.Silver.GetValueOrDefault(), "后台发放");
        //     }
        //
        //     if (req.Jade.GetValueOrDefault() != 0)
        //     {
        //         var jade = req.Jade.GetValueOrDefault() * GameDefine.JadePerYuan;
        //         await grain.AddMoney((byte) MoneyType.Jade, jade, "后台发放");
        //     }
        //
        //     if (req.BindJade.GetValueOrDefault() != 0)
        //     {
        //         await grain.AddMoney((byte) MoneyType.BindJade, req.BindJade.GetValueOrDefault(), "后台发放");
        //     }
        //
        //     if (req.Contrib.GetValueOrDefault() != 0)
        //     {
        //         await grain.AddMoney((byte) MoneyType.Contrib, req.Contrib.GetValueOrDefault(), "后台发放");
        //     }
        //
        //     if (req.SldhGongJi.GetValueOrDefault() != 0)
        //     {
        //         await grain.AddMoney((byte) MoneyType.SldhGongJi, req.SldhGongJi.GetValueOrDefault(), "后台发放");
        //     }
        //
        //     return JsonResp.Ok();
        // }

        private async Task<JsonResp> ChangeRoleItem(uint opUid, AdminCategory category, ChangeRoleItemReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new
                {
                    it.ParentId
                });
            if (role == null) return JsonResp.Error("角色不存在");
            // 代理只能修改自己名下的用户
            if (category >= AdminCategory.Agency && role.ParentId != opUid)
                return JsonResp.Error(ErrCode.NoPermission);

            // 修改道具必须激活
            var ret = await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .AddBagItem(req.ItemId.GetValueOrDefault(), req.Value.GetValueOrDefault(), tag: "后台发放");
            if (!ret) return JsonResp.Error("操作失败");

            return JsonResp.Ok();
        }

        private async Task<JsonResp> ChangeRoleStar(uint opUid, ChangeRoleStarReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new {it.Star});
            if (role == null) return JsonResp.Error("角色不存在");

            await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmAddStar(req.Value.GetValueOrDefault());
            return JsonResp.Ok();
        }
        private async Task<JsonResp> ChangeRoleTotalPay(uint opUid, ChangeRoleTotalPayReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new { it.TotalPay });
            if (role == null) return JsonResp.Error("角色不存在");

            await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmAddTotalPay(req.Value.GetValueOrDefault());
            return JsonResp.Ok();
        }

        private async Task<JsonResp> AddRoleSkillExp(uint opUid, GetRoleDetailReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new {it.ParentId});
            if (role == null) return JsonResp.Error("角色不存在");

            await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmAddSkillExp();

            return JsonResp.Ok();
        }

        private async Task<JsonResp> AddRoleEquip(uint opUid, AddRoleEquipReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new {it.ParentId});
            if (role == null) return JsonResp.Error("角色不存在");

            var ret = await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmAddEquip(req.CfgId, req.Category, req.Index, req.Grade);
            if (!ret) return JsonResp.Error("添加失败");

            return JsonResp.Ok();
        }

        private async Task<JsonResp> AddRoleOrnament(uint opUid, AddRoleOrnamentReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new {it.ParentId});
            if (role == null) return JsonResp.Error("角色不存在");

            var ret = await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmAddOrnament(req.CfgId, req.Suit, req.Index, req.Grade);
            if (!ret) return JsonResp.Error("添加失败");

            return JsonResp.Ok();
        }

        private async Task<JsonResp> AddRoleWing(uint opUid, AddRoleWingReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new {it.ParentId});
            if (role == null) return JsonResp.Error("角色不存在");

            var ret = await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmAddWing(req.CfgId.GetValueOrDefault());
            if (!ret) return JsonResp.Error("添加失败");

            return JsonResp.Ok();
        }

        private async Task<JsonResp> AddRoleTitle(uint opUid, AddRoleTitleReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new {it.ParentId});
            if (role == null) return JsonResp.Error("角色不存在");

            var ret = await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmAddTitle(req.CfgId.GetValueOrDefault(), true);
            if (!ret) return JsonResp.Error("添加失败");

            return JsonResp.Ok();
        }

        private async Task<JsonResp> DelRoleTitle(uint opUid, DelRoleTitleReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new {it.ParentId});
            if (role == null) return JsonResp.Error("角色不存在");

            var ret = await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmAddTitle(req.CfgId.GetValueOrDefault(), false);
            if (!ret) return JsonResp.Error("移除失败");

            return JsonResp.Ok();
        }

        private async Task<JsonResp> DelRoleShane(uint opUid, DelRoleShaneReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new {it.ParentId});
            if (role == null) return JsonResp.Error("角色不存在");

            var ret = await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmDelShane(opUid);
            if (!ret) return JsonResp.Error("移除失败");

            return JsonResp.Ok();
        }

        private async Task<JsonResp> SetRoleType(SetRoleTypeReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new {it.ParentId});
            if (role == null) return JsonResp.Error("角色不存在");

            var ret = await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmSetRoleType(req.Type);
            if (!ret) return JsonResp.Error("修改失败");
            return JsonResp.Ok();
        }

        private async Task<JsonResp> SetRoleFlag(uint opUid, AdminCategory opCategory, SetRoleFlagReq req)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id.GetValueOrDefault())
                .FirstAsync(it => new {it.ParentId});
            if (role == null) return JsonResp.Error("角色不存在");
            // 代理只能修改自己名下的用户
            if (opCategory >= AdminCategory.Agency)
            {
                var exists = await DbService.Sql.Select<AdminEntity>()
                    .Where(it => it.Id == opUid)
                    .AsTreeCte()
                    .From<RoleEntity>((a, b) => a)
                    .LeftJoin((a, b) => b.ParentId == a.Id)
                    .Where((a, b) => b.Id == req.Id)
                    .AnyAsync();
                if (!exists) return JsonResp.NoPermission();
            }

            var ret = await GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault())
                .GmSetRoleFlag(req.Type, req.Value);
            if (!ret) return JsonResp.Error("修改失败");
            return JsonResp.Ok();
        }

        private async Task<JsonResp> Recharge(uint opUid, AdminCategory opCategory, RechargeReq req)
        {
            if (req.Id == 0 || req.Value == 0 || opCategory >= AdminCategory.Agency)
                return JsonResp.BadRequest();
            var tempTarget = await DbService.Sql.Queryable<AdminEntity>()
                .Where(it => it.Id == req.Id)
                .FirstAsync(it => new
                {
                    it.Category, it.ParentId, it.Agency, it.Status
                });
            if (tempTarget == null)
                return JsonResp.Error("代理不存在");
            if (tempTarget.Category <= AdminCategory.Admin)
                return JsonResp.Error("不能给管理员充值");
            if (tempTarget.Agency >= 2)
                return JsonResp.Error("只能给1级代理充值");
            if (tempTarget.Status != AdminStatus.Normal)
                return JsonResp.Error("代理已被冻结");

            // 防止这一瞬间代理在给角色充值, 所以利用Redis锁来做
            var locker = Guid.NewGuid().ToString();
            var ret = await RedisService.LockAgentPay(req.Id.GetValueOrDefault(), locker);
            if (!ret) return JsonResp.Error("代理正在使用额度,防止数据出错,请再次尝试");

            var temp = await DbService.Sql.Queryable<AdminEntity>()
                .Where(it => it.Id == req.Id)
                .FirstAsync(it => new {it.Money, it.TotalPay});
            if (temp == null)
            {
                await RedisService.UnlockAgentPay(req.Id.GetValueOrDefault(), locker);
                return JsonResp.Error("代理不存在");
            }

            var delta = req.Value.GetValueOrDefault();
            if (delta < 0 && Math.Abs(delta) > temp.Money)
            {
                await RedisService.UnlockAgentPay(req.Id.GetValueOrDefault(), locker);
                return JsonResp.Error($"当前余额{temp.Money}, 无法克扣{Math.Abs(delta)}");
            }

            // 事务操作, 确保一致性
            using (var uow = DbService.Sql.CreateUnitOfWork())
            {
                // 给to加上金额
                {
                    var newMoney = temp.Money + req.Value.GetValueOrDefault();
                    var newTotal = temp.TotalPay + req.Value.GetValueOrDefault();
                    // 更新值                    
                    var er = await DbService.Sql.Update<AdminEntity>()
                        .Where(it => it.Id == req.Id)
                        .Set(it => it.Money, newMoney)
                        .Set(it => it.TotalPay, newTotal)
                        .ExecuteAffrowsAsync();
                    if (er == 0)
                    {
                        uow.Rollback();
                        await RedisService.UnlockAgentPay(req.Id.GetValueOrDefault(), locker);
                        return JsonResp.Error("充值失败");
                    }
                }

                {
                    var repo = uow.GetRepository<RechargeEntity>();
                    var rEntity = new RechargeEntity
                    {
                        Operator = opUid,
                        From = 0,
                        To = req.Id.GetValueOrDefault(),
                        Money = req.Value.GetValueOrDefault(),
                        Remark = req.Remark,
                        CreateTime = TimeUtil.TimeStamp
                    };
                    await repo.InsertAsync(rEntity);
                    if (rEntity.Id == 0)
                    {
                        uow.Rollback();
                        await RedisService.UnlockAgentPay(req.Id.GetValueOrDefault(), locker);
                        return JsonResp.Error("充值失败");
                    }
                }

                uow.Commit();
            }

            await RedisService.UnlockAgentPay(req.Id.GetValueOrDefault(), locker);
            // 返回操作者的新货币
            return JsonResp.Ok();
        }

        private async Task<JsonResp> RechargeRole(uint opUid, AdminCategory opCategory, RechargeRoleReq req)
        {
            if (req.Id == 0 || req.Value == 0) return JsonResp.BadRequest();
            var costMoney = req.Value.GetValueOrDefault();

            var tempTarget = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == req.Id)
                .FirstAsync(it => new
                {
                    it.ServerId, it.NickName, it.Status, it.Spread, it.TotalPay, it.ParentId
                });
            if (tempTarget == null)
                return JsonResp.Error("角色不存在");
            if (tempTarget.Status != RoleStatus.Normal)
                return JsonResp.Error("角色已被冻结");

            var tempMy = await DbService.Sql.Queryable<AdminEntity>()
                .Where(it => it.Id == opUid)
                .FirstAsync(it => new {it.Status, it.NickName, it.InvitCode, it.Agency, it.Money});

            var isAdmin = opCategory <= AdminCategory.Admin;
            if (!isAdmin)
            {
                if (req.Value < 0)
                {
                    return JsonResp.Error("无法扣除，如有需要请联系管理员");
                }

                // 只有一级代理才可以给角色充值
                if (tempMy.Status != AdminStatus.Normal)
                {
                    await RedisService.DelAdminInfo(opUid);
                    await RedisService.DelAdminAgencyInfo(opUid);
                    return JsonResp.Error("您已被冻结");
                }

                if (tempMy.Agency > 1)
                    return JsonResp.NoPermission();

                // 检查角色id是否为该代理的角色或下级角色
                var tempRole = await DbService.Sql.Select<AdminEntity>()
                    .Where(it => it.Id == opUid)
                    .AsTreeCte()
                    .From<RoleEntity>((_, b) => _)
                    .LeftJoin((a, b) => b.ParentId == a.Id)
                    .Where((a, b) => b.Id == req.Id)
                    .FirstAsync((a, b) => new {b.Id});
                if (tempRole == null) return JsonResp.BadOperation();

                // 检查代理余额
                if (tempMy.Money < costMoney)
                    return JsonResp.Error("您的余额不足, 请及时充值");
            }

            // 代理在给角色充值的时候需要先将自己的余额锁定, 防止同一时刻管理在给他充值造成数据脏
            var locker = Guid.NewGuid().ToString();
            if (!isAdmin) await RedisService.LockAgentPay(opUid, locker);

            var newMoney = 0L;

            // 扣除余额, 角色充值, 充值奖励推广人
            using var uow = DbService.Sql.CreateUnitOfWork();
            // 扣除余额
            if (!isAdmin)
            {
                var agentMoney = await DbService.Sql.Queryable<AdminEntity>()
                    .Where(it => it.Id == opUid)
                    .FirstAsync(it => it.Money);
                if (agentMoney < costMoney)
                {
                    uow.Rollback();
                    await RedisService.UnlockAgentPay(opUid, locker);
                    return JsonResp.Error("您的余额不足, 请及时充值");
                }

                newMoney = agentMoney - costMoney;
                var er = await uow.Orm.Update<AdminEntity>()
                    .Where(it => it.Id == opUid)
                    .Set(it => it.Money, newMoney)
                    .ExecuteAffrowsAsync();
                if (er == 0)
                {
                    uow.Rollback();
                    await RedisService.UnlockAgentPay(opUid, locker);
                    return JsonResp.Error("充值失败");
                }
            }

            // 充值记录
            {
                req.Remark ??= "";
                req.Remark = req.Remark.Trim();
                var rEntity = new RechargeRoleEntity
                {
                    OpId = opUid,
                    OpName = tempMy.NickName,
                    OpInvitCode = tempMy.InvitCode,
                    RoleId = req.Id.GetValueOrDefault(),
                    ParentId = tempTarget.ParentId,
                    Money = costMoney,
                    Remark = req.Remark,
                    CreateTime = TimeUtil.TimeStamp
                };
                var repo = uow.GetRepository<RechargeRoleEntity>();
                await repo.InsertAsync(rEntity);
                if (rEntity.Id == 0)
                {
                    uow.Rollback();
                    if (!isAdmin) await RedisService.UnlockAgentPay(opUid, locker);
                    return JsonResp.Error("充值失败");
                }
            }

            try
            {
                // 发货
                var payRate = await RedisService.GetPayRateJade();

                var grain = GrainFactory.GetGrain<IPlayerGrain>(req.Id.GetValueOrDefault());
                await grain.OnPayed(costMoney, costMoney * (int) payRate);
            }
            catch
            {
                uow.Rollback();
                return JsonResp.Error("充值失败");
            }

            // 提交事务
            uow.Commit();

            return JsonResp.Ok(new Dictionary<string, object>
            {
                ["money"] = newMoney
            });
        }

        /// <summary>
        /// 代理线下充值, 管理员看到所有的充值记录，1级代理看到自己的购买记录
        /// </summary>
        private async Task<JsonResp> ListRecharge(uint opUid, AdminCategory opCategory, ListRechargeReq req)
        {
            req.Build();

            ISelect<RechargeEntity, AdminEntity> selector;
            if (opCategory <= AdminCategory.Admin)
            {
                selector = DbService.Sql.Select<RechargeEntity, AdminEntity>()
                    .LeftJoin((a, b) => a.To == b.Id)
                    .WhereIf(!string.IsNullOrWhiteSpace(req.Search),
                        (a, b) => b.NickName.Contains(req.Search) || b.InvitCode.Contains(req.Search));
            }
            else
            {
                var tempOp = await DbService.Sql.Queryable<AdminEntity>()
                    .Where(it => it.Id == opUid)
                    .FirstAsync(it => new {it.Agency});
                if (tempOp == null || tempOp.Agency > 1)
                    return JsonResp.BadOperation();

                // 1级代理看到自己的充值记录
                selector = DbService.Sql.Select<RechargeEntity, AdminEntity>()
                    .LeftJoin((a, b) => a.To == b.Id)
                    .Where((a, b) => a.To == opUid);
            }

            selector.WhereIf(req.Remark.HasValue && req.Remark.Value, (a, b) => a.Remark != "")
                .WhereIf(req.Remark.HasValue && !req.Remark.Value, (a, b) => a.Remark == "")
                .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                    (a, b) => a.CreateTime >= req.StartTime && a.CreateTime < req.EndTime);

            var sum = await selector.SumAsync((a, b) => a.Money);
            var total = await selector.CountAsync();

            if (req.Order == 1)
            {
                // 金额排序
                selector.OrderByDescending((a, b) => a.Money);
            }

            var rows = await selector
                .OrderByDescending((a, b) => a.Id)
                .Page(req.PageIndex, req.PageSize)
                .ToListAsync((a, b) => new
                {
                    id = a.Id,
                    op = a.Operator,
                    toId = a.To,
                    toName = b.NickName,
                    toInvitCode = b.InvitCode,
                    money = a.Money,
                    remark = a.Remark,
                    createTime = a.CreateTime
                });

            return JsonResp.Ok(new ListPageResp {Sum = (long) sum, Total = total, Rows = rows});
        }

        /// <summary>
        /// 删除代理线下充值记录
        /// </summary>
        private async Task<JsonResp> DelRecharge(uint opUid, AdminCategory opCategory, DelRecordsReq req)
        {
            if (req.StartTime > req.EndTime) return JsonResp.BadRequest();
            await DbService.Sql.Delete<RechargeEntity>()
                .Where(it => it.CreateTime >= req.StartTime && it.CreateTime < req.EndTime)
                .ExecuteAffrowsAsync();
            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListRechargeRole(uint opUid, AdminCategory opCategory, ListRechargeRoleReq req)
        {
            req.Build();
            var hasSearchParent = !string.IsNullOrWhiteSpace(req.SearchParent);
            var hasSearchOp = !string.IsNullOrWhiteSpace(req.SearchOp);
            var hasSearch = !string.IsNullOrWhiteSpace(req.Search);
            uint.TryParse(req.Search, out var searchRoleId);

            if (opCategory <= AdminCategory.Admin)
            {
                if (hasSearchParent)
                {
                    // 先定位parent
                    var parentId = await DbService.Sql.Select<AdminEntity>()
                        .Where(it => it.NickName.Contains(req.SearchParent) || it.InvitCode.Contains(req.SearchParent))
                        .FirstAsync(it => it.Id);
                    if (parentId == 0) return JsonResp.Ok();

                    // 这里要注意，是parent -> rechargeRole -> role
                    var selector = DbService.Sql.Select<AdminEntity>()
                        .Where(it => it.Id == parentId)
                        .AsTreeCte()
                        .From<RechargeRoleEntity, RoleEntity>((_, b, c) => _)
                        .InnerJoin((a, b, c) => b.ParentId == a.Id)
                        .LeftJoin((a, b, c) => b.RoleId == c.Id)
                        .WhereIf(req.Remark.HasValue && req.Remark.Value, (a, b, c) => b.Remark != "")
                        .WhereIf(req.Remark.HasValue && !req.Remark.Value, (a, b, c) => b.Remark == "")
                        .WhereIf(hasSearchOp,
                            (a, b, c) => b.OpName.Contains(req.SearchOp) || b.OpInvitCode.Contains(req.SearchOp))
                        .WhereIf(searchRoleId > 0, (a, b, c) => b.RoleId == searchRoleId)
                        .WhereIf(searchRoleId == 0 && hasSearch,
                            (a, b, c) => c.NickName.Contains(req.Search))
                        .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                            (a, b, c) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime);

                    var sum = (long) (await selector.SumAsync((a, b, c) => b.Money));
                    var total = await selector.CountAsync();

                    if (req.Order == 1)
                    {
                        // 金额排序
                        selector = selector.OrderByDescending((a, b, c) => b.Money);
                    }

                    var rows = await selector
                        .OrderByDescending((a, b, c) => b.Id)
                        .Page(req.PageIndex, req.PageSize)
                        .ToListAsync((a, b, c) => new
                        {
                            b.Id,
                            b.OpId,
                            b.OpName,
                            b.OpInvitCode,
                            b.RoleId,
                            RoleName = c.NickName,
                            b.ParentId,
                            ParentName = a.NickName,
                            ParentInvitCode = a.InvitCode,
                            b.Money,
                            b.Remark,
                            b.CreateTime
                        });
                    return JsonResp.Ok(new ListPageResp {Sum = sum, Total = total, Rows = rows});
                }
                else
                {
                    // 管理可以查看所有的角色充值 rechargeRole -> parent -> role
                    var selector = DbService.Sql.Select<RechargeRoleEntity, AdminEntity, RoleEntity>()
                        .LeftJoin((a, b, c) => a.ParentId == b.Id)
                        .LeftJoin((a, b, c) => a.RoleId == c.Id)
                        .WhereIf(req.Remark.HasValue && req.Remark.Value, (a, b, c) => a.Remark != "")
                        .WhereIf(req.Remark.HasValue && !req.Remark.Value, (a, b, c) => a.Remark == "")
                        .WhereIf(hasSearchOp,
                            (a, b, c) => a.OpName.Contains(req.SearchOp) || a.OpInvitCode.Contains(req.SearchOp))
                        .WhereIf(searchRoleId > 0, (a, b, c) => a.RoleId == searchRoleId)
                        .WhereIf(searchRoleId == 0 && hasSearch,
                            (a, b, c) => c.NickName.Contains(req.Search))
                        .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                            (a, b, c) => a.CreateTime >= req.StartTime && a.CreateTime < req.EndTime);

                    var sum = (long) (await selector.SumAsync((a, b, c) => a.Money));
                    var total = await selector.CountAsync();

                    if (req.Order == 1)
                    {
                        // 金额排序
                        selector = selector.OrderByDescending((a, b, c) => a.Money);
                    }

                    var rows = await selector
                        .OrderByDescending((a, b, c) => a.Id)
                        .Page(req.PageIndex, req.PageSize)
                        .ToListAsync((a, b, c) => new
                        {
                            a.Id,
                            a.OpId,
                            a.OpName,
                            a.OpInvitCode,
                            a.RoleId,
                            RoleName = c.NickName,
                            a.ParentId,
                            ParentName = b.NickName,
                            ParentInvitCode = b.InvitCode,
                            a.Money,
                            a.Remark,
                            a.CreateTime
                        });
                    return JsonResp.Ok(new ListPageResp {Sum = sum, Total = total, Rows = rows});
                }
            }
            else
            {
                // 代理只能查看自己下线用户
                var viewId = opUid;
                if (hasSearchParent)
                {
                    viewId = await DbService.Sql.Select<AdminEntity>()
                        .Where(it => it.Id == opUid)
                        .AsTreeCte()
                        .Where(it => it.NickName.Contains(req.SearchParent) || it.InvitCode.Contains(req.SearchParent))
                        .FirstAsync(it => it.Id);
                    if (viewId == 0) return JsonResp.Ok();
                }

                // 代理只能查看自己下线用户
                var selector = DbService.Sql.Select<AdminEntity, RechargeRoleEntity, RoleEntity>()
                    .Where((a, b, c) => a.Id == viewId)
                    .InnerJoin((a, b, c) => b.ParentId == a.Id)
                    .LeftJoin((a, b, c) => b.RoleId == c.Id)
                    .WhereIf(req.Remark.HasValue && req.Remark.Value, (a, b, c) => b.Remark != "")
                    .WhereIf(req.Remark.HasValue && !req.Remark.Value, (a, b, c) => b.Remark == "")
                    .WhereIf(hasSearchOp,
                        (a, b, c) => b.OpName.Contains(req.SearchOp) || b.OpInvitCode.Contains(req.SearchOp))
                    .WhereIf(searchRoleId > 0, (a, b, c) => b.RoleId == searchRoleId)
                    .WhereIf(searchRoleId == 0 && hasSearch,
                        (a, b, c) => c.NickName.Contains(req.Search))
                    .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                        (a, b, c) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime);

                var sum = (long) (await selector.SumAsync((a, b, c) => b.Money));
                var total = await selector.CountAsync();

                if (req.Order == 1)
                {
                    // 金额排序
                    selector = selector.OrderByDescending((a, b, c) => b.Money);
                }

                var rows = await selector
                    .OrderByDescending((a, b, c) => b.Id)
                    .Page(req.PageIndex, req.PageSize)
                    .ToListAsync((a, b, c) => new
                    {
                        b.Id,
                        b.OpId,
                        b.OpName,
                        b.OpInvitCode,
                        b.RoleId,
                        RoleName = c.NickName,
                        b.ParentId,
                        ParentName = a.NickName,
                        ParentInvitCode = a.InvitCode,
                        b.Money,
                        b.Remark,
                        b.CreateTime
                    });
                return JsonResp.Ok(new ListPageResp {Sum = sum, Total = total, Rows = rows});
            }
        }

        private async Task<JsonResp> DelRechargeRole(uint opUid, AdminCategory opCategory, DelRecordsReq req)
        {
            if (req.StartTime > req.EndTime) return JsonResp.BadRequest();
            await DbService.Sql.Delete<RechargeRoleEntity>()
                .Where(it => it.CreateTime >= req.StartTime && it.CreateTime < req.EndTime)
                .ExecuteAffrowsAsync();
            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListPay(uint opUid, AdminCategory opCategory, ListPayReq req)
        {
            req.Build();

            var hasSearch = !string.IsNullOrWhiteSpace(req.Search);
            uint.TryParse(req.Search, out var searchId);

            if (opCategory <= AdminCategory.Admin)
            {
                // 搜索代理 昵称或邀请码
                if (hasSearch && req.SearchTextType == 1)
                {
                    // 先定位parent
                    var parentId = await DbService.Sql.Select<AdminEntity>()
                        .Where(it => it.NickName.Contains(req.Search) || it.InvitCode.Contains(req.Search))
                        .FirstAsync(it => it.Id);
                    if (parentId == 0) return JsonResp.Ok();

                    // 这里要注意，是parent -> pay -> role
                    var selector = DbService.Sql.Select<AdminEntity>()
                        .Where(it => it.Id == parentId)
                        .AsTreeCte()
                        .From<PayEntity, RoleEntity>((_, b, c) => _)
                        .InnerJoin((a, b, c) => b.Rid == c.Id && c.ParentId == a.Id)
                        .WhereIf(req.Status.HasValue,
                            (a, b, c) => b.Status == (OrderStatus) req.Status.GetValueOrDefault())
                        .WhereIf(req.Server > 0, (a, b, c) => c.ServerId == req.Server)
                        .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                            (a, b, c) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime);

                    // 统计
                    var agg = await selector.ToAggregateAsync((a, b, c) => new
                    {
                        Total = b.Count(),
                        Sum = b.Sum(b.Key.Money),
                        Sum1 = b.Sum(b.Key.Status == OrderStatus.Created ? b.Key.Money : 0),
                        sum2 = b.Sum(b.Key.Status == OrderStatus.Success ? b.Key.Money : 0),
                        sum3 = b.Sum(b.Key.Status == OrderStatus.Fail ? b.Key.Money : 0)
                    });

                    if (req.Order == 1)
                    {
                        // 金额排序
                        selector = selector.OrderByDescending((a, b, c) => b.Money);
                    }

                    var rows = await selector
                        .OrderByDescending((a, b, c) => b.Id)
                        .Page(req.PageIndex, req.PageSize)
                        .ToListAsync((a, b, c) => new
                        {
                            b.Id,
                            b.Rid,
                            RoleName = c.NickName,
                            c.ParentId,
                            ParentName = a.NickName,
                            ParentInvitCode = a.InvitCode,
                            b.Money,
                            b.Jade,
                            b.BindJade,
                            b.PayChannel,
                            b.PayType,
                            b.Order,
                            b.Status,
                            b.CreateTime,
                            b.UpdateTime,
                            b.DelivTime
                        });
                    return JsonResp.Ok(new ListPageResp
                    {
                        Sum = (long) agg.Sum,
                        Sum1 = (long) agg.Sum1,
                        Sum2 = (long) agg.sum2,
                        Sum3 = (long) agg.sum3,
                        Total = agg.Total,
                        Rows = rows
                    });
                }
                else
                {
                    // 管理可以查看所有的角色充值 pay -> parent -> role
                    ISelect<PayEntity, RoleEntity, AdminEntity> selector;

                    if (req.SearchTextType == 2)
                    {
                        // 搜订单
                        selector = DbService.Sql.Select<PayEntity, RoleEntity, AdminEntity>()
                            .LeftJoin((a, b, c) => a.Rid == b.Id)
                            .LeftJoin((a, b, c) => b.ParentId == c.Id)
                            .WhereIf(hasSearch, (a, b, c) => a.Id == searchId)
                            // 搜索订单id的时候，忽略区服id、状态id、时间
                            .WhereIf(!hasSearch && req.Status.HasValue,
                                (a, b, c) => a.Status == (OrderStatus) req.Status.GetValueOrDefault())
                            .WhereIf(!hasSearch && req.Server > 0, (a, b, c) => b.ServerId == req.Server)
                            .WhereIf(!hasSearch && req.StartTime.HasValue && req.EndTime.HasValue,
                                (a, b, c) => a.CreateTime >= req.StartTime && a.CreateTime < req.EndTime);
                    }
                    else
                    {
                        // 搜索角色
                        selector = DbService.Sql.Select<PayEntity, RoleEntity, AdminEntity>()
                            .LeftJoin((a, b, c) => a.Rid == b.Id)
                            .LeftJoin((a, b, c) => b.ParentId == c.Id)
                            .WhereIf(searchId > 0, (a, b, c) => a.Rid == searchId)
                            .WhereIf(searchId == 0 && hasSearch,
                                (a, b, c) => b.NickName.Contains(req.Search))
                            .WhereIf(req.Status.HasValue,
                                (a, b, c) => a.Status == (OrderStatus) req.Status.GetValueOrDefault())
                            // 搜索角色id的时候忽略区服选项
                            .WhereIf(!hasSearch && req.Server > 0, (a, b, c) => b.ServerId == req.Server)
                            .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                                (a, b, c) => a.CreateTime >= req.StartTime && a.CreateTime < req.EndTime);
                    }

                    // 统计
                    var agg = await selector.ToAggregateAsync((a, b, c) => new
                    {
                        Total = b.Count(),
                        Sum = b.Sum(a.Key.Money),
                        Sum1 = b.Sum(a.Key.Status == OrderStatus.Created ? a.Key.Money : 0),
                        sum2 = b.Sum(a.Key.Status == OrderStatus.Success ? a.Key.Money : 0),
                        sum3 = b.Sum(a.Key.Status == OrderStatus.Fail ? a.Key.Money : 0)
                    });

                    if (req.Order == 1)
                    {
                        // 金额排序
                        selector = selector.OrderByDescending((a, b, c) => a.Money);
                    }

                    var rows = await selector
                        .OrderByDescending((a, b, c) => a.Id)
                        .Page(req.PageIndex, req.PageSize)
                        .ToListAsync((a, b, c) => new
                        {
                            a.Id,
                            a.Rid,
                            RoleName = b.NickName,
                            b.ParentId,
                            ParentName = c.NickName,
                            ParentInvitCode = c.InvitCode,
                            a.Money,
                            a.Jade,
                            a.BindJade,
                            a.PayChannel,
                            a.PayType,
                            a.Order,
                            a.Status,
                            a.CreateTime,
                            a.UpdateTime,
                            a.DelivTime
                        });
                    return JsonResp.Ok(new ListPageResp
                    {
                        Sum = (long) agg.Sum,
                        Sum1 = (long) agg.Sum1,
                        Sum2 = (long) agg.sum2,
                        Sum3 = (long) agg.sum3,
                        Total = agg.Total,
                        Rows = rows
                    });
                }
            }
            else
            {
                // 代理只能查看自己下线用户
                var viewId = opUid;
                if (hasSearch && req.SearchTextType == 1)
                {
                    viewId = await DbService.Sql.Select<AdminEntity>()
                        .Where(it => it.Id == opUid)
                        .AsTreeCte()
                        .Where(it => it.NickName.Contains(req.Search) || it.InvitCode.Contains(req.Search))
                        .FirstAsync(it => it.Id);
                    if (viewId == 0) return JsonResp.Ok();
                }

                var selector = DbService.Sql.Select<AdminEntity, PayEntity, RoleEntity>()
                    .Where((a, b, c) => a.Id == viewId)
                    .InnerJoin((a, b, c) => a.Id == c.ParentId && b.Rid == c.Id);

                if (req.SearchTextType == 1)
                {
                    // 查代理, 常规过滤
                    selector = selector
                        .WhereIf(req.Status.HasValue,
                            (a, b, c) => b.Status == (OrderStatus) req.Status.GetValueOrDefault())
                        .WhereIf(req.Server > 0, (a, b, c) => c.ServerId == req.Server)
                        .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                            (a, b, c) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime);
                }

                if (req.SearchTextType == 2)
                {
                    // 搜订单
                    selector = selector
                        .WhereIf(hasSearch, (a, b, c) => b.Id == searchId)
                        .WhereIf(!hasSearch && req.Status.HasValue,
                            (a, b, c) => b.Status == (OrderStatus) req.Status.GetValueOrDefault())
                        .WhereIf(!hasSearch && req.Server > 0, (a, b, c) => c.ServerId == req.Server)
                        .WhereIf(!hasSearch && req.StartTime.HasValue && req.EndTime.HasValue,
                            (a, b, c) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime);
                }
                else
                {
                    // 搜角色
                    selector = selector
                        .WhereIf(searchId > 0, (a, b, c) => b.Rid == searchId)
                        .WhereIf(searchId == 0 && hasSearch, (a, b, c) => c.NickName.Contains(req.Search))
                        .WhereIf(req.Status.HasValue,
                            (a, b, c) => b.Status == (OrderStatus) req.Status.GetValueOrDefault())
                        // 搜索角色时忽略区服
                        .WhereIf(!hasSearch && req.Server > 0, (a, b, c) => c.ServerId == req.Server)
                        .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                            (a, b, c) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime);
                }

                // 统计
                var agg = await selector.ToAggregateAsync((a, b, c) => new
                {
                    Total = b.Count(),
                    Sum = b.Sum(b.Key.Money),
                    Sum1 = b.Sum(b.Key.Status == OrderStatus.Created ? b.Key.Money : 0),
                    sum2 = b.Sum(b.Key.Status == OrderStatus.Success ? b.Key.Money : 0),
                    sum3 = b.Sum(b.Key.Status == OrderStatus.Fail ? b.Key.Money : 0)
                });

                if (req.Order == 1)
                {
                    // 金额排序
                    selector = selector.OrderByDescending((a, b, c) => b.Money);
                }

                var rows = await selector
                    .OrderByDescending((a, b, c) => b.Id)
                    .Page(req.PageIndex, req.PageSize)
                    .ToListAsync((a, b, c) => new
                    {
                        b.Id,
                        b.Rid,
                        RoleName = c.NickName,
                        c.ParentId,
                        ParentName = a.NickName,
                        ParentInvitCode = a.InvitCode,
                        b.Money,
                        b.Jade,
                        b.BindJade,
                        b.PayChannel,
                        b.PayType,
                        b.Order,
                        b.Status,
                        b.CreateTime,
                        b.UpdateTime,
                        b.DelivTime
                    });
                return JsonResp.Ok(new ListPageResp
                {
                    Sum = (long) agg.Sum,
                    Sum1 = (long) agg.Sum1,
                    Sum2 = (long) agg.sum2,
                    Sum3 = (long) agg.sum3,
                    Total = agg.Total,
                    Rows = rows
                });
            }
        }

        private async Task<JsonResp> DelPay(uint opUid, AdminCategory opCategory, DelRecordsReq req)
        {
            if (req.StartTime > req.EndTime) return JsonResp.BadRequest();
            await DbService.Sql.Delete<PayEntity>()
                .Where(it => it.CreateTime >= req.StartTime && it.CreateTime < req.EndTime)
                .ExecuteAffrowsAsync();
            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListPayRecords(uint opUid, AdminCategory opCategory, ListPayRecordsReq req)
        {
            req.Build();

            var parentId = opCategory <= AdminCategory.Admin ? 0 : opUid;

            var listAgency = await DbService.Sql.Queryable<AdminEntity>()
                .Where(it => (it.ParentId == parentId || it.Id == parentId) && it.Category == AdminCategory.Agency)
                .ToListAsync(it => new {it.Id, it.NickName, it.InvitCode});

            var rows = new List<object>(listAgency.Count);
            foreach (var agent in listAgency)
            {
                // 先查找我下面所有的一级代理
                var sum = await DbService.Sql.Select<AdminEntity, PayEntity, RoleEntity>()
                    .Where((a, b, c) => a.Id == agent.Id)
                    .InnerJoin((a, b, c) => a.Id == c.ParentId && b.Rid == c.Id)
                    .Where((a, b, c) => b.Status == OrderStatus.Success)
                    .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                        (a, b, c) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime)
                    .SumAsync((a, b, c) => b.Money);
                
                if (parentId == 0) {
                    //  查找二级代理的充值
                    var listAgency2 = await DbService.Sql.Queryable<AdminEntity>()
                        .Where(it => it.ParentId == agent.Id && it.Category == AdminCategory.Agency)
                        .ToListAsync(it => new {it.Id});

                    foreach (var agent2 in listAgency2) 
                    {
                        var sum2 = await DbService.Sql.Select<AdminEntity, PayEntity, RoleEntity>()
                            .Where((a, b, c) => a.Id == agent2.Id)
                            .InnerJoin((a, b, c) => a.Id == c.ParentId && b.Rid == c.Id)
                            .Where((a, b, c) => b.Status == OrderStatus.Success)
                            .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                                (a, b, c) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime)
                            .SumAsync((a, b, c) => b.Money);
                        
                        //  查找三级代理的充值
                        var listAgency3 = await DbService.Sql.Queryable<AdminEntity>()
                            .Where(it => it.ParentId == agent2.Id && it.Category == AdminCategory.Agency)
                            .ToListAsync(it => new {it.Id});
                        foreach (var agent3 in listAgency3)
                        {
                            var sum3 = await DbService.Sql.Select<AdminEntity, PayEntity, RoleEntity>()
                                .Where((a, b, c) => a.Id == agent3.Id)
                                .InnerJoin((a, b, c) => a.Id == c.ParentId && b.Rid == c.Id)
                                .Where((a, b, c) => b.Status == OrderStatus.Success)
                                .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                                    (a, b, c) => b.CreateTime >= req.StartTime && b.CreateTime < req.EndTime)
                                .SumAsync((a, b, c) => b.Money);

                            sum2 += sum3;
                        }

                        sum += sum2;           
                    }
                }

                if (sum > 0)
                {
                    rows.Add(new
                    {
                        agent.Id,
                        agent.NickName,
                        agent.InvitCode,
                        Sum = sum
                    });
                }
            }

            return JsonResp.Ok(new ListPageResp
            {
                Total = rows.Count,
                Rows = rows
            });
        }

        private async Task<JsonResp> ListRankLevel(ListRankReq req)
        {
            req.Build();
            var total = await RedisService.GetRoleLevelRankCount(req.Server.GetValueOrDefault());
            var rows = await RedisService.GetRoleLevelRank(req.Server.GetValueOrDefault(), req.PageIndex, req.PageSize);
            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> ListRankJade(ListRankReq req)
        {
            req.Build();
            var total = await RedisService.GetRoleJadeRankCount(req.Server.GetValueOrDefault());
            var rows = await RedisService.GetRoleJadeRank(req.Server.GetValueOrDefault(), req.PageIndex, req.PageSize);
            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> ListRankPay(ListRankReq req)
        {
            req.Build();
            var total = await RedisService.GetRolePayRankCount(req.Server.GetValueOrDefault());
            var rows = await RedisService.GetRolePayRank(req.Server.GetValueOrDefault(), req.PageIndex, req.PageSize);
            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> ListRankSldh(ListRankReq req)
        {
            req.Build();
            var total = await RedisService.GetRoleSldhRankCount(req.Server.GetValueOrDefault());
            var rows = await RedisService.GetRoleSldhRank(req.Server.GetValueOrDefault(), req.PageIndex, req.PageSize);
            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> ListRankSect(ListRankReq req)
        {
            req.Build();

            var serverGrain = GrainFactory.GetGrain<IServerGrain>(req.Server.GetValueOrDefault());
            if (!await serverGrain.CheckActive())
            {
                // 说明区服未启动
                return JsonResp.Ok();
            }
            var bytes = await serverGrain.GetSectRank(req.PageIndex, req.PageSize);
            if (bytes.Value == null) return JsonResp.Ok();

            var resp = S2C_RankSect.Parser.ParseFrom(bytes.Value);
            var total = await serverGrain.QuerySectNum();

            return JsonResp.Ok(new ListPageResp {Total = total, Rows = resp.List});
        }

        private async Task<JsonResp> DismissSect(uint opUid, DissmissSectReq req)
        {
            var entity = await DbService.Sql.Queryable<SectEntity>()
                .Where(it => it.Id == req.Id)
                .FirstAsync(it => new {it.Id, it.Name, it.OwnerId});
            if (entity == null) return JsonResp.Error("帮派不存在");
            var grain = GrainFactory.GetGrain<ISectGrain>(entity.Id);
            var ret = await grain.Dismiss();
            if (!ret) return JsonResp.Error("帮派不存在");

            return JsonResp.Ok();
        }

        private async Task<JsonResp> ReloadSects(ReloadSectsReq req)
        {
            if (req == null || req.Sid.GetValueOrDefault() == 0) return JsonResp.BadRequest();
            var sid = req.Sid.GetValueOrDefault();
            var serverGrain = GrainFactory.GetGrain<IServerGrain>(sid);
            if (await serverGrain.CheckActive())
            {
                await serverGrain.ReloadSects();
            }
            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListRankSinglePk(ListRankReq req)
        {
            req.Build();
            var total = await RedisService.GetRoleSinglePkRankCount(req.Server.GetValueOrDefault());
            var rows = await RedisService.GetRoleSinglePkRank(req.Server.GetValueOrDefault(), req.PageIndex,
                req.PageSize);
            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> ListOpLog(ListOpLogReq req)
        {
            await Task.CompletedTask;
            req.Build();
            return JsonResp.Ok();
        }

        private async Task<JsonResp> ListBugLog(ListBugLogReq req)
        {
            req.Build();
            var rows = await DbService.Sql.Select<ErrorEntity>()
                .WhereIf(req.Uid.HasValue, it => it.Uid == req.Uid)
                .WhereIf(req.Rid.HasValue, it => it.Rid == req.Rid)
                .WhereIf(req.Status.HasValue, it => it.Status == (ErrorStatus) req.Status)
                .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                    it => it.CreateTime >= req.StartTime && it.CreateTime < req.EndTime)
                .Count(out var total)
                .OrderByDescending(it => it.Id)
                .Page(req.PageIndex, req.PageSize)
                .ToListAsync();
            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> DelBugLog(uint opUid, AdminCategory opCategory, DelRecordsReq req)
        {
            if (req.StartTime > req.EndTime) return JsonResp.BadRequest();
            await DbService.Sql.Delete<ErrorEntity>()
                .Where(it => it.CreateTime >= req.StartTime && it.CreateTime < req.EndTime)
                .ExecuteAffrowsAsync();
            return JsonResp.Ok();
        }

        private async Task<JsonResp> SendPalaceNotice(uint serverId, string msg, uint times)
        {
            var grain = GrainFactory.GetGrain<IServerGrain>(serverId);
            if (grain != null && await grain.CheckActive())
            {
                await grain.GmBroadcastPalaceNotice(msg, times);
                return JsonResp.Ok();
            }
            return JsonResp.Error("区服不存在或是维护状态");
        }

        // 玩家是否已经被赠送过礼物？
        private bool IsGifted(RoleEntity role)
        {
            var Value = role.Flags;
            var ret = Value & (1 << 6);
            return ret != 0;
        }

        private async Task<JsonResp> SendRoleGift(uint roleId)
        {
            // 获取角色信息
            var role = await DbService.Sql.Queryable<RoleEntity>()
                .Where(it => it.Id == roleId)
                .FirstAsync();
            if (role == null) return JsonResp.Error("角色不存在");

            if (IsGifted(role)) return JsonResp.Error("角色已经获得了礼物");

            var msg = await GrainFactory.GetGrain<IPlayerGrain>(roleId).GmSendRoleGift();
            if (msg == null || msg.Length == 0)
            {
                return JsonResp.Ok();
            }
            else
            {
                return JsonResp.Error(msg);
            }
        }

        private async Task<JsonResp> SendSetLimitChargeRank(uint server, uint start, uint end, bool cleanup)
        {
            var grain = GrainFactory.GetGrain<IServerGrain>(server);
            if (grain != null && await grain.CheckActive())
            {
                await grain.GmSetLimitChargeRankTimestamp(start, end, cleanup);
                return JsonResp.Ok();
            }
            return JsonResp.Error("区服不存在或是维护状态");
        }

        private async Task<JsonResp> SendGetLimitChargeRank(uint server)
        {
            var start = await RedisService.GetLimitChargeStartTimestamp(server);
            var end = await RedisService.GetLimitChargeEndTimestamp(server);
            return JsonResp.Ok(new { start, end });
        }

        private async Task<JsonResp> ListLimitRank(ListLimitRankReq req)
        {
            req.Build();
            var total = await RedisService.GetLimitChargeRankCount(req.Server.GetValueOrDefault());
            var rows = await RedisService.GetLimitChargeRankList(req.Server.GetValueOrDefault(), req.PageIndex, req.PageSize);
            return JsonResp.Ok(new ListPageResp {Total = total, Rows = rows});
        }

        private async Task<JsonResp> ListChatLog(uint opUid, AdminCategory opCategory, ListChatLogReq req)
        {
            if (opCategory >= AdminCategory.Agency)
            {
                return JsonResp.Error(ErrCode.NoPermission);
            }
            req.Build();

            var selector = DbService.Sql.Select<RoleEntity, ChatMsgEntity>()
                    .LeftJoin((a, b) => a.Id == b.FromRid || a.Id == b.ToRid)
                    .WhereIf(req.FromRid.HasValue, (a, b) => b.FromRid == req.FromRid)
                    .WhereIf(req.ToRid.HasValue, (a, b) => b.ToRid == req.ToRid)
                    .WhereIf(req.MsgType.HasValue, (a, b) => b.MsgType == (byte)req.MsgType)
                    .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                        (a, b) => b.SendTime >= req.StartTime && b.SendTime < req.EndTime);
            var total = await selector.CountAsync();
            var rows = await selector
                .OrderByDescending((a, b) => b.SendTime)
                .Page(req.PageIndex, req.PageSize)
                .ToListAsync((a, b) => new
                {
                    b.Id,
                    b.FromRid,
                    b.ToRid,
                    b.MsgType,
                    b.Msg,
                    b.SendTime,
                    role_status = a.Status,
                });

            return JsonResp.Ok(new ListPageResp
            {
                Total = total,
                Rows = rows
            });
            /*
            var rows = await DbService.Sql.Select<ChatMsgEntity>()
                .WhereIf(req.FromRid.HasValue, it => it.FromRid == req.FromRid)
                .WhereIf(req.ToRid.HasValue, it => it.ToRid == req.ToRid)
                .WhereIf(req.MsgType.HasValue, it => it.MsgType == (byte)req.MsgType)
                .WhereIf(req.StartTime.HasValue && req.EndTime.HasValue,
                    it => it.SendTime >= req.StartTime && it.SendTime < req.EndTime)
                .Count(out var total)
                .OrderByDescending(it => it.Id)
                .Page(req.PageIndex, req.PageSize)
                .ToListAsync();
            return JsonResp.Ok(new ListPageResp { Total = total, Rows = rows });
            */
        }

        private async Task<JsonResp> DelChatLog(uint opUid, AdminCategory opCategory, DelRecordsReq req)
        {
            if (opCategory >= AdminCategory.Agency)
            {
                return JsonResp.Error(ErrCode.NoPermission);
            }
            if (req.StartTime > req.EndTime) return JsonResp.BadRequest();
            await DbService.Sql.Delete<ChatMsgEntity>()
                .Where(it => it.SendTime >= req.StartTime && it.SendTime < req.EndTime)
                .ExecuteAffrowsAsync();
            return JsonResp.Ok();
        }

        private static T ParsePayload<T>(Immutable<byte[]> payload)
        {
            return Json.Deserialize<T>(payload.Value);
        }
    }
}