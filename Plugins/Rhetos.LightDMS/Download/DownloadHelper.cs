using Rhetos.LightDms.Storage;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Net;
using System.Web;

namespace Rhetos.LightDMS
{
    public class DownloadHelper
    {
        private ILogger _performanceLogger;
        private ILogger _logger;

        public DownloadHelper()
        {
            var logProvider = new NLogProvider();
            _performanceLogger = logProvider.GetLogger("Performance");
            _logger = logProvider.GetLogger(GetType().Name);
        }

        public void HandleDownload(HttpContext context, Guid? documentId, Guid? fileContentId)
        {
            var query = HttpUtility.ParseQueryString(context.Request.Url.Query);

            int bufferSize = 100 * 1024; // 100 kB buffer
            byte[] buffer = new byte[bufferSize];
            long bytesRead = 0, size = 0;
            string fileName;

            SqlConnection sqlConnection = new SqlConnection(SqlUtility.ConnectionString);
            sqlConnection.Open();
            SqlTransaction sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadUncommitted);
            SqlDataReader reader = null;
            try
            {
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.BufferOutput = false;

                SqlCommand checkFileStreamEnabled = new SqlCommand("SELECT TOP 1 1 FROM sys.columns c WHERE OBJECT_SCHEMA_NAME(C.object_id) = 'LightDMS' AND OBJECT_NAME(C.object_id) = 'FileContent' AND c.Name = 'Content' AND c.is_filestream = 1", sqlConnection, sqlTransaction);
                if (checkFileStreamEnabled.ExecuteScalar() == null)
                { // FileStream not available - read from VarBinary(MAX) column using buffer;
                    SqlCommand getFileSize = new SqlCommand(@"
                        SELECT FileSize = DATALENGTH(Content), 
                                Name = dv.FileName,
                                FileContentID = fc.ID
                        FROM LightDMS.DocumentVersion dv
                            INNER JOIN LightDMS.FileContent fc ON dv.FileContentID = fc.ID
                            INNER JOIN LightDMS.DocumentVersionExt dvext ON dvext.ID = dv.ID
                        WHERE dv.ID = '" + documentId + @"'", sqlConnection, sqlTransaction);

                    if (!documentId.HasValue)
                        getFileSize = new SqlCommand(@"
                        SELECT FileSize = DATALENGTH(Content), 
                                Name='unknown.txt',
                                FileContentID = fc.ID 
                        FROM LightDMS.FileContent fc WHERE ID = '" + fileContentId + "'", sqlConnection, sqlTransaction);

                    var result = getFileSize.ExecuteReader(CommandBehavior.SingleRow);
                    result.Read();
                    fileName = (string)result["Name"];
                    size = (long)result["FileSize"];
                    var fileContentID = (Guid)result["FileContentID"];
                    result.Close();

                    // if as query is "filename" given, that one is used as download filename
                    foreach (var key in query.AllKeys) if (key.ToLower() == "filename") fileName = query[key];
                    context.Response.ContentType = MimeMapping.GetMimeMapping(fileName);
                    // Koristiti HttpUtility.UrlPathEncode umjesto HttpUtility.UrlEncode ili Uri.EscapeDataString jer drugačije handlea SPACE i specijalne znakove
                    context.Response.AddHeader("Content-Disposition", "attachment; filename*=UTF-8''" + HttpUtility.UrlPathEncode(fileName) + "");
                    context.Response.AddHeader("Content-Length", size.ToString());

                    SqlCommand readCommand = new SqlCommand("SELECT Content FROM LightDMS.FileContent WHERE ID='" + fileContentID.ToString() + "'", sqlConnection, sqlTransaction);
                    reader = readCommand.ExecuteReader(CommandBehavior.SequentialAccess);

                    while (reader.Read())
                    {
                        // Read bytes into outByte[] and retain the number of bytes returned.  
                        var readed = reader.GetBytes(0, 0, buffer, 0, bufferSize);
                        var startIndex = 0;
                        // Continue while there are bytes beyond the size of the buffer.  
                        while (readed == bufferSize)
                        {
                            context.Response.OutputStream.Write(buffer, 0, (int)readed);
                            context.Response.Flush();

                            // Reposition start index to end of last buffer and fill buffer.  
                            startIndex += bufferSize;
                            readed = reader.GetBytes(0, startIndex, buffer, 0, bufferSize);
                        }

                        context.Response.OutputStream.Write(buffer, 0, (int)readed);
                        context.Response.Flush();
                    }

                    reader.Close();
                    reader = null;
                }
                else
                {
                    string sqlQuery = @"
                        SELECT fc.Content.PathName(),
                                GET_FILESTREAM_TRANSACTION_CONTEXT(), 
                                FileSize = DATALENGTH(Content), 
                                Name = dv.FileName
                        FROM LightDMS.DocumentVersion dv
                            INNER JOIN LightDMS.FileContent fc ON dv.FileContentID = fc.ID
                            INNER JOIN LightDMS.DocumentVersionExt dvext ON dvext.ID = dv.ID
                        WHERE dv.ID = '" + documentId + "'";
                    if (!documentId.HasValue) sqlQuery = @"
                        SELECT fc.Content.PathName(),
                                GET_FILESTREAM_TRANSACTION_CONTEXT(), 
                                FileSize = DATALENGTH(Content), 
                                Name = 'unknown.txt'
                        FROM LightDMS.FileContent fc
                        WHERE fc.ID = '" + fileContentId + "'";

                    SqlFileStream sfs = SqlFileStreamProvider.GetSqlFileStreamForDownload(sqlQuery, sqlTransaction, out size, out fileName);

                    // if as query is "filename" given, that one is used as download filename
                    foreach (var key in query.AllKeys) if (key.ToLower() == "filename") fileName = query[key];
                    context.Response.ContentType = MimeMapping.GetMimeMapping(fileName);
                    // Koristiti HttpUtility.UrlPathEncode umjesto HttpUtility.UrlEncode ili Uri.EscapeDataString jer drugačije handlea SPACE i specijalne znakove
                    context.Response.AddHeader("Content-Disposition", "attachment; filename*=UTF-8''" + HttpUtility.UrlPathEncode(fileName) + "");
                    context.Response.AddHeader("Content-Length", size.ToString());

                    while (bytesRead < size)
                    {
                        var readed = sfs.Read(buffer, 0, bufferSize);
                        if (!context.Response.IsClientConnected)
                            break;
                        context.Response.OutputStream.Write(buffer, 0, readed);
                        context.Response.Flush();
                        bytesRead += readed;
                    }
                    sfs.Close();
                }

                sqlTransaction.Commit();
                sqlConnection.Close();
            }
            catch (Exception ex)
            {
                _logger.Error(ex.ToString());
                if (reader != null && !reader.IsClosed) reader.Close();

                if (sqlTransaction != null) sqlTransaction.Rollback();
                sqlConnection.Close();

                context.Response.ContentType = "application/json;";
                if (ex.Message == "Function PathName is only valid on columns with the FILESTREAM attribute.")
                {
                    var errorMessage = "FILESTREAM attribute is missing from LightDMS.FileContent.Content column. However, file is still available from download via REST interface.";
                    _logger.Error(errorMessage);
                    context.Response.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new { error = errorMessage }));
                }
                else
                {
                    context.Response.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new { error = ex.Message, trace = ex.StackTrace }));
                }

                context.Response.StatusCode = 400;
            }
        }
    }
}
