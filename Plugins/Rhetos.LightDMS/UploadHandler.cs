using Rhetos.LightDms.Storage;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Web;
using System.Linq;

namespace Rhetos.LightDMS
{
    public class UploadHandler : IHttpHandler
    {
        private ILogger _performanceLogger;

        public UploadHandler()
        {
            var logProvider = Activator.CreateInstance<NLogProvider>();
            _performanceLogger = logProvider.GetLogger("Performance");
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
        
        public void ProcessRequest(HttpContext context)
        {
            var id = Guid.NewGuid();
            var sw = Stopwatch.StartNew();
            int bufferSize = 100 * 1024; // 100 kB buffer
            byte[] buffer = new byte[bufferSize];
            long bytesRead = 0, size;
            string filetype;

            SqlConnection sqlConnection = new SqlConnection(SqlUtility.ConnectionString);
            sqlConnection.Open();
            SqlTransaction sqlTransaction = null;
            try
            {
                sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadUncommitted);
                SqlFileStream sfs = SqlFileStreamProvider.GetSqlFileStream(System.IO.FileAccess.Write, @"
                    INSERT INTO dbo.MyDocuments([stream_id], [name], [file_stream]) 
                    VALUES(@stream_id, @filename, CAST('' AS VARBINARY(MAX)));
                    
                    SELECT file_stream.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT(), 0, ''
                    FROM dbo.MyDocuments
                    WHERE stream_id = @stream_id", id, context.Request.Files[0].FileName, context.Request.Files[0].ContentType, sqlTransaction, out size, out filetype);
                
                while (bytesRead < context.Request.Files[0].ContentLength)
                {
                    var readed = context.Request.Files[0].InputStream.Read(buffer, 0, bufferSize);
                    sfs.Write(buffer, 0, readed);
                    bytesRead += readed;
                }
                sfs.Close();
                sqlTransaction.Commit();
                sqlConnection.Close();

                _performanceLogger.Write(sw, "Rhetos.LightDMS: UploadFile (" + id + ") Executed.");
                context.Response.ContentType = "application/json;";
                context.Response.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new { ID = id }));
                context.Response.StatusCode = 200;
            }
            catch (Exception ex) {
                // TODO: Log into Rhetos logger
                if (sqlTransaction != null) sqlTransaction.Rollback();
                sqlConnection.Close();

                context.Response.ContentType = "application/json;";
                context.Response.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new { error = ex.Message, trace = ex.StackTrace }));
                context.Response.StatusCode = 400;
            }
        }
    }
}
