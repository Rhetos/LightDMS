-- If the database supports filestream, use it,
-- otherwise the system will use the 'nvarbinary(max) Content' column as a simple byte[].
IF
    (SELECT SERVERPROPERTY('FilestreamEffectiveLevel')) >= 2
    AND (SELECT COUNT(*) FROM sys.database_files WHERE type = 2) > 0
    AND (SELECT is_filestream FROM sys.columns WHERE object_id = OBJECT_ID('LightDMS.FileContent') and name = 'Content') = 0
BEGIN
    -- T-SQL does not support adding FILESTREAM to an existing column. A new column must be created instead.
    ALTER TABLE LightDMS.FileContent ALTER COLUMN ID ADD ROWGUIDCOL;
    EXEC sp_rename 'LightDMS.FileContent.Content', 'Content_backup' , 'COLUMN';
    EXEC ('ALTER TABLE LightDMS.FileContent ADD Content varbinary(max) FILESTREAM');
    EXEC ('UPDATE LightDMS.FileContent SET Content = Content_backup;');
    EXEC ('ALTER TABLE LightDMS.FileContent DROP COLUMN Content_backup;');
END
