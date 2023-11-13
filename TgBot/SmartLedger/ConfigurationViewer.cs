using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgBot.TgDb;

namespace TgBot.SmartLedger
{
    class ConfigurationViewer : BotDialogBase
    {
        public ChatId chatId;
        public User user;
        SmartLedgerService service;
        TgDbService tgService;
        public override void SetServices(IServiceProvider services)
        {
            this.service = services.GetService<SmartLedgerService>();
            this.tgService = services.GetService<TgDbService>();
        }
        public ConfigurationViewer(SmartLedgerService service, TgDbService tgService,ChatId chatId, User user)
        {
            this.chatId = chatId;
            this.user = user;
            this.service = service;
            this.tgService = tgService;
        }

        private string FormatCurrentConfiguration()
        {
            var config = service.GetRuleData<SimplePaymentFlowConfiguration>();

            // Fetch user profiles for Checker, Approver, and Accountant
            var checkerProfile = !string.IsNullOrEmpty(config?.Checker1) ? service.GetUserProfile(config.Checker1) : null;
            var approverProfile = !string.IsNullOrEmpty(config?.Approver1) ? service.GetUserProfile(config.Approver1) : null;
            var accountantProfile = !string.IsNullOrEmpty(config?.Accountant) ? service.GetUserProfile(config.Accountant) : null;

            string configDisplay = $"Current Configuration:\n" +
                                   $"Checker: {checkerProfile?.FullName ?? "Not Set"}\n" +
                                   $"Approver: {approverProfile?.FullName ?? "Not Set"}\n" +
                                   $"Accountant: {accountantProfile?.FullName ?? "Not Set"}";

            return configDisplay;
        }


        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            string configDisplay = FormatCurrentConfiguration();

            InlineKeyboardMarkup buttons = new InlineKeyboardMarkup(new[]
            {
                InlineKeyboardButton.WithCallbackData("Set Checker", "set_checker"),
                InlineKeyboardButton.WithCallbackData("Set Approver", "set_approver"),
                InlineKeyboardButton.WithCallbackData("Set Accountant", "set_accountant")
            });

            await bot.SendTextMessageAsync(chatId,
                text: configDisplay,
                parseMode: ParseMode.Html,
                replyMarkup: buttons);

            return DialogResult.Handled;
        }

        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancellationToken)
        {
            var userId = callBack.From.Id.ToString();
            var chatId = callBack.Message.Chat.Id;

            switch (callBack.Data)
            {
                case "set_checker":
                    await TGBot.PushDialog(userId, new SetupFlowDialog(service, tgService, chatId, callBack.From, SetupFlowDialog.FIELD_CHECKER_ONE), cancellationToken);
                    break;

                case "set_approver":
                    await TGBot.PushDialog(userId, new SetupFlowDialog(service, tgService, chatId, callBack.From, SetupFlowDialog.FIELD_APPROVE_ONE), cancellationToken);
                    break;

                case "set_accountant":
                    await TGBot.PushDialog(userId, new SetupFlowDialog(service, tgService, chatId, callBack.From, SetupFlowDialog.FIELD_ACCOUNTANT), cancellationToken);
                    break;
            }

            return DialogResult.Handled;
        }


    }
}
