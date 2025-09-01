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

using Amazon.S3;
using Amazon.S3.Model;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rhetos.LightDms.Storage;
using Rhetos.LightDMS.TestApp;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Net.Http;
using System.Text;

namespace Rhetos.LightDMS.IntegrationTest.Utilities
{
    internal class UploadSuccessResponse
    {
        public Guid ID { get; set; }
    }

    internal static class TestDataUtilities
    {
        public const string BLOB_CONN_STRING = "UseDevelopmentStorage=true;";
        public const string BLOB_CONTAINER_NAME = "lightdmstest";
        private static BlobServiceClient _blobServiceClient = new BlobServiceClient(BLOB_CONN_STRING);

        public static HttpRequestMessage GenerateUploadRequest(int fileCount = 1)
        {
            byte[] fileContent = Encoding.UTF8.GetBytes("A test file content");
            var content = new MultipartFormDataContent();

            for (var i = 0; i < fileCount; ++i)
            {
                var memoryStream = new MemoryStream(fileContent);
                var streamContent = new StreamContent(memoryStream);
                content.Add(streamContent, $"file{i}", $"testfile{i}.txt");
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "LightDMS/Upload")
            {
                Content = content
            };

            return request;
        }

        public static void SeedDocumentVersionAndFileContent(WebApplicationFactory<Startup> factory,
            Guid documentVersionId,
            Guid fileContentId,
            string fileName,
            string fileContent,
            bool useFileStream = true)
        {
            var fileContentStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));

            var connectionString = GetHostConnectionString(factory);
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted);

            if (useFileStream)
                InsertFileContentAsFileStream(fileContentId, fileContentStream, transaction);
            else
                InsertFileContentAsVarBinary(fileContentId, connection, transaction, fileContent);

            InsertDocumentVersion(documentVersionId, fileContentId, fileName, connection, transaction);

            transaction.Commit();
            connection.Close();
        }

        public static void CleanupDocumentVersionAndFileContent(WebApplicationFactory<Startup> factory,
            Guid documentVersionId,
            Guid fileContentId)
        {
            var connectionString = GetHostConnectionString(factory);
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            var cleanupDataCommand = new SqlCommand(
                @"DELETE FROM [LightDMS].[DocumentVersion] WHERE [Id]=@DocumentVersionID;
                  DELETE FROM [LightDMS].[FileContent] WHERE [Id]=@FileContentID;",
                connection);
            cleanupDataCommand.Parameters.Add(new SqlParameter("@DocumentVersionID", documentVersionId));
            cleanupDataCommand.Parameters.Add(new SqlParameter("@FileContentID", fileContentId));

            cleanupDataCommand.ExecuteNonQuery();

            connection.Close();
        }

        public static void SeedAzureBlobFile(WebApplicationFactory<Startup> factory,
            Guid documentVersionId,
            Guid fileContentId,
            string fileContent)
        {
            var fileName = $"doc-{fileContentId}";

            var connectionString = GetHostConnectionString(factory);
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            InsertFileContentAsVarBinary(fileContentId, connection, transaction, isAzureBlob: true);
            InsertDocumentVersion(documentVersionId, fileContentId, fileName, connection, transaction);

            transaction.Commit();
            connection.Close();

            // Upload file to Azure blob
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(BLOB_CONTAINER_NAME);
            containerClient.CreateIfNotExists();

            var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
            BlobClient blobClient = containerClient.GetBlobClient(fileName);
            blobClient.Upload(fileStream, true);
        }

        public static Guid SeedS3StorageFile(WebApplicationFactory<Startup> factory,
            Guid documentVersionId,
            Guid fileContentId,
            string fileContent)
        {
            var fileName = $"doc-{fileContentId}";

            using var scope = factory.Server.Services.CreateScope();
            var s3Options = scope.ServiceProvider.GetService<IRhetosComponent<S3Options>>().Value;
            var lightDmsOptions = scope.ServiceProvider.GetService<IRhetosComponent<LightDmsOptions>>().Value;
            lightDmsOptions.UploadTarget = Storage.UploadTarget.S3;
            var uploadHelper = scope.ServiceProvider.GetService<IRhetosComponent<UploadHelper>>();
            using var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
            var uploadedFileResult = uploadHelper.Value.UploadStream(fileStream).GetAwaiter().GetResult();
            var connectionString = GetHostConnectionString(factory);
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
            InsertDocumentVersion(documentVersionId, uploadedFileResult.ID.Value, fileName, connection, transaction);
            transaction.Commit();
            connection.Close();
            return uploadedFileResult.ID.Value;
        }

        public static void CleanupBlobFile(WebApplicationFactory<Startup> factory,
            Guid documentVersionId,
            Guid fileContentId)
        {
            CleanupDocumentVersionAndFileContent(factory, documentVersionId, fileContentId);

            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(BLOB_CONTAINER_NAME);
            BlobClient blobClient = containerClient.GetBlobClient($"doc-{fileContentId}");
            blobClient.Delete();
        }

        private static string GetHostConnectionString(WebApplicationFactory<Startup> factory)
        {
            using var scope = factory.Server.Services.CreateScope();
            return scope.ServiceProvider.GetService<IRhetosComponent<ConnectionString>>().Value;
        }

        private static void InsertDocumentVersion(Guid documentVersionId,
            Guid fileContentId,
            string fileName,
            SqlConnection connection,
            SqlTransaction transaction)
        {
            var insertDocumentVersionCommand = new SqlCommand(
                @"INSERT INTO [LightDMS].[DocumentVersion] (ID, VersionNumber, DocumentID, FileName, CreatedDate, FileContentID)
                  VALUES (@ID, 1, NEWID(), @FileName, GETDATE(), @FileContentID)",
                            connection, transaction);
            insertDocumentVersionCommand.Parameters.Add(new SqlParameter("@ID", documentVersionId));
            insertDocumentVersionCommand.Parameters.Add(new SqlParameter("@FileName", fileName));
            insertDocumentVersionCommand.Parameters.Add(new SqlParameter("@FileContentID", fileContentId));
            insertDocumentVersionCommand.ExecuteNonQuery();
        }

        private static void InsertFileContentAsVarBinary(Guid fileContentId, SqlConnection connection, SqlTransaction transaction,
            string fileContent = "",
            bool isAzureBlob = false,
            bool isS3 = false)
        {
            var insertFileContentCommand = new SqlCommand(
                @"INSERT INTO [LightDMS].[FileContent] (ID, AzureStorage, S3Storage, Content) 
                VALUES (@ID, @IsAzureBlob, @IsS3, @FileContent)",
                connection,
                transaction);

            insertFileContentCommand.Parameters.AddWithValue("@ID", fileContentId);
            insertFileContentCommand.Parameters.AddWithValue("@IsAzureBlob", isAzureBlob ? 1 : 0);
            insertFileContentCommand.Parameters.AddWithValue("@IsS3", isS3 ? 1 : 0);

            var fileContentParam = new SqlParameter("@FileContent", SqlDbType.VarBinary)
            {
                Value = Encoding.UTF8.GetBytes(fileContent)
            };
            insertFileContentCommand.Parameters.Add(fileContentParam);

            insertFileContentCommand.ExecuteNonQuery();
        }

        private static void InsertFileContentAsFileStream(Guid fileContentId, MemoryStream fileContentStream, SqlTransaction transaction)
        {
            UploadHelper.InsertEmptyFileContent(fileContentId, transaction, false, false);
            using var uploadStream = SqlFileStreamProvider.GetSqlFileStreamForUpload(fileContentId, transaction);
            fileContentStream.CopyTo(uploadStream);
            uploadStream.Close();
        }
    }
}
