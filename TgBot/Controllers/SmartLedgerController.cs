using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using TgBot.SmartLedger;
using static TgBot.FormDialog;

namespace TgBot.Controllers
{
    public class APIReturn
    {
        public string error { get; set; } = null;
        public object res { get; set; } = null;
    }

    public class SmartLedgerController : Controller
    {

        
        [HttpGet]
        [Route("/sl/file/")]
        public IActionResult GetFile(String id)
        {
            try
            {
                var service = new SmartLedger.SmartLedgerService();
                var picData = service.GetPicture(Guid.Parse(id));
                if (picData == null || picData.Image == null)
                    throw new Exception("Attachment doesn't exist");
                return base.File(picData.Image, picData.ImgeMime,id+ TGBot.MimeToExtension(picData.ImgeMime));
            }
            catch (Exception ex)
            {
                Program.LogException("Error to call: GetPicture", ex);
                return StatusCode(500);

            }
        }
        
        public static bool isPicture(String mime)
        {
            return "image/jpeg".Equals(mime, StringComparison.OrdinalIgnoreCase)
                || "image/jpg".Equals(mime, StringComparison.OrdinalIgnoreCase)
                || "image/png".Equals(mime, StringComparison.OrdinalIgnoreCase);
        }

        const int PREVIEW_WDITH = 256;
        const int PREVIEW_HEGHT = 256;
        [HttpGet]
        [Route("/sl/preview/")]
        public IActionResult GetPicturePreview(String id)
        {
            try
            {
                var service = new SmartLedger.SmartLedgerService();
                var picData = service.GetPicture(Guid.Parse(id));
                if (picData == null || picData.Image == null)
                    throw new Exception("Picture doesn't exist");
                var data = GetPicturePreview(picData.AsContentData(), out var mime);
                return base.File(data, mime);
            }
            catch (Exception ex)
            {
                Program.LogException("Error to call: GetPicture", ex);
                return StatusCode(500);

            }
        }

        public static byte[] GetPicturePreview(ContentData picData,out String mime)
        {
            if (picData == null || picData.Image == null)
                throw new Exception("Picture doesn't exist");
            if (!isPicture(picData.ImageMime))
                throw new Exception("Attachment is not a picture");
            byte[] outData;
            using (var ms = new System.IO.MemoryStream(picData.Image))
            {
                using (var outputMs = new System.IO.MemoryStream())
                {
                    var image = Image.Load(ms);

                    var sampler = KnownResamplers.Lanczos3;
                    if (PREVIEW_HEGHT * image.Width > PREVIEW_WDITH * image.Height)
                    {
                        var ofs = (PREVIEW_HEGHT - image.Height * PREVIEW_WDITH / image.Width) / 2;
                        image.Mutate(x => x.Resize(PREVIEW_WDITH, PREVIEW_HEGHT, sampler, new Rectangle(0, 2 * ofs, PREVIEW_WDITH, PREVIEW_HEGHT - 2 * ofs), false));
                    }
                    else
                    {
                        var ofs = (PREVIEW_WDITH - image.Width * PREVIEW_HEGHT / image.Height) / 2;
                        image.Mutate(x => x.Resize(PREVIEW_WDITH, PREVIEW_HEGHT, sampler, new Rectangle(ofs, 0, PREVIEW_WDITH - 2 * ofs, PREVIEW_HEGHT), false));
                    }
                    image.Save(outputMs, new SixLabors.ImageSharp.Formats.Png.PngEncoder());
                    outData = outputMs.ToArray();
                }
                mime = "image/png";
                return outData;;
            }
        }
        public class PaymentViewModel
        {
            public class WorkItemAttachment
            {
                public String PictureId { get; set; }
                public bool IsPicture { get; set; }
            }
            public class WorkItem
            {
                public DateTime Date { get; set; }
                public String User { get; set; }
                public String Action { get; set; }
                public List<WorkItemAttachment> Attachments { get; set; }
                public String Note { get; set; }
                public List<PaymentSource> SetSources { get; set; }
            }
            public Payment Payment { get; set; }
            public String StatusString { get; set; }
            public List<WorkItem> WorkItems { get; set; }
            public String CompanyName { get; set; }
        }

