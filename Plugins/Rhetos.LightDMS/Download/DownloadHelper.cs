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

using Amazon.S3.Model;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Rhetos.LightDms.Storage;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace Rhetos.LightDMS
{
    public class DownloadHelper
    {
        private const int BUFFER_SIZE = 100 * 1024; // 100 kB buffer

        private readonly ILogger _logger;
        private readonly ConnectionString _connectionString;
        private readonly IContentTypeProvider _contentTypeProvider;
        private readonly LightDMSOptions _lightDMSOptions;
        private readonly Respond _respond;
        private readonly S3Options _s3Options;

        public DownloadHelper(
            ILogProvider logProvider,
            ConnectionString connectionString,
            IContentTypeProvider contentTypeProvider,
            LightDMSOptions lightDMSOptions,
            S3Options s3Options)
        {
            _logger = logProvider.GetLogger(GetType().Name);
            _connectionString = connectionString;
            _contentTypeProvider = contentTypeProvider;
            _lightDMSOptions = lightDMSOptions;
            _respond = new Respond(logProvider);
            _s3Options = s3Options;
        }

        public async Task HandleDownload(HttpContext context, Guid? documentVersionId, Guid? fileContentId)
        {
            try
            {
                using (var sqlConnection = new SqlConnection(_connectionString))
                {
                    sqlConnection.Open();
                    var fileMetadata = GetFileMetadata(documentVersionId, fileContentId, sqlConnection, GetFileNameFromQueryString(context));

                    PopulateHeader(context, fileMetadata.FileName);

                    await ResolveDownload(fileMetadata, sqlConnection, context.Response.Body, context.Response, context);
                }
            }
            catch (Exception ex)
            {
                if (ex.Message == "Function PathName is only valid on columns with the FILESTREAM attribute.")
                    await _respond.BadRequest(context, "FILESTREAM attribute is missing from LightDMS.FileContent.Content column. However, file is still available from download via REST interface.");
                else
                    await _respond.InternalError(context, ex);
            }
        }

        private async Task Download(FileMetadata fileDownloadMetadata, HttpContext context, Stream stream)
        {
            context.Response.Headers.Add("Content-Length", fileDownloadMetadata.Size.ToString());

            var buffer = new byte[BUFFER_SIZE];
            int bytesRead;

            int totalBytesWritten = 0;
            while ((bytesRead = stream.Read(buffer, 0, BUFFER_SIZE)) > 0)
            {
                if (context.RequestAborted.IsCancellationRequested)
                    break;

                ////TODO: This locking issue might been solved on .NET 5. Removed this code after testing shows there is no issue here.
                //void writeResponse() => await context.Response.Body.WriteAsync(buffer, 0, bytesRead);
                //if (_detectResponseBlockingErrors)
                //{
                //    // HACK: `Response.OutputStream.Write` sometimes blocks the process at System.Web.dll!System.Web.Hosting.IIS7WorkerRequest.ExplicitFlush();
                //    // Until the issue is solved, this hack allows 1. logging of the problem, and 2. closing the SQL transaction while the thread remains blocked.
                //    // Tried removing PreSendRequestHeaders, setting aspnet:UseTaskFriendlySynchronizationContext and disabling anitivirus, but it did not help.
                //    // Total performance overhead of Task.Run...Wait is around 0.01 sec when downloading 200MB file with BUFFER_SIZE 100kB.
                //    var task = Task.Run(writeResponse);
                //    if (!task.Wait(_detectResponseBlockingErrorsTimeoutMs))
                //    {
                //        throw new FrameworkException(ResponseBlockedMessage +
                //            $" Process {Process.GetCurrentProcess().Id}, thread {Thread.CurrentThread.ManagedThreadId}," +
                //            $" streamed {totalBytesWritten} bytes of {fileDownloadMetadata.Size}, current batch {bytesRead} bytes.");
                //    }
                //}
                //else
                //    writeResponse();

                await context.Response.Body.WriteAsync(buffer, 0, bytesRead);
                totalBytesWritten += bytesRead;
                await context.Response.Body.FlushAsync();
            }
        }

        public async Task ResolveDownload(FileMetadata fileMetadata, SqlConnection sqlConnection, Stream outputStream = null, HttpResponse httpResponse = null, HttpContext context = null)
        {
            if (fileMetadata.S3Storage)
                await DownloadFromS3(fileMetadata, context);
            else if (fileMetadata.AzureStorage)
                await DownloadFromAzureBlob(fileMetadata.FileContentId, outputStream, httpResponse);
            else if (IsFileStream(sqlConnection))
                await DownloadFromFileStream(fileMetadata, sqlConnection, context);
            else
                await DownloadFromVarbinary(fileMetadata, sqlConnection, context);
        }

        private bool IsFileStream(SqlConnection sqlConnection)
        {
            using (var sqlCommand = new SqlCommand("SELECT TOP 1 1 FROM sys.columns c WHERE OBJECT_SCHEMA_NAME(C.object_id) = 'LightDMS' AND OBJECT_NAME(C.object_id) = 'FileContent' AND c.Name = 'Content' AND c.is_filestream = 1", sqlConnection))
            {
                return sqlCommand.ExecuteScalar() != null;
            }
        }

        private static string GetFileNameFromQueryString(HttpContext context)
        {
            var query = context.Request.Query;
            string queryFileName = null;
            foreach (var key in query.Keys) if (key.ToLower() == "filename") queryFileName = query[key];

            return queryFileName;
        }

        private FileMetadata GetFileMetadata(Guid? documentVersionId, Guid? fileContentId, SqlConnection sqlConnection, string queryStringFileName)
        {
            SqlCommand getFileMetadata;
            if (documentVersionId != null)
                getFileMetadata = new SqlCommand(@"
                        SELECT
                            dv.FileName,
                            FileSize = DATALENGTH(Content),
                            dv.FileContentID,
                            fc.AzureStorage,
                            fc.S3Storage
                        FROM
                            LightDMS.DocumentVersion dv
                            INNER JOIN LightDMS.FileContent fc ON dv.FileContentID = fc.ID
                        WHERE 
                            dv.ID = '" + documentVersionId + @"'", sqlConnection);
            else
                getFileMetadata = new SqlCommand(@"
                        SELECT 
                            FileName ='unknown.txt',
                            FileSize = DATALENGTH(Content),
                            FileContentID = fc.ID,
                            AzureStorage = CAST(0 AS BIT),
                            S3Storage = CAST(0 AS BIT)
                        FROM 
                            LightDMS.FileContent fc 
                        WHERE 
                            ID = '" + fileContentId + "'", sqlConnection);

            using (var result = getFileMetadata.ExecuteReader(CommandBehavior.SingleRow))
            {
                result.Read();
                return new FileMetadata
                {
                    FileContentId = (Guid)result["FileContentID"],
                    FileName = queryStringFileName ?? (string)result["FileName"],
                    AzureStorage = result["AzureStorage"] != DBNull.Value && (bool)result["AzureStorage"],
                    S3Storage = result["S3Storage"] != DBNull.Value && (bool)result["S3Storage"],
                    Size = (long)result["FileSize"]
                };
            }
        }

        private async Task DownloadFromAzureBlob(Guid fileContentId, Stream outputStream, HttpResponse httpResponse)
        {
            var storageConnectionVariable = _lightDMSOptions.StorageConnectionVariable;
            string storageConnectionString;
            if (!string.IsNullOrWhiteSpace(storageConnectionVariable))
                storageConnectionString = Environment.GetEnvironmentVariable(storageConnectionVariable, EnvironmentVariableTarget.Machine);
            else
                //variable name has to be defined if AzureStorage bit is set to true
                throw new FrameworkException("Azure Blob Storage connection variable name missing.");

            if (!string.IsNullOrEmpty(storageConnectionString))
            {
                if (!CloudStorageAccount.TryParse(storageConnectionString, out CloudStorageAccount storageAccount))
                    throw new FrameworkException("Invalid Azure Blob Storage connection string.");

                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                var storageContainerName = _lightDMSOptions.StorageContainer;
                if (string.IsNullOrWhiteSpace(storageContainerName))
                    throw new FrameworkException("Azure blob storage container name is missing from configuration.");

                CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(storageContainerName);
                if (!await cloudBlobContainer.ExistsAsync())
                    throw new FrameworkException("Azure blob storage container doesn't exist.");

                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference("doc-" + fileContentId.ToString());
                if (await cloudBlockBlob.ExistsAsync())
                {
                    try
                    {
                        await cloudBlockBlob.FetchAttributesAsync();

                        if (httpResponse != null)
                            httpResponse.Headers.Add("Content-Length", cloudBlockBlob.Properties.Length.ToString());

                        // Downloads directly to outputStream
                        await cloudBlockBlob.DownloadToStreamAsync(outputStream);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error("Azure storage error. Error: " + ex.ToString());
                    }
                }
            }
            else
                throw new FrameworkException("Azure Blob Storage environment variable missing.");
        }

        private async Task DownloadFromS3(FileMetadata fileMetadata, HttpContext context)
        {
            ServicePointManager.ServerCertificateValidationCallback +=
                    delegate (
                        object sender,
                        X509Certificate certificate,
                        X509Chain chain,
                        SslPolicyErrors sslPolicyErrors)
                    {
                        if (certificate.Subject.IndexOf("ssc.gov.hr") > -1)
                            return true;
                        return sslPolicyErrors == SslPolicyErrors.None;
                    };

            using (var client = new S3StorageClient(_s3Options).GetAmazonS3Client())
            {
                GetObjectRequest getObjRequest = new GetObjectRequest();
                getObjRequest.BucketName = _s3Options.BucketName;
                if (string.IsNullOrWhiteSpace(getObjRequest.BucketName))
                    throw new FrameworkException("Missing S3 storage bucket name.");
                
                var s3Folder = _s3Options.DestinationFolder;
                if (string.IsNullOrWhiteSpace(s3Folder))
                    throw new FrameworkException("Missing S3 folder name.");

                getObjRequest.Key = s3Folder + "/doc-" + fileMetadata.FileContentId.ToString();

                try
                {
                    using (GetObjectResponse getObjResponse = await client.GetObjectAsync(getObjRequest))
                    {
                        fileMetadata.Size = getObjResponse.ContentLength;
                        await Download(fileMetadata, context, getObjResponse.ResponseStream);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("S3 storage error. Error: " + ex.ToString());
                }
            }
        }

        private async Task DownloadFromFileStream(FileMetadata fileMetadata, SqlConnection sqlConnection, HttpContext context)
        {
            using (SqlTransaction sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadCommitted)) // Explicit transaction is required when working with SqlFileStream class.
            {
                using (var stream = SqlFileStreamProvider.GetSqlFileStreamForDownload(fileMetadata.FileContentId, sqlTransaction))
                {
                    await Download(fileMetadata, context, stream);
                }
            }
        }

        ////TODO: This locking issue might been solved on .NET 5. Removed this code after testing shows there is no issue here.
        //public static readonly string ResponseBlockedMessage = $"Response.Body.WriteAsync blocked.";

        private async Task DownloadFromVarbinary(FileMetadata fileMetadata, SqlConnection sqlConnection, HttpContext context)
        {
            using (SqlCommand readCommand = new SqlCommand("SELECT Content FROM LightDMS.FileContent WHERE ID='" + fileMetadata.FileContentId.ToString() + "'", sqlConnection))
            {
                using (var sqlDataReader = readCommand.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    var success = sqlDataReader.Read();
                    if (!success)
                        return;
                    await Download(fileMetadata, context, sqlDataReader.GetStream(0));
                }
            }
        }

        private void PopulateHeader(HttpContext context, string fileName)
        {
            _contentTypeProvider.TryGetContentType(fileName, out string contentType);
            context.Response.ContentType = contentType;
            // Using HttpUtility.UrlPathEncode instead of HttpUtility.UrlEncode or Uri.EscapeDataString because it correctly encodes SPACE and special characters.
            context.Response.Headers.Add("Content-Disposition", "attachment; filename*=UTF-8''" + HttpUtility.UrlPathEncode(fileName) + "");
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        }

        public static Guid? GetId(HttpContext context)
        {
            var idString = context.Request.Query["id"].FirstOrDefault() ?? context.Request.Path.ToUriComponent().Split('/').Last();
            if (!string.IsNullOrEmpty(idString) && Guid.TryParse(idString, out Guid id))
                return id;
            else
                return null;
        }
    }
}