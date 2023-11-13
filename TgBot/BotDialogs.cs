using Newtonsoft.Json;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TgBot
{
    public enum DialogResult
    {
        Handled,
        Continue,
        Terminated
    }
    public enum DialogOverlapPolicy
    {
        AllowTop = 1,
        AllowBottom = 2,
        Allow=AllowTop|AllowBottom,
        CancelOnOverlap=4,
    }
    public interface IBotDialog
    {
        Task<DialogResult> HandleUpdateAsync(ITelegramBotClient bot, Update update,CancellationToken cancelationToken);
        Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken);
        Task<DialogResult> HandlePaymentAsync(ITelegramBotClient bot,String checkOutCode,CancellationToken cancelationToken);        
        Task<DialogResult> HandleCancel(ITelegramBotClient bot, CancellationToken cancelationToken);
        DialogOverlapPolicy OverlapPolicy { get; }
        void SetServices(IServiceProvider services);
    }
    public abstract class BotDialogBase : IBotDialog
    {
        [JsonIgnore]
        protected IBotDialog _childDialog = null;
        [JsonIgnore]
        protected bool _childDialogDeserialized = false;
        public String JsonChildDialog { get; set; } = null;
        public String JsonChildDialogType { get; set; } = null;
        [JsonIgnore]
        public IBotDialog ChildDialog
        {
            get
            {
                if (_childDialogDeserialized)
                    return _childDialog;
                if (JsonChildDialog == null)
                    _childDialog = null;
                else
                    _childDialog = Newtonsoft.Json.JsonConvert.DeserializeObject(JsonChildDialog, Type.GetType(JsonChildDialogType)) as IBotDialog;
                _childDialogDeserialized = true;
                return _childDialog;
            }
            set
            {
                
                _childDialog = value;
                serializeChild();
            }
        }
        void serializeChild()
        {
            if (_childDialog == null)
            {
                JsonChildDialog = null;
                JsonChildDialogType = null;
            }
            else
            {
                JsonChildDialog = Newtonsoft.Json.JsonConvert.SerializeObject(_childDialog);
                JsonChildDialogType = _childDialog.GetType().ToString();
            }
        }
        protected async Task SetChildDialog(ITelegramBotClient bot, IBotDialog childDialog,CancellationToken cancellationToken)
        {
            this.ChildDialog = childDialog;
            var res=await childDialog.StartAsync(bot, cancellationToken);
            this.serializeChild();
            if (res == DialogResult.Terminated)
                await HandleChildTerminate(bot, null, childDialog, cancellationToken);
        }
        public DialogOverlapPolicy OverlapPolicy => DialogOverlapPolicy.CancelOnOverlap;
        protected virtual Task<DialogResult> HandleChildTerminate(ITelegramBotClient bot, Update update, IBotDialog child, CancellationToken cancelationToken)
        {
            return Task.FromResult(DialogResult.Terminated);
        }
        public virtual async Task<DialogResult> HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken cancelationToken)
        {
            if (ChildDialog != null)
            {
                var res = await ChildDialog.HandleUpdateAsync(bot, update, cancelationToken);
                if (res == DialogResult.Terminated)
                {
                    var chd = ChildDialog;
                    ChildDialog = null;  // HandleChildTerminate is allowed to set ChildDialog back
                    var ret =await HandleChildTerminate(bot, update, chd, cancelationToken);
                    return ret;
                }
                this.serializeChild();
                return res;
            }
            switch (update.Type)
            {
                case UpdateType.CallbackQuery:
                    if (update.CallbackQuery == null
                        || update.CallbackQuery.From == null
                        || update.CallbackQuery.Data == null
                        )
                        return DialogResult.Continue;
                    var q = update.CallbackQuery;
                    await bot.AnswerCallbackQueryAsync(
                        callbackQueryId: q.Id,
                        text: $"Received {q.Data}"
                    );

                    return await HandleCallBackAsync(bot, update.CallbackQuery,cancelationToken);
                case UpdateType.Message:
                    var msg = update.Message;
                    if (msg == null
                        || msg.Chat == null
                        || msg.Chat.Type != ChatType.Private
                        || msg.From == null
                        || msg.From.IsBot
                        )
                        return DialogResult.Continue;
                    return await HandleMessageAsync (bot, update.Message,cancelationToken);
            }
            return DialogResult.Continue;
        }
        public virtual  Task<DialogResult> HandleCallBackAsync(ITelegramBotClient bot, CallbackQuery callBack, CancellationToken cancelationToken)
        {
            return Task.FromResult(DialogResult.Continue);
        }
        public virtual  Task<DialogResult> HandleMessageAsync(ITelegramBotClient bot, Message message, CancellationToken cancelationToken)
        {
            return Task.FromResult(DialogResult.Continue);
        }
        public virtual  Task<DialogResult> StartAsync(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            return Task.FromResult(DialogResult.Continue);
        }

        public virtual Task<DialogResult> HandlePaymentAsync(ITelegramBotClient bot, string checkOutCode, CancellationToken cancelationToken)
        {
            return Task.FromResult(DialogResult.Continue);
        }

        public virtual Task<DialogResult> HandleCancel(ITelegramBotClient bot, CancellationToken cancelationToken)
        {
            if (ChildDialog != null)
                ChildDialog.HandleCancel(bot, cancelationToken);
            return Task.FromResult(DialogResult.Terminated);
        }

        public abstract void SetServices(IServiceProvider services);
    }
}
