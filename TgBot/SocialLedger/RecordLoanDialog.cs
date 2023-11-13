using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace TgBot.SocialLedger
{
    public class RecordLoanDialog:FormDialog
    {
        const String FIELD_FRIEND = "Friend";
        const String FIELD_AMOUNT = "Amount";
        const String FIELD_REMARK = "Remark";
        string FIELD_ATTACHMENT_PREFIX = "Attachment";
        string MORE_ATTACHMENT_PREFIX = "More Attachment";
        public RecordLoanDialog(ChatId chatId, User from) : base(chatId, from)
        {

        }
        public override void SetServices(IServiceProvider services)
        {
        }
        public override string FirstField => FIELD_FRIEND;

        public override FormDialogField GetFieldDef(string key)
        {
            throw new NotImplementedException();
        }
    }
}
