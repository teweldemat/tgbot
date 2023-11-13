using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.SmartLedger
{
    public class SetupCompanyDialog<T> : FormDialog where T:TgBotDb
    {
        const string FIELD_COMPANY_NAME = "CompanyName";
        TgBotService<T> service;
        public SetupCompanyDialog(TgBotService<T>  service,ChatId chatId, User from) : base(chatId, from)
        {
            this.service = service;
        }
         public override void SetServices(IServiceProvider services)
        {
            this.service = services.GetService<TgBotService<T>>();
        }
        public override string FirstField => FIELD_COMPANY_NAME;
        public static List<FormFieldChoiceItem> GetUserChoices(TgBotService<T> service,Func<MisUserProfile, bool> filter = null)
        {
            if (filter == null)
                return service.GetAllUserProfiles().Select(
                    x =>
                    new FormFieldChoiceItem(
                        x.UserId,
                        x.FullName
                    )).ToList();
            else
                return service.GetAllUserProfiles().Where(filter).Select(
                    x =>
                    new FormFieldChoiceItem(
                        x.UserId,
                        x.FullName
                    )).ToList();
        }
        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_COMPANY_NAME:
                    return new FormDialogField
                    {
                        Prompt = "What is the name of your organization?",
                        FieldType = FieldType.Text,
                        NextField = null,
                    };
            }
            return null;
        }


        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            try
            {
                var e = service.GetEntity();
                if (e != null)
                {
                    await bot.SendTextMessageAsync(chatId, "Sorry the setup couldn't be applied because there is an existing setup");
                    return DialogResult.Terminated;
                }
                service.CreateEntity(from.Id.ToString(), (string)FieldData[FIELD_COMPANY_NAME].Val(),
                    null, null);
                await bot.SendTextMessageAsync( chatId,"Congradulations! Your company is registered."
                    +$"\nNow send the telegram bot @{TGBot.meName} to relevant users"
                    , cancellationToken:cancellationToken);
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error trying to register a setup", ex);
                await bot.SendTextMessageAsync(chatId, "Sorry the setup couldn't be applied because internal error. Please try again later");
            }
            return DialogResult.Terminated;
        }
    }
}
