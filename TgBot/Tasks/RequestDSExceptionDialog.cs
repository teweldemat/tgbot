using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.Tasks
{
    public class RequestDSExceptionDialog:FormDialog
    {
        const string FIELD_REASON = "Reason";
        const string FIELD_FROM = "From";
        const string FIELD_TO= "TO";
        private const int MIN_LEAVE_MINUTES = 120;

        public bool RemoteWork { get; set; }

        public override string FirstField => FIELD_REASON;

        public RequestDSExceptionDialog():base(null,null)
        {

        }
        public RequestDSExceptionDialog(ChatId chatId, User from,bool remoteWork):base(chatId,from)
        {
            this.RemoteWork = remoteWork;
        }
        public override void SetServices(IServiceProvider services)
        {
        }
        public override FormDialogField GetFieldDef(string key)
        {
            switch(key)
            {
                case FIELD_REASON:
                    return new FormDialogField
                    {
                        Prompt=$"Enter reason",
                        NextField=d=>Task.FromResult(FIELD_FROM),
                    };
                case FIELD_FROM:
                    return new FormDialogField
                    {
                        Prompt = RemoteWork?"When will you start working remotely":"When will your leave start",
                        NextField = d => Task.FromResult(FIELD_TO),
                        ParseFunction=(b,t,c)=>
                        {
                            if (!DateTime.TryParse(t, out var dt) || dt<TGBot.NowDt())
                                return Task.FromResult(new ParseResult { Error = "Invalid date/time" });
                            return Task.FromResult(new ParseResult { Data = dt });
                        }
                    };
                case FIELD_TO:
                    return new FormDialogField
                    {
                        Prompt = RemoteWork ? "When the last day (or time) of your remote work" : "When is the last day (or time) of your leave",
                        NextField = null,
                        ParseFunction = (b, t, c) =>
                        {
                            var startDate = (DateTime)this.FieldData[FIELD_FROM].Val();
                            if (!DateTime.TryParse(t, out var dt) || dt.Subtract(startDate).TotalMinutes < MIN_LEAVE_MINUTES)
                                return Task.FromResult(new ParseResult { Error = "Invalid date/time" });
                            if (dt.Second != 0 || dt.Millisecond != 0)
                                return Task.FromResult(new ParseResult { Error = "Don't specify seconds" });
                            dt = new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0).AddSeconds(1);
                            return Task.FromResult(new ParseResult { Data = dt });
                        }
                    };
            }
            return null;
        }
        protected override Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            return base.OnCompleteAsync(bot, cancellationToken);
        }
    }
}
