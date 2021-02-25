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

using Microsoft.AspNetCore.Http;
using Rhetos.LightDms.Storage;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Rhetos.LightDMS
{
    public class UploadHandler
    {
        private readonly ILogger _performanceLogger;
        private readonly ConnectionString _connectionString;
        private readonly Respond _respond;

        public UploadHandler(ILogProvider logProvider, ConnectionString connectionString)
        {
            _connectionString = connectionString;
            _performanceLogger = logProvider.GetLogger("Performance.LightDMS");
            _respond = new Respond(logProvider);
        }

        public async Task ProcessRequest(HttpContext context)
        {
            if (context.Request.Form.Files.Count != 1)
            {
                await _respond.BadRequest(context, "Exactly one file has to be sent as request in Multipart format. There were " + context.Request.Form.Files.Count + " files in upload request.");
                return;
            }

            var id = Guid.NewGuid();
            var sw = Stopwatch.StartNew();
            int bufferSize = 100 * 1024; // 100 kB buffer
            byte[] buffer = new byte[bufferSize];
            long totalbytesRead = 0;

            SqlConnection sqlConnection = new SqlConnection(_connectionString);
            sqlConnection.Open();
            SqlTransaction sqlTransaction = null;
            try
            {
                sqlTransaction = sqlConnection.BeginTransaction(IsolationLevel.ReadUncommitted);

                SqlCommand checkFileStreamEnabled = new SqlCommand("SELECT TOP 1 1 FROM sys.columns c WHERE OBJECT_SCHEMA_NAME(C.object_id) = 'LightDMS' AND OBJECT_NAME(C.object_id) = 'FileContent' AND c.Name = 'Content' AND c.is_filestream = 1", sqlConnection, sqlTransaction);
                string createdDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");

                if (checkFileStreamEnabled.ExecuteScalar() == null)
                {   //FileStream is not supported
                    SqlCommand createEmptyFileContent = new SqlCommand("INSERT INTO LightDMS.FileContent(ID, [Content], [CreatedDate]) VALUES('" + id + "', 0x0, '" + createdDate + "');", sqlConnection, sqlTransaction);
                    createEmptyFileContent.ExecuteNonQuery();
                    SqlCommand fileUpdateCommand = new SqlCommand("update LightDMS.FileContent set Content.WRITE(@Data, @Offset, null) where ID = @ID", sqlConnection, sqlTransaction);

                    fileUpdateCommand.Parameters.Add("@Data", SqlDbType.Binary);
                    fileUpdateCommand.Parameters.AddWithValue("@ID", id);
                    fileUpdateCommand.Parameters.AddWithValue("@Offset", 0);

                    var fileStream = context.Request.Form.Files[0].OpenReadStream();
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
                        var fileStream = context.Request.Form.Files[0].OpenReadStream();
                        while (totalbytesRead < context.Request.Form.Files[0].Length)
                        {
                            var readed = await fileStream.ReadAsync(buffer, 0, bufferSize);
                            sfs.Write(buffer, 0, readed);
                            totalbytesRead += readed;
                        }
                        sfs.Close();
                    }
                }

                sqlTransaction.Commit();
                sqlConnection.Close();
                _performanceLogger.Write(sw, "UploadFile (" + id + ") Executed.");
                await _respond.Ok(context, new { ID = id });
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
                }

                if (ex.Message == "Function PathName is only valid on columns with the FILESTREAM attribute.")
                    await _respond.BadRequest(context, "FILESTREAM is not enabled on Database, or FileStream FileGroup is missing on database, or FILESTREAM attribute is missing from LightDMS.FileContent.Content column. Try with enabling FileStream on database, add FileGroup to database and transform Content column to VARBINARY(MAX) FILESTREAM type.");
                else
                    await _respond.InternalError(context, ex);
            }
        }
    }
}
