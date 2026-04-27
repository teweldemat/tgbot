using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SixLabors.ImageSharp;
using System.IO;
using System.Reflection;
using System;
using TgBot.Exchange;
using TgBot.SocialLedger;
using TgBot.TgDb;
using TgBot.WeTicket;
using TgBot.Tasks;
using Npgsql;
using System.Data.Common;

namespace TgBot.SmartLedger
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSmartLedger(this IServiceCollection services, IConfiguration configuration)
        {
            /*services.AddDbContext<WeTicketDb>(options =>
                options.UseNpgsql(configuration.GetConnectionString("TGBot"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddDbContext<TgBotDbContext>(options =>
                options.UseNpgsql(configuration.GetConnectionString("TGBot"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddDbContext<SmartLedgerDb>(options =>
                options.UseNpgsql(configuration.GetConnectionString("TGBot"))
                       .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddDbContext<SocialLedgerDb>(options =>
                options.UseNpgsql(configuration.GetConnectionString("TGBot"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));


            services.AddDbContext<ExchangeDb>(options =>
                options.UseNpgsql(configuration.GetConnectionString("Exchange"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddDbContext<TgBotDb>(options =>
                options.UseNpgsql(configuration.GetConnectionString("TGBot"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
            */
            services.AddDbContext<WeTicketDb>(options =>
                options.UseNpgsql(configuration.GetConnectionString("TGBot")));
            services.AddDbContext<TgBotDb>();
            services.AddDbContext<SocialLedgerDb>(options =>
                options.UseNpgsql(configuration.GetConnectionString("TGBot")));
            services.AddDbContext<SmartLedgerDb>();
            services.AddDbContext<TgBotDbContext>();
            services.AddScoped<DbConnection>(serviceProvider =>
            {
                var connectionString = configuration.GetConnectionString("TGBot");
                return new NpgsqlConnection(connectionString);
            });

            services.AddScoped<TgDbService>();
            services.AddScoped<SmartLedgerService>();
            services.AddScoped<TaskDbService>();
            services.AddScoped<SocialLedgerDbService>();
            services.AddScoped<SocialLedgerDbService>();
            return services;
        }
        public static ServiceProvider CreateScope()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            Console.WriteLine("Loading config from " + dir);
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(dir)
                .AddJsonFile("appsettings.json")
                .AddEnvironmentVariables()
                .Build();
            var services = new ServiceCollection();
            services.AddSmartLedger(configuration);
            return services.BuildServiceProvider();
        }
        public static T GetServiceAssert<T>(this IServiceProvider services) where T : class
        {
            var ret = services.GetService<T>();
            if (ret == null)
                throw new InvalidOperationException($"Service {typeof(T)} couldn't be loaded");
            return ret;
        }
    }

}
