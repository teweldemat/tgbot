using Microsoft.AspNetCore.Mvc;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using TgBot.SmartLedger;
using TgBot.Tasks;
using static TgBot.Tasks.TaskDbService;

namespace TgBot.Controllers
{
    public class TasksController : Controller
    {
        //planning constants
        private const int PERF_PLANNING_CREATE_TASK = 10;
        private const int PERF_PLANNING_ADD_CHECKLIST = 3;
        private const int PERF_PLANNING_EDIT_TITLE = 3;
        private const int PERF_PLANNING_CANCEL_TASK = 2;
        //enagement constants
        private const int PERF_ENGAGEMENT_SELF_ACTIVITIY = 2;
        private const int PERF_ENGAGEMENT_OTHER_ACTIVITIY = 3;
        private const double PERF_ENGAGMENT_SPREAD_SCORE_MAX = 10;
        private const double PERF_ENGAGMENT_SPREAD_SCORE_MIN = 1;
        private const double PERF_ENGAGMENT_SPREAD_DECLINE_FACTOR = 1;
        //documentation constants
        private const int PERF_DOCUMENT_GOOGLE_DOCS_SCORE = 10;
        private const int PERF_DOCUMENT_DOC_ATTCHMENT_SCORE = 7;
        private const int PERF_DOCUMENT_PICTURE_SCORE = 5;
        private const int PERF_DOCUMENT_OTHER_FILE = 3;
        private const int PERF_DOCUMENT_OTHER_LINK_SCORE = 1;
        
        private const int PEF_VERSION = 1;

        const double MIN_INDEX_DAYS = 3;

        [HttpGet]
        [Route("/task/file/")]
        public IActionResult GetTaskFile(String id)
        {
            try
            {
                var service = new TaskDbService();
                var picData = service.GetTaskFile(Guid.Parse(id));
                if (picData == null || picData.Image == null)
                    throw new Exception("Attachment doesn't exist");
                return base.File(picData.Image, picData.ImageMime, id + TGBot.MimeToExtension(picData.ImageMime));
            }
            catch (Exception ex)
            {
                Program.LogException("Error to call: GetPicture", ex);
                return StatusCode(500);

            }
        }
        [HttpGet]
        [Route("/task/preview/")]
        public IActionResult GetPicturePreview(String id)
        {
            try
            {
                var service = new TaskDbService();
                var picData = service.GetTaskFile(Guid.Parse(id));
                if (picData == null || picData.Image == null)
                    throw new Exception("Picture doesn't exist");
                var data = SmartLedgerController.GetPicturePreview(picData, out var mime);
                return base.File(data, mime);
            }
            catch (Exception ex)
            {
                Program.LogException("Error calling GetPicturePreview", ex);
                return StatusCode(500);
            }
        }

        public class SummaryViewModel
        {
            public String CompanyName { get; set; }
            public List<TaskInfo> ActiveTasks { get; set; }
            public List<TaskInfo> SuspendedTasks { get; set; }
            public List<TaskInfo> PendingTasks { get; set; }
        }

        void processTask(TaskDbService service, int level,MisTask x, List<TaskInfo> tasks, long now) 
        {
            tasks.Add(ToTaskInfo(service,x,level,now));
            foreach (var t in service.GetSubTasks(x.Id))
                processTask(service, level+1, t,tasks, now);
        }
        String WorkingOnIt(TaskDbService service, MisTask task)
        {
            var workers = service.GetTaskUsers(task.Id, TaskUserRole.Worker).ToList();
            String ret = null;
            for (int i = 0; i < Math.Min(workers.Count, 3); i++)
            {
                var w = workers[i];
                ret = ret == null ? w.Name() : ret = ret + ", " + w.Name();
            }
            if (workers.Count > 3)
                ret += $" and {workers.Count - 3} other";
            if (ret == null)
                ret = "none";
            return ret;
        }
        TaskInfo ToTaskInfo(TaskDbService service,  MisTask x, int level,long now)
        {
            var lastUpdate = service.GetLastTaskUpdateTimeByTask(x.Id);
            String startDate = null;
            if (x.StartTime != null)
                startDate = TGBot.ToRelativeTime(x.StartTime.Value);
            if (x.PlannedStartTime != null)
            {
                var str = $"(Planned to start {TGBot.ToRelativeTime(x.PlannedStartTime.Value)})";
                if (startDate == null)
                    startDate = str;
                else
                    startDate += str;
            }
            if (startDate == null)
                startDate = "Not Started and not Planned";
            String endTime = null;
            if (x.EndTime != null)
                endTime = TGBot.ToRelativeTime(x.EndTime.Value);
            if (x.PlannedEndTime != null)
            {
                var str = $"(Planned to end {TGBot.ToRelativeTime(x.PlannedEndTime.Value)})";
                if (startDate == null)
                    endTime = str;
                else
                    endTime += str;
            }

            if (x.PlannedEndTime != null && x.EndTime == null && now > x.PlannedEndTime.Value)
            {
                endTime += $"({TGBot.ToTimeLengthStr(now - x.PlannedEndTime.Value)})";
            }
            var comment = service.GetLastComment(x.Id);
            var ret = new TaskInfo
            {
                NWorkers = service.GetTaskUsersCount(x.Id, TaskUserRole.Worker),
                Task = service.GetTask(x.Id),
                LastUpdate = lastUpdate == null ? "Never updatd" : TGBot.ToRelativeTime(lastUpdate.Time),
                StatusString = TaskFullData.StatusString(x),
                WorkingOnIt = WorkingOnIt(service,x),
                Level = level,
                LastComment= comment==null?null:new TaskViewModel.TaskCommentViewModel
                {
                    Commenter = service.GetUserProfile(comment.UserId).Name(),
                    Comment = comment.Comment,
                    Time = TGBot.ToRelativeTime(comment.Time),
                }
            };
            return ret;
        }
        [HttpGet]
        [Route("/task/activetasks/")]
        public IActionResult GetSummary()
        {
            try
            {
                var service = new TaskDbService();                
                var tasks = service.GetActiveTasks(true,true,null,0, -1,out var n);
                var config = service.GetRuleData<SimplePaymentFlowConfiguration>();
                var now = TGBot.Now();
                
                
                var activeTasks = new List<TaskInfo>();
                foreach (var t in tasks)
                    processTask(service,0, t, activeTasks,now);
                var model = new SummaryViewModel
                {
                    CompanyName = service.GetEntity().Name,
                    ActiveTasks = activeTasks,
                };
                    
                return View("/Views/Task/TaskList.cshtml", model);
            }
            catch (Exception ex)
            {
                return View("/Views/ErrorView.cshtml", ex);
            }
        }
        public class TaskViewModel
        {
            public class TaskHistoryItem
            {

