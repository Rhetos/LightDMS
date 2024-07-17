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

using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.IO;

namespace Rhetos.LightDMS.Storage
{
    /// <summary>
    /// SqlFileStreamProvider for SqlFileStream
    /// </summary>
    public static class SqlFileStreamProvider
    {
        public static SqlFileStream GetSqlFileStreamForUpload(Guid fileContentId, SqlTransaction sqlTransaction)
        {
            string insertSqlText =
                @"
                SELECT Content.PathName(), GET_FILESTREAM_TRANSACTION_CONTEXT()
                FROM LightDMS.FileContent
                WHERE ID = @id";

            SqlCommand sqlCommand = new SqlCommand(insertSqlText, sqlTransaction.Connection, sqlTransaction);
            sqlCommand.Parameters.Add("@id", SqlDbType.UniqueIdentifier).Value = fileContentId;

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

        public static SqlFileStream GetSqlFileStreamForDownload(Guid fileContentId, SqlTransaction sqlTransaction)
        {
            string sqlQuery = $@"
                SELECT 
                    fc.Content.PathName(),
                    GET_FILESTREAM_TRANSACTION_CONTEXT()
                FROM 
                    LightDMS.FileContent fc
                WHERE 
                    fc.ID = @fileContentId";

            SqlCommand sqlCommand = new SqlCommand(sqlQuery, sqlTransaction.Connection, sqlTransaction);
            sqlCommand.Parameters.Add("@fileContentId", SqlDbType.UniqueIdentifier).Value = fileContentId;

            using (var reader = sqlCommand.ExecuteReader())
            {
                if (reader.Read())
                {
                    var path = reader.GetString(0);
                    byte[] transactionContext = reader.GetSqlBytes(1).Buffer;
                    return new SqlFileStream(path, transactionContext, FileAccess.Read, FileOptions.SequentialScan, 0);
                }
                else
                    throw new ClientException($"Missing LightDMS.FileContent ID '{fileContentId}'.");
            }
        }
    }
}