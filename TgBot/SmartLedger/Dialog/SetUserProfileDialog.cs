using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.SmartLedger.Dialog
{
    public class SetUserProfileDialog<T, TS> : FormDialog where T : TgBotDb where TS : TgBotService<T>
    {
        const string FIELD_SPECIFY_NAME = "SpecifyName";
        const string FIELD_NAME = "Name";
        const string FIELD_SHORT_NAME = "ShortName";
        const string FIELD_GENDER = "Gender";
        
        TS service;
        bool UpdateMode => service.GetUserProfile(from.Id.ToString()) != null;
        public SetUserProfileDialog(TS service, ChatId chatId, User from) : base(chatId, from)
        {
            this.service = service;
        }
        public override void SetServices(IServiceProvider services)
        {
            service = services.GetService<TS>();
            if (service == null)
                throw new InvalidOperationException($"Sevice {typeof(TgBotService<T>)} could not be loaded");
        }
        [JsonIgnore]
        public override string FirstField => UpdateMode ? FIELD_NAME : FIELD_SPECIFY_NAME;

        public override FormDialogField GetFieldDef(string key)
        {

            switch (key)
            {
                case FIELD_SPECIFY_NAME:
                    return new FormDialogField
                    {
                        Prompt = $"Your name on telegram is {TGBot.FullName(from)}, is this your formal name different?",
                        FieldType = FieldType.Choices,
                        Choices = new[] { new FormFieldChoiceItem("YES", "Yes, it is different"), new("NO", "No, it is the same") },
                        NextField = d => Task.FromResult("YES".Equals(d[FIELD_SPECIFY_NAME].Val()) ? FIELD_NAME : FIELD_SHORT_NAME)
                    };
                case FIELD_NAME:
                    return new FormDialogField
                    {
                        Prompt = $"Enter your full formal name. Please spell carefully",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_SHORT_NAME)
                    };
                case FIELD_SHORT_NAME:
                    return new FormDialogField
                    {
                        Prompt = $"Enter a short form of your name",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_GENDER)
                    };
                case FIELD_GENDER:
                    return new FormDialogField
                    {
                        Prompt = $"What is your gender?",
                        FieldType = FieldType.Choices,
                        Choices = new[] { new FormFieldChoiceItem(MisUserProfile.GenderType.Male.ToString(),"Male")
                            ,new FormFieldChoiceItem(MisUserProfile.GenderType.Female.ToString(),"Female")}
                    };

            }
            return null;
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            string name;
            if (FieldData.ContainsKey(FIELD_SPECIFY_NAME) && !"YES".Equals(FieldData[FIELD_SPECIFY_NAME].Val()))
                name = TGBot.FullName(from);
            else
                name = (string)FieldData[FIELD_NAME].Val();
            service.SetPaymentProfile(new MisUserProfile
            {
                UserId = from.Id.ToString(),
                FullName = name,
                ShortName = (string)FieldData[FIELD_SHORT_NAME].Val(),
                Gender = Enum.Parse<MisUserProfile.GenderType>((string)FieldData[FIELD_GENDER].Val())
            });
            await bot.SendTextMessageAsync(chatId, $"Profile saved", cancellationToken: cancellationToken);
            return DialogResult.Terminated;
        }
    }
}
