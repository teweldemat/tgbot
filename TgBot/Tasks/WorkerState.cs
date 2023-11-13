using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Telegram.Bot.Types;
using TgBot.SmartLedger;
using TgBot.TgDb;

namespace TgBot.Tasks
{
    public class WorkerState
    {
        const double PING_DURATION_SECONDS = 5;
        const double PING_WAIT_ACTIVE_DIAG_SECONDS = 10 * 60; //ten minutes
        const double ON_DUTY_NOTFICATION_INTERVAL = 30;
        const double NOTIFY_AFTER_CHECKIN_TIME_MINUTES = 5;
        const double NOTIFY_ETA_CHECKIN_TIME_MINUTES = 5;
        public const double IN_SHIFT_TOLLERANCE_MINUTES = 60;

        public const double LAST_NOTIFY_DAYS = 1;
        public const double MIN_LEAVE_NOTIFIABLE_LEAVE = 1.5;
        public const double MIN_ADVANCE_NOTIFY_DAYS = 5;
        public const double ADVANCE_NOTIFY_DAYS = 3;
        const double PLANED_TASKS_PROMPT_INTERVAL_MINUTES = 1 * 60;

        public String UserId { get; set; }
        public long? LastPingTime { get; set; } = null;
        public long? LastOnDuytNotifcationTime { get; set; } = null;


        [JsonIgnore]
        Telegram.Bot.TelegramBotClient bot = null;
        [JsonIgnore]
        System.Threading.CancellationToken cancellationToken;
        [JsonIgnore]
        ChatId chatId = null;
        [JsonIgnore]
        User user = null;
        public WorkerState()
        {
            UserId = null;
        }
        public WorkerState SetBotContext(Telegram.Bot.TelegramBotClient bot, ChatId chatId, User from, System.Threading.CancellationToken cancellationToken)
        {
            this.bot = bot;
            this.cancellationToken = cancellationToken;
            this.chatId = chatId;
            this.user = from;
            return this;
        }
        public WorkerState(string userId)
        {
            this.UserId = userId;
        }
        class DSPingResult
        {
            public bool onDuty = false;
            public bool notified = false;
            public OnDutyCheck lastCheck = null;
        }
        async Task<DSPingResult> PingDutySchedule(TaskDbService service, TgDbService tgService,long time)
        {
            var dsException = service.GetDSException(this.UserId, time);
            var user = service.GetUserProfile(this.UserId);
            if (dsException != null && !dsException.remoteWork) //if on leave, we will check if we need to 
            {
                var totalDays = new TimeSpan(dsException.ToTime - dsException.FromTime).TotalDays;
                var daysToComeBack = new TimeSpan(dsException.ToTime - time).TotalDays;
                if (daysToComeBack < LAST_NOTIFY_DAYS && totalDays > MIN_LEAVE_NOTIFIABLE_LEAVE)
                {
                    var text = $"Hi {user.FullName},\nI hope you are doing good. This is a gentle reminder that {LAST_NOTIFY_DAYS} day is left from your leave. See you soon!";
                    await this.bot.SendTextMessageAsync(this.chatId, text, cancellationToken: this.cancellationToken);
                    return new DSPingResult { notified = true };
                }
                if (daysToComeBack < ADVANCE_NOTIFY_DAYS && totalDays > MIN_ADVANCE_NOTIFY_DAYS)
                {
                    var text = $"Hi {user.FullName},\nI hope you are doing good. This is a gentle reminder that {ADVANCE_NOTIFY_DAYS} days are left from your leave. See you soon!";
                    await this.bot.SendTextMessageAsync(this.chatId, text, cancellationToken: this.cancellationToken);
                    return new DSPingResult { notified = true }; ;
                }
                return new DSPingResult();
            }
            DutyStation dutyStation = null;
            var dutySchedule = service.GetDutySchedule(UserId) as IDutyStationSechdule;
            DutyTimeSpan span = null;
            if (dutySchedule != null)
            {
                span = dutySchedule.GetOnCurrentOnDutySpan(service, UserId, time);
                if (span != null)
                {
                    dutyStation = service.GetUserDutyStation(this.UserId, time);
                }
            }
            if (dutyStation == null) //he is not on duty. Probably a subcontractor or a consultant
                return new DSPingResult();
            var dt = new DateTime(time);

            if (new TimeSpan(time - span.StartTime(dt)).TotalMinutes < NOTIFY_AFTER_CHECKIN_TIME_MINUTES) //if it only few minutes since the start of the current duty time span
                return new DSPingResult();

            var lastCheck = service.GetLastOnDutyCheck(UserId);
            if (lastCheck != null)
            {
                if (!span.InShift(lastCheck.Time, TimeSpan.FromMinutes(IN_SHIFT_TOLLERANCE_MINUTES).Ticks))
                    lastCheck = null;
            }

            if (lastCheck != null)
            {
                if (lastCheck.CheckType == OnDutyCheckType.NotComing) //if already notified absence
                    return new DSPingResult() { lastCheck = lastCheck };

                if (lastCheck.CheckType == OnDutyCheckType.DontDisturb) //if asked to be not disturbed
                    return new DSPingResult() { lastCheck = lastCheck };

                if (lastCheck.CheckType == OnDutyCheckType.CheckOut) //if already left
                    return new DSPingResult() { lastCheck = lastCheck };

                if (lastCheck.CheckType == OnDutyCheckType.RunningLate
                    || lastCheck.CheckType == OnDutyCheckType.TakingBreak
                    || lastCheck.CheckType == OnDutyCheckType.OffDutyStationAssignment
                    || lastCheck.CheckType == OnDutyCheckType.DontDisturb
                    ) //if temporarily unavialable
                {
                    if (new TimeSpan(time - lastCheck.Eta.Value).TotalMinutes < NOTIFY_ETA_CHECKIN_TIME_MINUTES) //if it is five minutes after the estimated time of arrival
                        return new DSPingResult() { lastCheck = lastCheck };
                }

                if (lastCheck.CheckType == OnDutyCheckType.CheckIn)
                {
                    return new DSPingResult { onDuty = true, lastCheck = lastCheck };
                }
            }
            await TGBot.PushDialog(this.UserId, new OnDutyCheckDialog(service,tgService, this.chatId, this.user, false), this.cancellationToken);
            return new DSPingResult { onDuty = true, notified = true };
        }


