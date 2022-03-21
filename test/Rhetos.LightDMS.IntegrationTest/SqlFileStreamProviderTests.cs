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
