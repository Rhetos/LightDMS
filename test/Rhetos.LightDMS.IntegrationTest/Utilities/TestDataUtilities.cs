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

        public static void SeedS3StorageFile(WebApplicationFactory<Startup> factory,
            Guid documentVersionId,
            Guid fileContentId,
            string fileContent)
        {
            var fileName = $"doc-{fileContentId}";

            var connectionString = GetHostConnectionString(factory);
            using var connection = new SqlConnection(connectionString);
            connection.Open();
            using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

            InsertFileContentAsVarBinary(fileContentId, connection, transaction, isS3: true);
            InsertDocumentVersion(documentVersionId, fileContentId, fileName, connection, transaction);

            transaction.Commit();
            connection.Close();

            // Upload file to S3 storage
            using var scope = factory.Server.Services.CreateScope();
            var s3Options = scope.ServiceProvider.GetService<IRhetosComponent<S3Options>>().Value;
            UploadFileToS3Storage(fileContent, fileName, s3Options);
        }

        private static void UploadFileToS3Storage(string fileContent, string fileName, S3Options s3Options)
        {
            var config = new AmazonS3Config()
            {
                ServiceURL = s3Options.ServiceURL,
                ForcePathStyle = s3Options.ForcePathStyle
            };
            using var client = new AmazonS3Client(s3Options.AccessKeyID, s3Options.Key, config);
            using var fileStream = new MemoryStream(Encoding.UTF8.GetBytes(fileContent));

            var folder = string.IsNullOrEmpty(s3Options.DestinationFolder) ? string.Empty : $"{s3Options.DestinationFolder}/";
            client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = s3Options.BucketName,
                Key = $"{folder}{fileName}",
                InputStream = fileStream
            }).Wait();
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