                public static TaskHistoryItem CreateFromDelta(TaskDbService service, GetTaskHistoryResultItem x)
                {
                    if (x.obj is TaskDelta.DeltaUpdateComment)
                    {
                        var delta = x.obj as TaskDelta.DeltaUpdateComment;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Update his comment on comment {service.GetTask(delta.TaskId).CodeName}",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaAddSubTask)
                    {
                        var delta = x.obj as TaskDelta.DeltaAddSubTask;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Added substask {service.GetTask(delta.SubtaskID).CodeName}",
                        };
                    }

                    if (x.obj is TaskDelta.DeltaRemoveComment)
                    {
                        var delta = x.obj as TaskDelta.DeltaRemoveComment;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Removed his/her comment",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaAddComment)
                    {
                        var delta = x.obj as TaskDelta.DeltaAddComment;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Added comment",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaRemoveContent)
                    {
                        var delta = x.obj as TaskDelta.DeltaRemoveContent;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Removed a content",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaSetContentCaption)
                    {
                        var delta = x.obj as TaskDelta.DeltaSetContentCaption;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Set caption to a document",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaUpdateFullCheckList)
                    {
                        var delta = x.obj as TaskDelta.DeltaUpdateFullCheckList;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Updated Checklist",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaChangeTitle)
                    {
                        var delta = x.obj as TaskDelta.DeltaChangeTitle;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Changed Title",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaChangeDescription)
                    {
                        var delta = x.obj as TaskDelta.DeltaChangeDescription;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Changed Description",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaChangeDueDate)
                    {
                        var delta = x.obj as TaskDelta.DeltaChangeDueDate;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Changed Due Date",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaCreateTask)
                    {
                        var delta = x.obj as TaskDelta.DeltaCreateTask;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = "Created Task",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaAddTaskContent)
                    {
                        var delta = x.obj as TaskDelta.DeltaAddTaskContent;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = $"Created {delta.Content.Count} attachment",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaAddTaskUser)
                    {
                        var delta = x.obj as TaskDelta.DeltaAddTaskUser;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation =
                            delta.AddUserId.Equals(delta.UserId) ?
                            $"Joined the task" :
                            $"Added {service.GetUserProfile(delta.AddUserId).FullName} to the task",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaRemoveUser)
                    {
                        var delta = x.obj as TaskDelta.DeltaRemoveUser;
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation =
                            delta.UserToRemove.Equals(delta.UserId) ?
                            $"Left the task" :
                            $"Removed {service.GetUserProfile(delta.UserToRemove).FullName} from the task",
                        };
                    }
                    if (x.obj is TaskDelta.DeltaUpdateCheckList)
                    {
                        var delta = x.obj as TaskDelta.DeltaUpdateCheckList;
                        String txt = null;
                        foreach (var t in delta.UpdatedItem)
                        {
                            var item = t.DoneTime == null ? $"Reported checklist item {t.Name} as not done" : $"Reported checklist item {t.Name} as done";
                            txt = txt == null ? item : txt + ", " + item;
                        }
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = txt,
                        };
                    }
                    if (x.obj is TaskDelta.DeltaChangeStatus)
                    {
                        var delta = x.obj as TaskDelta.DeltaChangeStatus;
                        String txt = null;
                        switch (delta.NetState)
                        {
                            case TaskStatus.Planned:
                                txt = $"planned the task";
                                break;
                            case TaskStatus.Started:
                                txt = $"started the task";
                                break;
                            case TaskStatus.Done:
                                txt = $"finished the task";
                                break;
                            case TaskStatus.Waiting:
                                txt = $"supended the task waiting for {delta.Remark}";
                                break;
                            case TaskStatus.Canceled:
                                txt = $"Cancelled the task";
                                break;
                            default:
                                break;
                        }
                        return new TaskViewModel.TaskHistoryItem
                        {
                            On = IntData.toDateString(x.time),
                            By = service.GetUserProfile(delta.UserId).FullName,
                            Operation = txt,
                        };
                    }
                    return null;
                }

                public String By { get;set; }
                public String Operation { get; set; }
                public String On { get; set; }
            }
            public class ContentIndex
            {
                public Guid Id;
                public bool IsPicture;

                public bool IsLink { get; internal set; }
                public string Caption { get; internal set; }
                public string Url { get; internal set; }
            }

            public class TaskCommentViewModel
            {
                public string Commenter { get; set; }
                public String Comment { get; set; }
                public String Time { get; set; }
            }
            public class WorkerViewModel
            {
                public MisUserProfile User { get; set; }
                public String JoinOn { get; set; }
            }
            
            public MisTask Task { get; set; }
            public MisTask ParentTask { get; set; }
            public List<MisTask> Subtasks { get; set; }
            public string CompanyName { get; set; }            
            public string StatusString { get; set; }
            public String DueDate { get; set; }
            public string CreatedBy { get; set; }
            public IList<TaskCheckListItem> CheckList { get; set; }
            public IList<WorkerViewModel> Workers { get; set;}
            public IList<WorkerViewModel> Followers  { get; set; }
            public IList<ContentIndex> Contents { get; set; }
            public List<TaskHistoryItem> History { get; set; }
            public List<TaskCommentViewModel> Comments { get; internal set; }
        }
        [HttpGet]
        [Route("/task/")]
        public IActionResult GetTaskDetail(String id)
        {
            try
            {
                var service = new TaskDbService();
                var taskId = Guid.Parse(id);
                var task = service.GetTask(taskId);
                if (task== null)
                    throw new Exception("Invalid task id:" + id);

                var model = new TaskViewModel
                {
                    Task = task,
                    DueDate = task.PlannedEndTime == null ? "Open" : TGBot.ToRelativeTime(task.PlannedEndTime.Value),
                    Comments = service.GetTaskComments(taskId).Select(x => new TaskViewModel.TaskCommentViewModel
                    {
                        Commenter = service.GetUserProfile(x.UserId).Name(),
                        Comment = x.Comment,
                        Time = TGBot.ToRelativeTime(x.Time),
                    }).ToList(),
                    ParentTask = task.ParentTaskId == null ? null : service.GetTask(task.ParentTaskId.Value),
                    Subtasks = service.GetSubTasks(taskId),
                    CheckList = service.GetTaskCheckList(taskId),
                    CompanyName = service.GetEntity().Name,
                    StatusString = TaskFullData.StatusString(task),
                    Contents = service.GetTaskContentIndex(taskId).Select(x => new TaskViewModel.ContentIndex
                    {
                        Id = x.Id,
                        IsPicture = SmartLedgerController.isPicture(x.ImageMime),
                        IsLink = x.LinkType == FormDialog.ContentLinkType.Url,
                        Caption = x.Caption,
                        Url = x.ContentLink
                    }
                    ).ToList(),
                    CreatedBy = service.GetUserProfile(task.CreatedBy).FullName,
                    History = service.GetTaskHistory(taskId)
                    .Select(x => TaskViewModel.TaskHistoryItem.CreateFromDelta(service, x))
                    .ToList(),
                    Workers = service.GetTaskUserInfo(taskId, TaskUserRole.Worker).Select(x =>
                       new TaskViewModel.WorkerViewModel {
                           User = service.GetUserProfile(x.UserID),
                           JoinOn = TGBot.ToRelativeTime(x.JoinedTime),
                       }).ToList(),
                    Followers= service.GetTaskUserInfo(taskId, TaskUserRole.Follower).Select(x =>
                       new TaskViewModel.WorkerViewModel
                       {
                           User = service.GetUserProfile(x.UserID),
                           JoinOn = TGBot.ToRelativeTime(x.JoinedTime),
                       }).ToList(),
                };
                return View("/Views/Task/TaskView.cshtml", model);
            }
            catch (Exception ex)
            {
                return View("/Views/ErrorView.cshtml", ex);
            }
        }
    
        public class TeamStatusViewModel
        {
            public class UserStatus
            {
                public String Name { get; set; }
                public String UserId { get; set; }
                public int NTasks { get; set; }
                public String LastTaskUpdate { get; set; }
                public string DutyStation{ get; set;}
                public int NWaiting { get; internal set; }
            }
            public String CompanyName { get; set; }
            public List<UserStatus> Users { get; set; }
        }
        [HttpGet]
        [Route("/task/team")]
        public IActionResult GetTeamOverview()
        {
            try
            {
                var service = new TaskDbService();
                var e = service.GetEntity();
                var users = service.GetAllUserProfiles();
                var now = TGBot.Now();
                var model = new TeamStatusViewModel
                {
                    CompanyName = service.GetEntity().Name,
                    Users = users.Where(x=>x.UserId!=e.Owner).Select(x =>
                    {
                        var ds = service.GetUserDutyStation(x.UserId, now);
                        var dsstate = service.GetUserDutyStationStatus(x.UserId, now);
                        String dstxt = null;
                        if (ds == null)
                            dstxt = "Not assigned to duty station";
                        else
                        {
                            dstxt = "Assigned to " + ds.Name;
                            
                            var s=service.GetDutySchedule(x.UserId) as IDutyStationSechdule;
                            if (s != null)
                            {
                                var span = s.GetOnCurrentOnDutySpan(x.UserId, now);
                                if(span!=null && span.Remote)
                                {
                                    dstxt += " as remote worker";
                                }
                            }
                        }
                        if (dsstate!=null)
                        {
                            String checkInText = null;
                            if (dsstate.CheckInTime != null)
                                checkInText = $"Checked in since {TGBot.ToRelativeTime(dsstate.CheckInTime.Value)}";
                            else if (dsstate.CheckOutTime != null)
                                checkInText = $"Checked out on {TGBot.ToRelativeTime(dsstate.CheckOutTime.Value)}";
                            else if (dsstate.BreakTime!= null)
                                checkInText = $"Taking break since {TGBot.ToRelativeTime(dsstate.BreakTime.Value)}";
                            else if (dsstate.DontDesturbTime!= null)
                                checkInText = $"Asked not to be disturbed on {TGBot.ToRelativeTime(dsstate.DontDesturbTime.Value)}. Reason: '{dsstate.Reason}'";
                            else if (dsstate.OutOfficeTaskTime!= null)
                                checkInText = $"Out for work since {TGBot.ToRelativeTime(dsstate.OutOfficeTaskTime.Value)}. Task: '{dsstate.Reason}'";
                            if (checkInText != null)
                                dstxt = dstxt == null ? checkInText : dstxt + ", " + checkInText;
                        }
                        var lastTaskAudit = service.GetLastTaskUpdateTime(x.UserId);
                        string lastTaskOp;
                        if (lastTaskAudit == null)
                            lastTaskOp = "Never Updated a Task";
                        else if (lastTaskAudit.Operation == null)
                        {
                            lastTaskOp = $"{TGBot.ToRelativeTime(lastTaskAudit.Time)}";
                        }
                        else
                        {
                            lastTaskOp = $"{TGBot.ToRelativeTime(lastTaskAudit.Time)}, ";
                            if (lastTaskAudit.Operation.EndsWith("_attempt"))
                                lastTaskOp += $"({lastTaskAudit.Operation.Substring(0, lastTaskAudit.Operation.Length - "_attempt".Length)})";
                            else
                                lastTaskOp += "$({lastTaskAudit.Operation})";
                        }

                        var us = new TeamStatusViewModel.UserStatus
                        {
                            NTasks = service.GetUserActiveTaskCount(x.UserId, TaskUserRole.Worker),
                            NWaiting= service.GetUserWaitingTaskCount(x.UserId, TaskUserRole.Worker),
                            Name = x.FullName,
                            UserId=x.UserId,
                            DutyStation = dstxt,
                            LastTaskUpdate= lastTaskOp,
                            
                        };
                        return us;
                    }).ToList()
                };
                return View("/Views/Task/TeamStatus.cshtml", model);
            }
            catch (Exception ex)
            {
                return View("/Views/ErrorView.cshtml", ex);
            }
        }
        public class TaskInfo
        {
            public MisTask Task { get; set; }
            public int NWorkers { get; set; }
            public String LastUpdate { get; set; }
            public string StatusString { get; set; }
            public String WorkingOnIt { get; set; }
            public String StartDate { get; set; }
            public String EndDate { get; set; }
            public int Level { get; internal set; }
            public TaskViewModel.TaskCommentViewModel LastComment { get; internal set; }

            //delta 
            public String DeltaBy { get; set; }
            public String DeltaDate { get; set; }
        }
        public class UserOverviewViewModel
        {            
            public class DutyStationChange
            {
                public String Operation { get; set; }
                public String Time { get; set; }
            }
            public String CompanyName { get; set; }
            public MisUserProfile User { get; set; }
            public List<DutyStation> DSHistory { get; set; }
            public List<TaskInfo> Tasks { get; set; }
            public List<TaskInfo> Following { get; set; }
        }
        [HttpGet]
        [Route("/task/user")]
        public IActionResult GetUserOverview(String userid)
        {
            try
            {
                var service = new TaskDbService();
                var e = service.GetEntity();
                var users = service.GetAllUserProfiles();
                var now = TGBot.Now();
                var tasks = service.GetUserActiveTasks(userid, TaskUserRole.Worker);
                var following = service.GetUserActiveTasks(userid, TaskUserRole.Follower);
                var model = new UserOverviewViewModel
                {
                    CompanyName = service.GetEntity().Name,
                    User = service.GetUserProfile(userid),
                    DSHistory = null,
                    Following = following.Select(x => new TaskInfo
                    {
                        Task = x,
                        NWorkers = service.GetTaskUsersCount(x.Id, TaskUserRole.Worker),
                        StatusString = TaskFullData.StatusString(x)
                    }).ToList(),
                    Tasks = tasks.Select(x => new TaskInfo
                    {
                        Task = x,
                        NWorkers = service.GetTaskUsersCount(x.Id, TaskUserRole.Worker),
                        StatusString = TaskFullData.StatusString(x)
                    }).ToList(),
                };
                return View("/Views/Task/UserOverview.cshtml", model);
            }
            catch (Exception ex)
            {
                return View("/Views/ErrorView.cshtml", ex);
            }
        }

        public class TaskChangeSummary
        {
            public class SummerySection
            {
                public String Title;
                public List<TaskInfo> Tasks;
                public String ReportedByColumn;
                
            }
            public class UserPerformance
            {
                public MisUserProfile User;
                public int DocumentationScore=0;
                public int EngagmentScore = 0;
                public List<long> Engagements = new List<long> ();
            }
            public String CompanyName { get; set; }
            public List<TaskInfo> CreatedTasks { get; set; } = new List<TaskInfo>();
            public List<TaskInfo> StartedTasks { get; set; } = new List<TaskInfo>();
            public List<TaskInfo> CompletedTasks { get; set; } = new List<TaskInfo>();
            public List<TaskInfo> CanceledTasks { get; set; } = new List<TaskInfo>();
            public List<TaskInfo> Suspended{ get; set; } = new List<TaskInfo>();
            public List<TaskInfo> OverDueTasks { get; set; } = new List<TaskInfo>();
            public List<TaskInfo> NoChange { get; set; } = new List<TaskInfo>();
            public List<UserPerformance> UserContributions { get; set; }
            public bool IncludeIndex { get; set; }
            public bool IncludeScore{ get; set; }
            public String FromDate { get; set; }
            public String ToDate{ get; set; }
            public int NCreated { get; set; }
            public int NStarted { get; set; } = 0;
            public int NCompleted { get; set; } = 0;
            public int NCanceled { get; set; } = 0;
            public int NSuspended { get; set; } = 0;
            public string CompletionIndex { get; internal set; }
            public string CancellationIndex { get; internal set; }
            public string TaskDelayIndex { get; internal set; }
            public SummerySection[] Sections()
            {
                return new SummerySection[]
                {
                    new SummerySection
                    {
                        Title="Completed Tasks",
                        Tasks=this.CompletedTasks,
                        ReportedByColumn="Reported By"
                    },
                    new SummerySection
                    {
                        Title="Created Tasks",
                        Tasks=this.CreatedTasks,
                        ReportedByColumn="Created By"
                    },
                    new SummerySection
                    {
                        Title="Suspended Tasks",
                        Tasks=this.Suspended,
                        ReportedByColumn="Suspended By"
                    },
                    new SummerySection
                    {
                        Title="Cancelled Tasks",
                        Tasks=this.CanceledTasks,
                        ReportedByColumn="Cancelled By"
                    },
                    new SummerySection
                    {
                        Title="Overdue Tasks",
                        Tasks=this.OverDueTasks,
                        ReportedByColumn=null
                    },
                    new SummerySection
                    {
                        Title="Unchanged Tasks",
                        Tasks=this.NoChange,
                        ReportedByColumn=null
                    },
                };
            }
        }

        void addUserStat(TaskDbService service, Dictionary<String, TaskChangeSummary.UserPerformance> userStat, String userId, int doc, long? eng)
        {
            TaskChangeSummary.UserPerformance data;
            if (userStat.ContainsKey(userId))
                data = userStat[userId];
            else
                userStat.Add(userId, data=new TaskChangeSummary.UserPerformance() { User = service.GetUserProfile(userId) });
            if (eng != null)
                data.Engagements.Add(eng.Value);
            data.DocumentationScore += doc;
        }
        [HttpGet]
        [Route("/task/changesummary")]
        public IActionResult GetChangeSummary(String userid,long from,long to)
        {
            try
            {
                var service = new TaskDbService();
                
                var ver = service.GetCurrentFRVersion();
                if (ver != null)
                {
                    if (ver.VersionNumber != PEF_VERSION || from<ver.FromTime)
                        throw new UserFriendlyError("This report is not avialable for the selected time range.");
                }
                
                var e = service.GetEntity();
                var model = new TaskChangeSummary()
                {
                    CompanyName = e.Name,
                    FromDate = IntData.toDateString(from, "MMM dd, yyy"),
                    ToDate = new DateTime(to).AddDays(-1).ToString("MMM dd, yyy"),
                    IncludeIndex = new TimeSpan(to - from).TotalDays >= MIN_INDEX_DAYS,
                    IncludeScore= new TimeSpan(to - from).TotalDays >= MIN_INDEX_DAYS,
                };
                var now = TGBot.Now();
                var changedTasks = new HashSet<Guid>();
                Func<MisTask, AuditRecord, TaskInfo> toTaskInfoWithDelta = (t, a) =>
                    {
                        var ti = ToTaskInfo(service, t, 0, now);
                        ti.DeltaBy = service.GetUserProfile(a.UserId).FullName;
                        ti.DeltaDate = TGBot.ToRelativeTime(a.Time);
                        return ti;
                    };
                Action<Guid> removeFromOthers = x =>
                  {
                      foreach (var s in model.Sections())
                      {
                          bool found = false;
                          for (var i = 0; i < s.Tasks.Count; i++)
                          {
                              if (s.Tasks[i].Task.Id == x)
                              {
                                  s.Tasks.RemoveAt(i);
                                  found = true;
                                  break;
                              }
                          }
                          if (found)
                              break;
                      }
                  };

                var userStat = new Dictionary<String, TaskChangeSummary.UserPerformance>();
                foreach (var u in service.GetAllUserProfiles())
                    userStat.Add(u.UserId, new TaskChangeSummary.UserPerformance() { User = u });
                Func<FormDialog.ContentData, int> scoreContent = x =>
                {
                    if(x.LinkType==FormDialog.ContentLinkType.Url)
                    {
                        var ur = new Uri(x.ContentLink);
                        if(ur.Host.Equals("drive.google.com",StringComparison.OrdinalIgnoreCase))
                        {
                            if (ur.PathAndQuery.Contains("document"))
                                return PERF_DOCUMENT_GOOGLE_DOCS_SCORE;
                        }
                        return PERF_DOCUMENT_OTHER_LINK_SCORE;
                    }
                    if (x.ImageMime != null)
                    {
                        if (x.ImageMime.Contains("image", StringComparison.OrdinalIgnoreCase))
                        {
                            return PERF_DOCUMENT_PICTURE_SCORE;
                        }

                        if ("application/pdf".Equals(x.ImageMime, StringComparison.OrdinalIgnoreCase)
                            || "application/msword".Equals(x.ImageMime, StringComparison.OrdinalIgnoreCase)
                            || x.ImageMime.Contains("openxmlformats", StringComparison.OrdinalIgnoreCase)
                            || x.ImageMime.Contains("text", StringComparison.OrdinalIgnoreCase)
                            )
                        {
                            return PERF_DOCUMENT_DOC_ATTCHMENT_SCORE;
                        }
                        return PERF_DOCUMENT_OTHER_FILE;
                    }
                    return 0;
                };

                service.ParseTaskDeltas(from, to, (task, audit, delta) =>
                  {
                      if (!changedTasks.Contains(task.Id))
                          changedTasks.Add(task.Id);
                      bool selfTask = service.GetTaskUsers(task.Id, TaskUserRole.Worker).Where(x => x.UserId.Equals(audit.UserId)).Any();
                      
                      addUserStat(service, userStat, audit.UserId,0, audit.Time);
                      if(delta is TaskDelta.DeltaAddTaskContent)
                      {
                          var x = (TaskDelta.DeltaAddTaskContent)delta;
                          if (x.Content != null)
                              foreach (var doc in x.Content)
                              {
                                  addUserStat(service, userStat, audit.UserId, scoreContent(doc), null);
                              }
                      }
                      if (delta is TaskDelta.DeltaCreateTask)
                      {
                          var x = (TaskDelta.DeltaCreateTask)delta;
                          
                          removeFromOthers(task.Id);
                          model.CreatedTasks.Add(toTaskInfoWithDelta(task, audit));
                          model.NCreated++;
                          if (x.Task.Task.Status == TaskStatus.Started)
                          {
                              //removeFromOthers(task.Id);
                              //model.StartedTasks.Add(toTaskInfoWithDelta(task, audit));
                              //model.NStarted++;
                          }
                          if (x.Task.Content != null)
                              foreach (var doc in x.Task.Content)
                              {
                                  addUserStat(service, userStat, audit.UserId,scoreContent(doc),null);
                              }
                          
                          return true;
                      }
                      if (delta is TaskDelta.DeltaChangeStatus)
                      {

                          var x = (TaskDelta.DeltaChangeStatus)delta;
                          switch (x.NetState)
                          {
                              case TaskStatus.Planned:
                                  break;
                              case TaskStatus.Started:
                                  //removeFromOthers(task.Id);
                                  //model.StartedTasks.Add(toTaskInfoWithDelta(task, audit));
                                  //model.NStarted++;
                                  return true;
                              case TaskStatus.Done:
                                  removeFromOthers(task.Id);
                                  model.CompletedTasks.Add(toTaskInfoWithDelta(task, audit));
                                  model.NCompleted++;
                                  return true;
                              case TaskStatus.Waiting:
                                  removeFromOthers(task.Id);
                                  model.Suspended.Add(toTaskInfoWithDelta(task, audit));
                                  model.NSuspended++;
                                  return true;
                              case TaskStatus.Canceled:
                                  removeFromOthers(task.Id);
                                  model.CanceledTasks.Add(toTaskInfoWithDelta(task, audit));
                                  model.NCanceled++;
                                  return true;
                              default:
                                  break;
                          }
                          return true;
                      }

                      return true;
                  });
                bool first = true;
                if (userStat.ContainsKey(e.Owner))
                    userStat.Remove(e.Owner);
                foreach(var u in userStat.Values)
                {
                    if (u.Engagements.Count < 2)
                    {
                        u.EngagmentScore = 0;
                    }
                    else
                    {
                        u.Engagements.Sort();                        
                        double A = 0;
                        int n = u.Engagements.Count - 1;
                        double tau = ((double)(u.Engagements[n] - u.Engagements[0])) / (n+1);
                        for(int i=0;i<=n-1;i++)
                        {
                            double diff = ((double)u.Engagements[i + 1] - (double)u.Engagements[i])/tau-1;
                            A += diff * diff;
                        }
                        A = A / n;
                        double S = PERF_ENGAGMENT_SPREAD_SCORE_MIN + Math.Exp(-PERF_ENGAGMENT_SPREAD_DECLINE_FACTOR * A) * (PERF_ENGAGMENT_SPREAD_SCORE_MAX - PERF_ENGAGMENT_SPREAD_SCORE_MIN);
                        u.EngagmentScore = (int)Math.Round((double)u.Engagements.Count*PERF_ENGAGEMENT_SELF_ACTIVITIY * S);
                        if(first)
                        {
                            foreach (var eng in u.Engagements)
                                Console.WriteLine(eng);
                            Console.WriteLine($"tau:{tau}");
                            Console.WriteLine($"A:{A}");
                            Console.WriteLine($"S:{S}");
                            Console.WriteLine($"Score:{u.EngagmentScore}");
                            first = false;
                        }
                    }
                }
                model.UserContributions = userStat.Values.OrderByDescending(x => x.EngagmentScore).ToList();
                var overDueTasks = service.GetAllTasksList(createTimeTo: to, filter: x =>
                    x.Status != TaskStatus.Canceled
                    && x.Status != TaskStatus.Done
                    &&
                    x.PlannedEndTime != null && x.PlannedEndTime.Value < to);
                model.OverDueTasks =overDueTasks.Select( x=>ToTaskInfo(service, x, 0, now)).ToList();

                var notChanged = service.GetAllTasksList(createTimeTo: from,
                    filter: 
                    x => 
                    x.Status != TaskStatus.Canceled 
                    && x.Status != TaskStatus.Done
                    && (x.PlannedStartTime==null || x.PlannedStartTime.Value<to) // tasks that are planned to start after the reporting period are not included
                    && !changedTasks.Contains(x.Id)
                    );

                //not changed
                for(int i= notChanged.Count-1;i>=0;i--) 
                {
                    
                    bool remove = false;
                    service.TraverseChild(notChanged[i].Id, false, task =>
                    {
                        if(changedTasks.Contains(task.Id)) //if any of the childs are changed, the task is removed from the not changed list
                        {
                            remove = true;
                            return false;
                        }
                        return true;
                    });
                    if(remove)
                    {
                        notChanged.RemoveAt(i);
                    }
                }
                model.NoChange = notChanged.Select(x => ToTaskInfo(service, x, 0, now)).ToList();

                model.CompletionIndex = model.CreatedTasks.Count == 0 ?  (model.CompletedTasks.Count==0?"0": "100") : (model.CompletedTasks.Count * 100 / model.CreatedTasks.Count).ToString();
                model.CancellationIndex = model.CreatedTasks.Count == 0 ? (model.CanceledTasks.Count == 0 ? "0" : "100") : (model.CanceledTasks.Count * 100 / model.CreatedTasks.Count).ToString();
                model.TaskDelayIndex= model.CreatedTasks.Count == 0 ? (model.OverDueTasks.Count == 0 ? "0" : "100") : (model.OverDueTasks.Count * 100 / model.CreatedTasks.Count).ToString();               return View("/Views/Task/TaskChangeSummary.cshtml", model);
            }
            catch (Exception ex)
            {
                return View("/Views/ErrorView.cshtml", ex);
            }
        }

        public class TaskChartViewModel
        {
            public class TimePoint
            {
                public int i;
                public long time;
            }
            public abstract class ChartSpan
            {
                public abstract int Index { get; }
                public abstract int Width { get; }
                public int Right => Index + Width;
            }
            public class TaskSpan: ChartSpan
            {
                public MisTask Task;
                public int TaskLevel;
                public TimePoint From;
                public TimePoint To;
                public override int Width => To.i - From.i;
                public override int Index=> From.i;
                public string HCode { get; internal set; }
            }
            public class Spacer:ChartSpan
            {
                public int i;
                public int w;
                public override int Width => w;
                public override int Index => i;
            }
            public List<List<ChartSpan>> Spans;
            public int ColCount;
            public string From;
            public String To;
            public TimePoint NowPoint;
        }
        
        [HttpGet]
        [Route("/task/planchart")]
        public IActionResult TaskChart()
        {
            try
            {
                var service = new TaskDbService();
                var tasks = service.GetActiveTasks(false, true, null, 0, -1, out var m);
                var points = new List<TaskChartViewModel.TimePoint>();
                var now = TGBot.Now();
                var taskSpans = tasks.Where(x=>x.PlannedStartTime!=null && x.PlannedEndTime!=null).Select(x =>
                {
                    var ret = new TaskChartViewModel.TaskSpan
                    {
                        Task = x,
                        TaskLevel=service.GetTaskHeirarchyLevel(x.Id),
                        HCode=x.ParentTaskId==null?x.Code:service.GetTaskHeirarchyCode(x.Id,"/"),
                        From = new TaskChartViewModel.TimePoint
                        {
                            time = x.PlannedStartTime.Value,
                        },
                        To = new TaskChartViewModel.TimePoint
                        {
                            time = x.PlannedEndTime.Value<= x.PlannedStartTime.Value?new DateTime(x.PlannedStartTime.Value).AddDays(1).Ticks: x.PlannedEndTime.Value,
                        },
                    };
                    points.Add(ret.From);
                    points.Add(ret.To);
                    return ret;
                }).OrderBy(x=>x.From.time)
                .ToList();
                var nowPoint = new TaskChartViewModel.TimePoint { time = now };
                points.Add(nowPoint);
                points.Sort((x,y) => x.time.CompareTo(y.time));
                int i = -1;
                TaskChartViewModel.TimePoint prev = null;
                foreach (var p in points)
                {
                    if(prev==null || p.time!=prev.time)
                    {
                        i++;
                    }
                    p.i = i;
                    prev = p;
                }
                var list = new List<List<TaskChartViewModel.ChartSpan>>();
                bool first = true;
                long minT=0, maxT=0;
                foreach (var t in taskSpans)
                {
                    if (t.From.time < minT || first)
                        minT = t.From.time;
                    if (t.To.time > maxT|| first)
                        maxT= t.To.time;

                    first = false;

                    List<TaskChartViewModel.ChartSpan> row = null;
                    foreach (var r in list)
                    {
                        if (r.Last().Right <= t.Index)
                        {
                            row = r;
                            break;
                        }
                    }
                    if (row == null)
                        list.Add(row = new List<TaskChartViewModel.ChartSpan>());

                    if (row.Count == 0 || row.Last().Right == t.Index)
                        row.Add(t);
                    else
                    {
                        row.Add(new TaskChartViewModel.Spacer
                        {
                            i = row.Last().Right,
                            w = t.Index - row.Last().Right
                        });
                        row.Add(t);
                    }
                }
                int ColCount= list.Count == 0 ? 0 : list.Max(x => x.Last().Right);
                foreach(var row in list)
                {
                    var firstCell = row.First();
                    if(firstCell.Index>0)
                    {
                        row.Insert(0,new TaskChartViewModel.Spacer
                        {
                            i = 0,
                            w = firstCell.Index
                        });
                    }
                    var last = row.Last();
                    if (last.Right<ColCount)
                    {
                        row.Add(new TaskChartViewModel.Spacer
                        {
                            i = last.Right,
                            w = ColCount-last.Right
                        });
                    }
                }
                var model = new TaskChartViewModel
                {
                    NowPoint=nowPoint,
                    ColCount = ColCount,
                    Spans = list,
                    From=list.Count==0?"":IntData.toDateString(minT,"MMM dd, yyyy")+(nowPoint.i==0?"(now)":""),
                    To= list.Count == 0 ? "" : IntData.toDateString(maxT, "MMM dd, yyyy") + (nowPoint.i == ColCount-1 ? "(now)" : ""),
                };
                return View("/Views/Task/PlanChart.cshtml", model);
            }
            catch (Exception ex)
            {
                return View("/Views/ErrorView.cshtml", ex);
            }
        }
    }
}
