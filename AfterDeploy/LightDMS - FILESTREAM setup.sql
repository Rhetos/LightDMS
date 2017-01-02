IF (
	NOT EXISTS (SELECT TOP 1 1 
	FROM sys.columns c 
	WHERE OBJECT_SCHEMA_NAME(C.object_id) = 'LightDMS' 
		AND OBJECT_NAME(C.object_id) = 'FileContent' 
		AND c.Name = 'Content' 
		AND c.is_filestream = 1
	) AND
	(
		((SELECT SERVERPROPERTY('FilestreamEffectiveLevel')) >= 2) AND 
		((SELECT COUNT(*) FROM sys.database_files WHERE type = 2) > 0)
	)
)
BEGIN

    -- SET LightDMS.FileContent.Content AS FILESTREAM varbinary(MAX) column
    EXEC ('ALTER TABLE LightDMS.FileContent
			ALTER COLUMN ID ADD ROWGUIDCOL;
		EXEC sp_rename ''LightDMS.FileContent.Content'', ''Content_backup'' , ''COLUMN'';
		ALTER TABLE LightDMS.FileContent
			ADD Content varbinary(max) FILESTREAM
			');

    EXEC ('
        UPDATE LightDMS.FileContent
            SET Content = Content_backup;
        ALTER TABLE LightDMS.FileContent
            DROP COLUMN Content_backup;
    ');

END

