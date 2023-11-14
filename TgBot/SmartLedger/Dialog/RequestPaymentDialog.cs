using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.TgDb;

namespace TgBot.SmartLedger.Dialog
{
    public enum PaymentType
    {
        Payment,
        Deposit,
        Transfer
    }
    public class RequestPaymentDialog : FormDialog
    {
        const string FIELD_AMOUNT = "Amount";
        const string FIELD_NOTE = "Note";
        const string FIELD_TO = "To";
        const string FIELD_ATTACHMENT_PREFIX = "Attachment";
        const string MORE_ATTACHMENT_PREFIX = "More Attachment";
        public PaymentType PaymentType { get; set; }
        public Guid? RestartPayment { get; set; }
        
        
        SmartLedgerService service;
        TgDbService tgService;
        public RequestPaymentDialog(SmartLedgerService service, TgDbService tgService, ChatId chatId, User from, PaymentType paymentType = PaymentType.Payment, Guid? restartPayment = null) 
            : base(chatId, from)
        {
            PaymentType = paymentType;
            RestartPayment = restartPayment;
            this.service = service;
            this.tgService = tgService;
        }

        public override void SetServices(IServiceProvider services)
        {
            service = services.GetService<SmartLedgerService>();
            tgService = services.GetService<TgDbService>();
        }

        public long Amount() => (long)FieldData[FIELD_AMOUNT].Val();
        public string Note() => (string)FieldData[FIELD_NOTE].Val();
        public string PaymenTypeText(bool caps = false) => caps ? PaymentType.ToString() : PaymentType.ToString().ToLower();
        public override string FirstField => FIELD_AMOUNT;
        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_AMOUNT:
                    return new FormDialogField
                    {
                        Prompt = "What amount are you requesting?",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_NOTE),
                        ParseFunction = (bot, t, c) =>
                          {
                              if (double.TryParse(t, out var d))
                              {
                                  if (d > 0)
                                  {
                                      return Task.FromResult(new ParseResult
                                      {
                                          Data = IntData.toIntMoney(d)
                                      });
                                  }
                              }
                              return Task.FromResult(new ParseResult
                              {
                                  Error = "Invalid amount"
                              });
                          }
                    };
                case FIELD_NOTE:
                    return new FormDialogField
                    {
                        Prompt = $"What is the purpose of the {PaymenTypeText()}?",
                        FieldType = FieldType.Text,
                        NextField = d => Task.FromResult(FIELD_TO)
                    };
                case FIELD_TO:
                    if (PaymentType == PaymentType.Transfer)
                    {
                        return new FormDialogField
                        {
                            Prompt = Program.lm.payment_request_subject_question(PaymentType),
                            FieldType = FieldType.Choices,
                            Choices = service.GetCashAccounts().Select(x =>
                                    new FormFieldChoiceItem(x.Id.ToString(), x.Name)).ToList(),
                            NextField = d => Task.FromResult(FIELD_ATTACHMENT_PREFIX + "0"),
                            ParseFunction = (c, t, d) =>
                            {
                                return Task.FromResult(new ParseResult { Data = Guid.Parse(t) });
                            }
                        };
                    }
                    else
                    {
                        return new FormDialogField
                        {
                            Prompt = Program.lm.payment_request_subject_question(PaymentType),
                            FieldType = FieldType.Text,
                            NextField = d => Task.FromResult(FIELD_ATTACHMENT_PREFIX + "0")
                        };
                    }
            }
            if (key.StartsWith(FIELD_ATTACHMENT_PREFIX))
            {
                int index = int.Parse(key.Substring(FIELD_ATTACHMENT_PREFIX.Length));
                return new FormDialogField
                {
                    Prompt = "Upload an attachment for your request",
                    FieldType = FieldType.Attachment,
                    NextField = d => Task.FromResult(MORE_ATTACHMENT_PREFIX + index)
                };
            }
            if (key.StartsWith(MORE_ATTACHMENT_PREFIX))
            {
                int index = int.Parse(key.Substring(MORE_ATTACHMENT_PREFIX.Length));
                return new FormDialogField
                {
                    Prompt = "Do you want to add more attachment?",
                    FieldType = FieldType.Choices,
                    Choices = new[] { new FormFieldChoiceItem("YES"), new("NO") },
                    NextField = d =>
                    {
                        var ret = Task.FromResult("YES".Equals(d[key].Val()) ? FIELD_ATTACHMENT_PREFIX + (index + 1) : null);
                        return ret;
                    }
                };
            }
            return null;
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var guid = service.CreatePaymentFlow(
                from.Id.ToString(),
                Note(),
                PaymentType == PaymentType.Deposit ? -Amount() : Amount(),
                PaymentType == PaymentType.Transfer ? null : (string)FieldData[FIELD_TO].Val(),
                PaymentType == PaymentType.Transfer ? (Guid)base.FieldData[FIELD_TO].Val() : null,
                Pictures(FIELD_ATTACHMENT_PREFIX).Select(x => new WorkItemPicture
                {
                    Image = x.Image,
                    ImgeMime = x.ImageMime,
                    LinkedImage = x.ContentLink,
                    LinkedImageType = "1"
                }).ToList(),
                RestartPayment
            );
            var payment = service.GetPayment(guid);
            try
            {
                await bot.SendTextMessageAsync(chatId, "The request is registered");
            }
            catch (Exception ex)
            {
                TGBot.LogException("CRITICAL: Error send confirmation for request creation", ex);
            }

            try
            {
                var state = tgService.GetUserState(from.Id.ToString());
                await SmartLedgerBot.NotifyGroups(bot, tgService, $"{TGBot.FullName(from)} requested {System.Web.HttpUtility.HtmlEncode(IntData.toString(Amount()))} Birr"
                    + $"\n {SmartLedgerBot.PaymentLink(payment.Id, payment.Reference)}", true, cancellationToken);
                var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
                if (config != null && config.Checker1 != null)
                {
                    var user = new User();
                    user.Id = long.Parse(config.Checker1);
                    user.FirstName = "Unknown";
                    await TGBot.PushDialog(config.Checker1, new PaymentDetailDialog(service, tgService, config.Checker1, user, guid), cancellationToken);
                }
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error notifying groups and checkers of creation", ex);
            }
            return DialogResult.Terminated;
        }

    }
}
