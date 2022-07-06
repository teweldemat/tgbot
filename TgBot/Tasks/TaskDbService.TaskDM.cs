using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using TgBot.SmartLedger;
using static TgBot.FormDialog;

namespace TgBot.Tasks
{
    public partial class TaskDbService : SmartLedger.SmartLedgerService
    {
        public void AddComment(String userId,Guid taskId,string comment,Guid ?replyOf=null)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(userId, "AddComment",new TaskDelta.DeltaAddComment{UserId=userId,TaskId=taskId,Comment=comment},now,
                (db,aid)=>
                {
                    var task = GetTaskInternal(db, taskId);
                    if (task == null)
                        throw new UserFriendlyError("Task doesnt't exist");
                    db.TaskComments.Add(new TaskComment
                    {
                        AuidtId=aid,
                        Comment=comment,
                        Id=Guid.NewGuid(),
                        ReplyOf= replyOf,
                        TaskId=taskId,
                        Time=now,
                        UserId=userId,
                    });
                    AddTaskDelta(db, taskId, aid);
                });
        }
        public void UpdateComment(String userId, Guid taskId,Guid commentId, string comment, Guid? replyOf = null)
        {
            var now = TGBot.Now();
            
            AuditTransactNoReturn(userId, "UpdateComment", new TaskDelta.DeltaUpdateComment { UserId = userId, TaskId = taskId, Comment = comment }, now,
                (db, aid) =>
                {
                    var task = GetTaskInternal(db, taskId);
                    if (task == null)
                        throw new UserFriendlyError("Task doesnt't exist");
                    var c = db.TaskComments.Where(x => x.Id == commentId && x.TaskId == taskId).FirstOrDefault();
                    if (c== null)
                        throw new UserFriendlyError("Comment not found");
                    c.Comment = comment;
                    c.AuidtId = aid;
                    c.Time = now;
                    AddTaskDelta(db, taskId, aid);
                });
        }
        public void RemoveComment(String userId, Guid commentId)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(userId, "AddComment", new TaskDelta.DeltaRemoveComment { UserId=userId,CommentId=commentId }, now,
                (db, aid) =>
                {
                    var comment = db.TaskComments.Where(x => x.Id == commentId).FirstOrDefault();
                    if (comment== null)
                        throw new UserFriendlyError("Comment doesnt't exist");
                    if (!comment.UserId.Equals(userId))
                        throw new UserFriendlyError("Only the user that added the comment can remove it");
                    db.TaskComments.Remove(comment);
                    AddTaskDelta(db, comment.TaskId, aid);
                });
        }
        public List<TaskComment> GetTaskComments(Guid taskId) => DbRead(db =>
          db.TaskComments.Where(x=>x.TaskId==taskId).OrderByDescending(x=>x.Time).ToList()
        );
        public int GetTaskCommentCount(Guid taskId) => DbRead(db =>
          db.TaskComments.Where(x => x.TaskId == taskId).Count()
        );
        public TaskComment GetLastComment(Guid taskId) => DbRead(db =>
          db.TaskComments.Where(x => x.TaskId == taskId).OrderByDescending(x => x.Time).FirstOrDefault()
        );
        internal void SetContentCaption(string userId, Guid id, string caption)
        {
            AuditTransactNoReturn(userId, "SetContentCaption",
            new TaskDelta.DeltaSetContentCaption { UserId = userId, ContentId = id,Caption=caption },
                TGBot.Now(), (db, aid) =>
                {
                    var cont = db.TaskContents.Where(x => x.Id == id).FirstOrDefault();
                    if (cont == null)
                        throw new UserFriendlyError("Content not found");
                    var task = GetTaskInternal(db, cont.TaskId);
                    if (task.Status == TaskStatus.Canceled || task.Status == TaskStatus.Done)
                        throw new UserFriendlyError("Task can't be updated");
                    cont.Caption = caption;
                    AddTaskDelta(db, task.Id, aid);
                });
        }
        internal void RemoveContent(string userId, Guid taskId, Guid id)
        {
            AuditTransactNoReturn(userId, "RemoveContent",
            new TaskDelta.DeltaRemoveContent { UserId = userId, ContentId = id },
                TGBot.Now(), (db, aid) =>
            {
                var task = GetTaskInternal(db, taskId);
                if (task.Status == TaskStatus.Canceled || task.Status == TaskStatus.Done)
                    throw new UserFriendlyError("Task can't be updated");
                var cont = db.TaskContents.AsNoTracking().Where(x => x.Id == id).FirstOrDefault();
                db.TaskContents.Remove(cont);
                AddTaskDelta(db, taskId, aid);
            });
        }
        internal void ChangeTitle(string userId, Guid taskId, String title)
        {
            AuditTransactNoReturn(userId, "ChangeTitle",
            new TaskDelta.DeltaChangeTitle { UserId = userId, Title= title},
                TGBot.Now(), (db, aid) =>
                {
                    var task = GetTaskInternal(db, taskId);
                    if (task.Status == TaskStatus.Canceled || task.Status == TaskStatus.Done)
                        throw new UserFriendlyError("Task can't be updated");
                    task.Title=title;
                    task.AuidtId = aid;
                    db.Tasks.Update(task);
                    AddTaskDelta(db, taskId, aid);
                });
        }
        internal void ChangeDescription(string userId, Guid taskId, String description)
        {
            AuditTransactNoReturn(userId, "ChangeDescription",
            new TaskDelta.DeltaChangeDescription { UserId = userId, Description = description},
                TGBot.Now(), (db, aid) =>
                {
                    var task = GetTaskInternal(db, taskId);
                    if (task.Status == TaskStatus.Canceled || task.Status == TaskStatus.Done)
                        throw new UserFriendlyError("Task can't be updated");
                    task.Description= description;
                    task.AuidtId = aid;
                    db.Tasks.Update(task);
                    AddTaskDelta(db, taskId, aid);
                });
        }
        internal void ChangeDueDate(string userId, Guid taskId, long dueDate)
        {
            long now = TGBot.Now();
            AuditTransactNoReturn(userId, "ChangeDueDate",
            new TaskDelta.DeltaChangeDueDate{ UserId = userId, DueDate = dueDate},
                now, (db, aid) =>
                {
                    var task = GetTaskInternal(db, taskId);
                    if (task.Status == TaskStatus.Canceled || task.Status == TaskStatus.Done)
                        throw new UserFriendlyError("Task can't be updated");
                    task.PlannedEndTime = dueDate;
                    task.AuidtId = aid;
                    db.Tasks.Update(task);
                    AddTaskDelta(db, taskId, aid);
                });
        }
        internal void UpdateFullCheckList(string userId, Guid taskId, List<TaskCheckListItem> checkList)
        {
            AuditTransactNoReturn(userId, "UpdateFullCheckList",
        new TaskDelta.DeltaUpdateFullCheckList { UserId = userId, UpdatedItem = checkList },
    TGBot.Now(), (db, aid) =>
    {
        int n = 1;
        var set = new HashSet<Guid>();
        foreach (var item in checkList)
        {
            var existing = db.CheckLists.Where(x => x.Id == item.Id).FirstOrDefault();
            if (existing == null)
            {
                item.Id = Guid.NewGuid();
                item.OrderN = n++;
                item.TaskId = taskId;
                item.AuditId = aid;
                db.CheckLists.Add(item);
            }
            else
            {
                existing.AuditId = aid;
                existing.Name = item.Name;
                existing.OrderN = n++;
            }
            set.Add(item.Id);
        }
        foreach(var item in db.CheckLists.Where(x=>x.TaskId==taskId).ToList())
        {
            if (!set.Contains(item.Id))
                db.CheckLists.Remove(item);
        }
        AddTaskDelta(db, taskId, aid);
    });

        }

        internal void UpdateCheckList(string userId, Guid taskId, IEnumerable<TaskCheckListItem> items)
        {
            AuditTransactNoReturn(userId, "UpdateCheckList", 
                new TaskDelta.DeltaUpdateCheckList {UserId=userId, UpdatedItem=items}, 
                TGBot.Now(), (db, aid) =>
            {
                foreach (var item in items)
                {
                    var existing = db.CheckLists.Where(x => x.Id == item.Id).FirstOrDefault();
                    if (existing == null)
                        throw new UserFriendlyError("Checklist item doesn't exist");
                    existing.DoneTime = item.DoneTime;
                    existing.AuditId = aid; 
                }
                AddTaskDelta(db, taskId, aid);
            });
        }
        void AddTaskDelta(SmartLedgerDb db, Guid taskId,Guid aid)
        {
            db.TaskDeltas.Add(new TaskDelta { AuditId = aid, TaskId = taskId });
        }
        internal String CreateTask(string userId, TaskFullData data, IEnumerable<TaskUser> users)
        {
            var now = TGBot.Now();
            return AuditTransactReturn<String>(userId, "CreateTask", 
                new TaskDelta.DeltaCreateTask
                {
                    UserId=userId,
                    Task=data
                }, 
                now, (db, aid) =>
            {
                var max = 0;
                foreach (var ds in db.Tasks)
                {
                    var index = int.Parse(ds.Code.Substring(MisTask.TASK_CODE_PREFIX.Length));
                    if (index > max)
                        max = index;
                }
                //validate
                if (data.Task.Status == TaskStatus.Started)
                {
                    data.Task.StartTime = now;
                    data.Task.PlannedStartTime = now;
                }
                else
                {
                    if (data.Task.StartTime != null)
                        throw new UserFriendlyError("If start time can bet set for 'planned' task");
                }
                if (data.Task.PlannedStartTime == null)
                    throw new UserFriendlyError("Planned start time should be specified");
                if (data.Task.PlannedStartTime.Value < now)
                    throw new UserFriendlyError("Planned start time can't be in the past");
                if (data.Task.PlannedEndTime == null)
                    throw new UserFriendlyError("Planned end time should be specified");
                if (data.Task.PlannedEndTime.Value < data.Task.PlannedStartTime.Value)
                    throw new UserFriendlyError("Planned start time can't be in the past");

                data.Task.Id = Guid.NewGuid();
                data.Task.Code = MisTask.TASK_CODE_PREFIX + (max + 1);
                data.Task.UpdateTime = data.Task.CreateTime = now;
                data.Task.UpdatedBy = data.Task.CreatedBy = userId;
                data.Task.AuidtId = aid;
                if (data.Task.ParentTaskId != null)
                {
                    var parent = db.Tasks.Where(x=>x.Id==data.Task.ParentTaskId.Value).FirstOrDefault();
                    if (parent == null)
                        throw new UserFriendlyError("Parent task not found");
                    parent.ChildCount++;
                }
                db.Tasks.Add(data.Task);
                
                if (data.Content != null)
                {
                    var n = 1;
                    foreach (var c in data.Content)
                    {
                        VaidateContent(c);
                        db.TaskContents.Add(new TaskContent
                        {
                            Id = Guid.NewGuid(),
                            TaskId = data.Task.Id,
                            ContentLink = c.ContentLink,
                            LinkType = c.LinkType,
                            Image = c.Image,
                            ImgeMime = c.ImageMime,
                            Caption=c.Caption,
                            OrderN = n++,
                        });
                    }
                }
                if (data.CheckList != null)
                {
                    var n = 1;
                    foreach (var c in data.CheckList)
                    {
                        db.CheckLists.Add(new TaskCheckListItem
                        {
                            Id = Guid.NewGuid(),
                            TaskId = data.Task.Id,
                            Name = c,
                            OrderN = n++,
                        });
                    }
                }
                if (users != null)
                {
                    var hash = new HashSet<String>();
                    foreach (var u in users)
                    {
                        if (hash.Contains(u.UserID))
                            throw new UserFriendlyError("A user can be assigned only once to a task");
                        db.TaskUsers.Add(new TaskUser
                        {
                            Id = Guid.NewGuid(),
                            JoinedTime = now,
                            LeftTime = null,
                            Role = u.Role,
                            TaskId = data.Task.Id,
                            UserID = u.UserID,
                        });
                    }
                }
                AddTaskDelta(db, data.Task.Id, aid);
                return data.Task.Code;
            });
        }

        void TraverseChildInternal(SmartLedgerDb db, Guid? task, bool onlyActive = false, Func<MisTask, bool> func = null)
        {
            IQueryable<MisTask> childTasks;
            if(onlyActive)
                childTasks = db.Tasks.AsNoTracking().Where(x => x.ParentTaskId == task && x.Status!=TaskStatus.Canceled && x.Status!=TaskStatus.Done);
            else
                childTasks = db.Tasks.AsNoTracking().Where(x => x.ParentTaskId == task);
            foreach (var child in childTasks.ToList())
            {
                if (!func(child))
                    return;
                TraverseChildInternal(db, child.Id, onlyActive, func);
            }
        }

        public void TraverseChild(Guid? task, bool onlyActive = false, Func<MisTask, bool> func = null)
        {
            DbReadVoid(db =>
            {
               TraverseChildInternal(db, task, onlyActive, func);
            });
        }
        internal void AddSubTask(string userId, Guid taskId, Guid subTaskId)
        {
            AuditTransactNoReturn(userId, "AddDubTask",
                    new TaskDelta.DeltaAddSubTask { TaskId = taskId, UserId = userId, SubtaskID = subTaskId },
                    TGBot.Now(), (db, aid) =>
                    {
                        var task = GetTaskInternal(db, taskId);
                        if(task.Status==TaskStatus.Canceled || task.Status == TaskStatus.Done)
                            throw new UserFriendlyError("Sub tasks can't be inacative tasks");
                        if(taskId==subTaskId)
                            throw new UserFriendlyError($"A task can't be the subtask of itself.");
                        var child = GetTaskInternal(db, subTaskId);
                        if (child.ParentTaskId != null)
                            throw new UserFriendlyError("The task is a sub task of another task already");
                        bool linked = false;
                        TraverseChildInternal(db, subTaskId, onlyActive: false, func: x =>
                           {
                               if (x.Id == taskId)
                               {
                                   linked = true;
                                   return false;
                               }
                               return true;
                           });
                        if(linked)
                            throw new UserFriendlyError($"The {task.CodeName} is the sub task of {child.CodeName}");

                        var curTask= child;
                        while (curTask.ParentTaskId != null)
                        {
                            if (curTask.ParentTaskId==taskId)
                                throw new UserFriendlyError($"The {child.CodeName} is alrady subtask of {task.CodeName}");
                            curTask = GetTaskInternal(db, curTask.ParentTaskId.Value);
                        }
                        child.ParentTaskId = task.Id;
                        child.AuidtId = aid;
                        task.ChildCount++;
                        db.Tasks.Update(task);
                        db.Tasks.Update(child);
                        AddTaskDelta(db, taskId, aid);
                    });

        }

        internal void AddTaskContents(string userId, Guid taskId, List<ContentData> content)
        {
            AuditTransactNoReturn(userId, "AddTaskContents", 
                new TaskDelta.DeltaAddTaskContent { UserId = userId,  Content= content },
                TGBot.Now(), (db, aid) =>
            {
                var task = GetTaskInternal(db, taskId);
                var userRole = db.TaskUsers.Where(x => x.TaskId == taskId && x.UserID == userId).FirstOrDefault();
                if (!task.CreatedBy.Equals(userId) && (userRole == null || userRole.Role != TaskUserRole.Worker))
                    throw new UserFriendlyError("You are not allowed to do this");
                int n = db.TaskContents.Where(x => x.TaskId == taskId).Count() + 1;
                foreach (var c in content)
                {
                    VaidateContent(c);
                    db.TaskContents.Add(new TaskContent
                    {
                        Id = Guid.NewGuid(),
                        TaskId = taskId,
                        OrderN = n++,
                        Image = c.Image,
                        ImgeMime = c.ImageMime,
                        LinkType = c.LinkType,
                        ContentLink = c.ContentLink,
                        Caption=c.Caption,
                    });
                };
                AddTaskDelta(db,taskId, aid);
            });
        }

        private static void VaidateContent(ContentData c)
        {
            if (c.LinkType != ContentLinkType.Url && c.Image == null)
                throw new UserFriendlyError("Image should be specified unless the attachment is a url");
            if (!string.IsNullOrEmpty(c.ImageMime))
                try
                {
                    TGBot.MimeToExtension(c.ImageMime);
                }
                catch
                {
                    throw new UserFriendlyError("Unsupported file type");
                }
        }

        internal void AddTaskUser(string userId, Guid taskId, string userToAdd, TaskUserRole addedUserRole)
        {
            var now = TGBot.Now();
            AuditTransactNoReturn(userId, "AddTaskUser",
                new TaskDelta.DeltaAddTaskUser { UserId=userId, AddUserId=userToAdd,Role=addedUserRole },
                now, (db, aid) =>
            {
                var task = GetTaskInternal(db, taskId);
                var userRole = db.TaskUsers.Where(x => x.TaskId == taskId && x.UserID == userId).FirstOrDefault();
                if (!userId.Equals(userToAdd) && !task.CreatedBy.Equals(userId) && (userRole == null || userRole.Role != TaskUserRole.Worker))
                    throw new UserFriendlyError("You are not allowed to do this");
                var role = db.TaskUsers.Where(x => x.TaskId == taskId && x.UserID == userToAdd).FirstOrDefault();
                if (role != null && role.Role == addedUserRole)
                    throw new UserFriendlyError("The user already in the task");
                if (role == null)
                {
                    db.TaskUsers.Add(new TaskUser { 
                        Id = Guid.NewGuid(), 
                        UserID = userToAdd, 
                        TaskId = taskId, 
                        Role = addedUserRole, 
                        JoinedTime=now,
                        AuditId = aid });
                }
                else
                {
                    role.Role = addedUserRole;
                    role.JoinedTime = now;
                    role.AuditId = aid;
                }
                AddTaskDelta(db, taskId, aid);
            });
        }
        internal void RemoveTaskUser(string userId, Guid taskId, string userToRemove)
        {
            AuditTransactNoReturn(userId, "RemoveTaskUser",
                new TaskDelta.DeltaRemoveUser{ UserId = userId, UserToRemove = userToRemove},
                TGBot.Now(), (db, aid) =>
            {
                var task = GetTaskInternal(db, taskId);
                var userRole = db.TaskUsers.Where(x => x.TaskId == taskId && x.UserID == userId).FirstOrDefault();
                if (!task.CreatedBy.Equals(userId) && (userRole == null || userRole.Role != TaskUserRole.Worker))
                    throw new UserFriendlyError("You are not allowed to do this");

                var role = db.TaskUsers.Where(x => x.TaskId == taskId && x.UserID == userToRemove).FirstOrDefault();
                if (role == null)
                    throw new UserFriendlyError("The user is not in the task");
                db.TaskUsers.Remove(role);
                AddTaskDelta(db, taskId, aid);
            });
        }

    }

}
