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
        private readonly bool _detectResponseBlockingErrors;
        private readonly int _detectResponseBlockingErrorsTimeoutMs;

        public DownloadHelper()
        {
            var logProvider = new NLogProvider();
            _logger = logProvider.GetLogger(GetType().Name);
            var configuration = new Utilities.Configuration();
            _detectResponseBlockingErrors = configuration.GetBool("LightDMS.DetectResponseBlockingErrors", true).Value;
            _detectResponseBlockingErrorsTimeoutMs = configuration.GetInt("LightDMS.DetectResponseBlockingErrorsTimeoutMs", 60 * 1000).Value;
        }

        public void HandleDownload(HttpContext context, Guid? documentVersionId, Guid? fileContentId)
        {
            try
            {
                using (var sqlConnection = new SqlConnection(SqlUtility.ConnectionString))
                {
                    sqlConnection.Open();
                    var fileMetadata = GetFileMetadata(documentVersionId, fileContentId, sqlConnection, GetFileNameFromQueryString(context));

                    PopulateHeader(context, fileMetadata.FileName, fileMetadata.Size);

                    using (FileDownloadResult fileDownloadResult = ResolveFileDownloadResult(fileMetadata, sqlConnection, context.Response.OutputStream, context.Response))
                    {
                        if (fileDownloadResult.Stream != null)
                            Download(fileDownloadResult, context);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ex.Message == "Function PathName is only valid on columns with the FILESTREAM attribute.")
                    Respond.BadRequest(context, "FILESTREAM attribute is missing from LightDMS.FileContent.Content column. However, file is still available from download via REST interface.");
                else
                    Respond.InternalError(context, ex);
            }
        }

        private void Download(FileDownloadResult fileDownloadMetadata, HttpContext context)
        {
            try
            {
                context.Response.AddHeader("Content-Length", fileDownloadMetadata.Metadata.Size.ToString());

                var buffer = new byte[BUFFER_SIZE];
                int bytesRead;

                int totalBytesWritten = 0;
                while ((bytesRead = fileDownloadMetadata.Stream.Read(buffer, 0, BUFFER_SIZE)) > 0)
                {
                    if (!context.Response.IsClientConnected)
                        break;

                    void writeResponse() => context.Response.OutputStream.Write(buffer, 0, bytesRead);

                    if (_detectResponseBlockingErrors)
                    {
                        // HACK: `Response.OutputStream.Write` sometimes blocks the process at System.Web.dll!System.Web.Hosting.IIS7WorkerRequest.ExplicitFlush();
                        // Until the issue is solved, this hack allows 1. logging of the problem, and 2. closing the SQL transaction while the thread remains blocked.
                        // Tried removing PreSendRequestHeaders, setting aspnet:UseTaskFriendlySynchronizationContext and disabling anitivirus, but it did not help.
                        // Total performance overhead of Task.Run...Wait is around 0.01 sec when downloading 200MB file with BUFFER_SIZE 100kB.
                        var task = Task.Run(writeResponse);
                        if (!task.Wait(_detectResponseBlockingErrorsTimeoutMs))
                        {
                            throw new FrameworkException(ResponseBlockedMessage +
                                $" Process {Process.GetCurrentProcess().Id}, thread {Thread.CurrentThread.ManagedThreadId}," +
                                $" streamed {totalBytesWritten} bytes of {fileDownloadMetadata.Metadata.Size}, current batch {bytesRead} bytes.");
                        }
                    }
                    else
                        writeResponse();

                    totalBytesWritten += bytesRead;
                    context.Response.Flush();
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Download from FileDownloadResult.Stream error. Error: " + ex.ToString());
            }
        }

        public FileDownloadResult ResolveFileDownloadResult(FileMetadata fileMetadata, SqlConnection sqlConnection, Stream outputStream = null, HttpResponse httpResponse = null)
        {
            var fileDownloadResult =
                    DownloadFromS3(fileMetadata.FileContentId, fileMetadata.S3Storage, outputStream, httpResponse) ??
                    DownloadFromAzureBlob(fileMetadata.FileContentId, fileMetadata.AzureStorage, outputStream, httpResponse) ??
                    DownloadFromFileStream(fileMetadata.FileContentId, sqlConnection) ??
                    DownloadFromVarbinary(fileMetadata.FileContentId, sqlConnection);

            fileDownloadResult.Metadata = fileMetadata;

            return fileDownloadResult;
        }

        private static string GetFileNameFromQueryString(HttpContext context)
        {
            var query = HttpUtility.ParseQueryString(context.Request.Url.Query);
            string queryFileName = null;
            foreach (var key in query.AllKeys) if (key.ToLower() == "filename") queryFileName = query[key];

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

        private FileDownloadResult DownloadFromAzureBlob(Guid fileContentId, bool azureStorage, Stream outputStream, HttpResponse httpResponse)
        {
            if (!azureStorage)
                return null;

            var storageConnectionVariable = System.Configuration.ConfigurationManager.AppSettings.Get("LightDms.StorageConnectionVariable");
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
                var storageContainerName = System.Configuration.ConfigurationManager.AppSettings.Get("LightDms.StorageContainer");
                if (string.IsNullOrWhiteSpace(storageContainerName))
                    throw new FrameworkException("Azure blob storage container name is missing from configuration.");

                CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(storageContainerName);
                if (!cloudBlobContainer.Exists())
                    throw new FrameworkException("Azure blob storage container doesn't exist.");

                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference("doc-" + fileContentId.ToString());
                if (cloudBlockBlob.Exists())
                {
                    try
                    {
                        cloudBlockBlob.FetchAttributes();

                        if (httpResponse != null)
                            httpResponse.AddHeader("Content-Length", cloudBlockBlob.Properties.Length.ToString());

                        // Downloads directly to outputStream
                        cloudBlockBlob.DownloadToStream(outputStream);
                      
                        return new FileDownloadResult();
                    }
                    catch (Exception ex)
                    {
                        //when unexpected error occurs log it, then fall back to DB
                        _logger.Error("Azure storage error, falling back to DB. Error: " + ex.ToString());
                        return null;
                    }
                }
                else
                    return null; //if no blob is present fall back to DB
            }
            else
                throw new FrameworkException("Azure Blob Storage environment variable missing.");
        }

        private FileDownloadResult DownloadFromS3(Guid fileContentId, bool s3Storage, Stream outputStream, HttpResponse httpResponse)
        {
            if (!s3Storage)
                return null;

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
            
            using (var client = S3StorageClient.GetAmazonS3Client())
            {
                GetObjectRequest getObjRequest = new GetObjectRequest();
                getObjRequest.BucketName = ConfigUtility.GetAppSetting("StorageBucketT1");
                if (string.IsNullOrWhiteSpace(getObjRequest.BucketName))
                    throw new FrameworkException("Missing S3 storage bucket name.");
                
                var s3Folder = ConfigUtility.GetAppSetting("StorageS3Folder");
                if (string.IsNullOrWhiteSpace(s3Folder))
                    throw new FrameworkException("Missing S3 folder name.");

                getObjRequest.Key = s3Folder + "/doc-" + fileContentId.ToString();

                try
                {
                    using (GetObjectResponse getObjResponse = client.GetObject(getObjRequest))
                    {
                        getObjResponse.ResponseStream.CopyTo(outputStream);
                        return new FileDownloadResult();
                    }
                }
                catch (Exception ex)
                {
                    _logger.Error("S3 storage error, falling back to DB. Error: " + ex.ToString());
                    return null;
                }
            }
        }

        private FileDownloadResult DownloadFromFileStream(Guid fileContentId, SqlConnection sqlConnection)
        {
            SqlCommand checkFileStreamEnabled = new SqlCommand("SELECT TOP 1 1 FROM sys.columns c WHERE OBJECT_SCHEMA_NAME(C.object_id) = 'LightDMS' AND OBJECT_NAME(C.object_id) = 'FileContent' AND c.Name = 'Content' AND c.is_filestream = 1", sqlConnection);
            if (checkFileStreamEnabled.ExecuteScalar() == null)
                return null;

            SqlTransaction sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadCommitted); // Explicit transaction is required when working with SqlFileStream class.

            return new FileDownloadResult
            {
                Stream = SqlFileStreamProvider.GetSqlFileStreamForDownload(fileContentId, sqlTransaction)
            };
        }

        public static readonly string ResponseBlockedMessage = $"Response.OutputStream.Write blocked.";

        private FileDownloadResult DownloadFromVarbinary(Guid fileContentId, SqlConnection sqlConnection)
        {
            using (SqlCommand readCommand = new SqlCommand("SELECT Content FROM LightDMS.FileContent WHERE ID='" + fileContentId.ToString() + "'", sqlConnection))
            {
                FileDownloadResult fileDownloadResult = new FileDownloadResult();
                using (var sqlDataReader = readCommand.ExecuteReader(CommandBehavior.SequentialAccess))
                {
                    var success = sqlDataReader.Read();
                    if (!success)
                        return null;
                    sqlDataReader.GetStream(0).CopyTo(fileDownloadResult.Stream);
                }
                return fileDownloadResult;
            }
        }

        private void PopulateHeader(HttpContext context, string fileName, long length)
        {
            context.Response.ContentType = MimeMapping.GetMimeMapping(fileName);
            // Koristiti HttpUtility.UrlPathEncode umjesto HttpUtility.UrlEncode ili Uri.EscapeDataString jer drugačije handlea SPACE i specijalne znakove
            context.Response.AddHeader("Content-Disposition", "attachment; filename*=UTF-8''" + HttpUtility.UrlPathEncode(fileName) + "");
            context.Response.StatusCode = (int)HttpStatusCode.OK;
            context.Response.BufferOutput = false;
        }

        public static Guid? GetId(HttpContext context)
        {
            var idString = context.Request.QueryString["id"] ?? context.Request.Url.LocalPath.Split('/').Last();
            if (!string.IsNullOrEmpty(idString) && Guid.TryParse(idString, out Guid id))
                return id;
            else
                return null;
        }
    }
}