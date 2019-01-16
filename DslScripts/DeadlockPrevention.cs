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

string lockDocument =
	string.Join("\r\n",
		insertedNew.Concat(updatedNew).Concat(deletedIds)
		.Where(item => item.DocumentID.HasValue)
		.Select(item => item.DocumentID)
		.Distinct()
		.Select(documentId => "exec sp_getapplock 'LightDms.Document_" + documentId + "', 'Exclusive';"));

_executionContext.SqlExecuter.ExecuteSql(lockDocument);