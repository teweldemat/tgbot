using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Telegram.Bot.Types;
using TgBot.SmartLedger;
using static TgBot.FormDialog;

namespace TgBot.Tasks
{
    public partial class TaskDbService : SmartLedger.SmartLedgerService
    {
        public class ServiceCache
        {
            TaskDbService service;
            public CachedObject<Guid, MisTask> Tasks;
            public CachedObject<String, MisUserProfile> Users;
            public ServiceCache()
            {
                this.service = new TaskDbService();
                this.Tasks = new CachedObject<Guid, MisTask>(x => service.GetTask(x));
                this.Users = new CachedObject<String, MisUserProfile>(x => service.GetUserProfile(x));
            }
        }
        public static IEnumerable<TaskType> TaskTypes(SmartLedger.SmartLedgerDb db)
            => db.TaksTypes.OrderBy(x => x.OrderN).AsEnumerable();

        internal object GetDutySchedule(string userId) => DbRead<Object>(db =>
        {
            var ds = db.DutySchedules.Where(x => x.UserId.Equals(userId)).FirstOrDefault();
            if (ds == null || string.IsNullOrEmpty(ds.ScheduleData))
                return null;
            var type = Type.GetType(ds.ScheduleType);
            if (type == null)
                throw new UserFriendlyError($"{ds.ScheduleType} couldn't be loaded");
            return Newtonsoft.Json.JsonConvert.DeserializeObject(ds.ScheduleData, type);
        });

        internal ContentData GetTaskFile(Guid guid)
        {
            return DbRead(db =>
            {
                var rec = db.TaskContents.Where(x => x.Id == guid).FirstOrDefault();
                if (rec == null)
                    return null;
                return rec.AsContentData();
            });            
        }

        internal IDutyStationSechdule GetDutySchedule(string userId, long now) => DbRead(db =>
        {
            var ds = db.DutySchedules.Where(x => x.UserId.Equals(userId)).FirstOrDefault();
            if (ds == null || string.IsNullOrEmpty(ds.ScheduleData))
                return null;
            var type = Type.GetType(ds.ScheduleType);
            if (type == null)
                throw new UserFriendlyError($"{ds.ScheduleType} couldn't be loaded");
            var ret = Newtonsoft.Json.JsonConvert.DeserializeObject(ds.ScheduleData, type) as IDutyStationSechdule;
            return ret;
        });


        internal int DutyStationWorkerCount(Guid id) =>
            DbRead(db => db.UserDutyStations.Where(x => x.DutyStationId == id).Count());
        internal List<MisUserProfile> GetDutyStationWorkes(Guid id) =>
            DbRead(db =>
            db.UserDutyStations.Join(db.MisUserProfiles.Where(x => x.Permitted), x => x.UserId, y => y.UserId, (x, y) => y).ToList());
        
        public void ParseTaskDeltas(long from,long to, Func<MisTask,AuditRecord,Object,bool> func)
        {
            using(var db=new SmartLedgerDb())
            using (var db2 = new SmartLedgerDb())
            {

                var task = new CachedObject<Guid, MisTask>(x => GetTaskInternal(db2, x));
                var rs = db.TaskDeltas
                    .Join(db.AuditRecords, x => x.AuditId, x => x.Id, (x, y) => new { delta = x, auidt = y })
                    .Where(x => x.auidt.Time >= from && x.auidt.Time < to)
                    .OrderBy(x=>x.auidt.Time);
                foreach (var r in rs)
                {
                    if (!func(task[r.delta.TaskId], r.auidt, GetTaskHistoryItem(r.auidt.Id)))
                        break;
                }
            }
        }
        internal List<MisTask> GetUserActiveTasks(string userId, TaskUserRole role) =>
            DbRead(db =>
                db.TaskUsers
            .Where(x => x.UserID.Equals(userId) && x.Role == role)
            .Join(db.Tasks, x => x.TaskId, y => y.Id, (x, y) => y)
            .Where(x => x.Status != TaskStatus.Canceled && x.Status != TaskStatus.Done)
            .ToList());
       
        internal int GetUserActiveTaskCount(string userId, TaskUserRole role) =>
            DbRead(db =>
                db.TaskUsers
            .Where(x => x.UserID.Equals(userId) && x.Role == role)
            .Join(db.Tasks, x => x.TaskId, y => y.Id, (x, y) => y)
            .Where(x => x.Status != TaskStatus.Canceled && x.Status != TaskStatus.Done)
            .Count());

