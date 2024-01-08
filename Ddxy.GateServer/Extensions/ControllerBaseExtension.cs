using System;
using System.Net;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Ddxy.Common.Model;
using Ddxy.Common.Model.Admin;
using Microsoft.AspNetCore.Mvc;

namespace Ddxy.GateServer.Extensions
{
    public static class ControllerBaseExtension
    {
        public static TokenInfo GetTokenInfo(this ControllerBase ctx)
        {
            uint uid = 0;
            byte category = 0;

            foreach (var claim in ctx.User.Claims)
            {
                switch (claim.Type)
                {
                    case ClaimTypes.Sid:
                    {
                        uint.TryParse(claim.Value, out uid);
                    }
                        break;
                    case ClaimTypes.Role:
                    {
                        byte.TryParse(claim.Value, out category);
                    }
                        break;
                }
            }

            return new TokenInfo(uid, (AdminCategory) category);
        }

        public static bool CheckGameUser(this ControllerBase ctx, out TokenInfo info)
        {
            info = ctx.GetTokenInfo();
            var ret = info.Id > 0 && info.Category == 0;
            if (!ret)
            {
                ctx.Response.StatusCode = (int) HttpStatusCode.Unauthorized;
            }

            return ret;
        }

        public static bool CheckGameUser(this ControllerBase ctx)
        {
            var info = ctx.GetTokenInfo();
            var ret = info.Id > 0 && info.Category == 0;
            if (!ret)
            {
                ctx.Response.StatusCode = (int) HttpStatusCode.Unauthorized;
            }

            return ret;
        }

        public static bool CheckAdminUser(this ControllerBase ctx, out TokenInfo info)
        {
            info = ctx.GetTokenInfo();
            var ret = info.Id > 0 && info.Category >= AdminCategory.System && info.Category <= AdminCategory.Agency;
            if (!ret)
            {
                ctx.Response.StatusCode = (int) HttpStatusCode.Unauthorized;
            }

            return ret;
        }

        public static async Task WriteBytes(this ControllerBase ctx, ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            // 防止中文乱码
            ctx.Response.ContentType = "application/json;charset=utf-8";
            await ctx.Response.Body.WriteAsync(buffer, cancellationToken);
        }

        /// <summary>
        /// 获取真实IP地址,nginx反向代理记得配置X-Real-IP或X-Forwarded-For
        /// </summary>
        public static string GetIp(this ControllerBase ctx)
        {
            string ip;
            if (ctx.Request.Headers.ContainsKey("X-Real-IP"))
            {
                ip = ctx.Request.Headers["X-Real-IP"].ToString();
            }
            else if (ctx.Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                ip = ctx.Request.Headers["X-Forwarded-For"].ToString();
            }
            else
            {
                ip = ctx.Request.HttpContext.Connection.RemoteIpAddress.ToString();
            }

            return ip;
        }
    }
}