# LightDMS

LightDMS is a light document version system implementation plugin for [Rhetos development platform](https://github.com/Rhetos/Rhetos).
It automatically creates DocumentVersion and other entities for managing documents (and their version) in Rhetos based solutions.
Aside entities, versioning, it also exposes additional web interface for uploading/downloading files.

See [rhetos.org](http://www.rhetos.org/) for more information on Rhetos.

## Features

### Web service methods

**Upload:**

* Uploading a file with predefined document ID: `<RhetosSite>/LightDMS/Upload/{{ID}}`
    * Query parameters ID is required. ID is GUID formatted identifier of DocumentVersion.
    * Example format `http://localhost/Rhetos/LightDMS/Upload/8EF65043-2E2A-424D-B76F-4DAA5A48CB3D`

**Download:**

* Downloading a file with predefined document ID: `<RhetosSite>/LightDMS/Download/{{ID}}`
    * Query parameters ID is required. ID is GUID formatted identifier of DocumentVersion.
    * Example format `http://localhost/Rhetos/LightDMS/Download/8EF65043-2E2A-424D-B76F-4DAA5A48CB3D`

## Database preparation

**Enable FileStream:**

1. Enable FileStream on SqlServer instance - Sql Server Configuration Manager [Steps](https://msdn.microsoft.com/en-us/library/cc645923.aspx)

2. Enable FileStream on database level:

    ```SQL
    EXEC sp_configure filestream_access_level, 2
    RECONFIGURE;
    ```

3. Setup FileGroup on database level to enable FileStream option for varbinary(max) column:

    ```SQL
    ALTER DATABASE <DB_Name>
    ADD FILEGROUP fs_Group CONTAINS FILESTREAM;
    GO
    -- Minimum one location where to save files for that fileGroup
    ALTER DATABASE <DB_Name>
    ADD FILE ( NAME = 'fs_<DB_Name>', FILENAME = '<LOCAL_DIR_PATH>' )
    TO FILEGROUP fs_Group;
    ```

4. Test that FileStream is enabled and can be used for varbinary(max) COLUMN

    ```SQL
    CREATE TABLE dbo.Test_FS
    (
        ID uniqueidentifier PRIMARY KEY ROWGUIDCOL,
        Content varbinary(max) FILESTREAM
    );
    DROP TABLE dbo.Test_FS;
    ```

## If you enabled FILESTREAM and made setup to DATABASE after LightDMS package deployment, you still can add FILESTREAM attribute to FileContent.Content column as following:

On the next execution of DeployPackages.exe, the FILESTREAM will be automatically added to the LightDMS.FileContent.Content column.

Instead of running DeployPackages.exe, you can execute the following script to use FILESTREAM:

```SQL
ALTER TABLE LightDMS.FileContent
    ALTER COLUMN ID ADD ROWGUIDCOL;
EXEC sp_rename 'LightDMS.FileContent.Content', 'Content_backup' , 'COLUMN';
ALTER TABLE LightDMS.FileContent
    ADD Content varbinary(max) FILESTREAM

GO

EXEC ('
    UPDATE LightDMS.FileContent
        SET Content = Content_backup;
    ALTER TABLE LightDMS.FileContent
        DROP COLUMN Content_backup;
');
```

## Build

**Note:** This package is already available at the [NuGet.org](https://www.nuget.org/) online gallery.
You don't need to build it from source in order to use it in your application.

To build the package from source, run `Build.bat`.
The script will pause in case of an error.
The build output is a NuGet package in the "Install" subfolder.

## Installation

To install this package to a Rhetos server, add it to the Rhetos server's *RhetosPackages.config* file
and make sure the NuGet package location is listed in the *RhetosPackageSources.config* file.

* The package ID is "**Rhetos.LightDMS**".
  This package is available at the [NuGet.org](https://www.nuget.org/) online gallery.
  It can be downloaded or installed directly from there.
* For more information, see [Installing plugin packages](https://github.com/Rhetos/Rhetos/wiki/Installing-plugin-packages).
