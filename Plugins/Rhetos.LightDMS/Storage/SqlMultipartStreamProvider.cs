using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;

namespace Rhetos.LightDms.Storage
{
    /// <summary>
    /// SqlFileStreamProvider for SqlFileStream
    /// </summary>
    public class SqlFileStreamProvider
    {
        public static SqlFileStream GetSqlFileStream(FileAccess access, string InsertSqlText, Guid id, string fileName, string fileType, SqlTransaction SqlTransaction, out long size, out string type)
        {
            SqlCommand SqlCommand = new SqlCommand(InsertSqlText, SqlTransaction.Connection)
            {
                Transaction = SqlTransaction
            };
            
            SqlCommand.Parameters.Add("@stream_id", SqlDbType.UniqueIdentifier).Value = id;
            SqlCommand.Parameters.Add("@filename", SqlDbType.VarChar).Value = fileName;

            using (var reader = SqlCommand.ExecuteReader())
            {
                size = 0;
                type = "";
                if (reader.Read())
                {
                    var path = reader.GetString(0);
                    byte[] transactionContext = reader.GetSqlBytes(1).Buffer;
                    if (access == FileAccess.Read)
                    {
                        size = reader.GetInt64(2);
                        type = reader.GetString(3);
                    }
                    return new SqlFileStream(path, transactionContext, access, FileOptions.SequentialScan, 0);
                }
                else return null;
            }
        }
    }
}