using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace TgBot
{
    public abstract class FormDialog : BotDialogBase
    {
        public enum FieldType
        {
            Text,
            Choices,
            Picture,
            Attachment,
            TextList,
            ContentList,
            ChildDialog
        }
        public class ParseResult
        {
            public String Error;
            public Object Data;
        }
        public class JsonData
        {
            public String DotNotType { get; set; }
            public String Data { get; set; }
            [JsonIgnore]
            Object Deserialized = null;
            public Object Val()
            {
                if (Data == null)
                    return null;
                if (Deserialized != null)
                    return Deserialized;
                var type = Type.GetType(DotNotType);
                return Deserialized = Newtonsoft.Json.JsonConvert.DeserializeObject(Data, type);
            }
            public T Val<T>() where T : class
            {
                return Val() as T;
            }
            public JsonData()
            {
                this.DotNotType = null;
                this.Data = null;
            }
            public JsonData(object value)
            {
                if (value == null)
                {
                    this.DotNotType = null;
                    this.Data = null;
                }
                else
                {
                    this.DotNotType = value.GetType().ToString();
                    this.Data = Newtonsoft.Json.JsonConvert.SerializeObject(value);
                }
            }
        }

        public enum ContentLinkType
        {
            TelegramFileId = 1,
            Url = 2,
        }
        public class ContentData
        {
            public Guid Id { get; set; }
            public byte[] Image { get; set; }
            public String ImageMime { get; set; }
            public ContentLinkType LinkType { get; set; }
            public String ContentLink { get; set; }
            public String Caption { get; set; } = null;
        }
        public class FormFieldChoiceItem
        {
            public String Key;
            public String Name;
            public FormFieldChoiceItem():this(null,null)
            {

            }
            public FormFieldChoiceItem(string key) : this(key, key)
            {

            }
            public FormFieldChoiceItem(string key, string name)
            {
                Key = key;
                Name = name;
            }
        }
        public delegate Task<ParseResult> TryParseInputDelegate(ITelegramBotClient bot, string input, CancellationToken cancellationToken);
        public delegate Task<ParseResult> ProcessChildDialogDelegate(ITelegramBotClient bot, IBotDialog dialog, CancellationToken cancellationToken);
        public delegate Task<ParseResult> ParseTextListDelegate(ITelegramBotClient bot, List<string> input, CancellationToken cancellationToken);
        public delegate Task<ParseResult> ParseConentListDelegate(ITelegramBotClient bot, List<ContentData> input, CancellationToken cancellationToken);
        public delegate Task<String> ValidateListItemDelegate(ITelegramBotClient bot,List<String> list, string input, CancellationToken cancellationToken);
        public class FormDialogField
        {
            public String Prompt;
            public String PromptHtml = null;
            public FieldType FieldType=FieldType.Text;
            public IList<FormFieldChoiceItem> Choices = null;
            public int ChoicesCol = 1;
            public IBotDialog ChildDialog = null;
            public Func<Dictionary<String, JsonData>, Task<String>> NextField=null;
            public TryParseInputDelegate ParseFunction=null;
            public ParseTextListDelegate ParseTextListFunction = null;
            public ParseConentListDelegate ParseConentListFunction = null;
            public ValidateListItemDelegate ValidateListItemFunction = null;
            public ProcessChildDialogDelegate ProcessChildDilogFunction = null;
        }
        protected virtual String MapChildDialogToField(IBotDialog childDialog) => null;
        public List<ContentData> Pictures(String prefix)
        {
            var pics = new List<KeyValuePair<int, ContentData>>();
            foreach (var kv in FieldData)
            {
                if (kv.Key.StartsWith(prefix))
                {
                    pics.Add(new(int.Parse(kv.Key.Substring(prefix.Length)), (ContentData)kv.Value.Val()));
                }
            }
            pics.Sort((x, y) => x.Key.CompareTo(y.Key));
            return pics.Select(x => x.Value).ToList();
        }
        public ChatId chatId { get; set; }
        public User from { get; set; }
        public String CurrentField { get; set; }
        public List<String> TextList { get; set; }
        public List<ContentData> ContentList { get; set; }
        public bool WaitingForCaption { get; set; } = false;
        public Message Message { get; set; }
        public Dictionary<String, JsonData> FieldData { get; set; }
        public abstract FormDialogField GetFieldDef(String key);
        public abstract String FirstField { get; }
        
        void SetFieldData(String key, Object data)
        {
            if (FieldData == null)
                FieldData = new Dictionary<string, JsonData>();
            if (FieldData.ContainsKey(key))
                FieldData[key] = new JsonData(data);
            else
                FieldData.Add(key, new JsonData(data));
        }
        public FormDialog(ChatId chatId, User from)
        {
            this.chatId = chatId;
            this.from = from;
        }
        public override async Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            if (FirstField == null)
                return DialogResult.Terminated;
            CurrentField = FirstField;
            var field = GetFieldDef(FirstField);
            await PromptField(bot, field,cancelationToken);
            return DialogResult.Handled;
        }
        public static InlineKeyboardMarkup CreateInlineButtons(IEnumerable<KeyValuePair<String,String>> keyvalues,int nCol=1)
        {
            var buttons = new List<List<InlineKeyboardButton>>();

            List<InlineKeyboardButton> currentRow = null;
            int i = 0;
            foreach(var kv in keyvalues)
            {
                if (i % nCol == 0)
                {
                    currentRow = new List<InlineKeyboardButton>();
                    buttons.Add(currentRow);
                }
                currentRow.Add(InlineKeyboardButton.WithCallbackData(kv.Value, kv.Key));
                i++;                
            }
            return new InlineKeyboardMarkup(buttons);
        }
        private async Task PromptField(ITelegramBotClient bot, FormDialogField field,CancellationToken cancellationToken)
        {
            
            if (field.FieldType == FieldType.Choices)
            {
                var buttons = CreateInlineButtons(field.Choices.Select(x=>new KeyValuePair<string, string>(x.Key,x.Name)), field.ChoicesCol);
                if (field.PromptHtml == null)
                    this.Message = await bot.SendTextMessageAsync(chatId: chatId, text: field.Prompt, replyMarkup: buttons);
                else
                    this.Message = await bot.SendTextMessageAsync(chatId: chatId, text: field.PromptHtml, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html, replyMarkup: buttons);
            }
            else
            {
                if (field.FieldType == FieldType.TextList)
                    this.TextList = new List<string>();
                else if (field.FieldType == FieldType.ContentList)
                    this.ContentList = new List<ContentData>();
                
                if (field.PromptHtml == null)
                    this.Message = await bot.SendTextMessageAsync(chatId, field.Prompt);
                else
                    this.Message = await bot.SendTextMessageAsync(chatId, field.PromptHtml, parseMode: Telegram.Bot.Types.Enums.ParseMode.Html);
                
                if (field.FieldType == FieldType.ChildDialog)
                {
                    await this.SetChildDialog(bot, field.ChildDialog, cancellationToken);
                }
            }
        }
        protected override async Task<DialogResult> HandleChildTerminate(ITelegramBotClient bot, Update update, IBotDialog child, CancellationToken cancellationToken)
        {
            if (CurrentField == null)
                return DialogResult.Continue;
            var field = GetFieldDef(CurrentField);
            if(field==null)
                return DialogResult.Continue;
            ParseResult res;
            if (field.ProcessChildDilogFunction != null)
            {
                res = await field.ProcessChildDilogFunction(bot, child, cancellationToken);
            }
            else
                res = new ParseResult { Data = child };
            if(res.Error!=null)
            {
                await bot.SendTextMessageAsync(chatId, res.Error, cancellationToken: cancellationToken);
                await PromptField(bot, field, cancellationToken);
                return DialogResult.Handled;
            }
            this.SetFieldData(CurrentField, res.Data);
            return await AdvanceNext(bot, field, cancellationToken);
        }
        public override async Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancellationToken)
        {
            if (CurrentField == null)
                return DialogResult.Continue;
            if (message.Type == Telegram.Bot.Types.Enums.MessageType.Text)
            {
                return await processText(bot, message, cancellationToken);
            }
            else
                return await processAttachment(bot, message, cancellationToken);
        }
        public override async Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancellationToken)
        {
            if (CurrentField == null)
                return DialogResult.Continue;
            var field = GetFieldDef(CurrentField);
            if (field.FieldType != FieldType.Choices)
                return DialogResult.Continue;
            ParseResult res;
            if (field.ParseFunction != null)
                res = await field.ParseFunction(bot, callBack.Data, cancellationToken);
            else
                res = new ParseResult { Data = callBack.Data };
            if (res.Error != null)
            {
                await bot.SendTextMessageAsync(chatId, res.Error);
                await PromptField(bot, field,cancellationToken);
                return DialogResult.Handled;
            }
            this.SetFieldData(CurrentField, res.Data);
            await bot.EditMessageReplyMarkupAsync(this.chatId, this.Message.MessageId, new InlineKeyboardMarkup(new InlineKeyboardButton[] { }));
            await bot.SendTextMessageAsync(this.chatId,
                $"<strong>{field.Choices.Where(x => x.Key.Equals(callBack.Data)).First().Name}</strong>",
                Telegram.Bot.Types.Enums.ParseMode.Html);
            return await AdvanceNext(bot, field, cancellationToken);

        }
        
        protected virtual Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            return Task.FromResult(DialogResult.Terminated);
        }
        private async Task<DialogResult> processAttachment(ITelegramBotClient bot, Message Message, CancellationToken cancellationToken)
        {
            if(WaitingForCaption)
            {
                await bot.SendTextMessageAsync(this.chatId, "Please enter label for the previous attachment, this attachment is ingored.", cancellationToken: cancellationToken);
                return DialogResult.Handled;
            }
            var field = GetFieldDef(CurrentField);

            
            if (field.FieldType != FieldType.Attachment && field.FieldType!=FieldType.ContentList)
            {
                return DialogResult.Continue;
            }
            String fileID = null;
            String mime = null;
            if (Message.Photo != null && Message.Photo.Length > 0)
            {
                var maxPhoto = Message.Photo[0];
                for(int i=1;i<Message.Photo.Length;i++)
                {
                    if(maxPhoto.FileSize<Message.Photo[i].FileSize)
                    {
                        maxPhoto = Message.Photo[i];
                    }
                }
                fileID = maxPhoto.FileId;
                mime = "image/jpeg";
            }
            else if (Message.Document != null && Message.Document.FileId != null)
            {
                fileID = Message.Document.FileId;
                mime = Message.Document.MimeType;
            }
            //if a picture is sent
            if (fileID != null)
            {
                var pic = await bot.GetFileAsync(fileID, cancellationToken);
                byte[] data;
                using (var io = new System.IO.MemoryStream())
                {
                    await bot.DownloadFileAsync(pic.FilePath, io, cancellationToken);
                    io.Seek(0, System.IO.SeekOrigin.Begin);
                    data = new byte[io.Length];
                    io.Read(data, 0, data.Length);
                }
                var fileData = new ContentData
                {
                    ContentLink = fileID,
                    Image = data,
                    ImageMime = mime,
                    LinkType=ContentLinkType.TelegramFileId
                };
                if (field.FieldType == FieldType.ContentList)
                {
                    this.ContentList.Add(fileData);
                    await bot.SendTextMessageAsync(chatId, "Attachment added.\n Enter label for the attachment", cancellationToken: cancellationToken);
                    this.WaitingForCaption = true; 
                    return DialogResult.Handled;
                }
                else
                {
                    this.SetFieldData(CurrentField, fileData);
                    return await AdvanceNext(bot, field, cancellationToken);
                }
            }
            return DialogResult.Continue;
        }
        private async Task<DialogResult> processText(ITelegramBotClient bot, Message message, CancellationToken cancellationToken)
        {
            if(WaitingForCaption)
            {
                this.ContentList.Last().Caption = message.Text;
                this.WaitingForCaption = false;
                await bot.SendTextMessageAsync(chatId, "Add more url or attachment. Enter enter /end to finish", cancellationToken: cancellationToken);
                return DialogResult.Handled;
            }
            var field = GetFieldDef(CurrentField);
            switch (field.FieldType)
            {
                case FieldType.Text:
                    {
                        ParseResult res;
                        if (field.ParseFunction != null)
                            res = await field.ParseFunction(bot, message.Text, cancellationToken);
                        else
                        {
                            if (string.IsNullOrWhiteSpace(message.Text))
                                res = new ParseResult { Error = "Invalid entry" };
                            else
                                res = new ParseResult { Data = message.Text };
                        }
                        if (res.Error != null)
                        {
                            await bot.SendTextMessageAsync(chatId, res.Error);
                            await PromptField(bot, field,cancellationToken);
                            return DialogResult.Handled;
                        }
                        this.SetFieldData(CurrentField, res.Data);

                        return await AdvanceNext(bot, field, cancellationToken);
                    }
                case FieldType.Picture:
                    await bot.SendTextMessageAsync(chatId, "Upload picture");
                    return DialogResult.Handled;
                case FieldType.Attachment:
                    await bot.SendTextMessageAsync(chatId, "Upload attachment");
                    return DialogResult.Handled;
                case FieldType.ContentList:
                    {
                        if ("/end".Equals(message.Text, StringComparison.OrdinalIgnoreCase))
                        {

                            if (field.ParseConentListFunction != null)
                            {
                                var res = await field.ParseConentListFunction(bot, ContentList, cancellationToken);
                                if (res.Error != null)
                                {
                                    await bot.SendTextMessageAsync(chatId, res.Error);
                                    await PromptField(bot, field,cancellationToken);
                                    return DialogResult.Handled;
                                }
                                this.SetFieldData(CurrentField, res.Data);
                            }
                            else
                            {
                                this.SetFieldData(CurrentField,  ContentList );
                            }
                            return await AdvanceNext(bot, field, cancellationToken);
                        }
                        try
                        {
                            var url = new Uri(message.Text);
                            this.ContentList.Add(new ContentData
                            {
                                LinkType = ContentLinkType.Url,
                                ContentLink = message.Text,
                            });
                            await bot.SendTextMessageAsync(chatId, "Url added.\n Add more url or attachment. Enter enter /end to finish",cancellationToken:cancellationToken);
                        }
                        catch
                        {
                            await bot.SendTextMessageAsync(chatId, "Enter valid content url");
                        }
                        return DialogResult.Handled;
                    }
                case FieldType.TextList:
                    {
                        if ("/end".Equals(message.Text, StringComparison.OrdinalIgnoreCase))
                        {
                            if (field.ParseTextListFunction != null)
                            {
                                var res = await field.ParseTextListFunction(bot, TextList, cancellationToken);
                                if (res.Error != null)
                                {
                                    await bot.SendTextMessageAsync(chatId, res.Error);
                                    await PromptField(bot, field,cancellationToken);
                                    return DialogResult.Handled;
                                }
                                this.SetFieldData(CurrentField, res.Data);
                            }
                            else
                            {
                                this.SetFieldData(CurrentField, new ParseResult { Data = TextList });
                            }
                            return await AdvanceNext(bot, field, cancellationToken);
                        }
                        String error = null;
                        if (field.ValidateListItemFunction != null)
                        {
                            error = await field.ValidateListItemFunction(bot, TextList, message.Text, cancellationToken);
                        }
                        if (error == null)
                            this.TextList.Add(message.Text);
                        else
                            await bot.SendTextMessageAsync(chatId, error);
                        return DialogResult.Handled;
                    }
                default:
                    return DialogResult.Continue;
            }
        }

        private async Task<DialogResult> AdvanceNext(ITelegramBotClient bot, FormDialogField field, CancellationToken cancellationToken)
        {
            if (field.NextField == null)
                return await OnCompleteAsync(bot, cancellationToken);
            var next = await field.NextField(this.FieldData);
            if (next == null)
                return await OnCompleteAsync(bot, cancellationToken);
            await PromptField(bot, GetFieldDef(next),cancellationToken);
            this.CurrentField = next;
            return DialogResult.Handled;
        }
        public override async Task<DialogResult> HandleCancel(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            await base.HandleCancel(bot, cancelationToken);
            var field = GetFieldDef(CurrentField);

            if (field.FieldType != FieldType.Choices)
                return DialogResult.Terminated;
            try
            {
                await bot.EditMessageReplyMarkupAsync(this.chatId, this.Message.MessageId, new InlineKeyboardMarkup(new InlineKeyboardButton[] { }));
            }
            catch(Exception ex)
            {
                TGBot.LogException("Error trying to remove buttons", ex);
            }
            return DialogResult.Terminated;
        }
    }
    public class ContentListDialog : FormDialog
    {
        public String Prompt { get; set; }
        public ContentListDialog():base(null,null)
        {

        }
        public ContentListDialog(string userId,string prompt):base(userId,new User { Id=long.Parse(userId),FirstName="Default"})
        {
            this.Prompt = prompt;
        }

        public override string FirstField => "Default";

        public override FormDialogField GetFieldDef(string key)
        {
            return new FormDialogField
            {
                PromptHtml = Prompt,
                FieldType = FieldType.ContentList,
                NextField = null
            };
        }
        public List<ContentData> Data()
        {
            return this.FieldData[FirstField].Val<List<ContentData>>();
        }
    }
}
