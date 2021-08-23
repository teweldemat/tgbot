using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TgBot.WFDB;

namespace TgBot.Dialogs
{
    public class ProcessWithdrawalRequest:BotDialogBase
    {
        const string FIELD_TRANSFER_REFERENCE = "TransferRef";
        const string FIELD_REF = "Note";
        const string FIELD_REJECTION_NOTE = "RejectionNote";
        const string FIELD_PIC = "Picture";

        const string ACCEPT = "TransferAccept";
        const string REJECT = "TransferReject;";
        const string SAVE = "TransferSave;";
        const string CANCEL = "TransferCancel";
        public Guid frid;
        public Guid requstId;
        public User from;
        public ChatId chatId;
        public String selectedField;
        public string tranRef;
        public string tranMime;
        public byte[] tranPic;
        public string fileId;
        private string rejectionNote;

        public ProcessWithdrawalRequest(Guid frid, User from, ChatId chatId)
        {
            this.frid = frid;
            this.from = from;
            this.chatId = chatId;
        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            this.requstId = new WFDB.WFDBService().GetPendingWithdrawalRequest(frid).Id;

            var core = new WFDB.WFDBService();
            var req = core.GetPendingWithdrawalRequest(frid);
            if (req == null)
            {
                await bot.SendTextMessageAsync(chatId, "Sorry there is no pending withdrawal request");
                return DialogResult.Terminated;
            }
            var buttons = new InlineKeyboardButton[][]{
                new[] {InlineKeyboardButton.WithCallbackData("Accept and enter deails",ACCEPT)}
                ,new[] { InlineKeyboardButton.WithCallbackData("Reject", REJECT) }
            };
            var fr = core.GetFundRaiser(frid);
            var html = RequestWithdrawalDialg.FormatWithdrawlDetail(req,fr);
            await bot.SendTextMessageAsync(
                chatId:chatId,
                text:html,
                parseMode:ParseMode.Html,
                replyMarkup:new InlineKeyboardMarkup(buttons)
                );
            return DialogResult.Handled;
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancelationToken)
        {
            var core = new WFDB.WFDBService();
            switch (callBack.Data)
            {
                case ACCEPT:
                    await bot.SendTextMessageAsync(chatId,"Enter bank tranaction reference");
                    selectedField = FIELD_REF;
                    return DialogResult.Handled;
                case REJECT:
                    await bot.SendTextMessageAsync(chatId, "Enter a reason for rejection fundraiser owner");
                    selectedField = FIELD_REJECTION_NOTE;
                    return DialogResult.Handled;
                case CANCEL:
                    await bot.SendTextMessageAsync(chatId, "Alright, canceled");
                    return DialogResult.Terminated;
                case SAVE:
                    try
                    {
                        var fr = core.GetFundRaiser(frid);
                        var agent = core.GetAgentByChannel(WFDB.FundRaisingChannel.CHANNEL_TELEGRAM, from.Id.ToString());
                        core.ApproveWithdrawal(frid,
                            agent.Id
                            , this.tranRef
                            , this.tranPic
                            , this.tranMime
                            , PictureExternalStorageType.Telegram
                            , this.fileId
                            );
                        var channel = core.GetAgentChannelID(WFDB.FundRaisingChannel.CHANNEL_TELEGRAM, fr.AgentID);
                        if (channel != null)
                        {
                            try
                            {
                                var cid = new ChatId(long.Parse(channel.IdInChannel));
                                await bot.SendPhotoAsync(cid, new Telegram.Bot.Types.InputFiles.InputOnlineFile(this.fileId));
                                await bot.SendTextMessageAsync(cid,
                                    $"Your payment has been transfered\nTransfer ref: {this.tranRef}"
                                    );

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Couldn't send payment acceptance to owner: {ex.Message}");
                            }
                        }
                        await bot.SendTextMessageAsync(chatId, "Transfer succesfully saved");
                        return DialogResult.Terminated;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Couldn't send payment acceptance to owner: {ex.Message}\n{ex.StackTrace}");
                        return DialogResult.Continue;
                    }
            }
            return DialogResult.Handled;
        }
        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message msg, CancellationToken cancelationToken)
        {
            var core = new WFDB.WFDBService();
            switch(selectedField)
            {
                case FIELD_REJECTION_NOTE:
                    var fr = core.GetFundRaiser(frid);
                    this.rejectionNote = msg.Text;                    
                    core.RejectWithdrawl(frid, core.GetAgentByChannel(WFDB.FundRaisingChannel.CHANNEL_TELEGRAM, from.Id.ToString()).Id, this.rejectionNote);
                    var channel = core.GetAgentChannelID(WFDB.FundRaisingChannel.CHANNEL_TELEGRAM, fr.AgentID);
                    if (channel != null)
                    {
                        try
                        {
                            var req = core.GetTransferRequests(this.requstId);
                            var cid = new ChatId(long.Parse(channel.IdInChannel));
                            await bot.SendTextMessageAsync(cid,
                                $"Your payment request has been rejected\nMessage: {req.RejectNote}"
                                );

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Couldn't send payment acceptance to owner: {ex.Message}");
                        }
                    }
                    await bot.SendTextMessageAsync(chatId, "Rejected.");
                    return DialogResult.Terminated;
                case FIELD_REF:
                    this.tranRef = msg.Text;
                    this.selectedField = FIELD_PIC;
                    await bot.SendTextMessageAsync(chatId, "Upload picture of the transfer tranaction");
                    return DialogResult.Handled;
                case FIELD_PIC:
                    String fileID = null;
                    String mime = null;
                    if (msg.Photo != null && msg.Photo.Length > 0)
                    {
                        fileID = msg.Photo[0].FileId;
                        mime = "image/jpeg";
                    }
                    else if (msg.Document != null && msg.Document.FileId != null && "image/png".Equals(msg.Document.MimeType, StringComparison.CurrentCultureIgnoreCase))
                    {
                        fileID = msg.Document.FileId;
                        mime = msg.Document.MimeType;
                    }
                    
                    //if a picture is sent
                    if (fileID != null)
                    {
                        var pic = await bot.GetFileAsync(fileID, cancelationToken);
                        byte[] data;
                        using (var io = new System.IO.MemoryStream())
                        {
                            await bot.DownloadFileAsync(pic.FilePath, io, cancelationToken);
                            io.Seek(0, System.IO.SeekOrigin.Begin);
                            data = new byte[io.Length];
                            io.Read(data, 0, data.Length);
                        }
                        this.tranMime = mime;
                        this.tranPic = data;
                        this.fileId = pic.FileId;

                        var buttons = new InlineKeyboardButton[][]{
                                new[] {InlineKeyboardButton.WithCallbackData("Save",SAVE)}
                                ,new[] { InlineKeyboardButton.WithCallbackData("Cancel", CANCEL) }
                        };

                        var html = $"<strong>Transfer Ref:</strong> {this.tranRef}";

                        await bot.SendTextMessageAsync(
                            chatId: chatId,
                            text: html,
                            parseMode: ParseMode.Html,
                            replyMarkup: new InlineKeyboardMarkup(buttons)
                            );
                        return DialogResult.Handled;
                    }
                    return DialogResult.Continue;
            }
            return DialogResult.Continue;
        }
    }
}
