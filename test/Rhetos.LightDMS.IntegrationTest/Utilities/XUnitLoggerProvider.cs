/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using Microsoft.Extensions.Logging;
using System;
using Xunit.Abstractions;

namespace Rhetos.LightDMS.IntegrationTest
{
    /// <summary>
    /// Routes ASP.NET framework log messages (e.g. the unhandled-exception report from
    /// DeveloperExceptionPageMiddleware) to the xUnit test output instead of the console.
    /// The messages remain visible in the individual test's output/result, but no longer
    /// spam the shared test-run console. Register it after clearing the default providers.
    /// </summary>
    public class XUnitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _testOutput;

        public XUnitLoggerProvider(ITestOutputHelper testOutput)
        {
            _testOutput = testOutput;
        }

        public Microsoft.Extensions.Logging.ILogger CreateLogger(string categoryName)
        {
            return new XUnitLogger(_testOutput, categoryName);
        }

        public void Dispose()
        {
        }

        private class XUnitLogger : Microsoft.Extensions.Logging.ILogger
        {
            private readonly ITestOutputHelper _testOutput;
            private readonly string _categoryName;

            public XUnitLogger(ITestOutputHelper testOutput, string categoryName)
            {
                _testOutput = testOutput;
                _categoryName = categoryName;
            }

            public IDisposable BeginScope<TState>(TState state) => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                try
                {
                    var message = formatter(state, exception);
                    _testOutput.WriteLine($"{logLevel}: {_categoryName}[{eventId.Id}]{Environment.NewLine}      {message}");
                    if (exception != null)
                        _testOutput.WriteLine(exception.ToString());
                }
                catch (InvalidOperationException)
                {
                    // ITestOutputHelper throws when the host logs after the test has finished
                    // (e.g. during factory disposal). There is no active test to attribute the
                    // message to, so it is safely ignored.
                }
            }
        }
    }
}
