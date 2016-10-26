using Rhetos.LightDms.Storage;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace Rhetos.LightDMS
{
    public class DownloadHandler : IHttpHandler
    {
        private ILogger _performanceLogger;

        public DownloadHandler()
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
            // TODO: handle invalid ID
            var id = Guid.Parse(context.Request.Url.LocalPath.Split('/').Last());
            var sw = Stopwatch.StartNew();
            int bufferSize = 100 * 1024; // 100 kB buffer
            byte[] buffer = new byte[bufferSize];
            long bytesRead = 0, size = 0;
            string fileName;

            SqlConnection sqlConnection = new SqlConnection(SqlUtility.ConnectionString);
            sqlConnection.Open();
            SqlTransaction sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadUncommitted);
            SqlFileStream sfs = SqlFileStreamProvider.GetSqlFileStream(System.IO.FileAccess.Read, @"
                    SELECT file_stream.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT(), cached_file_size, name
                    FROM dbo.MyDocuments
                    WHERE stream_id = @stream_id", id, "", "", sqlTransaction, out size, out fileName);
            while (bytesRead < size)
            {
                var readed = sfs.Read(buffer, 0, bufferSize);
                if (readed == bufferSize)
                    context.Response.BinaryWrite(buffer);
                else
                    context.Response.BinaryWrite(buffer.Take(readed).ToArray());
                bytesRead += readed;
            }
            sfs.Close();
            sqlTransaction.Commit();
            sqlConnection.Close();
            _performanceLogger.Write(sw, "Rhetos.LightDMS: Downloaded file (" + id + ") Executed.");
            context.Response.ContentType = MimeMapping.GetMimeMapping(fileName);
            context.Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName);
            context.Response.StatusCode = 200;
        }
    }
}