        [HttpGet]
        [Route("/sl/payment/")]
        public IActionResult GetPaymentDetail(String id)
        {
            try
            {
                var service = new SmartLedger.SmartLedgerService();
                var payment = service.GetPayment(Guid.Parse(id));
                if (payment == null)
                    throw new Exception("Invalid payment id:" + id);
                var wi = payment.WorkItemHead;
                var sources = service.GetPaymentSources(payment.Id);
                string statusString = "";
                var list = new List<PaymentWorkItem>();
                service.ForEachWorkItem(payment.Id, p =>
                 {
                     list.Insert(0, p);
                     return true;
                 });
                var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
                if (config == null)
                    throw new Exception("Configuration not set");
                var workItems = new List<PaymentViewModel.WorkItem>();
                foreach (var w in list)
                {
                    var item = new PaymentViewModel.WorkItem
                    {
                        Date = new DateTime(w.Time),
                        Attachments = service.GetWorkItemPictures(w.Id)
                       .Select(x => new PaymentViewModel.WorkItemAttachment
                       {
                           IsPicture = isPicture(x.ImgeMime),
                           PictureId = x.Id.ToString(),
                       }).ToList(),
                        Note = w.Note,
                        User = service.GetUserProfile(w.UserId).FullName,
                    };
                    workItems.Add(item);
                    statusString = PaymentWorkItem.StatusString(w.WorkType, payment);
                    item.Action = PaymentWorkItem.GetActionString(w, payment,()=>sources,()=>config,x=>service.GetCashAccount(x));
                }
                var model = new PaymentViewModel
                {
                    Payment = payment,
                    StatusString = statusString,
                    WorkItems = workItems,
                    CompanyName = service.GetEntity().Name
                };
                return View("/Views/SL/PaymentView.cshtml", model);
            }
            catch (Exception ex)
            {
                return View("/Views/ErrorView.cshtml", ex);
            }
        }

        public class LedgerViewModel
        {
            public class LedgerRecord
            {
                public String Date { get; set; }
                public String DebitAmount { get; set; }
                public String CreditAmount { get; set; }
                public String Balance { get; set; }
                public String Remark { get; set; }
                public String PrLink { get; set; }
            }
            public String BeginningBalance { get; set; }
            public String AccountName { get; set; }
            public List<LedgerRecord> Ledger { get; set; } = new List<LedgerRecord>();
            public LedgerRecord EndingBalance { get; set; }
            public String CompanyName { get; set; }

        }
        [HttpGet]
        [Route("/sl/ledger/")]
        public IActionResult GetAccountLedger(String accountId)
        {
            try
            {
                var service = new SmartLedger.SmartLedgerService();
                var account = service.GetCashAccount(Guid.Parse(accountId));
                if (account == null)
                    throw new Exception("Invalid account id:" + accountId);
                var ledger = service.GetLedger(account.Id, null, null);

                long balance = ledger.begningBalance;
                long totalDebit = 0;
                long totalCredit = 0;
                var model = new LedgerViewModel();
                model.BeginningBalance = IntData.toString(balance, "");
                foreach (var w in ledger.record)
                {

                    balance += w.entry.Amount;
                    var debit = w.entry.Amount > 0 ? w.entry.Amount : 0;
                    var credit = w.entry.Amount < 0 ? -w.entry.Amount : 0;
                    model.Ledger.Add(new LedgerViewModel.LedgerRecord
                    {
                        Date = IntData.toDateString(w.entry.Time, ""),
                        Balance = IntData.toString(balance, ""),
                        DebitAmount = IntData.toString(debit, ""),
                        CreditAmount = IntData.toString(credit, ""),
                        Remark = w.entry.Remark,
                        PrLink = w.transaction.Payment == null ? null : $"{SmartLedgerBot.WebLinkBaseUrl}/sl/payment?id={w.transaction.Payment}"
                    }); ;
                    totalDebit += debit;
                    totalCredit += credit;
                }
                model.EndingBalance = new LedgerViewModel.LedgerRecord
                {
                    Balance = IntData.toString(balance, ""),
                    DebitAmount = IntData.toString(totalDebit, ""),
                    CreditAmount = IntData.toString(totalCredit, ""),
                };
                model.CompanyName = service.GetEntity().Name;
                model.AccountName = account.Name;
                return View("/Views/SL/LedgerView.cshtml", model);
            }
            catch (Exception ex)
            {
                return View("/Views/ErrorView.cshtml", ex);
            }
        }

