using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using TgBot.SmartLedger;
using TgBot.TgDb;

namespace TgBot.Tasks
{
    public class CreateTaskDialog : FormDialog
    {
        const string FIELD_TITLE = "Title";
        const string FIELD_DESCRIPTION = "Description";
        const string FIELD_CHECK_LIST = "CheckList";
        const string FIELD_TASK_CONTENT = "TaskContent";
        const string FIELD_START_TIME = "StartTime";
        const string FIELD_END_TIME = "EndTime";
        const double MIN_FUTURE_MINUTES = 5;

        public bool AutoAddCreator { get; set; }
        public bool PlannedTask { get; set; }
        public bool StartImmidiately { get; set; }
        public Guid? ParentTaskId { get; set; }
        public Guid? WaitingTaskId { get; set; }

        public override string FirstField => FIELD_TITLE;
        TaskDbService service;
        TgDbService tgService;
        public CreateTaskDialog(TaskDbService service, TgDbService tgService) : base(null, null)
        {
            this.service = service;
            this.tgService = tgService;
        }
        public override void SetServices(IServiceProvider services)
        {
            this.service = services.GetService<TaskDbService>();
            this.tgService = services.GetService<TgDbService>();
        }

        public CreateTaskDialog(TaskDbService service, TgDbService tgService, ChatId chatId, User user,
            bool addCreator = false,
            bool plannedTask = false,
            bool startImmidiately = false,
            Guid? parentTaskId = null,
            Guid? waitingTaskId = null
            ) : base(chatId, user)
        {
            this.AutoAddCreator = addCreator;
            this.PlannedTask = plannedTask;
            this.StartImmidiately = startImmidiately;
            this.ParentTaskId = parentTaskId;
            this.WaitingTaskId = waitingTaskId;
            this.service = service;
            this.tgService = tgService;
        }

