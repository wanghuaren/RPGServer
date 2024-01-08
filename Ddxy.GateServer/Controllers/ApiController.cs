using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Ddxy.Common.Model.Api;
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
    public class ApiController : ControllerBase
    {
        private readonly SiloClient _siloClient;

        public ApiController(SiloClient siloClient)
        {
            _siloClient = siloClient;
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("report_error")]
        public async Task ReportError([FromBody] ReportErrorReq req)
        {
            if (!TryFindApiGateGrain(out var grain)) return;
            var payload = new Immutable<byte[]>(Json.SerializeToBytes(req));
            await grain.ReportError(payload);
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("get_notice")]
        public async Task GetNotice()
        {
            if (!TryFindApiGateGrain(out var grain)) return;
            var result = await grain.GetNotice();
            await Response.Body.WriteAsync(result.Value);
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("signup")]
        public async Task Regist([FromBody] SignUpReq req)
        {
            if (!TryFindApiGateGrain(out var grain)) return;
            var result = await grain.SignUp(req.UserName, req.Password, req.InviteCode, req.IsRobot, req.Version);
            await Response.Body.WriteAsync(result.Value);
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("signin")]
        public async Task Login([FromBody] SignInReq req)
        {
            if (!TryFindApiGateGrain(out var grain)) return;
            var result = await grain.SignIn(this.GetIp(), req.UserName, req.Password, req.Version);
            await Response.Body.WriteAsync(result.Value);
        }

        // [HttpPost]
        // [Route("signout")]
        // public async Task Logout()
        // {
        //     if (!this.CheckGameUser(out var info)) return;
        //     if (!TryFindApiGateGrain(out var grain)) return;
        //     _ = grain.SignOut(info.Id);
        //     await Task.CompletedTask;
        // }

        [HttpPost]
        [Route("create_role")]
        public async Task CreateRole([FromBody] CreateRoleReq req)
        {
            if (!this.CheckGameUser(out var info)) return;
            if (!TryFindApiGateGrain(out var grain)) return;
            var result = await grain.CreateRole(info.Id, req.ServerId.GetValueOrDefault(),
                req.CfgId.GetValueOrDefault(), req.Nickname);
            await Response.Body.WriteAsync(result.Value);
        }

        [HttpGet]
        [Route("list_server")]
        public async Task ListServer()
        {
            if (!this.CheckGameUser(out var info)) return;
            if (!TryFindApiGateGrain(out var grain)) return;
            var result = await grain.ListServer(info.Id);
            await Response.Body.WriteAsync(result.Value);
        }

        [HttpPost]
        [Route("enter_server")]
        public async Task EnterServer([Required] [FromQuery] uint? roleId)
        {
            if (!this.CheckGameUser(out var info)) return;
            if (!TryFindApiGateGrain(out var grain)) return;
            var result = await grain.EnterServer(info.Id, roleId.GetValueOrDefault());
            await Response.Body.WriteAsync(result.Value);
        }
        //[HttpPost]
        //[Route("xin_notify")]
        [Consumes("application/json", new string[] { "application/x-www-form-urlencoded", "multipart/form-data" })]
        [AllowAnonymous]
        //[HttpGet]
        [Route("xin_notify")]
        public async Task<IActionResult> XinNotify(MYXinNotifyReq req)
        {
            if (TryFindApiGateGrain(out var grain))
            {
                string ret = await grain.MYXinNotify(Json.Serialize(req));
                return await Task.Run(() =>
                {
                    return Content(ret);
                });
            }
            else
            {
                return await Task.Run(() =>
                {
                    return Content("错误偶错位教哦放假哦i发生纠纷撒豆");
                });
            }
        }

        [HttpGet]
        [Route("xin_notify2")]
        [AllowAnonymous]
        public async Task<IActionResult> XinNotify2(MYXinNotifyReq2 req)
        {
            if (TryFindApiGateGrain(out var grain))
            {
                string ret = await grain.MYXinNotify2(Json.Serialize(req));
                return await Task.Run(() =>
                {
                    return Content(ret);
                });
            }
            else
            {
                return await Task.Run(() =>
                {
                    return Content("仙玉发生了错误");
                });
            }
        }

        [HttpGet]
        [Route("xin_notify_bind_jade")]
        [AllowAnonymous]
        public async Task<IActionResult> XinNotifyBindJade(YunDingNotifyReq req)
        {
            if (TryFindApiGateGrain(out var grain))
            {
                string ret = await grain.MYXinNotifyBindJade(Json.Serialize(req));
                return await Task.Run(() =>
                {
                    return Content(ret);
                });
            }
            else
            {
                return await Task.Run(() =>
                {
                    return Content("积分发生了错误");
                });
            }
        }

        [HttpGet]
        [Route("return_url")]
        [AllowAnonymous]
        public async Task<IActionResult> ReturnUrl(YunDingNotifyReq req)
        {
            await Task.CompletedTask;
            if (req.trade_status == "TRADE_SUCCESS")
            {
                return Content("支付成功");
            }
            else
            {
                return Content("支付失败");
            }
        }

        private bool TryFindApiGateGrain(out IApiGateGrain grain)
        {
            grain = _siloClient.GetGrain<IApiGateGrain>(0);
            if (grain == null)
            {
                Response.StatusCode = (int) HttpStatusCode.ServiceUnavailable;
                return false;
            }

            return true;
        }
    }
}