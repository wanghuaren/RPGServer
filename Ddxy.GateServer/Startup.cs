using System;
using System.Text;
using Ddxy.Common.Jwt;
using Ddxy.Common.Orleans;
using Ddxy.GateServer.HostedService;
using Ddxy.GateServer.Network;
using Ddxy.GateServer.Option;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;

namespace Ddxy.GateServer
{
    public class Startup
    {
        private const string CorsName = "_CorsPolicy";

        public IConfiguration Configuration { get; }

        public IHostEnvironment HostEnvironment { get; }

        public Startup(IHostEnvironment hostEnvironment, IConfiguration configuration)
        {
            HostEnvironment = hostEnvironment;
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.Configure<JwtOptions>(Configuration.GetSection("JwtOptions"));
            services.Configure<OrleansOptions>(Configuration.GetSection("OrleansOptions"));
            services.Configure<AppOptions>(Configuration.GetSection("AppOptions"));

            var jwtOptions = new JwtOptions();
            Configuration.Bind("JwtOptions", jwtOptions);

            var appOptions = new AppOptions();
            Configuration.Bind("AppOptions", appOptions);

            // Http Client, 对接第三方的时候通常能用得上
            services.AddHttpClient();
            // 进程缓存
            services.AddMemoryCache();
            // WebSocket
            services.AddWebSocketManager();
            // 后台服务
            services.AddHostedService<AppHostedService>();

            // 将Bearer作为身份认证的默认方案
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
            {
                options.RequireHttpsMetadata = false;
                options.SaveToken = true;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,

                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),

                    // 是否验证过期时间
                    RequireExpirationTime = true,
                    // 是否验证超时  当设置expires和notBefore时有效 同时启用ClockSkew
                    ValidateLifetime = true,
                    // 注意这是缓冲过期时间，总的Token有效时间 = JwtRegisteredClaimNames.Exp + ClockSkew ；
                    ClockSkew = TimeSpan.Zero
                };
            });

            // 并发控制
            services.AddQueuePolicy(options =>
            {
                // 最大并发请求数
                options.MaxConcurrentRequests = appOptions.MaxConcurrentRequests;
                // 请求队列长度限制
                options.RequestQueueLimit = appOptions.RequestQueueLimit;
            });

            // controller配置
            services.AddControllers(options =>
            {
                // 最大验证错误次数, 为了减少验证的成本, 设置很小的数字即可
                options.MaxModelValidationErrors = 2;
            }).AddJsonOptions(options =>
            {
                // 忽略null值, 对于简单类型, 建议使用可空类型, 来减少传输
                options.JsonSerializerOptions.IgnoreNullValues = true;
                // 是否允许json结构最后一个元素还有逗号
                options.JsonSerializerOptions.AllowTrailingCommas = true;
            }).ConfigureApiBehaviorOptions(options =>
            {
                // 禁用[FromForm]批注自动从multipart/form-data中推理
                options.SuppressConsumesConstraintForFormFileParameters = true;
                // 禁用绑定源推理
                options.SuppressInferBindingSourcesForParameters = true;
                // 禁用自动400行为
                // options.SuppressModelStateInvalidFilter = true;
                // 禁止自动创建ProblemDetails实例
                options.SuppressMapClientErrors = true;
            });

            // cros
            if (HostEnvironment.IsDevelopment())
            {
                services.AddCors(options =>
                {
                    options.AddPolicy(CorsName, builder =>
                    {
                        builder.AllowAnyOrigin();
                        builder.AllowAnyMethod();
                        builder.AllowAnyHeader();
                    });
                });
            }
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseConcurrencyLimiter();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            // app.UseHsts();
            // app.UseHttpsRedirection();
            // app.UseStaticFiles();

            app.UseRouting();
            if (env.IsDevelopment())
            {
                app.UseCors(CorsName);
            }

            app.UseAuthentication();
            app.UseAuthorization();

            // Custom middlewares begine
            app.MapWebSocketManager("/ws");
            // Custom middlewares end

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}