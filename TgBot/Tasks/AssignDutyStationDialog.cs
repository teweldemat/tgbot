using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.SmartLedger;

namespace TgBot.Tasks
{
    public class AssignDutyStationDialog:FormDialog
    {
        const string FIELD_SELECT_USER = "SelectUser";
        const string FIELD_SELECT_DUTY_STATION = "SelectDutyStation";
        const string FIELD_REMOTE = "Remote";
        public List<DutyStation> DutyStation { get; set; }
        public List<MisUserProfile> Users { get; set; }
        public AssignDutyStationDialog():base(null,null)
        {
            var service = new TaskDbService();
            this.DutyStation = service.GetAllDutyStations();
            this.Users = service.GetActiveUserProfiles();
        }
        public AssignDutyStationDialog(ChatId chatId,User user):base(chatId,user)
        {
            var service = new TaskDbService();
            this.DutyStation = service.GetAllDutyStations();
            this.Users = service.GetActiveUserProfiles();
        }

        public override string FirstField => FIELD_SELECT_USER;

        public override FormDialogField GetFieldDef(string key)
        {
            var service = new TaskDbService();
            switch (key)
            {
                case FIELD_SELECT_USER:
                    return new FormDialogField
                    {
                        Prompt = "Select a teammate",
                        FieldType = FieldType.Choices,
                        NextField = this.DutyStation.Count > 1 ? d => Task.FromResult(FIELD_SELECT_DUTY_STATION) : null,
                        Choices = Users.Select(x => new FormFieldChoiceItem(x.UserId, x.FullName)).ToList(),
                        
                    };
                case FIELD_SELECT_DUTY_STATION:
                    return new FormDialogField
                    {
                        Prompt = "Select duty station",
                        FieldType = FieldType.Choices,
                        Choices = service.GetAllDutyStations().Select(x => new FormFieldChoiceItem(x.Id.ToString(), x.Name)).ToList(),
                        ParseFunction = (b, t, c) => Task.FromResult(new ParseResult { Data = Guid.Parse(t) }),
                        NextField=d=>Task.FromResult(FIELD_REMOTE)
                    };
                case FIELD_REMOTE:
                    return new FormDialogField
                    {
                        Prompt = "How will the teammate work",
                        FieldType = FieldType.Choices,
                        Choices = new[] { new FormFieldChoiceItem("true", "Remote"), new("false", "On Site") },
                        ParseFunction = (b, t, c) => Task.FromResult(new ParseResult { Data = bool.Parse(t) })
                    };
                default:
                    return null;
            }            
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var userDS = new UserDutyStation
            {
                DutyStationId=this.DutyStation.Count>1?(Guid)this.FieldData[FIELD_SELECT_DUTY_STATION].Val(): this.DutyStation[0].Id,
                UserId=this.FieldData[FIELD_SELECT_USER].Val() as string
            };
            var schedule = SimpleWorkingDayWeek.CreateDefaultFullTime((bool)this.FieldData[FIELD_REMOTE].Val(), true);
            var service = new TaskDbService();
            var thisUser = service.GetUserProfile(this.from.Id.ToString());
            var ds = this.DutyStation.Where(x => x.Id == userDS.DutyStationId).First();
            var assigned = this.Users.Where(x => x.UserId.Equals(userDS.UserId)).First();
            try
            {
                service.AssignDutyStation(this.from.Id.ToString(), userDS,schedule);
                await bot.SendTextMessageAsync(chatId, "Done.", cancellationToken: cancellationToken);
                return DialogResult.Terminated;
            }
            catch(Exception ex)
            {
                TGBot.LogException("Error trying to assign duty station", ex);
                await bot.SendTextMessageAsync(chatId, "Sorry, we couldn't complete the operation", cancellationToken: cancellationToken);
            }
            try
            {
                await bot.SendTextMessageAsync(long.Parse(userDS.UserId), $"You have been assigned to duty station {ds.Name} by {thisUser.FullName}", cancellationToken: cancellationToken);
            }
            catch(Exception ex)
            {
                TGBot.LogException("Error trying to notifhy the assigned user", ex);
            }
            try
            {
                await TaskBot.NotifyGroups(bot,$"{assigned.Name()} have been assigned to duty station {ds.Name} by {thisUser.Name()}", false,cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error trying to notifhy the assigned user", ex);
            }
            return DialogResult.Terminated;
        }
    }
}
