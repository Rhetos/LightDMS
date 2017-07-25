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
            if (context.Request.Files.Count != 1)
            {
                context.Response.ContentType = "application/json;";
                context.Response.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new { error = "Exactly one file has to be sent as request in Multipart format. There were " + context.Request.Files.Count + " files in upload request." }));
                context.Response.StatusCode = 400;
                return;
            }
            var id = Guid.NewGuid();
            var sw = Stopwatch.StartNew();
            int bufferSize = 100 * 1024; // 100 kB buffer
            byte[] buffer = new byte[bufferSize];
            long totalbytesRead = 0;

            SqlConnection sqlConnection = new SqlConnection(SqlUtility.ConnectionString);
            sqlConnection.Open();
            SqlTransaction sqlTransaction = null;
            try
            {
                sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadUncommitted);
                System.IO.Stream fileStream = context.Request.Files[0].InputStream;

                SqlCommand checkFileStreamEnabled = new SqlCommand("SELECT TOP 1 1 FROM sys.columns c WHERE OBJECT_SCHEMA_NAME(C.object_id) = 'LightDMS' AND OBJECT_NAME(C.object_id) = 'FileContent' AND c.Name = 'Content' AND c.is_filestream = 1", sqlConnection, sqlTransaction);
                var createdDate = DateTime.Now.ToString("yyyy-MM-dd hh:mm:ss.fff");

                if (checkFileStreamEnabled.ExecuteScalar() == null)
                {   //FileStream is not supported
                    SqlCommand createEmptyFileContent = new SqlCommand("INSERT INTO LightDMS.FileContent(ID, [Content], [CreatedDate]) VALUES('" + id + "', 0x0, '" + createdDate + "');", sqlConnection, sqlTransaction);
                    createEmptyFileContent.ExecuteNonQuery();
                    SqlCommand fileUpdateCommand = new SqlCommand("update LightDMS.FileContent set Content.WRITE(@Data, @Offset, null) where ID = @ID", sqlConnection, sqlTransaction);

                    fileUpdateCommand.Parameters.Add("@Data", SqlDbType.Binary);
                    fileUpdateCommand.Parameters.AddWithValue("@ID", id);
                    fileUpdateCommand.Parameters.AddWithValue("@Offset", 0);

                    var bytesRead = fileStream.Read(buffer, 0, buffer.Length);
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
                        bytesRead = fileStream.Read(buffer, 0, buffer.Length);
                    }

                    fileUpdateCommand.Dispose();
                    fileStream.Close();

                    sqlTransaction.Commit();
                    sqlConnection.Close();
                }
                else
                {
                    SqlFileStream sfs = SqlFileStreamProvider.GetSqlFileStreamForUpload(@"
                    INSERT INTO LightDMS.FileContent(ID, [Content], [CreatedDate]) 
                    VALUES(@id, CAST('' AS VARBINARY(MAX)), '" + createdDate + @"');

                    SELECT Content.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT()
                    FROM LightDMS.FileContent
                    WHERE ID = @id", id, sqlTransaction);
                    while (totalbytesRead < context.Request.Files[0].ContentLength)
                    {
                        var readed = context.Request.Files[0].InputStream.Read(buffer, 0, bufferSize);
                        sfs.Write(buffer, 0, readed);
                        totalbytesRead += readed;
                    }
                    sfs.Close();
                    sqlTransaction.Commit();
                    sqlConnection.Close();
                }

                _performanceLogger.Write(sw, "Rhetos.LightDMS: UploadFile (" + id + ") Executed.");
                context.Response.ContentType = "application/json;";
                context.Response.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new { ID = id }));
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
                    context.Response.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new { error = "FILESTREAM is not enabled on Database, or FileStream FileGroup is missing on database, or FILESTREAM attribute is missing from LightDMS.FileContent.Content column. Try with enabling FileStream on database, add FileGroup to database and transform Content column to VARBINARY(MAX) FILESTREAM type." }));
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
