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

using Autofac;
using Microsoft.AspNetCore.Mvc.Testing;
using Rhetos.LightDMS.IntegrationTest.Utilities;
using Rhetos.LightDMS.TestApp;
using Rhetos.Utilities;
using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class VarBinaryDatabaseTests : IDisposable
    {
        private readonly WebApplicationFactory<Startup> _factory;

        private const string _fileName = "DownloadSqlFileStreamTest.txt";
        private readonly Guid _documentVersionId = Guid.NewGuid();
        private readonly string _fileContent = Guid.NewGuid().ToString();
        private readonly Guid _fileContentId = Guid.NewGuid();

        public VarBinaryDatabaseTests(ITestOutputHelper testOutputHelper)
        {
            var rawConnectionString = string.Format("Server={0};Database={1};{2};",
                TestConfigurations.Instance.SqlServerName,
                TestConfigurations.Instance.VarBinaryDatabaseName,
                TestConfigurations.Instance.SqlServerCredential);
            var connectionString = new ConnectionString(rawConnectionString);
            _factory = new CustomWebApplicationFactory<Startup>(testOutputHelper, configureRhetos: container =>
            {
                container.Register(context => connectionString).SingleInstance();
            });

            TestDataUtilities.SeedDocumentVersionAndFileContent(_factory,
                _documentVersionId,
                _fileContentId,
                _fileName,
                _fileContent,
                useFileStream: false);
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

        [Fact]
        public async Task Upload_ShouldSucceed()
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
