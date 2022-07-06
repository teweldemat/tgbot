using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.SmartLedger
{
    public class SetupCompanyDialog<T> : FormDialog where T:TgBotDb,new()
    {
        const string FIELD_COMPANY_NAME = "CompanyName";
        public SetupCompanyDialog(ChatId chatId, User from) : base(chatId, from)
        {

        }
        public override string FirstField => FIELD_COMPANY_NAME;
        public static List<FormFieldChoiceItem> GetUserChoices(Func<MisUserProfile, bool> filter = null)
        {
            if (filter == null)
                return new TgBotService<T>().GetAllUserProfiles().Select(
                    x =>
                    new FormFieldChoiceItem(
                        x.UserId,
                        x.FullName
                    )).ToList();
            else
                return new TgBotService<T>().GetAllUserProfiles().Where(filter).Select(
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
            var service = new TgBotService<T>();
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
