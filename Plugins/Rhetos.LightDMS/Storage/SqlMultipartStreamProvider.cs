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
        public static SqlFileStream GetSqlFileStreamForUpload(string insertSqlText, Guid id, SqlTransaction sqlTransaction)
        {
            SqlCommand sqlCommand = new SqlCommand(insertSqlText, sqlTransaction.Connection)
            {
                Transaction = sqlTransaction
            };
            
            sqlCommand.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = id;

            using (var reader = sqlCommand.ExecuteReader())
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
    }
}