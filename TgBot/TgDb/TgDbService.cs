using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using TgBot.SmartLedger;

namespace TgBot.TgDb
{
    partial class TgDbService : ServiceBase<TgBotDbContext>
    {
        private const int CHECKOUT_POLL_DELAY = 30000;
        private const int NOW_PAYMENTS_SECONDS = 300;
        private const int NOW_PAYMENTS_DELAY_MIL = 1000;
        private const int DIALOG_TIME_OUT_SECONDS = 3600;
        private const int CHECKOUT_TIME_OUT_SECONDS = 3 * 60 * 60;

        public TgDbService(TgBotDbContext db) : base(db)
        {
        }

        public void JoinedGroup(String botId, long chatId)
        {
            TransactNoReturn((db) => { DbOps.JoinedGroup(db, botId, chatId); });
        }
        public void LeftGroup(String botId, long chatId)
        {
            TransactNoReturn((db) => { DbOps.LeftGroup(db, botId, chatId); });
        }
        public List<JoinedTGGroup> GetAllJoinedTGGroups(String botId) => DbRead(db => DbOps.GetAllJoinedTGGroups(db, botId));


        public TgUserState GetUserState(string userID)
            => DbRead(db => DbOps.GetUserState(db, TGBot.BotToken, userID));

        public static void InitializePoller()
        {
            var t = PollPayments();
        }
        static CancellationTokenSource pollPaymentCT;
        public static void ResumePaymentPoll()
        {
            try
            {
                pollPaymentCT.CancelAfter(10);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error trying to resume payment poll");
                while (ex != null)
                {
                    Console.WriteLine($"{ex.Message}\n{ex.StackTrace}");
                    ex = ex.InnerException;
                }
            }
        }
        //static async Task TimeOutDialogStack(Telegram.Bot.TelegramBotClient botClient, String userID,CancellationToken cancellationToken)
        //{
        //    var s = new TgDbService();
        //    var state = s.GetUserState(userID);
        //    if (state == null || state.StackHead==null)
        //        return;
        //    var diag = state.StackHead;
        //    while(diag!=null)
        //    {
        //        var d = s.GetDialogItem(userID, diag.Value);
        //        await d.Deserialize().HandleCancel(botClient, cancellationToken);
        //        s.RemoveDialogItem(diag.Value);
        //        diag = d.Next;
        //    }
        //}
        static async Task PollPayments()
        {
            using (var serviceProvider = ServiceCollectionExtensions.CreateScope())
            {
                var s = serviceProvider.GetService<TgDbService>();
                var db = serviceProvider.GetService<TgBotDbContext>();
                pollPaymentCT = new CancellationTokenSource();
                while (true)
                {

                    List<TgUserState> payments;
                    var now = TGBot.NowDt();
                    payments = db.WFTelegramUserStates.Where(x => x.StackHead != null).ToList();

                    for (var i = payments.Count - 1; i >= 0; i--)
                    {
                        if (!payments[i].WaitingForPayment)
                        {

                            var seconds = now.Subtract(new DateTime(payments[i].LastUpdateTime)).TotalSeconds;
                            if (seconds > DIALOG_TIME_OUT_SECONDS)
                            {
                                s.ClearDialogStack(payments[i].TgUserID);
                            }
                            payments.RemoveAt(i);
                        }
                    }
                    DateTime latestDate;
                    if (payments.Any())
                    {
                        var maxTime = payments.First().LastUpdateTime;
                        foreach (var p in payments)
                        {
                            if (p.LastUpdateTime > maxTime)
                                maxTime = p.LastUpdateTime;

                            bool payment = false;
                            try
                            {
                                payment = await WBCheckOutAPI.CheckPayment(p.WbcCode);
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error trying to get payment status for {p.WbcCode}.\n{ex.Message}\n{ex.StackTrace}");
                                continue;
                            }
                            if (payment)
                            {
                                var c = new CancellationTokenSource();
                                await TGBot.HandleDialog(TGBot.Bot, null, p.WbcCode, p.TgUserID, c.Token);
                            }
                            else
                            {
                                var seconds = now.Subtract(new DateTime(p.LastUpdateTime)).TotalSeconds;
                                if (seconds > CHECKOUT_TIME_OUT_SECONDS)
                                {
                                    try
                                    {
                                        await s.CancelCheckOutCodeAsync(p.TgUserID, p.WbcCode);
                                        s.ClearDialogStack(p.TgUserID);
                                        try
                                        {
                                            await TGBot.Bot.SendTextMessageAsync(long.Parse(p.TgUserID), $"Your checkout code {p.WbcCode} is canceled because you didn't pay it.");
                                        }
                                        catch (Exception ex)
                                        {
                                            TGBot.LogException("Error trying to notify checkout timeout", ex);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        TGBot.LogException("Error trying to cancel timed out checkout", ex);
                                    }
                                }
                            }
                        }
                        latestDate = new DateTime(maxTime);
                    }
                    else
                        latestDate = TGBot.NowDt();

                    try
                    {
                        pollPaymentCT = new CancellationTokenSource();
                        int delay;
                        if (payments.Count > 0 && now.Subtract(latestDate).TotalSeconds < NOW_PAYMENTS_SECONDS)
                            delay = NOW_PAYMENTS_DELAY_MIL;
                        else
                            delay = CHECKOUT_POLL_DELAY;
                        await Task.Delay(delay, pollPaymentCT.Token);
                        Console.WriteLine($"Payment polling wokeup after {delay} milliseconds delay");
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Resuming payment polling because of wait cancelation");
                    }

                }
            }
        }

        private async Task CancelCheckOutCodeAsync(string tgUserID, string wbcCode)
        {
            await WBCheckOutAPI.CancelCheckOut(wbcCode);
            try
            {
                TransactNoReturn(db =>
                {
                    var state = DbOps.GetUserState(db, TGBot.BotToken, tgUserID);
                    state.WaitingForPayment = false;
                    DbOps.SetUserState(db, TGBot.BotToken, state);
                });
            }
            catch (Exception ex)
            {
                TGBot.LogException($"System in inconsistent state. Checkout code {wbcCode} is canceled in WeBirr but we failed to update user state data", ex);
            }
        }

        internal void SaveDialogState(WFDialogItem stack, IBotDialog dialog)
        {
            TransactNoReturn((db) =>
            {
                DbOps.SaveDialogState(db, stack, dialog);
            });
        }

        public TgUserState GetOrCreateUser(String userId)
        {
            var existing = DbRead(db => DbOps.GetUserState(db, TGBot.BotToken, userId));
            if (existing != null)
                return existing;
            return TransactReturn<TgUserState>((db) =>
            {
                DbOps.SetUserState(db,
                    TGBot.BotToken,
                    new TgUserState
                    {
                        TgUserID = userId
                    }
                    );
                return DbOps.GetUserState(db, TGBot.BotToken, userId);
            });
        }
        public WFDialogItem GetDialogItem(String tgUserId, Guid diagId)
        {
            return DbOps.GetDialogItem(db, diagId);
        }
        public Guid PushDialog(String tgUserId, IBotDialog dialog)
        {
            var ret=TransactReturn<Guid>((db) =>
            {
                return DbOps.PushDialog(db, TGBot.BotToken, tgUserId, dialog);
            });
            return ret;
        }
        public void RemoveDialogItem(Guid dialog)
        {
            TransactNoReturn((db) =>
            {
                DbOps.RemoveDialogItem(db, dialog);
            });

        }

        public int ClearDialogStack(String tgUserId)
        {
            return TransactReturn<int>((db) =>
            {
                return DbOps.ClearDialogStack(db, TGBot.BotToken, tgUserId);
            });
        }



    }
}
