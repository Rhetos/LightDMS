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
