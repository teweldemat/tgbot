using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.SmartLedger;

namespace TgBot.Tasks
{
    public class OnDutyCheckDialog : FormDialog
    {
        const string FIELD_CHECK_TYPE = "CheckType";
        
        const string FIELD_ABSENT_REASON = "AbsentReason";
        const string FIELD_LATE_REASON = "Reason";
        const string FIELD_BREAK_REASON = "BreakReason";
        const string FIELD_DD_REASON = "DDReason";
        const string FIELD_OO_REASON = "OOReason";

        const string FIELD_LATE_TIME = "LateTime";
        const string FIELD_BREAK_TIME = "BreakTime";
        const string FIELD_DD_TIME = "DDTime";
        const string FIELD_OO_TIME = "OOTime";
        public bool SelfReport { get; set; }
        
        public OnDutyCheckDialog():base(null,null)
        {

        }

        [JsonIgnore]
        bool _stateLoaded = false;
        [JsonIgnore]
        DutyTimeSpan _span;
        [JsonIgnore]
        UserDutyStationException _exception;
        [JsonProperty]
        UserDutyStationStatus _ddStatus;
        [JsonIgnore]
        public DutyTimeSpan Span
        {
            get
            {
                LoadState();
                return _span; ;
            }
        }
        [JsonIgnore]
        public UserDutyStationException DSException
        {
            get
            {
                LoadState();
                return _exception;
            }
        }
        [JsonIgnore]
        public UserDutyStationStatus DsStatus
        {
            get
            {
                LoadState();
                return _ddStatus;
            }
        }
        void LoadState()
        {
            if (_stateLoaded || from == null)
                return;

            var now = TGBot.Now();
            var service = new TaskDbService();
            var userId = from.Id.ToString();
            var schedule = service.GetDutySchedule(userId) as IDutyStationSechdule;
            if (schedule != null)
                _span = schedule.GetOnCurrentOnDutySpan(userId, now);
            _exception = service.GetDSException(userId, now);
            _ddStatus = service.GetUserDutyStationStatus(userId, now);
            _stateLoaded = true;
        }
        public bool ScheduledDutyTime => this.Span != null && (this.DSException == null || this.DSException.remoteWork);
        
        public OnDutyCheckDialog(ChatId chatId,User from, bool selfReport):base(chatId,from)
        {
            this.SelfReport = selfReport;
        }

        public override string FirstField
        {
            get
            {
                if (DsStatus == null) //if duty station not assigned do nothing
                    return null;
                if (this.DSException != null && !this.DSException.remoteWork) //if on leave do nothing
                {
                    return null;
                }
                return FIELD_CHECK_TYPE;
            }
        }

