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
        public static SqlFileStream GetSqlFileStreamForUpload(string InsertSqlText, Guid id, SqlTransaction SqlTransaction)
        {
            SqlCommand SqlCommand = new SqlCommand(InsertSqlText, SqlTransaction.Connection)
            {
                Transaction = SqlTransaction
            };
            
            SqlCommand.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;

            using (var reader = SqlCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    var path = reader.GetString(0);
                    byte[] transactionContext = reader.GetSqlBytes(1).Buffer;
                    return new SqlFileStream(path, transactionContext, FileAccess.Write, FileOptions.SequentialScan, 0);
                }
                else return null;
            }
        }

        public static SqlFileStream GetSqlFileStreamForDownload(string InsertSqlText, SqlTransaction SqlTransaction, out long size, out string filename)
        {
            SqlCommand SqlCommand = new SqlCommand(InsertSqlText, SqlTransaction.Connection)
            {
                Transaction = SqlTransaction
            };

            using (var reader = SqlCommand.ExecuteReader())
            {
                size = 0;
                filename = "";
                if (reader.Read())
                {
                    var path = reader.GetString(0);
                    byte[] transactionContext = reader.GetSqlBytes(1).Buffer;
                    size = reader.GetInt64(2);
                    filename = reader.GetString(3);
                    return new SqlFileStream(path, transactionContext, FileAccess.Read, FileOptions.SequentialScan, 0);
                }
                else return null;
            }
        }
    }
}