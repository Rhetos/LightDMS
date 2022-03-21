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
