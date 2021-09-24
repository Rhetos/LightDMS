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

using Amazon.S3.Transfer;
using Microsoft.WindowsAzure.Storage.Blob;
using Rhetos.LightDms.Storage;
using Rhetos.LightDMS.Storage;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Rhetos.LightDMS
{
    public class UploadHelper
    {
        private readonly ILogger _performanceLogger;
        private readonly ConnectionString _connectionString;
        private readonly LightDmsOptions _lightDmsOptions;
        private readonly S3Options _s3Options;
        private readonly AzureStorageClient _azureStorageClient;
        private readonly S3StorageClient _s3StorageClient;

        public UploadHelper(
            ILogProvider logProvider, 
            ConnectionString connectionString, 
            LightDmsOptions lightDmsOptions, 
            S3Options s3Options,
            AzureStorageClient azureStorageClient,
            S3StorageClient s3StorageClient)
        {
            _performanceLogger = logProvider.GetLogger("Performance.LightDMS");
            _connectionString = connectionString;
            _lightDmsOptions = lightDmsOptions;
            _s3Options = s3Options;
            _azureStorageClient = azureStorageClient;
            _s3StorageClient = s3StorageClient;
        }

        public async Task<FileUploadResult> UploadStream(Stream inputStream)
        {
            var id = Guid.NewGuid();
            var sw = Stopwatch.StartNew();

            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            sqlConnection.Open();
            SqlTransaction sqlTransaction = null;

            string createdDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            bool isS3 = _lightDmsOptions.UploadTarget == UploadTarget.S3;
            bool isAzure = _lightDmsOptions.UploadTarget == UploadTarget.Azure;
            try
            {
                sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadUncommitted);
                SqlCommand createEmptyFileContent = new SqlCommand("INSERT INTO LightDMS.FileContent(ID, [Content], [CreatedDate], [AzureStorage], [S3Storage]) VALUES('" + id + "', 0x0, '" + createdDate + "', " + (isAzure ? 1 : 0) + ", " + (isS3 ? 1 : 0) + ")", sqlConnection, sqlTransaction);
                createEmptyFileContent.ExecuteNonQuery();

                if (_lightDmsOptions.UploadTarget == UploadTarget.S3)
                    await UploadStreamToS3(inputStream, id);
                else if (_lightDmsOptions.UploadTarget == UploadTarget.Azure)
                    await UploadStreamToAzure(inputStream, id);
                else
                    await UploadStreamToDatabase(inputStream, id, sqlConnection, sqlTransaction, createdDate);

                sqlTransaction.Commit();
                sqlConnection.Close();
                _performanceLogger.Write(sw, "UploadFile (" + id + ") Executed.");
                return new FileUploadResult { StatusCode = HttpStatusCode.OK, ID = id };
            }
            catch (Exception ex)
            {
                try
                {
                    // Try to discard the database transaction (if still open and working).
                    if (sqlTransaction != null) sqlTransaction.Rollback();
                    sqlConnection.Close();
                }
                catch
                {
                    // No need to report an additional error when closing a failed transaction, because it might already be closed or rolled back.
                }

                if (ex.Message == "Function PathName is only valid on columns with the FILESTREAM attribute.")
                    return new FileUploadResult
                    {
                        StatusCode = HttpStatusCode.BadRequest,
                        Error = "FILESTREAM is not enabled on Database, or FileStream FileGroup is missing on database, or FILESTREAM attribute is missing from LightDMS.FileContent.Content column. Try with enabling FileStream on database, add FileGroup to database and transform Content column to VARBINARY(MAX) FILESTREAM type."
                    };
                else
                    return new FileUploadResult
                    {
                        StatusCode = HttpStatusCode.InternalServerError,
                        Exception = ex
                    };
            }
        }

        private async Task UploadStreamToAzure(Stream inputStream, Guid id)
        {
            var cloudBlobContainer = await _azureStorageClient.GetCloudBlobContainer();
            CloudBlockBlob cloudBlockBlob = cloudBlobContainer.GetBlockBlobReference("doc-" + id.ToString());
            await cloudBlockBlob.UploadFromStreamAsync(inputStream);
        }

        private async Task UploadStreamToS3(Stream inputStream, Guid id)
        {
            if (!string.IsNullOrEmpty(_s3Options.CertificateSubject))
            {
                ServicePointManager.ServerCertificateValidationCallback +=
                    delegate (
                        object ssender,
                        X509Certificate certificate,
                        X509Chain chain,
                        SslPolicyErrors sslPolicyErrors)
                    {
                        if (certificate.Subject.IndexOf(_s3Options.CertificateSubject) > -1)
                            return true;
                        return sslPolicyErrors == SslPolicyErrors.None;
                    };
            }
            
            using (var client = _s3StorageClient.GetAmazonS3Client())
            {
                using (var transferUtility = new TransferUtility(client))
                {
                    var s3Folder = _s3Options.DestinationFolder;
                    var req = new TransferUtilityUploadRequest
                    {
                        BucketName = _s3Options.BucketName
                    };
                    var fileName = s3Folder + "/doc-" + id.ToString();
                    req.Key = fileName;
                    req.InputStream = inputStream;
                    await transferUtility.UploadAsync(req);
                }
            }
        }

        private static async Task UploadStreamToDatabase(Stream inputStream, Guid id, SqlConnection sqlConnection, SqlTransaction sqlTransaction, string createdDate)
        {
            int bufferSize = 100 * 1024; // 100 kB buffer
            byte[] buffer = new byte[bufferSize];
            long totalbytesRead = 0;
            SqlCommand checkFileStreamEnabled = new SqlCommand("SELECT TOP 1 1 FROM sys.columns c WHERE OBJECT_SCHEMA_NAME(C.object_id) = 'LightDMS' AND OBJECT_NAME(C.object_id) = 'FileContent' AND c.Name = 'Content' AND c.is_filestream = 1", sqlConnection, sqlTransaction);
            if (checkFileStreamEnabled.ExecuteScalar() == null)
            {   //FileStream is not supported
                SqlCommand fileUpdateCommand = new SqlCommand("update LightDMS.FileContent set Content.WRITE(@Data, @Offset, null) where ID = @ID", sqlConnection, sqlTransaction);

                fileUpdateCommand.Parameters.Add("@Data", SqlDbType.Binary);
                fileUpdateCommand.Parameters.AddWithValue("@ID", id);
                fileUpdateCommand.Parameters.AddWithValue("@Offset", 0);

                var fileStream = inputStream;
                var bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length);
                while (bytesRead > 0)
                {
                    if (bytesRead < buffer.Length)
                    {
                        fileUpdateCommand.Parameters["@Data"].Value = buffer.Where((val, ix) => ix < bytesRead).ToArray();
                    }
                    else
                    {
                        fileUpdateCommand.Parameters["@Data"].Value = buffer;
                    }
                    fileUpdateCommand.Parameters["@Offset"].Value = totalbytesRead;
                    fileUpdateCommand.ExecuteNonQuery();
                    totalbytesRead += bytesRead;
                    bytesRead = await fileStream.ReadAsync(buffer, 0, buffer.Length);
                }

                fileUpdateCommand.Dispose();
                fileStream.Close();
            }
            else
            {
                using (SqlFileStream sfs = SqlFileStreamProvider.GetSqlFileStreamForUpload(id, createdDate, sqlTransaction))
                {
                    while (totalbytesRead < inputStream.Length)
                    {
                        var readed = await inputStream.ReadAsync(buffer, 0, bufferSize);
                        sfs.Write(buffer, 0, readed);
                        totalbytesRead += readed;
                    }
                    sfs.Close();
                }
            }
        }
    }
}
