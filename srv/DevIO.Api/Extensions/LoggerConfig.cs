
using Elmah.Io.AspNetCore;
using Elmah.Io.Extensions.Logging;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DevIO.Api.Extensions
{
    public static class LoggerConfig
    {
        public static IServiceCollection AddLoggingConfiguration(this IServiceCollection services, IConfiguration configuration)
        {
            //services.AddElmahIo(o =>
            //{
            //    o.ApiKey = "API_KEY";
            //    o.LogId = new Guid(g: "LOG_ID");
            //});

            //services.AddLogging(builder =>
            //{
            //    builder.AddElmahIo(o =>
            //    {
            //        o.ApiKey = "API_KEY";
            //        o.LogId = new Guid(g: "LOG_ID");
            //    });
            //    builder.AddFilter<ElmahIoLoggerProvider>(category: null, LogLevel.Warning);
            //});

            services.AddHealthChecks()
                //AddElmahIoPublisher(apiKey: "Api_key", new Guid(g: "key"))
               .AddCheck(name: "Produtos", new SqlServerHealthCheck(configuration.GetConnectionString(name: "DefaultConnection")))
               .AddSqlServer(configuration.GetConnectionString(name: "DefaultConnection"), name: "BancoSQL");
            services.AddHealthChecksUI();
            return services;
        }

        public static IApplicationBuilder UseLoggingConfiguration(this IApplicationBuilder app)
        {
            // app.UseElmahIo();

            app.UseHealthChecks("/api/hc", new HealthCheckOptions()
            {
                Predicate = _ => true,
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
            });
            app.UseHealthChecksUI(options =>
            {
                options.UIPath = "/api/hc-ui";
            });
            return app;
        }
    }
}
