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