        public class SummaryViewModel
        {
            public class AccountViewModel
            {
                public SmartLedger.CashAccount Account { get; set; }
                public String PayerName { get; set; }
                public string DepositorName { get; set; }
                public String Balance { get; set; }
                public String LedgerLink { get; set; }

            }
            public class RequestViewModel
            {
                public SmartLedger.Payment Payment { get; set; }
                public String StatusText { get; set; }
            }

            public String CompanyName { get; set; }
            public IEnumerable<AccountViewModel> Accounts { get; set; }
            public String TotalAccount { get; set; }
            public IEnumerable<RequestViewModel> Payments { get; set; }
            public String TotalPayment { get; set; }
            public IEnumerable<RequestViewModel> Deposits { get; set; }
            public String TotalDeposits { get; set; }
            public IEnumerable<RequestViewModel> Transfer { get; set; }
            public String TotalTransfers { get; set; }
        }
        [HttpGet]
        [Route("/sl/summary/")]
        public IActionResult GetSummary()
        {
            try
            {
                var service = new SmartLedger.SmartLedgerService();
                var request = service.GetOpenPayments(0,-1,out var N,true);
                var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
                Func<IEnumerable<Payment>, Tuple<IEnumerable<SummaryViewModel.RequestViewModel>, String>>
                    summerize = x =>
                      {
                          long total = 0;
                          var list = x.Select(y => {
                              total += y.PositiveAmount;
                              return new SummaryViewModel.RequestViewModel
                              {

                                  Payment = y,
                                  StatusText = y.HeadType == null ? "" : PaymentWorkItem.StatusString(y.HeadType.Value, y)
                              };
                              }).ToList();
                          return new Tuple<IEnumerable<SummaryViewModel.RequestViewModel>, string>(
                          
                              item1:list,
                              item2:IntData.toString(total)
                              );;
                              

                          };
                var payments = summerize(request.Where(x => !x.IsDeposit && !x.IsTransferTransaction));
                var deposits= summerize(request.Where(x => x.IsDeposit));
                var transfers= summerize(request.Where(x => x.IsTransferTransaction));
                
                var cashAccounts = service.GetCashAccounts();
                long totalBalance = cashAccounts.Sum(x => x.Balance);

                var model = new SummaryViewModel
                {
                    
                    Accounts=cashAccounts.Select(x=> {
                        var payer = service.GetUserProfile(config.GetPayer(x.Id, false));
                        var depositor= service.GetUserProfile(config.GetPayer(x.Id, true));
                        return new SummaryViewModel.AccountViewModel
                        {
                            Account = x,
                            PayerName = payer == null ? null : payer.FullName,
                            DepositorName = depositor == null ? null : depositor.FullName,
                            Balance = IntData.toString(x.Balance, zeroAmount: "-"),
                            LedgerLink= SmartLedgerBot.GetLedgerLink(x.Id)
                        };
                    }),
                    TotalAccount=IntData.toString(totalBalance,"-"),
                    CompanyName=service.GetEntity().Name,
                    Deposits= deposits.Item1,
                    TotalDeposits=deposits.Item2,
                    Payments=payments.Item1,
                    TotalPayment=payments.Item2,
                    Transfer=transfers.Item1,
                    TotalTransfers=transfers.Item2,
                };
                return View("/Views/SL/SummaryView.cshtml", model);
            }
            catch (Exception ex)
            {
                return View("/Views/ErrorView.cshtml", ex);
            }
        }
    }


}
