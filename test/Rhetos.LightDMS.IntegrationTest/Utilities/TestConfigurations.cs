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

using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Threading;

namespace Rhetos.LightDMS.IntegrationTest.Utilities
{
    public class TestConfigurations
    {
        private static readonly Lazy<Content> _instance = new(() =>
        {
            string testConfigPath = Path.Combine("..", "..", "..", "..", "..", "test-config.json");
            var fullPath = Path.GetFullPath(testConfigPath);
            var builder = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddJsonFile(fullPath);

            var configuration = builder.Build();

            var x = configuration.Get<Content>();
            return x;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        public static Content Instance => _instance.Value;

        public class Content
        {
            public string MasterConnectionString { get; set; }
            public string SqlServerName { get; set; }
            public string FileStreamDatabaseName { get; set; }
            public string FileStreamFileLocation { get; set; }
            public string VarBinaryDatabaseName { get; set; }
            public string SqlServerCredential { get; set; }
        }
    }
}