        public override FormDialogField GetFieldDef(string key)
        {
            var now = TGBot.NowDt();
            var service = new TaskDbService();
            var schedule = service.GetDutySchedule(from.Id.ToString()) as IDutyStationSechdule;
            DutyTimeSpan span = null;
            if(schedule!=null)
            {
                span = schedule.GetOnCurrentOnDutySpan(this.from.Id.ToString(), now.Ticks);
            }
            TryParseInputDelegate TimeParseFunction = (b, t, c) =>
             {
                 int sep;
                 if ((sep = t.IndexOf(":")) == -1)
                 {
                     if (int.TryParse(t, out var min) && min > 0)
                     {
                         return Task.FromResult(new ParseResult { Data = min });
                     }
                 }
                 else
                 {
                     if (int.TryParse(t.Substring(0, sep), out var hr)
                         && int.TryParse(t.Substring(sep + 1), out var min)
                         && hr >= 0
                         && min >= 0
                         && hr * 60 + min > 0
                         )
                     {
                         return Task.FromResult(new ParseResult { Data = hr * 60 + min });
                     }
                 }
                 return Task.FromResult(new ParseResult { Error = "Sorry i don't understand what you typed.\nExamples of correct input:\n For five minutes enter 5\nFor one hour and thirty mintues enter 1:30" });
             };

            switch (key)
            {
                case FIELD_CHECK_TYPE:
                    var choices = new List<FormFieldChoiceItem>();
                    var notAtWork = DsStatus.CheckInTime == null
                        && DsStatus.DontDesturbTime == null
                        && DsStatus.BreakTime == null
                        && DsStatus.OutOfficeTaskTime == null;
                    if (notAtWork) //not checked in
                        choices.Add(new(OnDutyCheckType.CheckIn.ToString(),  "On duty station"));
                    if(DsStatus.BreakTime != null) //or taking break
                        choices.Add(new(OnDutyCheckType.CheckIn.ToString(), "Back from Break"));
                    if (DsStatus.OutOfficeTaskTime != null) //or taking break
                        choices.Add(new(OnDutyCheckType.CheckIn.ToString(), "Back from out of office work"));
                    if(DsStatus.DontDesturbTime != null) //or out of office for work
                        choices.Add(new(OnDutyCheckType.CheckIn.ToString(), "Now Avialable"));

                    if (notAtWork && this.ScheduledDutyTime && DsStatus.RunningLateTime == null) //if on duty
                    {
                        choices.Add(new(OnDutyCheckType.RunningLate.ToString(), "Running Late"));
                        choices.Add(new(OnDutyCheckType.NotComing.ToString(), "Not Comming"));
                    }
                    
                    if (DsStatus.CheckInTime != null && DsStatus.BreakTime== null) //checked in
                        choices.Add(new(OnDutyCheckType.TakingBreak.ToString(), "Taking Break"));

                    if (DsStatus.CheckInTime != null && DsStatus.OutOfficeTaskTime==null) //checked in
                        choices.Add(new(OnDutyCheckType.CheckOut.ToString(), "Leaving Duty Station"));
                    if(DsStatus.OutOfficeTaskTime== null)
                        choices.Add(new(OnDutyCheckType.OffDutyStationAssignment.ToString(), "Working Outside of Duty Station"));
                    if(DsStatus.CheckInTime!=null && DsStatus.DontDesturbTime== null)
                        choices.Add(new(OnDutyCheckType.DontDisturb.ToString(), "Don't Disturn"));

                    return new FormDialogField
                    {
                        FieldType = FieldType.Choices,
                        Prompt="What is up?",
                        Choices = choices,
                        NextField= d =>
                         {
                             switch ((OnDutyCheckType)d[FIELD_CHECK_TYPE].Val())
                             {
                                 case OnDutyCheckType.RunningLate:
                                     return Task.FromResult(FIELD_LATE_REASON);
                                 case OnDutyCheckType.TakingBreak:
                                     return Task.FromResult(FIELD_BREAK_REASON);
                                 case OnDutyCheckType.NotComing:
                                     return Task.FromResult(FIELD_ABSENT_REASON);
                                 case OnDutyCheckType.DontDisturb:
                                     return Task.FromResult(FIELD_DD_REASON);
                                 case OnDutyCheckType.OffDutyStationAssignment:
                                     return Task.FromResult(FIELD_OO_REASON);
                             }
                             return Task.FromResult<String>(null);
                         },
                        ParseFunction=(b,t,c)=>
                        {
                            return Task.FromResult(new ParseResult { Data = Enum.Parse<OnDutyCheckType>(t) });
                        }

                    };
                case FIELD_OO_REASON:
                    return new FormDialogField
                    {
                        FieldType = FieldType.Text,
                        Prompt = "Ok, what are you going to do?",
                        NextField = d =>
                        {
                            return Task.FromResult(FIELD_OO_TIME);
                        }
                    };
                case FIELD_DD_REASON:
                    return new FormDialogField
                    {
                        FieldType = FieldType.Text,
                        Prompt = "Ok, tell us why?",
                        NextField = d =>
                        {
                            return Task.FromResult(FIELD_DD_TIME);
                        }
                    };
                case FIELD_BREAK_REASON:
                    return new FormDialogField
                    {
                        FieldType = FieldType.Text,
                        Prompt = "Ok, what are you taking the break for?",
                        NextField = d =>
                        {
                            return Task.FromResult(FIELD_BREAK_TIME);
                        }
                    };
                case FIELD_LATE_REASON:
                    return new FormDialogField
                    {
                        FieldType = FieldType.Text,
                        Prompt = "We hope everthing is alright with you. What happened?",
                        NextField = d =>
                        {
                            return Task.FromResult(FIELD_LATE_TIME);
                        }
                    };
                case FIELD_ABSENT_REASON:
                    return new FormDialogField
                    {
                        FieldType = FieldType.Text,
                        Prompt = "We hope everthing is alright with you. What happened?",
                        NextField = null
                    };
                case FIELD_DD_TIME:
                    return new FormDialogField
                    {
                        FieldType = FieldType.Text,
                        Prompt = "How many minutes do you want quiet?",
                        NextField = null,
                        ParseFunction = TimeParseFunction
                    };
                case FIELD_BREAK_TIME:
                    return new FormDialogField
                    {
                        FieldType = FieldType.Text,
                        Prompt = "How many minutes of break are you taking?",
                        NextField = null,
                        ParseFunction = TimeParseFunction
                    };
                case FIELD_LATE_TIME:
                    return new FormDialogField
                    {
                        FieldType = FieldType.Text,
                        Prompt = "How many minutes will it take you to get to your duty station?",
                        NextField = null,
                        ParseFunction = TimeParseFunction
                    };
                case FIELD_OO_TIME:
                    return new FormDialogField
                    {
                        FieldType = FieldType.Text,
                        Prompt = "How many minutes will it take you to finish the work?",
                        NextField = null,
                        ParseFunction = TimeParseFunction
                    };
                default:
                    return null;
            }
        }

        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            var now = TGBot.NowDt();
            var check = new OnDutyCheck
            {
                CheckType = (OnDutyCheckType)this.FieldData[FIELD_CHECK_TYPE].Val(),
                SelfReported = this.SelfReport,
                UserId = this.from.Id.ToString(),
                Time = now.Ticks,                
            };
            var message = "Thanks, talk to you later!";
            var service = new TaskDbService();
            String notifcation = null;
            var prof =service.GetUserProfile(this.from.Id.ToString());
            Func<DutyStation> dutyStation= () => service.GetDutyStation(service.GetUserDutyStation(this.from.Id.ToString(), now.Ticks).Id);
            switch (check.CheckType)
            {
                case OnDutyCheckType.CheckIn:
                    message = "Welcome!";
                    if(DsStatus.DontDesturbTime!=null)
                        notifcation = $"{service.GetUserProfile(this.from.Id.ToString()).Name()} is now aviable for communication";
                    if (DsStatus.BreakTime!= null)
                        notifcation = $"{service.GetUserProfile(this.from.Id.ToString()).Name()} is now back from break";
                    if (DsStatus.OutOfficeTaskTime != null)
                        notifcation = $"{service.GetUserProfile(this.from.Id.ToString()).Name()} is now back to duty station";
                    else
                        notifcation = $"{service.GetUserProfile(this.from.Id.ToString()).Name()} has checked in {(_span!=null && _span.Remote?"remotely ":"")}to {dutyStation().Name}";
                    break;
                case OnDutyCheckType.RunningLate:
                    {
                        check.Eta = now.AddMinutes((int)this.FieldData[FIELD_LATE_TIME].Val()).Ticks;
                        check.Reason = (string)this.FieldData[FIELD_LATE_REASON].Val();
                        notifcation = $"{prof.Name()} has reported he is running late to come to {dutyStation().Name}"
                            + $"\n{Program.lm._pronoun(prof.Gender,true)} reported: '{check.Reason}'"
                                + $"\n{Program.lm._pronoun(prof.Gender,true)} expects to get to the office by {TGBot.ToRelativeTime(check.Eta.Value)}";
                    }
                    break;
                case OnDutyCheckType.NotComing:
                    check.Reason = (string)this.FieldData[FIELD_ABSENT_REASON].Val();
                    message = "Ok, noted!";
                    notifcation = $"{prof.Name()} is not comming to {dutyStation().Name}"
                         + $"\n{Program.lm._pronoun(prof.Gender)} reported: '{check.Reason}'";

                    break;
                case OnDutyCheckType.TakingBreak:
                    check.Eta = now.AddMinutes((int)this.FieldData[FIELD_BREAK_TIME].Val()).Ticks;
                    check.Reason = (string)this.FieldData[FIELD_BREAK_REASON].Val();
                    message = "Ok, enjoy your break!";
                    break;
                case OnDutyCheckType.DontDisturb:
                    check.Eta = now.AddMinutes((int)this.FieldData[FIELD_DD_TIME].Val()).Ticks;
                    check.Reason = (string)this.FieldData[FIELD_DD_REASON].Val();
                    message = "Ok, understood!";
                    notifcation = $"{prof.Name()} has requestd that he be not disturbed"
                        + $"\n{Program.lm._pronoun(prof.Gender,true)} reported: '{check.Reason}'"
                            + $"\n{Program.lm._pronoun(prof.Gender)} expects to be avialable for communication by {TGBot.ToRelativeTime(check.Eta.Value)}";
                    break;
                case OnDutyCheckType.OffDutyStationAssignment:
                    check.Eta = now.AddMinutes((int)this.FieldData[FIELD_OO_TIME].Val()).Ticks;
                    check.Reason = (string)this.FieldData[FIELD_OO_REASON].Val();
                    message = "Ok, staty safe and good luck with the work!";
                    notifcation = $"{prof.Name()} has reported {Program.lm._pronoun(prof.Gender)} he is outside {dutyStation().Name} for work"
                        + $"\n{Program.lm._pronoun(prof.Gender)} reported: '{check.Reason}'"
                            + $"\n{Program.lm._pronoun(prof.Gender)} expects to get to the office by {TGBot.ToRelativeTime(check.Eta.Value)}";
                    break;
                case OnDutyCheckType.CheckOut:
                    notifcation = $"{service.GetUserProfile(this.from.Id.ToString()).Name()} has checked out of {dutyStation().Name}"; break;
                default:
                    break;
            }
            
            service.LogDutyCheck(check);
            try
            {
                await bot.SendTextMessageAsync(this.chatId, message, cancellationToken: cancellationToken);
            }
            catch(Exception ex)
            {
                TGBot.LogException("Error trying to reply to the user after database update", ex);
            }
            if (notifcation != null)
                await TaskBot.NotifyGroups(bot, notifcation, cancellationToken);
            return DialogResult.Terminated;
        }
    }
}
