using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using static TgBot.FormDialog;

namespace TgBot.Tasks
{
    [Table("TaskComment")]
    public class TaskComment
    {
        public Guid Id { get; set; }
        public Guid? ReplyOf { get; set; } = null;
        public long Time { get; set; }
        public String UserId { get; set; }
        public Guid TaskId { get; set; }
        public String Comment { get; set; }
        public Guid AuidtId { get; set; }
    }
    [Table("TaskEntityConfiguration")]
    public class TaskEntityConfiguration
    {
        [Key]
        public Guid EntityID { get; set; }
        [MaxLength]
        public String Config { get; set; }
        public Guid AuditId { get; set; }
    }
    public class TaskConfigurationData
    {
        public String HROfficer { get; set; }
        public String HRManager { get; set; }
        public String HRBookKeeper { get; set; }
    }

    [Table("TaskWorkType")]
    public class TaskType
    {
        public int Id { get; set; }
        public String Name { get; set; }
        public int OrderN { get; set; }
    }
    [Table("UserTaskType")]
    public class UserTaskType
    {
        public Guid Id { get; set; }        
        public String UserId { get; set; }
        public int OrderN { get; set; }
        public int WorkTypeId { get; set; }
        public int AuditId { get; set; }
    }
    public enum OnDutyCheckType
    {
        CheckIn, 
        CheckOut,
        RunningLate,
        NotComing,
        TakingBreak,
        OffDutyStationAssignment,
        DontDisturb,
    }
    [Table("OnDutyCheck")]
    public class OnDutyCheck
    {
        public Guid Id { get; set; }
        public String UserId { get; set; }
        public long Time { get; set; }
        public long? Eta { get; set; }
        public bool SelfReported { get; set; }   
        public OnDutyCheckType CheckType { get; set; }
        public string Reason { get; set; }
        public Guid AuditId { get; set; }
    }
    [Table("DutyStationSchedule")]
    public class DutyStationSechedule
    {   
        public Guid Id { get; set; }
        public String UserId{ get; set; }
        public Guid DutyStationId { get; set; }
        public String ScheduleType { get; set; }
        [MaxLength]
        public String ScheduleData { get; set; }
        public long SetTime { get; set; }
        public Guid AuditId { get; set; }
    }
    [Table("UserDutyStationException")]
    public class UserDutyStationException
    {
        public Guid Id { get; set; }
        public String UserId { get; set; }
        public Guid RequestId { get; set; }
        public bool remoteWork { get; set; }
        public long FromTime { get; set; }
        public long ToTime { get; set; }
        public String Reason { get; set; }        
        public Guid AuditId { get; set; }
    }
    [Table("DutyStation")]
    public class DutyStation
    {
        internal const string CODE_PREFIX="DS";

        public Guid Id { get; set; }
        public String Name { get; set; }
        public String Address { get; set; }
        public String Code { get; set; }
        public Guid AuditId { get; set; }
        
    }
    [Table("UserDutyStation")]
    public class UserDutyStation
    {
        public Guid Id { get; set; }
        public String UserId { get; set; }
        public Guid DutyStationId { get; set; }
        public long AssignedTime { get; set; }
        public long? LeftTime { get; set; }
        public Guid AuditId { get; set; }
    }
    [Table("UserDutyStationStatus")]
    public class UserDutyStationStatus
    {
        [Key]
        public String UserId { get; set; }
        public Guid DutyStationId { get; set; }
        public long? CheckInTime { get; set; } = null;
        public long? CheckOutTime { get; set; } = null;
        public long? BreakTime { get; set; } = null;
        public long? OutOfficeTaskTime { get; set; } = null;
        public long? DontDesturbTime { get; set; } = null;
        public long? RunningLateTime { get; set; } = null;
        public long? NotComingTime { get; set; }
        public long? Eta { get; set; } = null;
        public string Reason { get; set; }
        public Guid AuditId { get; set; }
        
    }
    public enum TaskStatus
    {
        Planned=1,
        Started=2,
        Done=3,
        Waiting=4,
        Canceled=5
    }
    public class TaskFullData
    {
        public MisTask Task { get; set; }
        public IEnumerable<String> CheckList { get; set; }
        public IEnumerable<ContentData> Content { get; set; }