        internal int GetUserWaitingTaskCount(string userId, TaskUserRole role) =>
            DbRead(db =>
                db.TaskUsers
            .Where(x => x.UserID.Equals(userId) && x.Role == role)
            .Join(db.Tasks, x => x.TaskId, y => y.Id, (x, y) => y)
            .Where(x => x.Status == TaskStatus.Waiting)
            .Count());
        internal UserDutyStationStatus GetUserDutyStationStatus(string userId, long time)
            => DbRead(db =>
            {
                var assign = GetUserDutyStationInternal(db,userId, time);
                if (assign == null)
                    return null;
                var status = db.UserDutyStationStatus.Where(x => x.UserId == userId && x.DutyStationId == assign.Id).FirstOrDefault();
                if (status == null)
                    return new UserDutyStationStatus { UserId = userId, DutyStationId = assign.Id };
                return status;
            });


        internal List<MisTask> GetActiveTasks(bool rootOnly,bool activeOnly, String filter, int index, int pageSize, out int totalN)
        {
            int count = 0;
            var ret = DbRead(db =>
            {
                var rs = db.Tasks.AsNoTracking();
                if (activeOnly)
                    rs = rs.Where(x => x.Status != TaskStatus.Canceled && x.Status != TaskStatus.Done);
                if (rootOnly)
                    rs = rs.Where(x => x.ParentTaskId == null);
                
                if (filter != null)
                    rs = rs.Where(x => x.Title.Contains(filter) ||
                      x.Description.Contains(filter) ||
                      db.CheckLists.Where(x=>x.TaskId==x.Id && x.Name.Contains(filter)).Any()
                    );
                rs=rs.OrderBy(x=>x.Status==TaskStatus.Canceled?1:0).ThenByDescending(x => x.UpdateTime);
                count = rs.Count();
                if (pageSize == -1)
                    return rs.ToList();
                return rs.Skip(index)
                .Take(pageSize)
                .ToList();
            });
            totalN = count;
            return ret;
        }
        internal List<MisTask> GetActiveTasksByUser(string user,TaskUserRole role,string filter, int index, int pageSize, out int totalN)
        {
            int count = 0;
            var ret = DbRead(db =>
            {
                var rs = db.Tasks.Join(db.TaskUsers.Where(x=>x.UserID==user && x.Role==role),x=>x.Id,x=>x.TaskId,(x,y)=>x)
                .AsNoTracking()
                .Where(x => x.Status != TaskStatus.Canceled && x.Status != TaskStatus.Done);
                if (filter != null)
                    rs = rs.Where(x => x.Title.Contains(filter) ||
                      x.Description.Contains(filter) ||
                      db.CheckLists.Where(x => x.TaskId == x.Id && x.Name.Contains(filter)).Any()
                    );
                rs = rs.OrderBy(x => x.Status == TaskStatus.Canceled ? 1 : 0).ThenByDescending(x => x.UpdateTime);
                count = rs.Count();
                return rs.Skip(index)
                .Take(pageSize)
                .ToList();
            });
            totalN = count;
            return ret;
        }

        internal DutyStation GetUserDutyStation(string userId, long time)
            => DbRead(db => GetUserDutyStationInternal(db, userId, time));

        private static DutyStation GetUserDutyStationInternal(SmartLedgerDb db, string userId, long time)
        {
            return db.UserDutyStations.AsNoTracking().Where(x =>
                        x.UserId.Equals(userId) && x.AssignedTime <= time && (x.LeftTime == null || time < x.LeftTime.Value)).Join(db.DutyStations
                            , x => x.DutyStationId, y => y.Id, (x, y) => y).FirstOrDefault();
        }

        

        internal DutyStation GetDutyStation(Guid id)
=> DbRead(db => db.DutyStations.AsNoTracking().Where(x => x.Id == id).FirstOrDefault());
        internal List<DutyStation> GetAllDutyStations()
            => DbRead(db => db.DutyStations.AsNoTracking().ToList());
        public OnDutyCheck GetLastOnDutyCheck(string userId)
            => DbRead(db =>
                db.OnDutyChecks.AsNoTracking().Where(x => x.UserId.Equals(userId))
                .OrderByDescending(x => x.Time).FirstOrDefault());

