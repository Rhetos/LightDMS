using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using Xunit.Abstractions;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class TestLogger : ILogger
    {
        private readonly ITestOutputHelper _testOutput;
        private readonly string _eventName;

        public TestLogger(ITestOutputHelper testOutput, string eventName)
        {
            _testOutput = testOutput;
            _eventName = eventName;
        }

        public string Name => _eventName;

        public void Write(EventType eventType, Func<string> logMessage)
        {
            _testOutput.WriteLine($"[{eventType}] {_eventName}: {CsUtility.Limit(logMessage(), 50000, true)}");
        }
    }
}