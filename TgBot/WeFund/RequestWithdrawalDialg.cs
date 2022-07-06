using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot.Dialogs
{
    public class RequestWithdrawalDialg:BotDialogBase
    {
        const string FIELD_AMOUNT = "WithdrawalAmout";
        const string FIELD_NOTE = "WithdrawalNote";
        const string FIELD_BANK = "WithdrawalBank";
        const string FIELD_ACCOUNTNAME = "WithdrawalAccountName";
        const string FIELD_ACCOUNTCODE = "WithdrawalAccountCode";

        const string YES = "WithdrawalYes";
        const string NO = "WithdrawalNo";
        private const string BANK_PREFIX = "BANK_";
        public Guid frid;
        public User from;
        public ChatId chatId;

        public string fieldName;
        public long amount;
        public string note;
        public int bankId;
        public string AccountName;
        public string AccountCode;
        public RequestWithdrawalDialg(Guid frid, User from, ChatId chatId)
        {
            this.frid = frid;
            this.from = from;
            this.chatId = chatId;
        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            await bot.SendTextMessageAsync(chatId, "How much money do you want to withdraw?\nCommission and applicable fees will be deducted.");
            fieldName = FIELD_AMOUNT;
            return DialogResult.Handled;
        }

        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message msg, CancellationToken cancelationToken)
        {
            var service = new WFTGDB.WFTGDBService();
            var corService = new WFDB.WFDBService();
            switch (fieldName)
            {
                case FIELD_AMOUNT:
                    
                    double amount_double;
                    if (!double.TryParse(msg.Text, out amount_double)
                                    || amount_double < 1
                                    )
                    {
                        await bot.SendTextMessageAsync(msg.Chat.Id, "Please enter a valid amount that is at least 1 Birr");
                        return DialogResult.Handled;
                    }
                    amount = IntData.toIntMoneyRoundDown(amount_double);
                    await bot.SendTextMessageAsync(
                        chatId: msg.Chat.Id,
                        text: "Enter a message for Awatu admin"
                    );

                    fieldName = FIELD_NOTE;
                    return DialogResult.Handled;
                case FIELD_NOTE:
                    {//scopping
                        if (string.IsNullOrEmpty(msg.Text) && msg.Text.Length < 3)
                        {
                            await bot.SendTextMessageAsync(msg.Chat.Id, "Please enter valid note");
                            return DialogResult.Handled;
                        }
                        this.note = msg.Text;
                        var banks = new WFDB.WFDBService().GetAllBanks();
                        if (banks.Count == 1)
                        {
                            await bot.SendTextMessageAsync(msg.Chat.Id, $"Please your {banks[0].Name} account code. Please enter carefully");
                            this.bankId = banks[0].Id;
                            fieldName = FIELD_ACCOUNTCODE;
                        }
                        else
                        {
                            var buttons = new List<InlineKeyboardButton[]>();
                            foreach (var bank in banks)
                            {
                                buttons.Add(new[] { InlineKeyboardButton.WithCallbackData(bank.Name, BANK_PREFIX + bank.Id) });
                            }
                            var replyKeyboardMarkup = new InlineKeyboardMarkup(buttons.ToArray());
                            await bot.SendTextMessageAsync(
                                chatId: chatId,
                                text: "Select your bank",
                                replyMarkup: replyKeyboardMarkup
                            );
                            fieldName = FIELD_BANK;
                        }
                    }
                    return DialogResult.Handled;
                case FIELD_ACCOUNTCODE:
                    if (string.IsNullOrEmpty(msg.Text) && msg.Text.Length < 3)
                    {
                        await bot.SendTextMessageAsync(msg.Chat.Id, "Please enter valid account code");
                        return DialogResult.Handled;
                    }
                    this.AccountCode = msg.Text;
                    await bot.SendTextMessageAsync(msg.Chat.Id, "Please enter account name. Please spell carefully");
                    this.fieldName = FIELD_ACCOUNTNAME;
                    return DialogResult.Handled;
                case FIELD_ACCOUNTNAME:
                    {//scopping
                        if (string.IsNullOrEmpty(msg.Text) && msg.Text.Length < 3)
                        {
                            await bot.SendTextMessageAsync(msg.Chat.Id, "Please enter valid account name. Please enter carefully");
                            return DialogResult.Handled;
                        }
                        this.AccountName = msg.Text;
                        
                        string html = FormatWithdrawlDetail(new WFDB.WithDrawalRequest
                        {
                            RequestNote=this.note,
                            Amount=this.amount,
                            BankId=this.bankId,
                            AccountName=this.AccountName,
                            AccountNo=this.AccountCode
                        },
                        corService.GetFundRaiser(frid)
                        );
                        html += $"<pre>\n</pre>Do you want to send the request?";
                        var replyKeyboardMarkup = new InlineKeyboardMarkup(
                            new[] {
                            new [] { InlineKeyboardButton.WithCallbackData("Yes, send request",YES) }
                            ,new [] { InlineKeyboardButton.WithCallbackData("Cancel",NO) }
                            });
                        await bot.SendTextMessageAsync(
                            chatId: chatId,
                            text: html,
                            parseMode: ParseMode.Html,
                            replyMarkup: replyKeyboardMarkup
                            );
                        return DialogResult.Handled;
                    }
            }
            return DialogResult.Continue;
        }

        public static string FormatWithdrawlDetail(WFDB.WithDrawalRequest request,WFDB.FundRaiser fr)
        {
            var theBank = new WFDB.WFDBService().GetWithDrawalBank(request.BankId);
            var html = $"<strong>Amount:</strong> {IntData.toString(request.Amount)}";
            html += $"<pre>\n</pre><strong>Message:</strong> {request.RequestNote}";
            html += $"<pre>\n</pre><strong>Bank:</strong> {theBank.Name}";
            html += $"<pre>\n</pre><strong>Account Code:</strong> {request.AccountNo}";
            html += $"<pre>\n</pre><strong>Account Name:</strong> {request.AccountName}";
            long comission;
            html += $"<pre>\n</pre><strong>Commission ({fr.CommssionInPrecent}%):</strong> {IntData.toString(comission = fr.CommssionOf(request.Amount))} Birr";
            html += $"<pre>\n</pre><strong>Registration Fee:</strong> {IntData.toString(fr.RegistrationFee)} Birr";
            html += $"<pre>\n</pre><strong>Net Payable:</strong> {IntData.toString(request.Amount-fr.RegistrationFee-comission)} Birr";
            return html;
        }

        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery q, CancellationToken cancelationToken)
        {
            var service = new WFTGDB.WFTGDBService();
            var wf = new WFDB.WFDBService();
            if(q.Data.StartsWith(BANK_PREFIX))
            {
                bankId = int.Parse(q.Data.Substring(BANK_PREFIX.Length));
                var bank=wf.GetWithDrawalBank(bankId);
                await bot.SendTextMessageAsync(q.Message.Chat.Id, $"Enter your {bank.Name} bank account. Please enter carefull");
                return DialogResult.Handled;
            }
            switch(q.Data)
            {
                case YES:
                    try
                    {
                        var agent = wf.GetAgentByChannel(WFDB.FundRaisingChannel.CHANNEL_TELEGRAM, q.From.Id.ToString());
                        wf.RequestWithdrawal(frid, agent.Id, amount, note,bankId, AccountName, AccountCode);
                        await bot.SendTextMessageAsync(q.Message.Chat.Id, $"We are processing your request. We will contact you once the payment is transfered to your account.");
                    }
                    catch(UserFriendlyError)
                    {
                        throw;
                    }
                    catch(Exception ex)
                    {
                        await bot.SendTextMessageAsync(q.Message.Chat.Id, $"Sorry but we couldn't process your request. Contact admin");
                        Console.WriteLine($"Error registering request. {ex.Message}");
                    }
                    return DialogResult.Terminated;
                case NO:
                    await bot.SendTextMessageAsync(q.Message.Chat.Id, $"Alright, canceled");
                    return DialogResult.Terminated;
            }
            return DialogResult.Continue;
        }
    }

}