        long? lastNoPlannedTasks = null;
        async Task<bool> PingTask(TaskDbService service,TgDbService tgService, DSPingResult pingDSRes, long time)
        {
            if (pingDSRes.lastCheck != null && pingDSRes.lastCheck.CheckType == OnDutyCheckType.DontDisturb)
                return false;
            var tasks = service.GetUserActiveTasks(UserId, TaskUserRole.Worker);
            var plannedTasks = new List<MisTask>();
            var activeTasks = new List<MisTask>();
            var awaitedTasks = new Dictionary<MisTask, List<MisTask>>();
            foreach (var task in tasks)
            {
                if (task.Status == TaskStatus.Planned)
                    plannedTasks.Add(task);
                else if (task.Status == TaskStatus.Started)
                    activeTasks.Add(task);
                var wt = service.GetWaitingTasks(task.Id);
                if (wt.Count > 0)
                    awaitedTasks.Add(task, wt);
            }
            if (activeTasks.Count == 0) //no active task
            {
                if (plannedTasks.Count > 0) //if there are planned tasks prompt him to start one of the planned tasks
                {
                    if (lastNoPlannedTasks == null && new TimeSpan(time - lastNoPlannedTasks.Value).TotalMinutes > PLANED_TASKS_PROMPT_INTERVAL_MINUTES)
                    {
                        plannedTasks.Sort((x, y) => x.PlannedStartTime.Value.CompareTo(y.PlannedStartTime.Value));
                        await TGBot.PushDialog(this.user.Id.ToString(), new StartPlannedTask(service,tgService, plannedTasks, this.chatId, this.user), this.cancellationToken);
                        lastNoPlannedTasks = time;
                        return true;
                    }
                }
                else if (plannedTasks.Count == 0) //if there is no planned task, offer to create new task
                {
                    if (pingDSRes.onDuty)
                    {
                        await TGBot.PushDialog(this.user.Id.ToString(), new JoinTaskMenuDialog(service,tgService,
                            "You don't have any task, what do you want to do?",
                            this.chatId,
                            this.user), cancellationToken);
                    }
                }
            }
            return true;
        }

        public async Task Ping(long time)
        {
            var current = TGBot.CurrentDialog(this.UserId);
            if (current != null && new TimeSpan(time - current.Time).TotalSeconds < PING_WAIT_ACTIVE_DIAG_SECONDS)
                return;
            if (LastPingTime != null && new TimeSpan(time - LastPingTime.Value).TotalSeconds < PING_DURATION_SECONDS)
                return;
            using (var serviceProvider = ServiceCollectionExtensions.CreateScope())
            {
                var service = serviceProvider.GetService<TaskDbService>();
                var tgService= serviceProvider.GetService<TgDbService>();

                var pingRes = await PingDutySchedule(service, tgService,time);
                if (!pingRes.notified)
                {
                    await PingTask(service,tgService, pingRes, time);
                }
            }
        }
    }
}
