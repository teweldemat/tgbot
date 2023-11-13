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

namespace TgBot.SmartLedger
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddSmartLedger(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<WeTicketDb>(options =>
                options.UseSqlServer(configuration.GetConnectionString("TGBot"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddDbContext<TgBotDbContext>(options =>
                options.UseSqlServer(configuration.GetConnectionString("TGBot"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddDbContext<SmartLedgerDb>(options =>
                options.UseSqlServer(configuration.GetConnectionString("TGBot"))
                       .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddDbContext<SocialLedgerDb>(options =>
                options.UseSqlServer(configuration.GetConnectionString("TGBot"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));


            services.AddDbContext<ExchangeDb>(options =>
                options.UseNpgsql(configuration.GetConnectionString("Exchange"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

            services.AddDbContext<TgBotDb>(options =>
                options.UseSqlServer(configuration.GetConnectionString("TGBot"))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));

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
    }

}