        public override FormDialogField GetFieldDef(string key)
        {
            switch (key)
            {
                case FIELD_TITLE:
                    return new FormDialogField
                    {
                        Prompt = "What is the title of the task?",
                        NextField = d => Task.FromResult(FIELD_DESCRIPTION)
                    };
                case FIELD_DESCRIPTION:
                    return new FormDialogField
                    {
                        Prompt = "Write a more detailed description for your task (or /none?",
                        NextField = d => Task.FromResult(FIELD_CHECK_LIST),
                        ParseFunction = (b, t, c) =>
                          {
                              if ("/none".Equals(t, StringComparison.OrdinalIgnoreCase))
                              {
                                  return Task.FromResult(new ParseResult { Data = null });
                              }
                              return Task.FromResult(new ParseResult { Data = t });
                          }
                    };
                case FIELD_CHECK_LIST:
                    return new FormDialogField
                    {
                        Prompt = "Enter checklist for your task. Enter each item as separate messages.\nType /end at the end.",
                        FieldType = FieldType.ChildDialog,
                        ChildDialog = new CheckListEditorDialog(this.chatId, new List<TaskCheckListItem>()),
                        NextField = d => Task.FromResult(FIELD_TASK_CONTENT),
                        ProcessChildDilogFunction = (b, d, c) =>
                        {
                            return Task.FromResult(new ParseResult { Data = ((CheckListEditorDialog)d).CheckList });
                        }
                    };
                case FIELD_TASK_CONTENT:
                    return new FormDialogField
                    {
                        Prompt = "Upload attachments and link to documents.\nType /end at the end.",
                        FieldType = FieldType.ContentList,
                        NextField = d =>
                        {
                            if (StartImmidiately)
                                return Task.FromResult(FIELD_END_TIME);
                            else
                                return Task.FromResult(FIELD_START_TIME);
                        },
                    };
                case FIELD_START_TIME:
                    return new FormDialogField
                    {
                        Prompt = "When will the task start. (Enter 'now' if it is starting immidiately)?",
                        NextField = d => Task.FromResult(FIELD_END_TIME),
                        ParseFunction = (b, t, c) =>
                        {
                            if ("now".Equals(t, StringComparison.OrdinalIgnoreCase))
                            {
                                return Task.FromResult(new ParseResult { Data = null });
                            }
                            if (!DateTime.TryParse(t, out var dt))
                                return Task.FromResult(new ParseResult { Error = "I couldn't undesrtand what you entered. Please enver valid date or time" });
                            if (dt.Subtract(TGBot.NowDt()).TotalMinutes < MIN_FUTURE_MINUTES)
                                return Task.FromResult(new ParseResult { Error = "Enter for a future time or enter 'now'" });
                            return Task.FromResult(new ParseResult { Data = dt.Ticks });
                        }
                    };
                case FIELD_END_TIME:
                    return new FormDialogField
                    {
                        Prompt = "When is the task planned to be completed.",
                        NextField = null,
                        ParseFunction = (b, t, c) =>
                        {
                            if (!DateTime.TryParse(t, out var dt))
                                return Task.FromResult(new ParseResult { Error = "I couldn't understand what you entered. Please enver valid date or time" });
                            if (dt.Subtract(TGBot.NowDt()).TotalMinutes < MIN_FUTURE_MINUTES)
                                return Task.FromResult(new ParseResult { Error = $"Enter for a future time. Current time is {TGBot.NowDt().ToLongDateString()}" });
                            return Task.FromResult(new ParseResult { Data = dt.Ticks });
                        }
                    };
                default:
                    return null;
            }
        }
        protected override async Task<DialogResult> OnCompleteAsync(ITelegramBotClient bot, CancellationToken cancellationToken)
        {
            long startTime;
            bool StartImmidiatelyEffective = StartImmidiately;
            if (StartImmidiately)
                startTime = TGBot.Now();
            else
            {
                if (this.FieldData[FIELD_START_TIME].Val() == null)
                {
                    startTime = TGBot.Now();
                    StartImmidiatelyEffective = true;
                }
                else
                    startTime = (long)this.FieldData[FIELD_START_TIME].Val();
            }
            var st = this.FieldData[FIELD_END_TIME].Val();
            var task = new MisTask
            {
                Title = this.FieldData[FIELD_TITLE].Val() as String,
                Description = this.FieldData[FIELD_DESCRIPTION].Val() as String,
                ParentTaskId = this.ParentTaskId,
                StartTime = StartImmidiatelyEffective ? startTime : null,
                PlannedStartTime = startTime,
                EndTime = null,
                PlannedEndTime = (long)this.FieldData[FIELD_END_TIME].Val(),
                Status = StartImmidiatelyEffective ? TaskStatus.Started : TaskStatus.Planned,
            };
            try
            {
                task.Code = service.CreateTask(this.from.Id.ToString(), new TaskFullData
                {
                    Task = task,
                    Content = this.FieldData[FIELD_TASK_CONTENT].Val<List<ContentData>>(),
                    CheckList = this.FieldData[FIELD_CHECK_LIST].Val<List<TaskCheckListItem>>().Select(x => x.Name).ToList(),
                },
                AutoAddCreator ? new[] { new TaskUser
                {
                    Role=TaskUserRole.Worker,
                    UserID=this.from.Id.ToString()
                }} : null
                );
            }
            catch (Exception ex)
            {
                TGBot.LogException("Error trying to create task", ex);
                await bot.SendTextMessageAsync(chatId, "System problem traying to create your task.", cancellationToken: cancellationToken);
                return DialogResult.Terminated;
            }
            task = service.GetTaskByCode(task.Code);
            await bot.SendTextMessageAsync(chatId,
                        $"Your task is succesfully created. Task Code: {task.Code}"
                      + $"Enter {task.Code} at any time to track the status of the task"
            , cancellationToken: cancellationToken);
            await TaskBot.NotifyGroups(bot, tgService, $"{service.GetUserProfile(this.from.Id.ToString()).Name()} created task {TaskBot.TaskLink(task.Id, task.CodeName)}", cancellationToken);
            return DialogResult.Terminated;
        }
    }
}
