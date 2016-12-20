using Rhetos.LightDms.Storage;
using Rhetos.Utilities;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Rhetos.LightDMS
{
    public class DownloadHelper
    {
        public static void HandleDownload(HttpContext context, string sqlQuery)
        {
            var query = HttpUtility.ParseQueryString(context.Request.Url.Query);

            int bufferSize = 100 * 1024; // 100 kB buffer
            byte[] buffer = new byte[bufferSize];
            long bytesRead = 0, size = 0;
            string fileName;

            SqlConnection sqlConnection = new SqlConnection(SqlUtility.ConnectionString);
            sqlConnection.Open();
            SqlTransaction sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadUncommitted);
            // check if FileStream is enabled
            //      if not, throw error or different upload/download procedure
            try
            {
                SqlFileStream sfs = SqlFileStreamProvider.GetSqlFileStreamForDownload(sqlQuery, sqlTransaction, out size, out fileName);

                // if as query is "filename" given, that one is used as download filename
                foreach (var key in query.AllKeys) if (key.ToLower() == "filename") fileName = query[key];

                context.Response.ContentType = MimeMapping.GetMimeMapping(fileName);
                context.Response.AddHeader("Content-Disposition", "attachment; filename=" + fileName);
                context.Response.AddHeader("Content-Length", size.ToString());
                context.Response.StatusCode = (int)HttpStatusCode.OK;
                context.Response.BufferOutput = false;

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
                sqlTransaction.Commit();
                sqlConnection.Close();
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
