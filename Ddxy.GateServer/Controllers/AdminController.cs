using System.Net;
using System.Threading.Tasks;
using Ddxy.Common.Model;
using Ddxy.Common.Model.Admin;
using Ddxy.GateServer.Extensions;
using Ddxy.GateServer.Network;
using Ddxy.GateServer.Util;
using Ddxy.GrainInterfaces.Gate;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Orleans.Concurrency;

namespace Ddxy.GateServer.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    [Consumes("application/json")]
    [Produces("application/json")]
    public class AdminController : ControllerBase
    {
        private readonly SiloClient _siloClient;

        public AdminController(SiloClient siloClient)
        {
            _siloClient = siloClient;
        }

        [HttpPost]
        [AllowAnonymous]
        [Route("signin")]
        public async Task Login([FromBody] SignInReq req)
        {
            if (!TryFindAdminGateGrain(out var grain)) return;
            var result = await grain.SignIn(this.GetIp(), req.UserName, req.Password);
            await this.WriteBytes(result.Value);
        }

        [HttpGet]
        [Route("profile")]
        public async Task Profile()
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var resp = await grain.Handle(info.Id, this.GetIp(), "profile", new Immutable<byte[]>());
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("signout")]
        public async Task Logout()
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var resp = await grain.Handle(info.Id, this.GetIp(), "signout", new Immutable<byte[]>());
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("save_profile")]
        public async Task SaveProfile([FromBody] SaveProfileReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "save_profile", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_admin")]
        public async Task ListAdmin([FromBody] ListPageReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_admin", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("add_admin")]
        public async Task AddAdmin([FromBody] AddAdminReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "add_admin", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("del_admin")]
        public async Task DelAdmin([FromBody] DelAdminReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "del_admin", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("edit_admin")]
        public async Task EditAdmin([FromBody] EditAdminReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "edit_admin", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("froze_admin")]
        public async Task FrozeAdmin([FromBody] FrozeAdminReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "froze_admin", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_agency")]
        public async Task ListAgency([FromBody] ListAgencyReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_agency", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("add_agency")]
        public async Task AddAgency([FromBody] AddAdminReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "add_agency", payload);
            await this.WriteBytes(resp.Value);
        }

        // [HttpPost]
        // [Route("del_agency")]
        // public async Task DelAgency([FromBody] DelAdminReq req)
        // {
        //     if (!this.CheckAdminUser(out var info)) return;
        //     if (!TryFindAdminGateGrain(out var grain)) return;
        //     var payload = BuildPayload(req);
        //     var resp = await grain.Handle(info.Id, this.GetIp(), "del_agency", payload);
        //     await this.WriteBytes(resp.Value);
        // }

        [HttpPost]
        [Route("edit_agency")]
        public async Task EditAgency([FromBody] EditAdminReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "edit_agency", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("froze_agency")]
        public async Task FrozeAgency([FromBody] FrozeAdminReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "froze_agency", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_server")]
        public async Task ListServer([FromBody] ListPageReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_server", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpGet]
        [Route("list_server1")]
        public async Task ListServer1()
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_server1", new Immutable<byte[]>(null));
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("add_server")]
        public async Task AddServer([FromBody] AddServerReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "add_server", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("edit_server")]
        public async Task EditServer([FromBody] EditServerReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "edit_server", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("change_server_status")]
        public async Task ChangeServerStatus([FromBody] ChangeServerStatusReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "change_server_status", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("start_server")]
        public async Task StartServer([FromBody] StartServerReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "start_server", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("stop_server")]
        public async Task StopServer([FromBody] StartServerReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "stop_server", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("open_activity")]
        public async Task OpenActivity([FromBody] OpenActivityReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "open_activity", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("query_combine_server")]
        public async Task QueryCombineServer()
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var resp = await grain.Handle(info.Id, this.GetIp(), "query_combine_server", new Immutable<byte[]>());
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("combine_server")]
        public async Task CombineServer([FromBody] CombineServerReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "combine_server", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpGet]
        [Route("get_notice")]
        public async Task GetNotice()
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var resp = await grain.Handle(info.Id, this.GetIp(), "get_notice", new Immutable<byte[]>());
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("set_notice")]
        public async Task SetNotice([FromBody] SetNoticeReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "set_notice", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpGet]
        [Route("get_pay_enable")]
        public async Task GetPayEnable()
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var resp = await grain.Handle(info.Id, this.GetIp(), "get_pay_enable", new Immutable<byte[]>());
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("set_pay_enable")]
        public async Task SetPayEnable([FromBody] SetPayEnableReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "set_pay_enable", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpGet]
        [Route("get_pay_rate")]
        public async Task GetPayRate()
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var resp = await grain.Handle(info.Id, this.GetIp(), "get_pay_rate", new Immutable<byte[]>());
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("set_pay_rate")]
        public async Task SetPayRate([FromBody] SetPayRateReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "set_pay_rate", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_mail")]
        public async Task ListMail([FromBody] ListMailReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_mail", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("add_mail")]
        public async Task AddMail([FromBody] AddMailReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "add_mail", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("del_mail")]
        public async Task DelMail([FromBody] DelMailReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "del_mail", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpGet]
        [Route("get_res_version")]
        public async Task GetResVersion()
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = new Immutable<byte[]>(null);
            var resp = await grain.Handle(info.Id, this.GetIp(), "get_res_version", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("set_res_version")]
        public async Task SetResVersion([FromBody] SetResVersionReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "set_res_version", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("reload_config")]
        public async Task ReloadConfig()
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = new Immutable<byte[]>(null);
            var resp = await grain.Handle(info.Id, this.GetIp(), "reload_config", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_user")]
        public async Task ListUser([FromBody] ListUserReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_user", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("edit_user")]
        public async Task EditUser([FromBody] EditUserReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "edit_user", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("froze_user")]
        public async Task FrozeUser([FromBody] FrozeUserReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "froze_user", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_role")]
        public async Task ListRole([FromBody] ListRoleReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_role", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("froze_role")]
        public async Task FrozeRole([FromBody] FrozeRoleReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "froze_role", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("change_role_online")]
        public async Task ChangeRoleOnline([FromBody] ChangeRoleOnlineReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "change_role_online", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("get_role_detail")]
        public async Task GetRoleDetail([FromBody] GetRoleDetailReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "get_role_detail", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("get_role_equips")]
        public async Task GetRoleEquip([FromBody] GetRoleEquipsReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "get_role_equips", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("set_equip_refine")]
        public async Task SetRoleEquip([FromBody] SetEquipRefineReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "set_equip_refine", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("get_role_ornaments")]
        public async Task GetRoleOrnaments([FromBody] GetRoleEquipsReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "get_role_ornaments", payload);
            await this.WriteBytes(resp.Value);
        }
        
        [HttpPost]
        [Route("get_role_mounts")]
        public async Task GetRoleMounts([FromBody] GetRoleEquipsReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "get_role_mounts", payload);
            await this.WriteBytes(resp.Value);
        }
        
        [HttpPost]
        [Route("set_mount_skill")]
        public async Task SetMountSkill([FromBody] SetMountSkillReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "set_mount_skill", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("change_role_level")]
        public async Task ChangeRoleLevel([FromBody] ChangeRoleLevelReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "change_role_level", payload);
            await this.WriteBytes(resp.Value);
        }

        // [HttpPost]
        // [Route("change_role_money")]
        // public async Task ChangeRoleMoney([FromBody] ChangeRoleMoneyReq req)
        // {
        //     if (!this.CheckAdminUser(out var info)) return;
        //     if (!TryFindAdminGateGrain(out var grain)) return;
        //     var payload = BuildPayload(req);
        //     var resp = await grain.Handle(info.Id, this.GetIp(), "change_role_money", payload);
        //     await this.WriteBytes(resp.Value);
        // }

        [HttpPost]
        [Route("change_role_item")]
        public async Task ChangeRoleItem([FromBody] ChangeRoleItemReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "change_role_item", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("change_role_star")]
        public async Task ChangeRoleStar([FromBody] ChangeRoleStarReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "change_role_star", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("change_role_totalpay")]
        public async Task ChangeRoleTotalPay([FromBody] ChangeRoleTotalPayReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "change_role_totalpay", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("add_role_skillexp")]
        public async Task AddRoleSkillExp([FromBody] GetRoleDetailReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "add_role_skillexp", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("add_role_equip")]
        public async Task AddRoleEquip([FromBody] AddRoleEquipReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "add_role_equip", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("add_role_ornament")]
        public async Task AddRoleOrnament([FromBody] AddRoleOrnamentReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "add_role_ornament", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("add_role_wing")]
        public async Task AddRoleWing([FromBody] AddRoleWingReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "add_role_wing", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("add_role_title")]
        public async Task AddRoleTitle([FromBody] AddRoleTitleReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "add_role_title", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("del_role_title")]
        public async Task DelRoleTitle([FromBody] DelRoleTitleReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "del_role_title", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("set_role_type")]
        public async Task SetRoleType([FromBody] SetRoleTypeReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "set_role_type", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("set_role_flag")]
        public async Task SetRoleFlag([FromBody] SetRoleFlagReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "set_role_flag", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("del_role_shane")]
        public async Task DelRoleShane([FromBody] DelRoleShaneReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "del_role_shane", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("recharge")]
        public async Task Recharge([FromBody] RechargeReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "recharge", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("recharge_role")]
        public async Task RechargeRole([FromBody] RechargeRoleReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "recharge_role", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_recharge")]
        public async Task ListRecharge([FromBody] ListRechargeReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_recharge", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("del_recharge")]
        public async Task DelRecharge([FromBody] DelRecordsReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "del_recharge", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_recharge_role")]
        public async Task ListRecharge([FromBody] ListRechargeRoleReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_recharge_role", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("del_recharge_role")]
        public async Task DelRechargeRole([FromBody] DelRecordsReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "del_recharge_role", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_pay")]
        public async Task ListPay([FromBody] ListPayReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_pay", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("del_pay")]
        public async Task DelPay([FromBody] DelRecordsReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "del_pay", payload);
            await this.WriteBytes(resp.Value);
        }
        
        // [HttpPost]
        // [Route("refresh_order")]
        // public async Task RefreshOrder([FromBody] RefreshOrderReq req)
        // {
        //     if (!this.CheckAdminUser(out var info)) return;
        //     if (!TryFindAdminGateGrain(out var grain)) return;
        //     var payload = BuildPayload(req);
        //     var resp = await grain.Handle(info.Id, this.GetIp(), "refresh_order", payload);
        //     await this.WriteBytes(resp.Value);
        // }

        [HttpPost]
        [Route("list_pay_records")]
        public async Task ListPayRecords([FromBody] ListPayRecordsReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_pay_records", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_rank_level")]
        public async Task ListRankLevel([FromBody] ListRankReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_rank_level", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_rank_jade")]
        public async Task ListRankJade([FromBody] ListRankReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_rank_jade", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_rank_pay")]
        public async Task ListRankPay([FromBody] ListRankReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_rank_pay", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_rank_sldh")]
        public async Task ListRankSldh([FromBody] ListRankReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_rank_sldh", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_rank_sect")]
        public async Task ListRankSect([FromBody] ListRankReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_rank_sect", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("dismiss_sect")]
        public async Task DismissSect([FromBody] DissmissSectReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "dismiss_sect", payload);
            await this.WriteBytes(resp.Value);
        }
        
        [HttpPost]
        [Route("reload_sects")]
        public async Task ReloadSects([FromBody] ReloadSectsReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "reload_sects", payload);
            await this.WriteBytes(resp.Value);
        }
        
        [HttpPost]
        [Route("list_rank_single_pk")]
        public async Task ListRankSinglePk([FromBody] ListRankReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_rank_single_pk", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_log_op")]
        public async Task ListOpLog([FromBody] ListOpLogReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_log_op", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_log_bug")]
        public async Task ListBugLog([FromBody] ListBugLogReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_log_bug", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("del_log_bug")]
        public async Task DelLogBug([FromBody] DelRecordsReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "del_log_bug", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("send_palace_notice")]
        public async Task SendPalaceNotice([FromBody] SendPalaceNoticeReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "send_palace_notice", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("send_role_gift")]
        public async Task SendRoleGift([FromBody] SendRoleGiftReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "send_role_gift", payload);
            await this.WriteBytes(resp.Value);
        }
        
        [HttpPost]
        [Route("send_set_limit_charge_rank")]
        public async Task SendSetLimitChargeRank([FromBody] SendSetLimitChargeRankReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "send_set_limit_charge_rank", payload);
            await this.WriteBytes(resp.Value);
        }
        
        [HttpPost]
        [Route("send_get_limit_charge_rank")]
        public async Task SendGetLimitChargeRank([FromBody] SendGetLimitChargeRankReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "send_get_limit_charge_rank", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_limit_charge_rank")]
        public async Task ListLimitChargeRank([FromBody] ListLimitRankReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_limit_charge_rank", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("list_chat_log")]
        public async Task ListChatLog([FromBody] ListChatLogReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "list_chat_log", payload);
            await this.WriteBytes(resp.Value);
        }

        [HttpPost]
        [Route("del_chat_log")]
        public async Task DelChatLog([FromBody] DelRecordsReq req)
        {
            if (!this.CheckAdminUser(out var info)) return;
            if (!TryFindAdminGateGrain(out var grain)) return;
            var payload = BuildPayload(req);
            var resp = await grain.Handle(info.Id, this.GetIp(), "del_chat_log", payload);
            await this.WriteBytes(resp.Value);
        }

        // [HttpPost]
        // [Route("reset_first_pay")]
        // public async Task ResetFirstPay()
        // {
        //     if (!this.CheckAdminUser(out var info)) return;
        //     if (!TryFindAdminGateGrain(out var grain)) return;
        //     var payload = new Immutable<byte[]>(null);
        //     var resp = await grain.Handle(info.Id, this.GetIp(), "reset_first_pay", payload);
        //     await this.WriteBytes(resp.Value);
        // }

        private bool TryFindAdminGateGrain(out IAdminGateGrain grain)
        {
            grain = _siloClient.GetGrain<IAdminGateGrain>(0);
            if (grain == null)
            {
                Response.StatusCode = (int) HttpStatusCode.ServiceUnavailable;
                return false;
            }

            return true;
        }

        private static Immutable<byte[]> BuildPayload(object o)
        {
            return new Immutable<byte[]>(Json.SerializeToBytes(o));
        }
    }
}