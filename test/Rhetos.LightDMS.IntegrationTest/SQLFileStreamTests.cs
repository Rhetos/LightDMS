using Microsoft.AspNetCore.Mvc.Testing;
using Rhetos.LightDMS.IntegrationTest.Utilities;
using Rhetos.LightDMS.TestApp;
using System.Net;
using System.Threading.Tasks;
using Xunit;

namespace Rhetos.LightDMS.IntegrationTest
{
    [Collection("SQL FILESTREAM enabled - local files")]
    public class SQLFileStreamTests
    {
        private static WebApplicationFactory<Startup> _factory;

        public SQLFileStreamTests()
        {
            _factory = new CustomWebApplicationFactory<Startup>();
        }

        [Fact]
        public async Task Upload_ShouldSuccess()
        {
            // Arrange
            var client = _factory.CreateClient();
            var request = TestDataUtilities.GenerateUploadRequest();

            // Act
            var response = await client.SendAsync(request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
