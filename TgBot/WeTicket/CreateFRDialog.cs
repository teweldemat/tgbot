using System;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot.Dialogs
{
    public class CreateFRDialog:BotDialogBase
    {
        const String TITLE = "SetTitle";
        const String DESC = "SetDescription";
        const String TARGET = "SetTarget";
        const String PICTURE = "SetPicture";
        const String CANCEL = "CreateCancel";
        const String YES = "CreateYes";
        const String NO = "CreateNo";
        const String SAVE = "CreateSave";
        
        public String selectedField=null;
        public bool creationMode = true;
        public User tgUser;
        public ChatId chatId;
        public CreateFRDialog(ChatId chatId,User tgUser)
        {
            this.tgUser = tgUser;
            this.chatId = chatId;
        }
        async Task PromptConfirmation(ITelegramBotClient bot, Telegram.Bot.Types.ChatId chatID, Telegram.Bot.Types.User user)
        {
            var service = new WFTGDB.WFTGDBService();
            creationMode = false;
            var state = service.GetUserState(user.Id.ToString());
            var replyKeyboardMarkup = new InlineKeyboardMarkup(
                                    new[]{new []{
                                        InlineKeyboardButton.WithCallbackData(Program.lm.Save_it_looks_right,SAVE)
                                    },
                                    new []{
                                        InlineKeyboardButton.WithCallbackData(Program.lm.Cancel,CANCEL)
                                    },
                                    new []{
                                        InlineKeyboardButton.WithCallbackData(Program.lm.Change_title,TITLE),
                                        InlineKeyboardButton.WithCallbackData(Program.lm.Change_description,DESC),
                                    },new []{
                                    InlineKeyboardButton.WithCallbackData(state.Data.FundRaiser.TargetAmount==-1?Program.lm.Set_Target:Program.lm.Change_target,TARGET),
                                    InlineKeyboardButton.WithCallbackData(state.Data.HasPicture?Program.lm.Change_picture:Program.lm.Set_Picture,PICTURE),
                                    }
                                    });
            if (state.Data.HasPicture)
            {
                try
                {
                    var file = await bot.GetFileAsync(state.Data.Picture.IdInExternalStorage);
                    await bot.SendPhotoAsync(chatId, new Telegram.Bot.Types.InputFiles.InputOnlineFile(file.FileId));
                }
                catch (Exception fex)
                {
                    Console.WriteLine("Error getting file " + fex.Message);
                }

            }
            await bot.SendTextMessageAsync(chatID,
                text: $"<strong>{System.Web.HttpUtility.HtmlEncode(state.Data.FundRaiser.ShortName)}</strong>"
                +$"<pre>\n</pre>{System.Web.HttpUtility.HtmlEncode(state.Data.FundRaiser.ShortDescription)}"
                +$"<pre>\n</pre>{(state.Data.FundRaiser.TargetAmount > 0 ?Program.lm.Target_amount(IntData.toString(state.Data.FundRaiser.TargetAmount)): Program.lm.No_traget_set)}",
                replyMarkup: replyKeyboardMarkup,
                parseMode: ParseMode.Html);
        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            var service = new WFTGDB.WFTGDBService();
            await bot.SendTextMessageAsync(chatId, Program.lm.Enter_a_short_title_for_your_fundraiser);
            selectedField = TITLE;
            return DialogResult.Handled;
        }
        public async override Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery q, CancellationToken cancelationToken)
        {
            switch (q.Data)
            {
                case CANCEL:
                    await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Alright_canceled);
                    return DialogResult.Terminated;
                case SAVE:
                    new WFTGDB.WFTGDBService().ConfirmFundRaiser(q.From.Id.ToString(),TGBot.FullName(q.From));
                    await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Congradulations_your_fundraiser_created);
                    return DialogResult.Terminated;
            }
            if (creationMode)
                return await HandleForCreateMode(bot, q);
            return await HandleForReviseMode(bot, q);
        }

        private async Task<DialogResult> HandleForReviseMode(ITelegramBotClient bot, CallbackQuery q)
        {
            var service = new WFTGDB.WFTGDBService();
            switch (q.Data)
            {
                case YES:
                    switch(selectedField)
                    {
                        case TARGET:
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Enter_fundraiser_target);
                            return DialogResult.Handled;
                        case PICTURE:
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Upload_a_picture_for_your_fundraiser);
                            return DialogResult.Handled;
                    }
                    return DialogResult.Handled;
                case NO:
                    switch (selectedField)
                    {
                        case TARGET:
                            service.SetFundRaiserTarget(q.From.Id.ToString(), -1);
                            await PromptConfirmation(bot, q.Message.Chat.Id, q.From);
                            return DialogResult.Handled;
                        case PICTURE:
                            service.SetFundRaiserPicture(q.From.Id.ToString(), null, null, null);
                            await PromptConfirmation(bot, q.Message.Chat.Id, q.From);
                            return DialogResult.Handled;
                    }
                    return DialogResult.Handled;
                case TITLE:
                    await bot.SendTextMessageAsync(chatId, Program.lm.Enter_a_new_title_for_your_fundraiser);
                    selectedField = TITLE;
                    return DialogResult.Handled;
                case DESC:
                    await bot.SendTextMessageAsync(chatId,Program.lm.Enter_a_description_of_your_fundraiser);
                    selectedField = DESC;
                    return DialogResult.Handled;
                case TARGET:
                    var state = service.GetUserState(q.From.Id.ToString());
                    if(state.Data.FundRaiser.TargetAmount==-1)
                    {
                        await bot.SendTextMessageAsync(chatId, Program.lm.Enter_target_for_your_fund_raiser);
                        selectedField = TARGET;
                        return DialogResult.Handled;

                    }
                    var replyKeyboardMarkup = new InlineKeyboardMarkup(new[]{new []{
                                                                            InlineKeyboardButton.WithCallbackData(Program.lm.Change_target,YES),
                                                                            InlineKeyboardButton.WithCallbackData(Program.lm.Remove_target,NO),
                                                                        }});
                    await bot.SendTextMessageAsync(
                        chatId: q.Message.Chat.Id,
                        text: Program.lm.What_do_you_want_to_do_with_the_fundraiser_target,
                        replyMarkup: replyKeyboardMarkup
                    );
                    selectedField = TARGET;
                    return DialogResult.Handled;
                case PICTURE:
                    state = service.GetUserState(q.From.Id.ToString());
                    if (!state.Data.HasPicture)
                    {
                        await bot.SendTextMessageAsync(chatId, Program.lm.Upload_picture_for_your_fund_raiser);
                        selectedField = TARGET;
                        return DialogResult.Handled;

                    }
                    replyKeyboardMarkup = new InlineKeyboardMarkup(new[]{new []{
                                                                            InlineKeyboardButton.WithCallbackData(Program.lm.Change_picture,YES),
                                                                            InlineKeyboardButton.WithCallbackData(Program.lm.Remove_the_picture,NO),
                                                                        }});
                    await bot.SendTextMessageAsync(
                        chatId: q.Message.Chat.Id,
                        text: Program.lm.What_do_you_want_to_do_with_the_fundraiser_picture,
                        replyMarkup: replyKeyboardMarkup
                    );
                    selectedField = PICTURE;
                    return DialogResult.Handled;
            }
            
            switch (selectedField)
            {
                case TARGET:
                    switch (q.Data)
                    {
                        case YES:
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Enter_fundraiser_target);
                            return DialogResult.Handled;
                        case NO:
                            service.SetFundRaiserTarget(q.From.ToString(), -1);
                            var notePrompt = new InlineKeyboardMarkup(
                                new[]{new []{
                                InlineKeyboardButton.WithCallbackData(Program.lm.Yes_please,YES),
                                InlineKeyboardButton.WithCallbackData(Program.lm.No_thanks,NO),
                            }});
                            await bot.SendTextMessageAsync(
                                chatId: q.Message.Chat.Id,
                                text: Program.lm.Do_you_want_to_set_a_picture_for_your_funraiser__it_is_highly_recommended,
                                replyMarkup: notePrompt
                            );
                            return DialogResult.Handled;
                    }
                    break;
                case PICTURE:
                    switch (q.Data)
                    {
                        case YES:
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Excellent_choice__Upload_a_picture_for_your_fundraiser);
                            return DialogResult.Handled;
                        case NO:
                            service.SetFundRaiserPicture(q.From.Id.ToString(), null, null, null);
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Alright_you_can_come_back_anytime_and_set_a_picture);
                            await PromptConfirmation(bot, q.Message.Chat.Id, q.From);
                            return DialogResult.Handled;
                    }
                    selectedField = PICTURE;
                    break;
            }

            return DialogResult.Continue;
        }
        private async Task<DialogResult> HandleForCreateMode(ITelegramBotClient bot, CallbackQuery q)
        {
            var service = new WFTGDB.WFTGDBService();
            switch (selectedField)
            {
                case TARGET:
                    switch (q.Data)
                    {
                        case YES:
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Enter_fundraiser_target);
                            return DialogResult.Handled;
                        case NO:
                            service.SetFundRaiserTarget(q.From.Id.ToString(), -1);
                            var notePrompt = new InlineKeyboardMarkup(
                                new[]{new []{
                                InlineKeyboardButton.WithCallbackData(Program.lm.Yes_please,YES),
                                InlineKeyboardButton.WithCallbackData(Program.lm.No_thanks,NO),
                            }});
                            await bot.SendTextMessageAsync(
                                chatId: q.Message.Chat.Id,
                                text: Program.lm.Do_you_want_to_set_a_picture_for_your_funraiser__it_is_highly_recommended,
                                replyMarkup: notePrompt
                            );
                            selectedField = PICTURE;
                            return DialogResult.Handled;
                    }
                    break;
                case PICTURE:
                    switch (q.Data)
                    {
                        case YES:
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Excellent_choice__Upload_a_picture_for_your_fundraiser);
                            return DialogResult.Handled;
                        case NO:
                            service.SetFundRaiserPicture(q.From.Id.ToString(), null, null, null);
                            await bot.SendTextMessageAsync(q.Message.Chat.Id, Program.lm.Alright_you_can_come_back_anytime_and_set_a_picture);
                            await PromptConfirmation(bot, q.Message.Chat.Id, q.From);
                            return DialogResult.Handled;
                    }
                    selectedField = PICTURE;
                    break;
            }

            return DialogResult.Continue;
        }

        private static async Task PromptSetPicture(ITelegramBotClient bot, Message msg)
        {
            var notePrompt = new InlineKeyboardMarkup(
                    new[]{new []{
                                InlineKeyboardButton.WithCallbackData(Program.lm.Yes_please,YES),
                                InlineKeyboardButton.WithCallbackData(Program.lm.No_thanks,NO),
                            }});

            await bot.SendTextMessageAsync(
                chatId: msg.Chat.Id,
                text: Program.lm.Do_you_want_to_set_a_picture_for_your_funraiser__it_is_highly_recommended,
                replyMarkup: notePrompt
            );
        }
        public async override Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message msg, CancellationToken cancelationToken)
        {
            var service = new WFTGDB.WFTGDBService();
            if (selectedField == null)
                return DialogResult.Continue;
            switch (selectedField)
            {
                case TITLE:
                    service.SetFundRaiserTitle(msg.From, msg.Text);
                    if (creationMode)
                    {
                        await bot.SendTextMessageAsync(msg.Chat.Id, Program.lm.Enter_a_description_of_your_fundraiser);
                        selectedField = DESC;
                    }
                    else
                        await PromptConfirmation(bot, msg.Chat.Id, msg.From);
                    return DialogResult.Handled;
                case DESC:
                    service.SetFundRaiserDescription(msg.From, msg.Text);
                    if (creationMode)
                    {
                        var replyKeyboardMarkup = new InlineKeyboardMarkup(new[]{new []{
                                                                            InlineKeyboardButton.WithCallbackData(Program.lm.Yes_please,YES),
                                                                            InlineKeyboardButton.WithCallbackData(Program.lm.No_thanks,NO),
                                                                        }});
                        await bot.SendTextMessageAsync(
                            chatId: msg.Chat.Id,
                            text: Program.lm.Enter_target_for_your_fund_raiser,
                            replyMarkup: replyKeyboardMarkup
                        );
                        selectedField = TARGET;
                    }
                    else
                        await PromptConfirmation(bot, msg.Chat.Id, msg.From);
                    return DialogResult.Handled;
                case TARGET:
                    if (!double.TryParse(msg.Text, out double amountUpdate)
                                            || amountUpdate < 1
                                            )
                        await bot.SendTextMessageAsync(msg.Chat.Id, Program.lm.Please_enter_a_valid_amount_that_is_at_least_1_Birr);
                    else
                    {
                        service.SetFundRaiserTarget(msg.From.Id.ToString(), IntData.toIntMoney(amountUpdate));
                        if (creationMode)
                        {
                            await PromptSetPicture(bot, msg);
                            selectedField = PICTURE;
                        }
                        else
                            await PromptConfirmation(bot, msg.Chat.Id, msg.From);
                    }
                    
                    return DialogResult.Handled;
                case PICTURE:
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
                        var pic = await bot.GetFileAsync(fileID,cancelationToken);
                        byte[] data;
                        using (var io = new System.IO.MemoryStream())
                        {
                            await bot.DownloadFileAsync(pic.FilePath, io, cancelationToken);
                            io.Seek(0, System.IO.SeekOrigin.Begin);
                            data = new byte[io.Length];
                            io.Read(data, 0, data.Length);
                        }

                        new WFTGDB.WFTGDBService().SetFundRaiserPicture(msg.From.Id.ToString(), mime, data, pic.FileId);
                        await PromptConfirmation(bot, msg.Chat.Id, msg.From);
                    }
                    return DialogResult.Handled;
            }
            return DialogResult.Continue;
        }

    }

}
