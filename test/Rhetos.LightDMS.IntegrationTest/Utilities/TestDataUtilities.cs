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
        public const string BLOB_CONN_STRING = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";
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
            string fileContent)
        {
            var fileContentStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
            var createdDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

            var connectionString = GetHostConnectionString(factory);
            using var connection = new SqlConnection(connectionString);
            connection.Open();

            using var transaction = connection.BeginTransaction(IsolationLevel.ReadUncommitted);

            // Upload
            var uploadStream = SqlFileStreamProvider.GetSqlFileStreamForUpload(fileContentId, createdDate, transaction);
            fileContentStream.CopyTo(uploadStream);
            uploadStream.Close();

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

            var insertFileContentCommand = new SqlCommand(
                @"INSERT INTO [LightDMS].[FileContent] (ID, AzureStorage, Content) 
                VALUES (@ID, 1, CONVERT(varbinary(max), ''))",
                connection,
                transaction);
            insertFileContentCommand.Parameters.Add(new SqlParameter("@ID", fileContentId));
            insertFileContentCommand.ExecuteNonQuery();

            InsertDocumentVersion(documentVersionId, fileContentId, fileName, connection, transaction);

            transaction.Commit();
            connection.Close();

            // Upload file to Azure blob
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(BLOB_CONTAINER_NAME);
            if (containerClient == null)
            {
                containerClient = _blobServiceClient.CreateBlobContainerAsync(BLOB_CONTAINER_NAME).Result;
            }
            var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));
            BlobClient blobClient = containerClient.GetBlobClient(fileName);
            blobClient.Upload(fileStream, true);
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
    }
}
