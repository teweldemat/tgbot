using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.SmartLedger;
using TgBot.TgDb;

namespace TgBot.Tasks
{
    public class SetTaskFlowDialog : FormDialog
    {
        const string FIELD_HR_OFFICER = "HROfficer";
        const string FIELD_HR_MANGER = "HRManager";
        const string FIELD_HR_BOOKKEEPER = "HRBookKeepr";
        TaskDbService service;
        TgDbService tgService;
        public SetTaskFlowDialog(TaskDbService service, TgDbService tgService, ChatId chatId, User from) : base(chatId, from)
        {
            this.service = service;
            this.service = service;
        }
        public override void SetServices(IServiceProvider services)
        {
            this.service = services.GetService<TaskDbService>();
        }
        public override string FirstField => FIELD_HR_OFFICER;

        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_HR_OFFICER:
                    return new FormDialogField
                    {
                        Prompt = "Whom would you like to check HR requests?",
                        FieldType = FieldType.Choices,
                        NextField = d => Task.FromResult(FIELD_HR_MANGER),
                        Choices = SetupCompanyDialog<SmartLedgerDb>.GetUserChoices(service)
                    };
                case FIELD_HR_MANGER:
                    return new FormDialogField
                    {
                        Prompt = "Whom do you want to approve HR requests?",
                        FieldType = FieldType.Choices,
                        NextField = d => Task.FromResult(FIELD_HR_BOOKKEEPER),
                        Choices = SetupCompanyDialog<SmartLedgerDb>.GetUserChoices(service)
                    };
                case FIELD_HR_BOOKKEEPER:
                    return new FormDialogField
                    {
                        Prompt = "Whom do the book keeping for the HR requests?",
                        FieldType = FieldType.Choices,
                        NextField = null,
                        Choices = SetupCompanyDialog<SmartLedgerDb>.GetUserChoices(service)
                    };
            }
            return null;
        }


        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            try
            {
                var e = service.GetEntity();
                if (e == null)
                {
                    await bot.SendTextMessageAsync(chatId, "Sorry the setup couldn't be applied because company is not setup");
                    return DialogResult.Terminated;
                }
                var oldConfig = service.GetTaskConfiguration<TaskConfigurationData>();

                var config = new TaskConfigurationData
                {
                    HROfficer = (string)FieldData[FIELD_HR_OFFICER].Val(),
                    HRManager = (string)FieldData[FIELD_HR_MANGER].Val(),
                    HRBookKeeper = (string)FieldData[FIELD_HR_BOOKKEEPER].Val(),
                };
                //notify the users assinged in the workflow
                if (config.HROfficer != null && (oldConfig == null || !config.HROfficer.Equals(oldConfig.HROfficer)))
                    await NotifyCheckerAssignment(bot, service, tgService, config.HROfficer, cancellationToken);


                if (config.HRManager != null && (oldConfig == null || !config.HRManager.Equals(oldConfig.HRManager)))
                    await NotifyApproverAssignment(bot, service, tgService, config.HRManager, cancellationToken);


                if (config.HRBookKeeper != null && (oldConfig == null || !config.HRBookKeeper.Equals(oldConfig.HRBookKeeper)))
                    await NotifyBookeperAssignment(bot, service, tgService, config.HRBookKeeper, cancellationToken);

                service.SetTaskConfiguration(from.Id.ToString(), config);
                var cashAccount = service.AccountsCount();
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error trying to register a setup", ex);
                await bot.SendTextMessageAsync(chatId, "Sorry the setup couldn't be applied because internal error. Please try again later");
            }
            return DialogResult.Terminated;
        }
        public static async Task NotifyCheckerAssignment(ITelegramBotClient bot, SmartLedgerService service, TgDbService tgService, String checker
            , CancellationToken cancellationToken)
        {
            var text = "You have been assigned as HR request checker.";
            await bot.SendTextMessageAsync(chatId: checker, text: text
                                    );
            var profile = service.GetUserProfile(checker);
            var textGroup = $"{profile.FullName} have been assigned as HR request checker.";
            await SmartLedgerBot.NotifyGroups(bot, tgService, textGroup, false, cancellationToken);
        }
        public static async Task NotifyBookeperAssignment(ITelegramBotClient bot, SmartLedgerService service, TgDbService tgService, String bookeeper,
            CancellationToken cancellationToken)
        {
            await bot.SendTextMessageAsync(chatId: bookeeper, text: "You have been assigned as a HR bookkeeper.");
            var profile = service.GetUserProfile(bookeeper);
            var textGroup = $"{profile.FullName} have been assigned as HRT bookkeeper.";
            await SmartLedgerBot.NotifyGroups(bot, tgService, textGroup, false, cancellationToken);
        }
        public static async Task NotifyApproverAssignment(ITelegramBotClient bot, SmartLedgerService service,TgDbService tgService, String approver,
            CancellationToken cancellationToken)
        {
            await bot.SendTextMessageAsync(chatId: approver, text: "You are assigned as HR approver.");
            var profile = service.GetUserProfile(approver);
            var textGroup = $"{profile.FullName} have been assigned as HR approver.";
            await SmartLedgerBot.NotifyGroups(bot,tgService, textGroup, false, cancellationToken);
        }
        public static async Task NotifyDutyStationCreation(ITelegramBotClient bot,TgDbService tgService, DutyStation station,
            CancellationToken cancellationToken)
        {
            var textGroup = $"Duty station {station.Name} is created and its code is: {station.Code}";
            await SmartLedgerBot.NotifyGroups(bot, tgService,textGroup, false, cancellationToken);
        }
    }
}
