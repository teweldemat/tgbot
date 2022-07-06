using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot.Types;

namespace TgBot.SocialLedger
{
    public class RequestTransactionDialog:FormDialog
    {
        const string FIELD_FRIEND = "Friend";
        const string FIELD_TYPE = "Type";
        const string FIELD_AMOUNT = "Amount";
        const string FIELD_CURRENCY = "Currency";
        const string FIELD_DESCRIPTION = "Description";
        public RequestTransactionDialog(ChatId chatId, User from,bool I) : base(chatId, from)
        {
            
        }

        public override string FirstField => FIELD_FRIEND;

        public override FormDialogField GetFieldDef(string key)
        {
            switch(key)
            {
                case FIELD_TYPE:
                    return new FormDialogField
                    {
                        Choices=new [] {new FormFieldChoiceItem("PAY","I Lent My Friend"), new FormFieldChoiceItem("RECEIVE", "My Friend Lent Me") }
                    };
            }
            return null;
        }
    }
}
