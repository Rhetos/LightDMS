string lockDocument =
	string.Join("\r\n",
		insertedNew.Concat(updatedNew).Concat(deletedIds)
		.Select(item => item.DocumentID)
		.Distinct()
		.Select(documentId => "sp_getapplock 'LightDms.Document_" + documentId + "', 'Exclusive';"));

_executionContext.NHibernateSession.CreateSQLQuery(lockDocument).ExecuteUpdate();