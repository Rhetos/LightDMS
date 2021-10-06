using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rhetos.LightDms.Storage;
using Rhetos.LightDMS.TestApp;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Rhetos.LightDMS.IntegrationTest
{
    [Collection("Utility classes")]
    public class SqlFileStreamProviderTests
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public SqlFileStreamProviderTests(ITestOutputHelper testOutputHelper)
        {
            _factory = new CustomWebApplicationFactory<Startup>(testOutputHelper);
        }

        [Fact]
        public async Task UploadThenDownload_ShouldSucceed()
        {
            string uploadContent = "Test file content";
            string downloadContent;

            var fileId = Guid.NewGuid();
            var fileContentStream = new MemoryStream(Encoding.UTF8.GetBytes(uploadContent));

            using var scope = _factory.Server.Services.CreateScope();
            var connectionString = scope.ServiceProvider.GetService<IRhetosComponent<ConnectionString>>().Value;

            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted);

            // Upload
            UploadHelper.InsertEmptyFileContent(fileId, transaction, false, false);
            var uploadStream = SqlFileStreamProvider.GetSqlFileStreamForUpload(fileId, transaction);
            await fileContentStream.CopyToAsync(uploadStream);
            uploadStream.Close();

            // Download
            var downloadStream = SqlFileStreamProvider.GetSqlFileStreamForDownload(fileId, transaction);
            var downloadBytes = new byte[downloadStream.Length];

            await downloadStream.ReadAsync(downloadBytes);
            downloadStream.Close();

            transaction.Rollback();
            connection.Close();

            downloadContent = Encoding.UTF8.GetString(downloadBytes);

            Assert.Equal(uploadContent, downloadContent);
        }
    }
}
