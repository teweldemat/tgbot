using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot.Dialogs
{
    public class ContributDialog : BotDialogBase
    {
        const string YES = "ContributionYes";
        const string NO = "ContributionNo";
        const string FIELD_AMOUNT = "ContAmount";
        const string FIELD_NOTE = "ContNote";
        const string FIELD_PRIVACEY = "ContPrivacy";

        public ChatId chatId;
        public User from;
        public Guid frid;
        public String selectedField;
        public string checkOutCode;
        public bool paid = false;
        public bool anon = false;
        public long amount;
        public ContributDialog(ChatId chatId, User from, Guid frid)
        {
            this.chatId = chatId;
            this.from = from;
            this.frid = frid;
        }
        
        public override async Task<DialogResult> HandlePaymentAsync(ITelegramBotClient bot, string checkOutCode, CancellationToken cancelationToken)
        {
            if (paid)
                return DialogResult.Terminated;
            if (checkOutCode == null || this.checkOutCode == null || !checkOutCode.Replace(" ", "").Equals(this.checkOutCode.Replace(" ", "")))
                return DialogResult.Continue;
            var service = new WFTGDB.WFTGDBService();
            var coreService = new WFDB.WFDBService();
            try
            {
                var cid = service.ConfirmContribution(this.from.Id.ToString(), TGBot.FullName(this.from), anon);
                paid = true;
                var c = coreService.GetContribution(cid);
                var fr = coreService.GetFundRaiser(frid);
                try
                {
                    await bot.SendTextMessageAsync(chatId, Program.lm.Thank_you_We_have_received_the_payment);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error trying to send thank you message after payment is made.\n{ex.Message}\n{ex.StackTrace}");
                }
                try
                {
                    var groups = service.GetFRGroups(frid);
                    var html = ContributionListViewer.FormatContribution(coreService, c, fr, "", true);
                    foreach (var g in groups)
                    {   
                        await bot.SendTextMessageAsync(g, html, ParseMode.Html);
                    }
                    var tgUserId = coreService.GetAgentChannelID(WFDB.FundRaisingChannel.CHANNEL_TELEGRAM, fr.AgentID);
                    if(tgUserId!=null)
                    {
                        await bot.SendTextMessageAsync(long.Parse(tgUserId.IdInChannel), html, ParseMode.Html);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error trying to update groups.\n{ex.Message}\n{ex.StackTrace}");
                }
                return DialogResult.Terminated;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error after payment: {ex.Message}");
                return DialogResult.Terminated;
            }
        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            await bot.SendTextMessageAsync(chatId, Program.lm.How_much_do_you_want_to_contribute);
            selectedField = FIELD_AMOUNT; ;
            return DialogResult.Handled;
        }
        async Task ShowPrivacyOptionAsync(ITelegramBotClient bot, Message msg, CancellationToken cancelationToken)
        {
            var notePrompt = new InlineKeyboardMarkup(
                            new[]{new []{
                                                                            InlineKeyboardButton.WithCallbackData(Program.lm.Yes ,YES),
                                                                            InlineKeyboardButton.WithCallbackData(Program.lm.No_hide_my_name,NO),
                                                                        }});

            await bot.SendTextMessageAsync(
                chatId: msg.Chat.Id,
                text: Program.lm.Can_we_show_your_name_in_the_contributers_list,
                replyMarkup: notePrompt
            );
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery q, CancellationToken  cancellationToken)
        {
            var service = new WFTGDB.WFTGDBService();
            switch (selectedField)
            {
                case FIELD_NOTE:
                    switch (q.Data)
                    {
                        case YES:
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Enter_a_small_message_to_inlcude_with_your_contribution);
                            return DialogResult.Handled;
                        case NO:
                            selectedField = FIELD_PRIVACEY;
                            await ShowPrivacyOptionAsync(bot, q.Message,  cancellationToken);
                            return DialogResult.Handled;
                    }
                    break;
                case FIELD_PRIVACEY:
                    switch (q.Data)
                    {
                        case YES:
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Thanks_your_name_will_be_shown_in_the_contribution_list);
                            this.anon = false;
                            break;
                        case NO:
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Alright_we_will_respect_your_privacy__We_will_not_show_your_name);
                            this.anon = true;
                            break;
                    }
                    await WaitForPaymentAsync(bot, q.From);
                    return DialogResult.Handled;

            }
            return DialogResult.Continue;
        }
        private async Task SetContributionNote(ITelegramBotClient bot,Message msg, User u, string note,CancellationToken cancellationToken)
        {
            var service = new WFTGDB.WFTGDBService();
            service.SetContributionNote(u.Id.ToString(), note);
            selectedField = FIELD_PRIVACEY;
            await ShowPrivacyOptionAsync(bot, msg, cancellationToken);
        }
        async Task WaitForPaymentAsync(ITelegramBotClient bot, User u)
        {
            var name = new WFDB.WFDBService().GetFundRaiser(frid).ShortName;
            this.checkOutCode= await WFTGDB.WFTGDBService.WBCheckOutAPI.CheckOut(TGBot.FullName(u), u.Username, $"Contribution to {name}", amount);
            new WFTGDB.WFTGDBService().SetStateData(u.Id.ToString(), data =>
            {
                data.WaitingForPayment = true;
                data.WbcCode = this.checkOutCode;
            });
            WFTGDB.WFTGDBService.ResumePaymentPoll();
            var html = Program.lm.Use_the_following_Code_to_pay_with_WeBirr;
            await bot.SendTextMessageAsync(chatId, html, ParseMode.Html);
            await bot.SendTextMessageAsync(chatId, $"<strong>{checkOutCode}</strong>", ParseMode.Html);
            await bot.SendTextMessageAsync(chatId, $"<a href=\"{Program.GetBotConfig("InstPage").Replace("{wbc_checkout}", checkOutCode)}\">{Program.lm.How_to_pay_with_WeBirr}</a>", ParseMode.Html);
        }

        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message msg, CancellationToken cancelationToken)
        {
            var service = new WFTGDB.WFTGDBService();
            switch (selectedField)
            {
                case FIELD_AMOUNT:
                    var er = TGBot.GetParseError(msg.Text, x=>x<1?Program.lm.Minimum_anount_is_1_Birr:null, out double contAmount);
                    if (er!=null)
                        await bot.SendTextMessageAsync(msg.Chat.Id, er);
                    else
                    {
                        service.SetContributionAmount(msg.From, frid, IntData.toIntMoney(contAmount));
                        var notePrompt = new InlineKeyboardMarkup(
                                new[]{new []{
                                                                            InlineKeyboardButton.WithCallbackData(Program.lm.Yes_please,YES),
                                                                            InlineKeyboardButton.WithCallbackData(Program.lm.No_thanks,NO),
                                                                        }});

                        await bot.SendTextMessageAsync(
                            chatId: msg.Chat.Id,
                            text: Program.lm.Do_you_want_to_include_a_small_message_with_your_contribution,
                            replyMarkup: notePrompt
                        );
                        this.amount =IntData.toIntMoneyRoundDown(contAmount);
                        selectedField = FIELD_NOTE;
                    }
                    return DialogResult.Handled;
                case FIELD_NOTE:
                    await SetContributionNote(bot, msg, msg.From, msg.Text,cancelationToken);
                    return DialogResult.Handled;
            }
            return DialogResult.Continue;
        }
    }
}
