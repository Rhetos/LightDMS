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
            var query = HttpUtility.ParseQueryString(context.Request.Url.Query);

            var sw = Stopwatch.StartNew();
            int bufferSize = 100 * 1024; // 100 kB buffer
            byte[] buffer = new byte[bufferSize];
            long bytesRead = 0, size = 0;
            string fileName, fileExtension;

            SqlConnection sqlConnection = new SqlConnection(SqlUtility.ConnectionString);
            sqlConnection.Open();
            SqlTransaction sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadUncommitted);
            // check if FileStream is enabled
            //      if not, throw error or differente upload/download procedure
            try
            {
                SqlFileStream sfs = SqlFileStreamProvider.GetSqlFileStreamForDownload(@"
                        SELECT fc.Content.PathName(),
                                GET_FILESTREAM_TRANSACTION_CONTEXT(), 
                                FileSize = DATALENGTH(Content), 
                                Name = dv.FileName, 
                                Extension = dvext.FileExtension
                        FROM LightDMS.DocumentVersion dv
                            INNER JOIN LightDMS.FileContent fc ON dv.FileContentID = fc.ID
                            INNER JOIN LightDMS.DocumentVersionExt dvext ON dvext.ID = dv.ID
                        WHERE dv.ID = '" + id.ToString() + "'", sqlTransaction, out size, out fileName, out fileExtension);

                // if as query is "filename" given, that one is used as download filename
                foreach (var key in query.AllKeys) if (key.ToLower() == "filename") fileName = query[key];

                context.Response.BufferOutput = false;
                context.Response.ContentType = MimeMapping.GetMimeMapping(fileName);
                context.Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName);
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
                context.Response.StatusCode = 200;
            }
            catch (Exception ex)
            {
                // TODO: Log into Rhetos logger
                if (sqlTransaction != null) sqlTransaction.Rollback();
                sqlConnection.Close();

                context.Response.ContentType = "application/json;";
                if (ex.Message == "Function PathName is only valid on columns with the FILESTREAM attribute.")
                {
                    context.Response.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new { error = "FILESTREAM attribute is missing from LightDMS.FileContent.Content column. However, file is still available from download via REST interface." }));
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
