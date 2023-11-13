using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TgBot.SocialLedger
{
    public class ProcessRequestDialog:FormDialog
    {
        const String FIELD_ACCEPT = "Accept";
        public Guid RequestId { get; set; }

        public override string FirstField => FIELD_ACCEPT;
        SocialLedgerDbService service;
        
        public ProcessRequestDialog(SocialLedgerDbService service,ChatId chatId,User from,Guid requestId):base(chatId,from)
        {
            this.RequestId = requestId;
            this.service = service;
        }
        public override void SetServices(IServiceProvider services)
        {
            this.service = services.GetService<SocialLedgerDbService>();
        }
        public override FormDialogField GetFieldDef(string key)
        {
            switch(key)
            {
                case FIELD_ACCEPT:
                    var request = service.GetLedgerPair(RequestId);
                    if (request == null)
                        throw new UserFriendlyError("Request doesn't exisit");
                    if (request.AcceptedOn!=null || request.DeclinedOn!=null)
                        throw new UserFriendlyError("Request can't be accepted");

                    return new FormDialogField
                    {
                        Prompt=$"{request.FullName} requested to keep social ledger with you. Do you accept?",
                        FieldType=FieldType.Choices,
                        Choices=new [] {new FormFieldChoiceItem("YES","Accept"), new FormFieldChoiceItem("NO","Decline") }
                    };
            }
            return null;
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var accepted = "YES".Equals(this.FieldData[FIELD_ACCEPT].Val());
            var request = service.GetLedgerPair(RequestId);
            if (accepted)
            {
                service.AcceptRequest(RequestId, from.Id.ToString());
                await bot.SendTextMessageAsync(chatId, $"Ok, we will notify {request.FullName}.",cancellationToken:cancellationToken);
                await bot.SendTextMessageAsync(long.Parse(request.UserOne), $"{TGBot.FullName(from)} accepted your request.", cancellationToken: cancellationToken);
                return DialogResult.Terminated;
            }
            else
            {
                service.DeclineRequest(RequestId, from.Id.ToString());
                await bot.SendTextMessageAsync(chatId, $"Alright, the request is canceled.", cancellationToken: cancellationToken);
                await bot.SendTextMessageAsync(long.Parse(request.UserOne), $"{TGBot.FullName(from)} declined your request.", cancellationToken: cancellationToken);
                return DialogResult.Terminated;
            }

        }
    }
}
