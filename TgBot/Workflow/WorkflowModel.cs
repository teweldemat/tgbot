using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace TgBot.Workflow
{
    [Table("WorkFlowMain")]
    public class WorkFlowInfo
    {
        public Guid Id { get; set; }
        public String Reference { get; set; }
        public long Time { get; set; }
        public String Note { get; set; }
        public String Creator { get; set; }
        public int WorkType { get; set; }
        public Guid? WorkItemHead { get; set; }
        public long? WorkItemHeadTime { get; set; }
        public int? WorkItemHeadType { get; set; }
        public Guid AuditId { get; set; }
    }
    [Table("WorkItem")]
    public class WorkItem
    {
        public Guid Id { get; set; }
        public Guid WorkFlowId{ get; set; }
        public Guid? PrevItem { get; set; }
        public String UserId { get; set; }
        public long Time { get; set; }
        public String Note { get; set; }
        public Guid AuditId { get; set; }
        public int WorkType { get; set; }
        [MaxLength]
        public String Data { get; set; }   
    }
}
