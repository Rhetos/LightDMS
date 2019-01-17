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

        private ILogger _logger;

        public DownloadHelper()
        {
            var logProvider = new NLogProvider();
            _logger = logProvider.GetLogger(GetType().Name);
        }

        public void HandleDownload(HttpContext context, Guid? documentVersionId, Guid? fileContentId)
        {
            try
            {
                using (var sqlConnection = new SqlConnection(SqlUtility.ConnectionString))
                {
                    sqlConnection.Open();
                    var fileMetadata = GetMetadata(context, documentVersionId, fileContentId, sqlConnection);

                    context.Response.StatusCode = (int)HttpStatusCode.OK;

                    if (!TryDownloadFromAzureBlob(context, fileMetadata))
                    {
                        //If there is no document on AzureBlobStorage, take it from DB
                        context.Response.BufferOutput = false;
                        if (!TryDownloadFromFileStream(context, fileMetadata, sqlConnection))
                            // If FileStream is not available - read from VarBinary(MAX) column using buffer;
                            DownloadFromVarbinary(context, fileMetadata, sqlConnection);
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

        class FileMetadata
        {
            public Guid FileContentId;
            public string FileName;
            public bool AzureStorage;
            public long Size;
        }

        private FileMetadata GetMetadata(HttpContext context, Guid? documentVersionId, Guid? fileContentId, SqlConnection sqlConnection)
        {
            // if "filename" is present in query, that one is used as download filename
            var query = HttpUtility.ParseQueryString(context.Request.Url.Query);
            string queryFileName = null;
            foreach (var key in query.AllKeys) if (key.ToLower() == "filename") queryFileName = query[key];

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

            using (var result = getMetadata.ExecuteReader(CommandBehavior.SingleRow))
            {
                result.Read();
                return new FileMetadata
                {
                    FileContentId = (Guid)result["FileContentID"],
                    FileName = queryFileName ?? (string)result["FileName"],
                    AzureStorage = result["AzureStorage"] != DBNull.Value ? (bool)result["AzureStorage"] : false,
                    Size = (long)result["FileSize"],
                };
            }
        }

        private bool TryDownloadFromAzureBlob(HttpContext context, FileMetadata file)
        {
            if (file.AzureStorage == false)
                return false;

            var storageConnectionVariable = System.Configuration.ConfigurationManager.AppSettings.Get("LightDms.StorageConnectionVariable");
            string storageConnectionString = null;
            if (!string.IsNullOrWhiteSpace(storageConnectionVariable))
                storageConnectionString = Environment.GetEnvironmentVariable(storageConnectionVariable, EnvironmentVariableTarget.Machine);
            else
                //variable name has to be defined if AzureStorage bit is set to true
                throw new FrameworkException("Azure Blob Storage connection variable name missing.");

            if (!string.IsNullOrEmpty(storageConnectionString))
            {
                CloudStorageAccount storageAccount = null;
                if (!CloudStorageAccount.TryParse(storageConnectionString, out storageAccount))
                    throw new FrameworkException("Invalid Azure Blob Storage connection string.");

                CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
                var storageContainerName = System.Configuration.ConfigurationManager.AppSettings.Get("LightDms.StorageContainer");
                if (string.IsNullOrWhiteSpace(storageContainerName))
                    throw new FrameworkException("Azure blob storage container name is missing from configuration.");

                CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(storageContainerName);
                if (!cloudBlobContainer.Exists())
                    throw new FrameworkException("Azure blob storage container doesn't exist.");

                CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference("doc-" + file.FileContentId.ToString());
                if (cloudBlockBlob.Exists())
                {
                    try
                    {
                        cloudBlockBlob.FetchAttributes();

                        PopulateHeader(context, file.FileName, cloudBlockBlob.Properties.Length);
                        cloudBlockBlob.DownloadToStream(context.Response.OutputStream);

                        context.Response.Flush();
                        return true;
                    }
                    catch (Exception ex)
                    {
                        //when unexpected error occurs log it, then fall back to DB
                        _logger.Error("Azure storage error, falling back to DB. Error: " + ex.ToString());
                        return false;
                    }
                }
                else
                    return false; //if no blob is present fall back to DB
            }
            else
                throw new FrameworkException("Azure Blob Storage environment variable missing.");
        }

        private bool TryDownloadFromFileStream(HttpContext context, FileMetadata file, SqlConnection sqlConnection)
        {
            SqlCommand checkFileStreamEnabled = new SqlCommand("SELECT TOP 1 1 FROM sys.columns c WHERE OBJECT_SCHEMA_NAME(C.object_id) = 'LightDMS' AND OBJECT_NAME(C.object_id) = 'FileContent' AND c.Name = 'Content' AND c.is_filestream = 1", sqlConnection);
            if (checkFileStreamEnabled.ExecuteScalar() == null)
                return false;

            SqlTransaction sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadCommitted); // Explicit transaction is required when working with SqlFileStream class.
            try
            {
                using (var fileStream = SqlFileStreamProvider.GetSqlFileStreamForDownload(file.FileContentId, sqlTransaction))
                {
                    byte[] buffer = new byte[BUFFER_SIZE];
                    int bytesRead;
                    PopulateHeader(context, file.FileName, file.Size);
                    while ((bytesRead = fileStream.Read(buffer, 0, BUFFER_SIZE)) > 0)
                    {
                        if (!context.Response.IsClientConnected)
                            break;
                        context.Response.OutputStream.Write(buffer, 0, bytesRead);
                        context.Response.Flush();
                    }
                }
            }
            finally
            {
                try { sqlTransaction.Rollback(); } catch { }
            }

            return true;
        }

        private void DownloadFromVarbinary(HttpContext context, FileMetadata file, SqlConnection sqlConnection)
        {
            byte[] buffer = new byte[BUFFER_SIZE];

            PopulateHeader(context, file.FileName, file.Size);

            SqlCommand readCommand = new SqlCommand("SELECT Content FROM LightDMS.FileContent WHERE ID='" + file.FileContentId.ToString() + "'", sqlConnection);
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
    }
}