﻿/*
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
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class DownloadAzureBlobFileTests : IDisposable
    {
        private readonly WebApplicationFactory<Startup> _factory;
        private readonly string _fileContent = Guid.NewGuid().ToString();
        private readonly Guid _documentVersionId = Guid.NewGuid();
        private readonly Guid _fileContentId = Guid.NewGuid();

        public DownloadAzureBlobFileTests(ITestOutputHelper testOutput)
        {
            var lightDmsOptions = new LightDmsOptions 
            { 
                StorageContainer = TestDataUtilities.BLOB_CONTAINER_NAME
            };

            _factory = new CustomWebApplicationFactory<Startup>(configureRhetos: containerBuilder =>
            {
                containerBuilder.RegisterType<HardcodedAzureBlobConnectionStringResolver>().AsImplementedInterfaces();
                containerBuilder.Register(context => lightDmsOptions).SingleInstance();
            }, testOutput: testOutput);
            TestDataUtilities.SeedAzureBlobFile(_factory, _documentVersionId, _fileContentId, _fileContent);
        }

        public void Dispose()
        {
            try
            {
                TestDataUtilities.CleanupBlobFile(_factory, _documentVersionId, _fileContentId);
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
