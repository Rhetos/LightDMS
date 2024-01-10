string lockDocument =
	string.Join("\r\n",
		insertedNew.Concat(updatedNew).Concat(deletedIds)
		.Where(item => item.DocumentID.HasValue)
		.Select(item => item.DocumentID)
		.Distinct()
		.Select(documentId => "exec sp_getapplock 'LightDms.Document_" + documentId + "', 'Exclusive';"));

_executionContext.SqlExecuter.ExecuteSql(lockDocument);