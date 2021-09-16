using Microsoft.AspNetCore.Mvc.Testing;
using Rhetos.LightDMS.IntegrationTest.Utilities;
using Rhetos.LightDMS.TestApp;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class RhetosEntiy
    {
        public string ID { get; set; }
    }

    [Collection("S3 Storage simulator - local files")]
    public class DownloadS3StorageTests : IDisposable
    {
        private static WebApplicationFactory<Startup> _factory;

        private readonly Guid _documentVersionId = Guid.NewGuid();
        private readonly string _fileContent = "Test file content";
        private readonly Guid _fileContentId = Guid.Parse("b83930a2-c8ab-4678-8457-e55bdcb18aa5");

        public DownloadS3StorageTests()
        {
            _factory = new CustomWebApplicationFactory<Startup>();
            TestDataUtilities.SeedS3StorageFile(_factory, _documentVersionId, _fileContentId);
        }

        public void Dispose()
        {
            try
            {
                TestDataUtilities.CleanupDocumentVersionAndFileContent(_factory, _documentVersionId, _fileContentId);
            }
            finally
            {
                _factory.Dispose();
            }
            GC.SuppressFinalize(this);
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
    }
}
