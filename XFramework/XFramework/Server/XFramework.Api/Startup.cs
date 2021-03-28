using System;
using System.Reflection;
using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using XFramework.Api.Extensions;
using XFramework.Core.DataAccess;
using XFramework.Core.DataAccess.Commands.Entity.User;
using XFramework.Core.DataAccess.Commands.Handlers;
using XFramework.Core.DataAccess.Commands.Handlers.User;
using XFramework.Core.Interfaces;
using XFramework.Core.PipelineBehaviors;
using XFramework.Domain.DataTransferObjects;

namespace XFramework.Api
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
            Env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            MyAllowSpecificOrigins = "_myAllowSpecificOrigins";
        }
        readonly string MyAllowSpecificOrigins;
        public readonly string Env;
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        
        public virtual void ConfigureServices(IServiceCollection services)
        {

            services.AddDbContext<XFrameworkContext>(options => options.UseNpgsql(Configuration.GetConnectionString("DatabaseConnection")));
            services.AddScoped<IDataLayer, DataLayer>();
            services.AddAutoMapper(typeof(Startup));
            services.AddAutoMapper(typeof(DataLayer));
            services.AddSwaggerGen();
            services.AddMediatR(typeof(CommandBaseHandler).GetTypeInfo().Assembly);
            services.AddValidatorsFromAssembly(typeof(CommandBaseHandler).GetTypeInfo().Assembly);
            services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

            if (Env == "Development")
            {
                services.AddCors(options =>
                {
                    options.AddPolicy(MyAllowSpecificOrigins,
                    builder =>
                    {
                        builder.WithOrigins("https://localhost/", "http://localhost/");
                    });
                });
            }
            else if (Env == "Production")
            {
                services.AddCors(options =>
                {
                    options.AddPolicy(MyAllowSpecificOrigins,
                    builder =>
                    {
                        builder.WithOrigins("https://localhost/", "http://localhost/");
                    });
                });
            }
          
          
            services.AddDistributedMemoryCache();
            services.AddSession(options => {
                options.IdleTimeout = TimeSpan.FromMinutes(30);//You can set Time   
                options.Cookie.HttpOnly = true;
                // Make the session cookie essential
                options.Cookie.IsEssential = true;
            });


            services.AddMvc(option => option.EnableEndpointRouting = false);
            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                .ConfigureApiBehaviorOptions(options =>
                {
                    options.SuppressModelStateInvalidFilter = true;
                });
            services.AddControllers().AddNewtonsoftJson(options => options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public virtual void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseCors(options => options.AllowAnyOrigin().AllowAnyHeader());

            app.UseSession();

            //app.UseHttpsRedirection();
            ApplicationBuilderExtension.UseFluentValidationExceptionHandler(app);

            app.UseRouting();

            app.UseAuthorization();

            app.UseMvc();

            app.UseSwagger();
            
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{ Assembly.GetEntryAssembly().GetName().Name.Split(".")[0] } API V {Assembly.GetEntryAssembly().GetName().Version.ToString()}");
            });

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
