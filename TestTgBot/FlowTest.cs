using Microsoft.Extensions.DependencyInjection;
using TgBot.SmartLedger;
using TgBot.SmartLedger.AccountReconciliation;

namespace TestTgBot
{
    public class FlowTest
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void Test1()
        {
            Assert.Pass();
        }
        public void TestFull()
        {
            var service = ServiceCollectionExtensions.CreateScope();
            var db=service.GetService<SmartLedgerDb>();
        }

        [Test]
        public void MyActionList_PaymentChecker_SeesCreatedRequest()
        {
            var checker = "checker";
            var actions = SmartLedgerActionPolicy.GetPaymentActions(
                checker,
                NewPayment(),
                NewWorkItem(PaymentWorkItem.WORK_TYPE_CREATE),
                new SimplePaymentFlowConfiguration { Checker1 = checker },
                Enumerable.Empty<PaymentSource>(),
                Enumerable.Empty<string>());

            Assert.That(actions, Is.EquivalentTo(new[] { "Check request" }));
        }

        [Test]
        public void MyActionList_Payer_DoesNotSeeRequestAlreadyPaidByThem()
        {
            var payer = "payer";
            var accountId = Guid.NewGuid();
            var payment = NewPayment();
            var config = new SimplePaymentFlowConfiguration
            {
                Payers = new List<SimplePaymentFlowConfiguration.AccountPayer>
                {
                    new SimplePaymentFlowConfiguration.AccountPayer { AccountId = accountId, Payer = payer }
                }
            };
            var sources = new[] { new PaymentSource { CashAccountId = accountId, Amount = payment.Amount } };

            var beforePay = SmartLedgerActionPolicy.GetPaymentActions(
                payer,
                payment,
                NewWorkItem(PaymentWorkItem.WORK_TYPE_APPROVE),
                config,
                sources,
                Enumerable.Empty<string>());
            var afterPay = SmartLedgerActionPolicy.GetPaymentActions(
                payer,
                payment,
                NewWorkItem(PaymentWorkItem.WORK_TYPE_PAY),
                config,
                sources,
                new[] { payer });

            Assert.That(beforePay, Does.Contain("Pay request"));
            Assert.That(afterPay, Is.Empty);
        }

        [Test]
        public void MyActionList_Accountant_SeesFullyPaidRequest()
        {
            var payer = "payer";
            var accountant = "accountant";
            var accountId = Guid.NewGuid();
            var payment = NewPayment();
            var config = new SimplePaymentFlowConfiguration
            {
                Accountant = accountant,
                Payers = new List<SimplePaymentFlowConfiguration.AccountPayer>
                {
                    new SimplePaymentFlowConfiguration.AccountPayer { AccountId = accountId, Payer = payer }
                }
            };

            var actions = SmartLedgerActionPolicy.GetPaymentActions(
                accountant,
                payment,
                NewWorkItem(PaymentWorkItem.WORK_TYPE_PAY),
                config,
                new[] { new PaymentSource { CashAccountId = accountId, Amount = payment.Amount } },
                new[] { payer });

            Assert.That(actions, Is.EquivalentTo(new[] { "Account request" }));
        }

        [Test]
        public void MyActionList_ReconciliationOwner_SeesRequestedReconciliation()
        {
            var owner = "owner";

            var actions = SmartLedgerActionPolicy.GetReconciliationActions(
                owner,
                new Reconciliation { Id = Guid.NewGuid(), Creator = "creator" },
                new ReconciliationWorkItem { WorkType = ReconciliationWorkItem.WORK_TYPE_REQUEST },
                new CashEntity { Owner = owner });

            Assert.That(actions, Is.EquivalentTo(new[] { "Approve reconciliation" }));
        }

        private static Payment NewPayment()
            => new Payment
            {
                Id = Guid.NewGuid(),
                Amount = 10000,
                ToPayTo = "Vendor",
                Note = "Office supplies",
                Reference = "PR1"
            };

        private static PaymentWorkItem NewWorkItem(int workType)
            => new PaymentWorkItem
            {
                Id = Guid.NewGuid(),
                WorkType = workType,
                PaymentId = Guid.NewGuid()
            };
    }
}
