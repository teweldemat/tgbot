using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TgBot.Tasks
{
    [Table("TaskDelta")]
    public class TaskDelta
    {        
        [Key]
        public Guid AuditId { get; set; }
        public Guid TaskId { get; set; }
        
        public class DeltaCreateTask
        {
            public String UserId { get; set; }
            public TaskFullData Task { get; set; }
        }

        internal class DeltaAddTaskContent
        {
            public List<FormDialog.ContentData> Content { get; set; }
            public string UserId { get; set; }
        }

        internal class DeltaAddTaskUser
        {
            public string UserId { get; set; }
            public string AddUserId { get; set; }
            public TaskUserRole Role { get; set; }
        }

        internal class DeltaRemoveUser
        {
            public string UserId { get; set; }
            public string UserToRemove { get; set; }
        }

        internal class DeltaUpdateCheckList
        {
            public String UserId { get;set; }
            public IEnumerable<TaskCheckListItem> UpdatedItem { get; set; }
        }

        internal class DeltaChangeStatus
        {
            public string UserId { get; set; }
            public TaskStatus NetState { get; set; }
            public string Remark { get; set; }
        }

        internal class DeltaUpdateFullCheckList
        {
            public string UserId { get; set; }
            public List<TaskCheckListItem> UpdatedItem { get; set; }
        }

        internal class DeltaRemoveContent
        {
            public string UserId { get; set; }
            public Guid ContentId { get; set; }
        }

        internal class DeltaChangeTitle
        {
            public string UserId { get; set; }
            public string Title { get; set; }
        }

        internal class DeltaChangeDescription
        {
            public string UserId { get; set; }
            public string Description { get; set; }
        }

        internal class DeltaChangeDueDate
        {
            public string UserId { get; set; }
            public long DueDate { get; set; }
        }

        internal class DeltaSetContentCaption
        {
            public string UserId { get; set; }
            public Guid ContentId { get; set; }
            public string Caption { get; set; }
        }

        internal class DeltaAddComment
        {
            public string UserId { get; set; }
            public Guid TaskId { get; set; }
            public string Comment { get; set; }
        }

        internal class DeltaRemoveComment
        {
            public string UserId { get; set; }
            public Guid CommentId { get; set; }
        }

        internal class DeltaAddSubTask
        {
            public Guid TaskId { get; set; }
            public string UserId { get; set; }
            public Guid SubtaskID { get; set; }
        }

        internal class DeltaUpdateComment
        {
            public string UserId { get; set; }
            public Guid TaskId { get; set; }
            public string Comment { get; set; }
        }
    }
    
}
