using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TgBot.SmartLedger
{
    enum PaymentFlowRoleType
    {
        Checker=1,
        Approver=2,
        Payer=2,
    }
    public class PaymentFlowRole
    {

        public int Id { get; set; }
        public String Name { get; set; }
    }
    
    public class PaymentFlowCommand
    {
        public String Key { get; set; }
        public String Name { get; set; }
    }
    public class PaymentFlowRuleNotification
    {
        public int Role { get; set; }
        public IList<PaymentFlowCommand> Commands { get; set; }
    }
    public class CommandResult
    {
        public IBotDialog Dialog { get; set; }
        public IList<PaymentFlowCommand> NotificationList {get;set;}
    }
    public interface IPaymentFlowRule
    {
        IBotDialog CreateDialog();
        CommandResult ProcessCommand(Guid payment, String command);
    }
   
}
