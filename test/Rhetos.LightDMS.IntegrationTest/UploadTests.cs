using Microsoft.AspNetCore.Mvc.Testing;
using Rhetos.LightDMS.IntegrationTest.Utilities;
using Rhetos.LightDMS.TestApp;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class UploadTests
    {
        private readonly WebApplicationFactory<Startup> _factory;

        public UploadTests(ITestOutputHelper testOutputHelper)
        {
            _factory = new CustomWebApplicationFactory<Startup>(testOutputHelper);
        }

        [Fact]
        public async Task Upload_ShouldSuccess()
        {
            // Arrange
            var client = _factory.CreateClient();
            using var request = TestDataUtilities.GenerateUploadRequest(1);

            // Act
            var response = await client.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();
            var uploadResponse = JsonSerializer.Deserialize<UploadSuccessResponse>(responseBody);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotNull(uploadResponse);
        }

        [Fact]
        public async Task Upload_WithoutFile_ShouldFail()
        {
            // Arrange
            var client = _factory.CreateClient();
            using var request = TestDataUtilities.GenerateUploadRequest(0);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            // Internal server error is not a nice behavior
            // but it is how current LightDMS implementation is behaving
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }

        [Fact]
        public async Task Upload_2Files_ShouldFail()
        {
            // Arrange
            var client = _factory.CreateClient();
            using var request = TestDataUtilities.GenerateUploadRequest(2);

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }
}
