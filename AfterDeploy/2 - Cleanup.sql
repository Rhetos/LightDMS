/*DatabaseGenerator:NoTransaction*/

-- Reclaims space from dropped variable-length columns in tables,
-- after migrating old varbinary(max) column to FILESTREAM
-- and dropping the old column.
IF
    -- Check if FILESTREAM is applied to this table.
    (SELECT is_filestream FROM sys.columns WHERE object_id = OBJECT_ID('LightDMS.FileContent') and name = 'Content')
        = 1
    AND
    -- Check if there is still some BLOB space used by this table.
    -- It should be 0 because filestream keeps data separately.
    (SELECT page_count
    FROM sys.dm_db_index_physical_stats(DB_ID(), OBJECT_ID('LightDMS.FileContent'), NULL, NULL , 'Detailed')
    WHERE alloc_unit_type_desc = 'LOB_DATA')
        > 0
BEGIN
    DBCC CLEANTABLE (0, 'LightDMS.FileContent', 1000);
END
