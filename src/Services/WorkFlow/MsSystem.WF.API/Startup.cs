﻿using Autofac;
using Autofac.Extensions.DependencyInjection;
using AutoMapper;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MsSystem.WF.API.Filters;
using MsSystem.WF.IRepository;
using MsSystem.WF.IService;
using MsSystem.WF.Repository;
using MsSystem.WF.Service;
using NLog.Extensions.Logging;
using NLog.Web;
using System;

namespace MsSystem.WF.API
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public IServiceProvider ConfigureServices(IServiceCollection services)
        {
            services.Configure<AppSettings>(Configuration);
            IOptions<AppSettings> appSettings = services.BuildServiceProvider().GetService<IOptions<AppSettings>>();

            services.AddServiceRegistration();
            services.AddResponseCompression();

            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opt =>
            {
                opt.Authority = appSettings.Value.Identity.Authority;
                opt.Audience = appSettings.Value.Identity.Audience;
                opt.RequireHttpsMetadata = false;
                opt.SaveToken = true;
            });


            services.AddScoped<IConfigService, ConfigService>();

            services.AddScoped<IWFDbContext, WFDbContext>();
            services.AddScoped<IWFDatabaseFixture, WFDatabaseFixture>();

            services.AddScoped<IWorkFlowService, WorkFlowService>();
            services.AddScoped<IFormService, FormService>();
            services.AddScoped<IWorkFlowInstanceService, WorkFlowInstanceService>();
            services.AddScoped<IWorkflowCategoryService, WorkflowCategoryService>();


            services.AddAutoMapper();

            services.AddMvc(option => option.Filters.Add(typeof(HttpGlobalExceptionFilter)))
                .AddJsonOptions(op => op.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver());//修改默认首字母为大写

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy",
                    builder => builder
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .SetIsOriginAllowed((host) => true)
                    .AllowCredentials());
            });

            services.AddCap(x =>
            {
                x.UseRabbitMQ(opt =>
                {
                    opt.HostName = appSettings.Value.RabbitMQ.HostName;
                    opt.UserName = appSettings.Value.RabbitMQ.UserName;
                    opt.Password = appSettings.Value.RabbitMQ.Password;
                });
                x.UseMySql(appSettings.Value.MySQL.Connection);
            });

            var container = new ContainerBuilder();
            container.Populate(services);
            return new AutofacServiceProvider(container.Build());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            loggerFactory.AddNLog();
            if (env.IsDevelopment())
            {
                env.ConfigureNLog("NLog.Development.config");
            }
            else
            {
                env.ConfigureNLog("NLog.config");
            }

            app.UseCors("CorsPolicy");

            app.UseResponseCompression();
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseAuthentication();
            app.UseMvc();
            app.UseServiceRegistration(new ServiceCheckOptions
            {
                HealthCheckUrl = "/api/HealthCheck/ping"
            });
        }

    }
}
