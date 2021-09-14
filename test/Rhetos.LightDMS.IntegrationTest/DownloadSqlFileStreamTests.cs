using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rhetos.LightDms.Storage;
using Rhetos.LightDMS.TestApp;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class DownloadSqlFileStreamTests
    {
        private static WebApplicationFactory<Startup> _factory;

        private readonly Guid _documentVersionId = Guid.NewGuid();
        private readonly string _fileName = "DownloadSqlFileStreamTest.txt";
        private readonly string _fileContent = "Test file content";
        private readonly Guid _fileContentId = Guid.NewGuid();

        public DownloadSqlFileStreamTests()
        {
            _factory = new CustomWebApplicationFactory<Startup>();
            SetUpTestDataInDatabase();
        }

        private void SetUpTestDataInDatabase()
        {
            var fileContentStream = new MemoryStream(Encoding.UTF8.GetBytes(_fileContent));
            var createdDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            using var scope = _factory.Server.Services.CreateScope();
            var connectionString = scope.ServiceProvider.GetService<IRhetosComponent<ConnectionString>>().Value;

            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted);

            // Upload
            var uploadStream = SqlFileStreamProvider.GetSqlFileStreamForUpload(_fileContentId, createdDate, transaction);
            fileContentStream.CopyTo(uploadStream);
            uploadStream.Close();

            var insertDocumentVersionCommand = new SqlCommand(
                @"INSERT INTO [LightDMS].[DocumentVersion] (ID, VersionNumber, DocumentID, FileName, CreatedDate, FileContentID)
                  VALUES (@ID, 1, NEWID(), @FileName, GETDATE(), @FileContentID)",
                connection, transaction);
            insertDocumentVersionCommand.Parameters.Add(new SqlParameter("@ID", _documentVersionId));
            insertDocumentVersionCommand.Parameters.Add(new SqlParameter("@FileName", _fileName));
            insertDocumentVersionCommand.Parameters.Add(new SqlParameter("@FileContentID", _fileContentId));
            insertDocumentVersionCommand.ExecuteNonQuery();

            transaction.Commit();
            connection.Close();
        }

        [Theory]
        [InlineData("LightDMS/Download/{0}")]
        [InlineData("LightDMS/Download?id={0}")]
        public async Task Download_ShouldSucceed(string route)
        {
            // Arrange
            var client = _factory.CreateClient();
            var requestUri = string.Format(route, _documentVersionId);
            var downloadRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            // Act
            var response = await client.SendAsync(downloadRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(_fileContent, responseContent);
        }

        [Theory]
        [InlineData("LightDMS/DownloadPreview/{0}?filename={1}")]
        [InlineData("LightDMS/DownloadPreview?id={0}&filename={1}")]
        public async Task DownloadPreview_ShouldSucceed(string route)
        {
            // Arrange
            var client = _factory.CreateClient();
            var requestUri = string.Format(route, _fileContentId, _fileName);

            var downloadRequest = new HttpRequestMessage(HttpMethod.Get, requestUri);

            // Act
            var response = await client.SendAsync(downloadRequest);
            var responseContent = await response.Content.ReadAsStringAsync();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(_fileContent, responseContent);
        }
    }
}
