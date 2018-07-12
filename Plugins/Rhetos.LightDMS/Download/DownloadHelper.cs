using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Rhetos.LightDms.Storage;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;
using System.Net;
using System.Web;

namespace Rhetos.LightDMS
{
    public class DownloadHelper
    {
        private const int BUFFER_SIZE = 100 * 1024; // 100 kB buffer

        private ILogger _performanceLogger;
        private ILogger _logger;

        public DownloadHelper()
        {
            var logProvider = new NLogProvider();
            _performanceLogger = logProvider.GetLogger("Performance");
            _logger = logProvider.GetLogger(GetType().Name);
        }

        public void HandleDownload(HttpContext context, Guid? documentVersionId, Guid? fileContentId)
        {
            var query = HttpUtility.ParseQueryString(context.Request.Url.Query);
            context.Response.StatusCode = (int)HttpStatusCode.OK;

            using (var sqlConnection = new SqlConnection(SqlUtility.ConnectionString))
            {
                sqlConnection.Open();
                string fileName = null;
                // if "filename" is present in query, that one is used as download filename
                foreach (var key in query.AllKeys) if (key.ToLower() == "filename") fileName = query[key];

                SqlCommand getMetadata = null;
                if (documentVersionId != null)
                    getMetadata = new SqlCommand(@"
                        SELECT
                            dv.FileName,
                            FileSize = DATALENGTH(Content),
                            dv.FileContentID,
                            fc.AzureStorage
                        FROM
                            LightDMS.DocumentVersion dv
                            INNER JOIN LightDMS.FileContent fc ON dv.FileContentID = fc.ID
                        WHERE 
                            dv.ID = '" + documentVersionId + @"'", sqlConnection);
                else
                    getMetadata = new SqlCommand(@"
                        SELECT 
                            FileName ='unknown.txt',
                            FileSize = DATALENGTH(Content),
                            FileContentID = fc.ID,
                            AzureStorage = CAST(0 AS BIT)
                        FROM 
                            LightDMS.FileContent fc 
                        WHERE 
                            ID = '" + fileContentId + "'", sqlConnection);

                bool azureStorage;
                long size;
                using (var result = getMetadata.ExecuteReader(CommandBehavior.SingleRow))
                {
                    result.Read();
                    fileName = fileName ?? (string)result["FileName"];
                    azureStorage = result["AzureStorage"] != DBNull.Value
                        ? (bool)result["AzureStorage"]
                        : false;
                    fileContentId = fileContentId ?? (Guid?)result["FileContentID"];
                    size = (long)result["FileSize"];
                    result.Close();
                }

                if (azureStorage == true && TryDownloadFromAzureBlob(context, fileContentId.Value, fileName))
                {
                    return;
                }

                //if any error (from azure) changed status code, stop further execution
                if (context.Response.StatusCode != (int)HttpStatusCode.OK)
                    return;

                //If there is no document on AzureBlobStorage, take it from DB
                try
                {
                    context.Response.BufferOutput = false;
                    // If FileStream is not available - read from VarBinary(MAX) column using buffer;
                    if (!TryDownloadFromFileStream(context, fileContentId.Value, fileName, size, sqlConnection))
                        DownloadFromVarbinary(context, fileContentId.Value, fileName, size, sqlConnection);
                }
                catch (Exception ex)
                {
                    if (ex.Message == "Function PathName is only valid on columns with the FILESTREAM attribute.")
                        LogError("FILESTREAM attribute is missing from LightDMS.FileContent.Content column. However, file is still available from download via REST interface.", context);
                    else
                        LogError(ex.Message, context, ex.ToString());
                }
            }
        }

        private bool TryDownloadFromAzureBlob(HttpContext context, Guid fileContentId, string fileName)
        {
            var storageConnectionVariable = System.Configuration.ConfigurationManager.AppSettings.Get("LightDms.StorageConnectionVariable");
            string storageConnectionString = null;
            if (!string.IsNullOrWhiteSpace(storageConnectionVariable))
                storageConnectionString = Environment.GetEnvironmentVariable(storageConnectionVariable, EnvironmentVariableTarget.Machine);
            else
            {
                //variable name has to be defined if AzureStorage bit is set to true
                LogError("Azure Blob Storage connection variable name missing.", context);
                return false;
            }

            if (!string.IsNullOrEmpty(storageConnectionString))
            {
                CloudStorageAccount storageAccount = null;
                if (!CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
                {
                    LogError("Invalid Azure Blob Storage connection string.", context);
                    return false;
                }

                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                var storageContainerName = System.Configuration.ConfigurationManager.AppSettings.Get("LightDms.StorageContainer");
                if (string.IsNullOrWhiteSpace(storageContainerName))
                {
                    LogError("Azure blob storage container name is missing from configuration.", context);
                    return false;
                }

                CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(storageContainerName);
                if (!cloudBlobContainer.Exists())
                {
                    LogError("Azure blob storage container doesn't exist.", context);
                    return false;
                }

                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference("doc-" + fileContentId.ToString());
                if (cloudBlockBlob.Exists())
                {
                    try
                    {
                        cloudBlockBlob.FetchAttributes();

                        PopulateHeader(context, fileName, cloudBlockBlob.Properties.Length);
                        cloudBlockBlob.DownloadToStream(context.Response.OutputStream);

                        context.Response.Flush();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        //when unexpected error occurs log it, then fall back to DB
                        LogError("Azure storage error, falling back to DB. Error:" + ex.Message, null, ex.ToString());
                        return false;
                    }
                }
                else
                    return false; //if no blob is present fall back to DB
            }
            else
            {
                //variable has to be defined if AzureStorage bit is set to true
                LogError("Azure Blob Storage environment variable missing.", context);
                return false;
            }
        }

        private bool TryDownloadFromFileStream(HttpContext context, Guid fileContentId, string fileName, long size, SqlConnection sqlConnection)
        {
            SqlCommand checkFileStreamEnabled = new SqlCommand("SELECT TOP 1 1 FROM sys.columns c WHERE OBJECT_SCHEMA_NAME(C.object_id) = 'LightDMS' AND OBJECT_NAME(C.object_id) = 'FileContent' AND c.Name = 'Content' AND c.is_filestream = 1", sqlConnection);
            if (checkFileStreamEnabled.ExecuteScalar() == null)
                return false;

            string sqlQuery = $@"
                SELECT 
                    fc.Content.PathName(),
                    GET_FILESTREAM_TRANSACTION_CONTEXT()
                FROM 
                    LightDMS.FileContent fc
                WHERE 
                    fc.ID = '{fileContentId}'";

            SqlCommand sqlCommand = new SqlCommand(sqlQuery, sqlConnection);
            using (var reader = sqlCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    var path = reader.GetString(0);
                    byte[] transactionContext = reader.GetSqlBytes(1).Buffer;
                    using (var fileStream = new SqlFileStream(path, transactionContext, FileAccess.Read, FileOptions.SequentialScan, 0))
                    {
                        byte[] buffer = new byte[BUFFER_SIZE];
                        int bytesRead;
                        while ((bytesRead = fileStream.Read(buffer, 0, BUFFER_SIZE)) > 0)
                        {
                            context.Response.OutputStream.Write(buffer, 0, bytesRead);
                            context.Response.Flush();
                        }
                    }
                }
                else
                    throw new ApplicationException($"Missing LightDMS.FileContent '{fileContentId}'.");
            }

            PopulateHeader(context, fileName, size);
            return true;
        }

        private void DownloadFromVarbinary(HttpContext context, Guid? fileContentId, string fileName, long size, SqlConnection sqlConnection)
        {
            byte[] buffer = new byte[BUFFER_SIZE];

            PopulateHeader(context, fileName, size);

            SqlCommand readCommand = new SqlCommand("SELECT Content FROM LightDMS.FileContent WHERE ID='" + fileContentId.ToString() + "'", sqlConnection);
            var reader = readCommand.ExecuteReader(CommandBehavior.SequentialAccess);

            while (reader.Read())
            {
                // Read bytes into outByte[] and retain the number of bytes returned.  
                var readed = reader.GetBytes(0, 0, buffer, 0, BUFFER_SIZE);
                var startIndex = 0;
                // Continue while there are bytes beyond the size of the buffer.  
                while (readed == BUFFER_SIZE)
                {
                    context.Response.OutputStream.Write(buffer, 0, (int)readed);
                    context.Response.Flush();

                    // Reposition start index to end of last buffer and fill buffer.  
                    startIndex += BUFFER_SIZE;
                    readed = reader.GetBytes(0, startIndex, buffer, 0, BUFFER_SIZE);
                }

                context.Response.OutputStream.Write(buffer, 0, (int)readed);
                context.Response.Flush();
            }

            reader.Close();
            reader = null;
        }

        private void PopulateHeader(HttpContext context, string fileName, long length)
        {
            context.Response.ContentType = MimeMapping.GetMimeMapping(fileName);
            // Koristiti HttpUtility.UrlPathEncode umjesto HttpUtility.UrlEncode ili Uri.EscapeDataString jer drugačije handlea SPACE i specijalne znakove
            context.Response.AddHeader("Content-Disposition", "attachment; filename*=UTF-8''" + HttpUtility.UrlPathEncode(fileName) + "");
            context.Response.AddHeader("Content-Length", length.ToString());
        }

        private void LogError(string error, HttpContext context = null, string trace = null)
        {
            _logger.Error(error + (trace != null ? "\r\n" + trace : ""));
            if (context != null)
            {
                context.Response.ContentType = "application/json;";
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                context.Response.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new { error, trace }));
            }
        }
    }
}