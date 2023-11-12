using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;

namespace TgBot
{
    class Program
    {
        public static LM lm = new LM();
        static IConfigurationRoot conf;
        public static long MaxContribution {
            get
            {
                return IntData.toIntMoney(10000.00);
            }
        }
        static void Main(string[] args)
        {
            LoadAppConfiguration();
            TGBot.StartAsync();
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel()
                    .UseStartup<Startup>();                    
                });

        private static void LoadAppConfiguration()
        {
            conf = new ConfigurationBuilder()
                            .AddJsonFile("appsettings.json").Build();
        }

        public static String GetConnectionString(String name)
        {
            String con;
            try
            {
                con = conf.GetSection("ConnectionStrings").GetSection(name).Value;
            }
            catch
            {
                con = null;
            }
            if (String.IsNullOrEmpty(con))
            {
                //con = "server=DESKTOP-0N0TOMS\\MSSQLSERVER2017; database=WeFund;Trusted_Connection=True;";
                //con = "server=api.webirr.com; database=WeFund;Trusted_Connection=False;User ID=WeFundUser;Password=YaBerYtmjuadQwEK";
                //con = "server=api.webirr.com; database=WeTT;Trusted_Connection=False;User ID=WeFundUser;Password=YaBerYtmjuadQwEK";
                //con = "server=api.webirr.com,4534;database=SocialLedger;Trusted_Connection=false;User Id=SocialLedgerUser;Password=jtr48mSYFR9DKb2Edd";

                //con = "server=localhost\\MSSQLSERVER02; database=IntapsPay;Trusted_Connection=True;";
                con = "server=64.225.3.202;database=IntapsPay;Trusted_Connection=false;User Id=IntapsPayment;Password=KkVX9YZJ";
                //con = "server=api.webirr.com,4534; database=WeBirrTask;Trusted_Connection=False;User ID=WeBirrTask;Password=j2R-mBy?LEwV^?UX";  //webirr task
                
                //con = "User ID=postgres;Password=admin;Server=localhost;Port=5432;Database=Exchange;Integrated Security=false;Pooling=true;";
            }
            return con;
        }
        public static String GetWBCConf(String name)
        {
            return conf.GetSection("WeBirrCheckOut").GetSection(name).Value;
        }
        public static String GeneralConfiguration(String name)
        {
            return conf.GetSection("General").GetSection(name).Value;
        }
        public static String GeneralConfiguration(String secion,String name)
        {
            return conf.GetSection(secion).GetSection(name).Value;
        }
        internal static string GetBotConfig(string name)
        {
            return conf.GetSection("Bot").GetSection(name).Value;
        }

        internal static void LogException(string msg, Exception ex)
        {
            TGBot.LogException(msg, ex);
        }
        public static object GetExceptionData(Exception ex)
        {
            var errors = new List<Object>();
            while (ex != null)
            {
                errors.Add(new
                {
                    message = ex.Message,
                    type = ex.GetType().ToString(),
                    stackTrace = ex.StackTrace
                });
                ex = ex.InnerException;
            }
            return errors;
        }
    }
}
