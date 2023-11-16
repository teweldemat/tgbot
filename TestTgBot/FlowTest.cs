using Microsoft.Extensions.DependencyInjection;
using TgBot.SmartLedger;

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
            var db=service.GetService<TgBotDb>();
        }
    }
}