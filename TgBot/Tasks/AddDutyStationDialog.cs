using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.Tasks
{
    public class AddDutyStationDialog: FormDialog
    {        
        const String FIELD_STATION_NAME = "StationName";
        const String FIELD_STATION_ADDRESS = "StationAddress";

        public override string FirstField => FIELD_STATION_NAME;

        public override FormDialogField GetFieldDef(string key)
        {
            switch(key)
            {
                case FIELD_STATION_NAME:
                    return new FormDialogField
                    {
                        Prompt = "Enter the name of the duty station",
                        FieldType = FieldType.Text,
                        NextField =d=> Task.FromResult(FIELD_STATION_ADDRESS),
                        ParseFunction=(bot,t,c)=>
                        {
                            if (String.IsNullOrWhiteSpace(t))
                                return Task.FromResult( new ParseResult { Error = "Enter valid name" });
                            return Task.FromResult(new ParseResult { Data = t });
                        }
                    };                
                case FIELD_STATION_ADDRESS:
                    return new FormDialogField
                    {
                        Prompt = "Enter the full address of the duty station",
                        FieldType = FieldType.Text,
                        NextField = null,
                        ParseFunction = (bot, t, c) =>
                        {
                            if (String.IsNullOrWhiteSpace(t) || t.Length<3)
                                return Task.FromResult(new ParseResult { Error = "Enter valid address" });
                            return Task.FromResult(new ParseResult { Data = t });
                        }
                    };
            }
            return null;
        }
        public AddDutyStationDialog(ChatId chatId, User from) : base(chatId, from)
        {

        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var service = new TaskDbService();
            var e = service.GetEntity();
            var userId = from.Id.ToString();
            if (!e.Owner.Equals(userId))
            {
                await bot.SendTextMessageAsync(chatId, "Sorry, only owner can add duty stations");
                return DialogResult.Terminated;
            }
            DutyStation ds = null;
            try
            {
                ds = new DutyStation
                {
                    Name = (string)FieldData[FIELD_STATION_NAME].Val(),
                    Address = (String)FieldData[FIELD_STATION_ADDRESS].Val(),
                };
                ds.Code= service.AddDutyStation(userId, ds);
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error trying to create duty station", ex);
                await bot.SendTextMessageAsync(chatId, "The duty station couldn't be added becaues of internal error. Try again latter");
                return DialogResult.Terminated;
            }
            await bot.SendTextMessageAsync(chatId, $"Duty station {ds.Name} with code {ds.Code} created");
            await SetTaskFlowDialog.NotifyDutyStationCreation(bot, ds, cancellationToken);
            return DialogResult.Terminated;

        }
    }
}