        public void LogDutyCheck(OnDutyCheck check)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(check.UserId, "LogDutyCheck", check, now, (db, aid) =>
            {
                var assignedDS = GetUserDutyStation(check.UserId, check.Time);
                if (assignedDS == null)
                    throw new UserFriendlyError("No duty station assigned");
                var status = db.UserDutyStationStatus.AsNoTracking().Where(x => x.UserId == check.UserId).FirstOrDefault();
                var insertStatus = status == null;
                if (insertStatus)
                    status = new UserDutyStationStatus
                    {
                        UserId = check.UserId,
                        DutyStationId = assignedDS.Id,
                    };
                else
                {
                    if (status.DutyStationId != assignedDS.Id)
                    {
                        if (status.CheckInTime != null)
                            throw new UserFriendlyError("Already checked on another duty station");
                    }
                }
                switch (check.CheckType)
                {
                    case OnDutyCheckType.CheckIn:
                        status = new UserDutyStationStatus
                        {
                            UserId = check.UserId,
                            DutyStationId = assignedDS.Id,
                            CheckInTime = check.Time
                        };
                        break;
                    case OnDutyCheckType.CheckOut:
                        if (status.CheckInTime == null)
                            throw new UserFriendlyError("Not checked in");
                        status = new UserDutyStationStatus
                        {
                            UserId = check.UserId,
                            DutyStationId = assignedDS.Id,
                            CheckOutTime = check.Time,
                        };
                        break;
                    case OnDutyCheckType.RunningLate:
                        status = new UserDutyStationStatus
                        {
                            UserId = check.UserId,
                            DutyStationId = assignedDS.Id,
                            RunningLateTime = check.Time,
                            Eta = check.Eta,
                            Reason = check.Reason,
                        };
                        break;
                    case OnDutyCheckType.NotComing:
                        status = new UserDutyStationStatus
                        {
                            UserId = check.UserId,
                            DutyStationId = assignedDS.Id,
                            NotComingTime=check.Time,
                            Reason=check.Reason
                        };
                        break;
                    case OnDutyCheckType.TakingBreak:
                        if (status.CheckInTime == null)
                            throw new UserFriendlyError("Not checked in");
                        status = new UserDutyStationStatus
                        {
                            UserId = check.UserId,
                            DutyStationId = assignedDS.Id,
                            BreakTime = check.Time,
                            Eta = check.Eta,
                            Reason = check.Reason,
                        };
                        break;
                    case OnDutyCheckType.OffDutyStationAssignment:
                        status = new UserDutyStationStatus
                        {
                            UserId = check.UserId,
                            DutyStationId = assignedDS.Id,
                            OutOfficeTaskTime = check.Time,
                            Eta = check.Eta,
                            Reason = check.Reason,
                        };
                        break;
                    case OnDutyCheckType.DontDisturb:
                        if (status.CheckInTime == null)
                            throw new UserFriendlyError("Not checked in");

                        status = new UserDutyStationStatus
                        {
                            UserId = check.UserId,
                            DutyStationId = assignedDS.Id,
                            DontDesturbTime = check.Time,
                            Eta = check.Eta,
                            Reason = check.Reason,
                        };
                        break;
                    default:
                        break;
                }
                check.Id = Guid.NewGuid();
                check.AuditId = aid;
                db.OnDutyChecks.Add(check);
                status.AuditId = aid;
                if (insertStatus)
                    db.UserDutyStationStatus.Add(status);
                else
                    db.UserDutyStationStatus.Update(status);
            });
        }
        public UserDutyStationException GetDSException(String userId, long time) =>
            DbRead(db => db.Leaves.Where(x => x.UserId.Equals(userId) && x.FromTime >= time && time < x.ToTime).FirstOrDefault());


