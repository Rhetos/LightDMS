using Microsoft.AspNetCore.Mvc.Testing;
using Rhetos.LightDMS.IntegrationTest.Utilities;
using Rhetos.LightDMS.TestApp;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class DownloadSqlFileStreamTests : IDisposable
    {
        private readonly WebApplicationFactory<Startup> _factory;

        private const string _fileName = "DownloadSqlFileStreamTest.txt";
        private readonly Guid _documentVersionId = Guid.NewGuid();
        private readonly string _fileContent = Guid.NewGuid().ToString();
        private readonly Guid _fileContentId = Guid.NewGuid();

        public DownloadSqlFileStreamTests(ITestOutputHelper testOutputHelper)
        {
            _factory = new CustomWebApplicationFactory<Startup>(testOutputHelper);
            TestDataUtilities.SeedDocumentVersionAndFileContent(_factory,
                _documentVersionId,
                _fileContentId,
                _fileName,
                _fileContent);
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
