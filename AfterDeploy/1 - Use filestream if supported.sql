/*DatabaseGenerator:NoTransaction*/
-- HACK: This script does not need to be executed without transaction, but this hack solves the
-- issue with SqlTransactionBatches in Rhetos that would cause the following script (2 - Cleanup.sql)
-- to be locked because executed in a different transaction while the current transaction is not yet completed.

DECLARE @Error INT = 0;

-- If the database supports filestream, use it (see how to enable it in Readme.md).
-- otherwise the system will use the 'nvarbinary(max) Content' column as a simple byte[].
IF
    (SELECT SERVERPROPERTY('FilestreamEffectiveLevel')) >= 2
    AND (SELECT COUNT(*) FROM sys.database_files WHERE type = 2) > 0
    AND (SELECT is_filestream FROM sys.columns WHERE object_id = OBJECT_ID('LightDMS.FileContent') and name = 'Content') = 0
BEGIN
    -- T-SQL does not support adding FILESTREAM to an existing column. A new column must be created instead.

    ALTER TABLE LightDMS.FileContent ALTER COLUMN ID ADD ROWGUIDCOL;
    IF @@ERROR > 0 RETURN;

    EXEC @Error = sp_rename 'LightDMS.FileContent.Content', 'Content_backup' , 'COLUMN';
    IF @Error > 0 OR @@ERROR > 0 RETURN;

    EXEC @Error = sp_executesql N'ALTER TABLE LightDMS.FileContent ADD Content varbinary(max) FILESTREAM';
    IF @Error > 0 OR @@ERROR > 0 RETURN;

    EXEC @Error = sp_executesql N'UPDATE LightDMS.FileContent SET Content = Content_backup';
    IF @Error > 0 OR @@ERROR > 0 RETURN;

    EXEC @Error = sp_executesql N'ALTER TABLE LightDMS.FileContent DROP COLUMN Content_backup';
    IF @Error > 0 OR @@ERROR > 0 RETURN;
END