        internal void AssignDutyStation(string userId, UserDutyStation userDS, object dutySchedule)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(userId, "AssignDutyStation", userDS, now, (db, aid) =>
            {
                var ds = new UserDutyStation
                {
                    AuditId = aid,
                    DutyStationId = userDS.DutyStationId,
                    UserId = userDS.UserId,
                };
                var schedule = new DutyStationSechedule
                {
                    AuditId = aid,
                    DutyStationId = userDS.DutyStationId,
                    UserId = userDS.UserId,
                    ScheduleType = dutySchedule.GetType().ToString(),
                    ScheduleData = Newtonsoft.Json.JsonConvert.SerializeObject(dutySchedule)
                };
                var existingDs = db.UserDutyStations.AsNoTracking().Where(x => x.DutyStationId == userDS.DutyStationId && x.UserId.Equals(userDS.UserId)).FirstOrDefault();
                if (existingDs != null)
                {
                    ds.Id = existingDs.Id;
                    db.UserDutyStations.Update(ds);
                }
                else
                {
                    var status = db.UserDutyStationStatus.Where(x => x.UserId == userId).FirstOrDefault();
                    if (status != null && status.CheckInTime != null)
                        throw new UserFriendlyError($"User must first checkout from his current duty station");
                    ds.Id = Guid.NewGuid();
                    db.UserDutyStations.Add(ds);
                }
                var existingSchedule = db.DutySchedules.AsNoTracking().Where(x => x.DutyStationId == userDS.DutyStationId && x.UserId.Equals(userDS.UserId)).FirstOrDefault();
                if (existingSchedule != null)
                {
                    schedule.Id = existingSchedule.Id;
                    db.DutySchedules.Update(schedule);
                }
                else
                {
                    schedule.Id = Guid.NewGuid();
                    db.DutySchedules.Add(schedule);
                }
            });
        }

        internal Holiday GetHoliday(long time) => DbRead(db => db.Holidays.Where(x => x.FromTime >= time && time < x.ToTime).FirstOrDefault());
        T GetTaskConfigurationInternal<T>(SmartLedger.SmartLedgerDb db) where T : class
        {
            var c = db.TaskConfig.AsNoTracking().FirstOrDefault();
            if (c == null || c.Config == null)
                return null;
            return Newtonsoft.Json.JsonConvert.DeserializeObject<T>(c.Config);
        }
        public void SetTaskConfiguration<T>(String userId, T config)
        {
            AuditTransactNoReturn(userId, "SetTaskConfiguration", config, TGBot.Now(), (db, aid) =>
            {
                var e = GetEntityInternal(db);
                if (e == null)
                    throw new UserFriendlyError("Company information not setup");
                if (!e.Owner.Equals(userId))
                    throw new UserFriendlyError("Only owner of the company can do this");

                var tc = new TaskEntityConfiguration
                {
                    Config = config == null ? null : Newtonsoft.Json.JsonConvert.SerializeObject(config),
                    EntityID = e.Id,
                    AuditId = aid,
                };
                if (db.TaskConfig.Any())
                    db.TaskConfig.Update(tc);
                else
                    db.TaskConfig.Add(tc);
            });
        }
        internal int DutyStationsCount()
        {
            return DbRead(db => db.DutyStations.Count());
        }
        public T GetTaskConfiguration<T>() where T : class => DbRead(db => GetTaskConfigurationInternal<T>(db));
        internal String AddDutyStation(string userId, DutyStation dutyStation)
        {
            var now = TGBot.Now();
            return AuditTransactReturn<String>(userId, "AddDutyStation", dutyStation, now, (db, aid) =>
            {
                var max = 0;
                foreach (var ds in db.DutyStations)
                {
                    var index = int.Parse(ds.Code.Substring(DutyStation.CODE_PREFIX.Length));
                    if (index > max)
                        max = index;
                }
                dutyStation.Id = Guid.NewGuid();
                dutyStation.AuditId = aid;
                dutyStation.Code = DutyStation.CODE_PREFIX + (max + 1).ToString();
                db.DutyStations.Add(dutyStation);
                return dutyStation.Code;
            });
        }

        internal List<MisTask> GetWaitingTasks(Guid taskId) =>
            DbRead(db =>
                db.WaitingTasks
            .Where(x => x.WaitingForTaskId == taskId)
            .Join(db.Tasks, x => x.TaskId, y => y.Id, (x, y) => y)
            .Where(x => x.Status != TaskStatus.Canceled && x.Status != TaskStatus.Done)
            .AsNoTracking().ToList());

        internal MisTask GetTask(Guid taskID) =>
            DbRead(db => GetTaskInternal(db, taskID));
        public List<TaskCheckListItem> GetTaskCheckList(Guid taskID) =>
            DbRead(db => db.CheckLists
            .AsNoTracking()
            .Where(x => x.TaskId == taskID)
            .OrderBy(x => x.OrderN).
            ToList());
        public List<ContentData> GetTaskContents(Guid taskID) =>
            DbRead(db => db.TaskContents
            .AsNoTracking()
            .Where(x => x.TaskId == taskID)
            .OrderBy(x => x.OrderN).AsNoTracking()
            .ToList()
            .Select(x => x.AsContentData())
            .ToList()
            );
        public List<ContentData> GetTaskContentIndex(Guid taskID) =>
            DbRead(db => db.TaskContents
            .AsNoTracking()
            .Where(x => x.TaskId == taskID)
            .OrderBy(x => x.OrderN).AsNoTracking()
            .Select(x=>new TaskContent { Id=x.Id,ImgeMime=x.ImgeMime,ContentLink=x.ContentLink,
                LinkType=x.LinkType,OrderN=x.OrderN,TaskId=x.TaskId,Caption=x.Caption})
            .ToList()
            .Select(x => x.AsContentData())
            .ToList()
            );
        private static MisTask GetTaskInternal(SmartLedgerDb db, Guid taskID)
        {
            return db.Tasks.AsNoTracking().Where(x => x.Id == taskID).FirstOrDefault();
        }
        internal MisTask GetTaskByCode(String code) =>
            DbRead(db => GetTaskInternal(db, code));

        private static MisTask GetTaskInternal(SmartLedgerDb db, string code)
        {
            return db.Tasks.AsNoTracking().Where(x => x.Code.ToLower()==code.ToLower()).FirstOrDefault();
        }

        internal void StartTask(string userId, Guid taskId)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(userId, "StartTask", 
                new TaskDelta.DeltaChangeStatus {UserId=userId,NetState=TaskStatus.Started }, 
                now, (db, aid) =>
            {
                var task = GetTaskInternal(db, taskId);
                if (task == null || task.Status == TaskStatus.Started)
                    throw new UserFriendlyError($"An task with the given id:{taskId} that is not started not found");
                var users = GetTaskUsersInternal(db, taskId, TaskUserRole.Worker);
                if (!users.Where(x => x.UserId.Equals(userId)).Any())
                    throw new UserFriendlyError($"Only users that have joined a task can start it. Task ID:{taskId}");
                task.StartTime = now;
                task.AuidtId = aid;
                task.WaitingSince = null;
                task.Status = TaskStatus.Started;
                db.Update(task);
                AddTaskDelta(db, taskId, aid);
            });
        }
        internal void FinishTask(string userId, Guid taskId)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(userId, "FinishTasks",
                new TaskDelta.DeltaChangeStatus { UserId = userId, NetState = TaskStatus.Done },
                now, (db, aid) =>
            {
                var task = GetTaskInternal(db, taskId);
                if (task == null || task.Status != TaskStatus.Started)
                    throw new UserFriendlyError($"A started task with the given id:{taskId} not found");
                var users = GetTaskUsersInternal(db, taskId, TaskUserRole.Worker);
                if (!users.Where(x => x.UserId.Equals(userId)).Any())
                    throw new UserFriendlyError($"Only users that have joined a task can finish it. Task ID:{taskId}");
                TraverseChildInternal(db, taskId, onlyActive: true, func: child =>
                {
                    if (child.Status != TaskStatus.Done && child.Status != TaskStatus.Canceled)
                        throw new UserFriendlyError($"Task {child.CodeName} is still not done");
                    return true;
                });
                    
                
                task.EndTime = now;
                task.Status = TaskStatus.Done;
                task.AuidtId = aid;
                task.WaitingSince = null;
                db.Update(task);
                AddTaskDelta(db, taskId, aid);
            });
        }
        internal void CancelTask(string userId, Guid taskId)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(userId, "CancelTask",
                new TaskDelta.DeltaChangeStatus { UserId = userId, NetState = TaskStatus.Canceled },
                now, (db, aid) =>
                {
                    var task = GetTaskInternal(db, taskId);
                    if (task == null || task.Status == TaskStatus.Canceled)
                        throw new UserFriendlyError($"A task with the given id:{taskId} that can be canceled not found");
                    var users = GetTaskUsersInternal(db, taskId, TaskUserRole.Worker);
                    if (!users.Where(x => x.UserId.Equals(userId)).Any())
                        throw new UserFriendlyError($"Only users that have joined a task can cancel it. Task ID:{taskId}");
                    task.EndTime= now;
                    task.Status = TaskStatus.Canceled;
                    task.AuidtId = aid;
                    task.WaitingSince = null;
                    db.Update(task);
                    AddTaskDelta(db, taskId, aid);
                });
        }

        internal FlowReportVersion GetCurrentFRVersion()
        => DbRead(db =>
        {
            return GetCurrentFRVersionInternal(db);
        });

        private static FlowReportVersion GetCurrentFRVersionInternal(SmartLedgerDb db)
        {
            var rs = db.FRVersions.AsNoTracking().Where(x => x.ToTime == null).FirstOrDefault();
            if (rs == null)
                return null;
            return rs;
        }

        internal void SetCurrentFRVersion(String userId,long fromTime,int verionNumber)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(userId, "SetCurrentFRVersion", verionNumber, now, (db, aid) =>
               {
                   var current=GetCurrentFRVersionInternal(db);
                   if (current == null)
                   {
                       db.FRVersions.Add(new FlowReportVersion()
                       {
                           VersionNumber = verionNumber,
                           FromTime = fromTime,
                           ToTime = null,
                           AuditId = aid
                       });
                   }
                   else
                   {
                       if (current.VersionNumber >= verionNumber)
                           throw new UserFriendlyError("Version number must be greater than the current version number");
                       if(current.FromTime> fromTime)
                           throw new UserFriendlyError("The lower time bound must be after the current version");
                       if (current.FromTime == fromTime)
                       {
                           db.FRVersions.Update(new FlowReportVersion()
                           {
                               FromTime = fromTime,
                               VersionNumber = verionNumber,
                               ToTime = null,
                               AuditId = aid
                           });
                       }
                       else
                       {
                           current.ToTime = fromTime;
                           current.AuditId = aid;
                           db.FRVersions.Update(current);
                           db.FRVersions.Add(new FlowReportVersion()
                           {
                               VersionNumber = verionNumber,
                               FromTime = fromTime,
                               ToTime = null,
                               AuditId = aid
                           });
                       }
                   }
               });
        }

        internal List<MisTask> GetSubTasks(Guid taskId) => DbRead(db =>
          db.Tasks.AsNoTracking().Where(x => x.ParentTaskId == taskId).OrderByDescending(x=>x.UpdateTime).ToList());

        internal AuditRecord GetLastTaskUpdateTime(string userId)
            => DbRead(db =>
            db.TaskDeltas
            .Join(db.AuditRecords, x => x.AuditId, y => y.Id, (x, y) => y)
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.Time)
            .FirstOrDefault()
            );
        internal AuditRecord GetLastTaskUpdateTimeByTask(Guid taskId)
            => DbRead(db =>
            db.TaskDeltas.Where(x=>x.TaskId==taskId)
            .Join(db.AuditRecords, x => x.AuditId, y => y.Id, (x, y) => y)
            .OrderByDescending(x => x.Time)
            .FirstOrDefault()
            );
        internal void PauseTask(string userId, Guid taskId,String Reason)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(userId, "PauseTask",
                new TaskDelta.DeltaChangeStatus { UserId = userId, NetState = TaskStatus.Waiting,Remark=Reason },
                now, (db, aid) =>
                {
                    var task = GetTaskInternal(db, taskId);
                    if (task == null || task.Status != TaskStatus.Started)
                        throw new UserFriendlyError($"A started task with the given id:{taskId} not found");
                    var users = GetTaskUsersInternal(db, taskId, TaskUserRole.Worker);
                    if (!users.Where(x => x.UserId.Equals(userId)).Any())
                        throw new UserFriendlyError($"Only users that have joined a task can cancel it. Task ID:{taskId}");
                    task.EndTime = now;
                    task.Status = TaskStatus.Waiting;
                    task.WaitingFor = Reason;
                    task.AuidtId = aid;
                    task.WaitingSince =now;
                    db.Update(task);
                    AddTaskDelta(db, taskId, aid);
                });
        }

        public List<MisUserProfile> GetTaskUsers(Guid taskId, TaskUserRole role) => DbRead(db => GetTaskUsersInternal(db, taskId, role));
        public int GetTaskUsersCount(Guid taskId, TaskUserRole role) => DbRead(db => GetTaskUsersCountInternal(db, taskId, role));
        private int GetTaskUsersCountInternal(SmartLedgerDb db, Guid taskId, TaskUserRole role)
        {
            return db.MisUserProfiles.Join(db.TaskUsers.Where(x => x.Role == role && x.TaskId == taskId), x => x.UserId, y => y.UserID, (x, y) => x)
                .AsNoTracking().Count();
        }
        private List<MisUserProfile> GetTaskUsersInternal(SmartLedgerDb db, Guid taskId, TaskUserRole role)
        {
            return db.MisUserProfiles.Join(db.TaskUsers.Where(x => x.Role == role && x.TaskId==taskId), x => x.UserId, y => y.UserID, (x, y) => x)
                .AsNoTracking().ToList();
        }
        public List<TaskUser> GetTaskUserInfo(Guid taskId, TaskUserRole role) => DbRead(db => GetTaskUserInfoInteral(db, taskId, role));
        private List<TaskUser> GetTaskUserInfoInteral(SmartLedgerDb db, Guid taskId, TaskUserRole role)
        {
            return db.TaskUsers.Where(x => x.Role == role && x.TaskId == taskId).ToList();
        }

        internal WorkerState GetUserWorkState(string userId)
            => DbRead(db =>
            {
                var res = db.WorkerStates.Where(x => x.UserId.Equals(userId)).FirstOrDefault();
                if (res == null)
                {
                    return new WorkerState() { UserId = userId };
                }
                return Newtonsoft.Json.JsonConvert.DeserializeObject<WorkerState>(res.StateData);
            });
        public void SaveWorkerState(WorkerState state)
        {
            TransactNoReturn(db =>
            {
                if (db.WorkerStates.Where(x => x.UserId.Equals(state.UserId)).Any())
                    db.WorkerStates.Update(new UserWorkState { UserId = state.UserId, StateData = Newtonsoft.Json.JsonConvert.SerializeObject(state) });
                else
                    db.WorkerStates.Add(new UserWorkState { UserId = state.UserId, StateData = Newtonsoft.Json.JsonConvert.SerializeObject(state) });
            });
        }
        public class GetTaskHistoryResultItem
        {
            public long time;
            public Object obj;
        }
        public List<GetTaskHistoryResultItem> GetTaskHistory(Guid taskId)
        {
            return DbRead(db => db.DeltaRecords.Join(db.TaskDeltas.Where(x=>x.TaskId==taskId), x => x.AuditId, x => x.AuditId, (x, y) => new { a = x, taskId = y.TaskId })
            .OrderByDescending(x => x.a.RecordNo)
            .AsEnumerable()
            .Select(x =>
            new GetTaskHistoryResultItem {
                time = x.a.Time,
                obj = Newtonsoft.Json.JsonConvert.DeserializeObject(x.a.Data, Type.GetType(x.a.DataType))
                }
            ).ToList()
            );
        }
        public Object GetTaskHistoryItem(Guid auidId)
        {
            return DbRead(db =>
            {
                var del = db.DeltaRecords.Where(x => x.AuditId == auidId).FirstOrDefault();
                if (del == null)
                    return null;


                return Newtonsoft.Json.JsonConvert.DeserializeObject(del.Data, Type.GetType(del.DataType));
            });            
        }

        internal List<MisTask> GetAllTasksList(long ?createTimeFrom=null, long ?createTimeTo=null, Func<MisTask,bool> filter=null)
        {
            return DbRead(db =>
            {
                var rs = db.Tasks.AsNoTracking();
                if (createTimeFrom != null)
                    rs=rs.Where(x => x.CreateTime >= createTimeFrom.Value);
                if (createTimeTo != null)
                    rs=rs.Where(x => x.CreateTime < createTimeTo.Value);
                if (filter != null)
                    return rs.AsEnumerable().Where(filter).OrderBy(x=>x.CreateTime).ToList();
                return rs.OrderBy(x => x.CreateTime).ToList();
            });
        }

        internal int GetTaskHeirarchyLevel(Guid id)
        {
            return DbRead(db =>
            {
                var level = 0;
                MisTask t;
                while((t=GetTaskInternal(db,id)).ParentTaskId!=null)
                {
                    id = t.ParentTaskId.Value;
                    level++;
                }
                return level;
            });
        }
        internal string GetTaskHeirarchyCode(Guid id,String separator)
        {
            return DbRead(db =>
            {
                string ret = GetTaskInternal(db, id).Code;
                MisTask t;
                while ((t = GetTaskInternal(db, id)).ParentTaskId != null)
                {
                    id = t.ParentTaskId.Value;
                    ret = GetTaskInternal(db, id).Code+separator+ret;
                }
                return ret;
            });
        }
    }

}
