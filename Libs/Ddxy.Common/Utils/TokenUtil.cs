using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Ddxy.Common.Jwt;
using Microsoft.IdentityModel.Tokens;

namespace Ddxy.Common.Utils
{
    public static class TokenUtil
    {
        /// <summary>
        /// 生成token
        /// </summary>
        /// <param name="settings">JwtToken配置</param>
        /// <param name="claims">token中需要包含的payload信息</param>
        /// <returns>token字符串</returns>
        public static string GenToken(JwtOptions settings, IEnumerable<Claim> claims)
        {
            var creds = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),
                SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
                DateTime.Now, DateTime.Now.AddMinutes(settings.ExpiresMinutes), creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// websocket从query中获得token后需要解析token拿到参数
        /// </summary>
        public static ClaimsPrincipal ParseToken(JwtOptions settings, string token)
        {
            if (string.IsNullOrWhiteSpace(token)) return null;
            try
            {
                var claims = new JwtSecurityTokenHandler().ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = settings.Issuer,

                    ValidateAudience = true,
                    ValidAudience = settings.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SecretKey)),

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out _);
                return claims;
            }
            catch
            {
                return null;
            }
        }
    }
}