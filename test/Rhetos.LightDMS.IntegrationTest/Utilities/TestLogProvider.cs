using Rhetos.Logging;
using Xunit.Abstractions;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class TestLogProvider: ILogProvider
    {
        private readonly ITestOutputHelper _testOutput;

        public TestLogProvider(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        public ILogger GetLogger(string eventName)
        {
            return new TestLogger(_testOutput, eventName);
        }
    }
}