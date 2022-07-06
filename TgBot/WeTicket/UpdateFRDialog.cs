using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot
{
    public class UpdateFRDialog : BotDialogBase
    {
        const String TITLE = "UpdateTitle";
        const String DESC = "UpdateDescription";
        const String TARGET = "UpdateTarget";
        const String REMOVE_TARGET = "RemoveTarget";
        const String PICTURE = "UpdatePicture";
        const String CANCEL = "UpdateCancel";
        const String YES = "YesCloseFR";
        const String NO = "No";

        public User from;
        public ChatId chatId;
        public Guid frid;
        public String changeField = null;
        public UpdateFRDialog(ChatId chatId, User from, Guid frid)
        {
            this.chatId = chatId;
            this.from = from;
            this.frid = frid;
        }

        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            var service = new WFTGDB.WFTGDBService();
            var state = service.GetUserState(from.Id.ToString());
            var keys = new List<InlineKeyboardButton[]>();
            var fr = new WFDB.WFDBService().GetFundRaiser(frid);

            keys.AddRange(new[]{
                new []{InlineKeyboardButton.WithCallbackData("Title.",TITLE),
                    InlineKeyboardButton.WithCallbackData("Description.",DESC)}
                });
            if(fr.TargetAmount>-1)
            {
                keys.Add(new[] { InlineKeyboardButton.WithCallbackData("Change Target", TARGET),InlineKeyboardButton.WithCallbackData("Remove Target", REMOVE_TARGET) });
            }
            else
                keys.Add(new[] { InlineKeyboardButton.WithCallbackData("Set Target", TARGET) });
            keys.Add(new[] { InlineKeyboardButton.WithCallbackData("Picture.", PICTURE) });
            keys.Add(new[] { InlineKeyboardButton.WithCallbackData("Cancel.", CANCEL) });
            var replyKeyboardMarkup = new InlineKeyboardMarkup(keys.ToArray());
            await bot.SendTextMessageAsync(chatId,
                text: "What do you want to change?",
                replyMarkup: replyKeyboardMarkup,
                parseMode: ParseMode.Html);
            return DialogResult.Handled;
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancelationToken)
        {
            changeField = callBack.Data;
            switch (callBack.Data)
            {
                case TITLE:
                    await bot.SendTextMessageAsync(chatId,
                        text: "Enter new title for your WeFund"
                        );
                    return DialogResult.Handled;
                case DESC:
                    await bot.SendTextMessageAsync(chatId,
                        text: "Enter new description for your WeFund"
                        );
                    return DialogResult.Handled;
                case PICTURE:
                    await bot.SendTextMessageAsync(chatId,
                        text: "Upload a picture"
                        );
                    return DialogResult.Handled;
                case TARGET:
                    await bot.SendTextMessageAsync(chatId,
                       text: "Enter new target for your WeFund"
                       );
                    return DialogResult.Handled;
                case REMOVE_TARGET:
                    var service = new WFDB.WFDBService();
                    var fr = service.GetFundRaiser(frid);
                    fr.TargetAmount = -1;
                    service.UpdateFundRaiser(fr);
                    await WeFundBotApp.ProcessShowDetail(callBack.Message.Chat.Id,callBack.From, frid,true,cancelationToken);
                    return DialogResult.Terminated;
                case CANCEL:
                    await WeFundBotApp.ProcessShowDetail(callBack.Message.Chat.Id, callBack.From, frid,true,cancelationToken);
                    return DialogResult.Terminated;
            }
            changeField = null;
            return DialogResult.Continue;
        }
        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancelationToken)
        {
            switch(changeField)
            {
                case PICTURE:
                    if (message.Photo.Length==0)
                        return DialogResult.Continue;
                    String fileID = null;
                    String mime = null;
                    if (message.Photo != null && message.Photo.Length > 0)
                    {
                        fileID = message.Photo[0].FileId;
                        mime = "image/jpeg";
                    }
                    else if (message.Document != null && message.Document.FileId != null && "image/png".Equals(message.Document.MimeType, StringComparison.CurrentCultureIgnoreCase))
                    {
                        fileID = message.Document.FileId;
                        mime = message.Document.MimeType;
                    }
                    //if a picture is sent
                    if (fileID == null)
                        return DialogResult.Continue;

                        var pic = await bot.GetFileAsync(fileID, cancelationToken);
                    if (pic.FileSize > WeFundBotApp.MAX_PIC_BYTES)
                    {
                        await bot.SendTextMessageAsync(message.Chat.Id, "The picture is too large, try sending smaller picture");
                        return DialogResult.Handled;
                    }
                    byte[] data;
                    using (var io = new System.IO.MemoryStream())
                    {
                        await bot.DownloadFileAsync(pic.FilePath, io, cancelationToken);
                        io.Seek(0, System.IO.SeekOrigin.Begin);
                        data = new byte[io.Length];
                        io.Read(data, 0, data.Length);
                    }

                    new WFDB.WFDBService().UpdateFundRaiserPicture(frid,new WFDB.FundRaiserPictureItem
                    {
                        ExternalStorageType=WFDB.PictureExternalStorageType.Telegram,
                        IdInExternalStorage=fileID,
                        Picture=data,
                        PictureMIME=mime
                    });
                    await WeFundBotApp.ProcessShowDetail(message.Chat.Id, message.From, frid,true,cancelationToken);
                    return DialogResult.Terminated;
                case TITLE:
                    if (!WFDB.WFDBService.IsValidTitle(message.Text))
                        await bot.SendTextMessageAsync(chatId,
                        text: "Invalid title"
                        );
                    var service = new WFDB.WFDBService();
                    var fr = service.GetFundRaiser(frid);
                    fr.ShortName = message.Text;
                    service.UpdateFundRaiser(fr);
                    await WeFundBotApp.ProcessShowDetail(message.Chat.Id,message.From, frid,true,cancelationToken);
                    return DialogResult.Terminated;
                case DESC:
                    if (!WFDB.WFDBService.IsValidDescriptoin(message.Text))
                        await bot.SendTextMessageAsync(chatId,
                        text: "Invalid description"
                        );
                    service = new WFDB.WFDBService();
                    fr = service.GetFundRaiser(frid);
                    fr.ShortDescription= message.Text;
                    service.UpdateFundRaiser(fr);
                    await WeFundBotApp.ProcessShowDetail(message.Chat.Id,message.From, frid,true,cancelationToken);
                    return DialogResult.Terminated;
                case TARGET:
                    if (!WFDB.WFDBService.IsValidAmount(message.Text,out var amount))
                        await bot.SendTextMessageAsync(chatId,
                        text: "Invalid description"
                        );
                    service = new WFDB.WFDBService();
                    fr = service.GetFundRaiser(frid);
                    fr.TargetAmount = amount;
                    service.UpdateFundRaiser(fr);
                    await WeFundBotApp.ProcessShowDetail(message.Chat.Id, message.From, frid,true,cancelationToken);
                    return DialogResult.Terminated;
            }
            return DialogResult.Continue;
        }
    }
}