        public static String StatusString(MisTask task)
        {
            switch (task.Status)
            {
                case TaskStatus.Planned:
                    return $"Pending, planned to start on {TGBot.ToRelativeTime(task.PlannedStartTime.Value)}";
                case TaskStatus.Started:
                    return $"Started on {TGBot.ToRelativeTime(task.StartTime.Value)}";
                case TaskStatus.Done:
                    return $"Finished on {TGBot.ToRelativeTime(task.EndTime.Value)}";
                case TaskStatus.Waiting:
                    return $"Waiting for {task.WaitingFor} since {TGBot.ToRelativeTime(task.WaitingSince.Value)}";
                case TaskStatus.Canceled:
                    return $"Canceled on {TGBot.ToRelativeTime(task.EndTime.Value)}";
                default:
                    return "Unknown status";
            }
        }
    }
    [Table("MisTask")]
    public class MisTask
    {
        public const string TASK_CODE_PREFIX = "TSK";
        public Guid Id { get; set; }
        public String Code { get; set; }
        public long CreateTime { get; set; }
        public long UpdateTime { get; set; }
        public String CreatedBy { get; set; }
        public String UpdatedBy { get; set; }
        public TaskStatus Status { get; set; }
        public String Title { get; set; }
        public String Description { get; set; }
        public long? PlannedStartTime { get; set; } = null;
        public long? PlannedEndTime { get; set; } = null;
        public long? StartTime { get; set; } = null;
        public long? EndTime { get; set; } = null;
        public long? WaitingSince { get; set; } = null;
        public string WaitingFor { get; set; }
        public Guid AuidtId { get; set; }
        public Guid? ParentTaskId { get; set; }
        public int ChildCount { get; set; }
        public String CodeName => $"{Code} - {Title}";
    }
    
    public class TaskContent
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public int OrderN { get; set; }
        public byte[] Image { get; set; }
        public String ImgeMime { get; set; }
        public ContentLinkType LinkType { get; set; }
        public String ContentLink { get; set; }
        public String Caption { get; set; } = null;
        internal ContentData AsContentData()
        {
            return new ContentData {Id=Id, 
                ContentLink = ContentLink, 
                Image = Image, 
                ImageMime = ImgeMime, 
                LinkType = LinkType,
                Caption=Caption
            };
        }
    }
    public class WaitingTask
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }        
        public Guid WaitingForTaskId { get; set; }      
        public MisTask WaitForTask { get; set; }
        public long WaitingSince { get; set; }
        public Guid AuditId { get; set; }
    }
    public class TaskProgress
    {
        public Guid Id { get; set; }
        public Guid TaskId { get; set; }
        public String ReportedBy { get; set; }
        public String Note { get; set; }
    }
    public class TaskCheckListItem
    {
        public Guid Id { get; set; }
        public int OrderN { get; set; }
        public String Name { get; set; }
        public Guid TaskId { get; set; }
        public long? DoneTime { get; set; } = null;
        public Guid AuditId { get; set; }
    }
    public enum TaskUserRole
    {
        None=0,
        Worker=1,
        Follower=2
    }
    public class TaskUser
    {
        [Key]
        public Guid Id { get; set; }
        public String UserID { get; set; }        
        public Guid TaskId { get; set; }
        public long JoinedTime { get; set; }
        public long? LeftTime { get; set; } = null;
        public TaskUserRole Role { get; set; }
        public Guid AuditId { get; set; }
    }
    [Table("UserWorkState")]
    public class UserWorkState
    {
        [Key]
        public String UserId { get; set; }
        [MaxLength]
        public String StateData { get; set; }
    }
    [Table("FlowReportVersion")]
    public class FlowReportVersion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long FromTime { get; set; }
        public int VersionNumber { get; set; }
        public long? ToTime { get; set; }
        public Guid AuditId { get; internal set; }
    }
    
}
