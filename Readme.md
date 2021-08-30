# LightDMS

LightDMS is a light document version system implementation plugin for [Rhetos development platform](https://github.com/Rhetos/Rhetos).
It automatically creates DocumentVersion and other entities for managing documents (and their version) in Rhetos based solutions.
Aside entities, versioning, it also exposes additional web interface for uploading/downloading files.

See [rhetos.org](http://www.rhetos.org/) for more information on Rhetos.

1. [Features](#features)
   1. [File web API](#file-web-api)
   2. [Storage options](#storage-options)
2. [Database preparation](#database-preparation)
   1. [Enable FILESTREAM on your application's database](#enable-filestream-on-your-applications-database)
   2. [Activate FILESTREAM usage in LightDMS](#activate-filestream-usage-in-lightdms)
3. [Installation and configuration](#installation-and-configuration)
4. [Building and testing the source code](#building-and-testing-the-source-code)

## Features

### File web API

LightDMS plugin provides the following web API methods.
Check out your application's Rhetos dashboard (for example http://localhos:5000/rhetos) for sample code and demonstration of the LightDMS web API.

Upload:

* Uploading a file: `<RhetosSite>/LightDMS/Upload`
  * Example format `http://localhost/Rhetos/LightDMS/Upload/8EF65043-2E2A-424D-B76F-4DAA5A48CB3D`
  * Response contains file content ID. Note that one LightDMS document may be related to many files, one for each version of the document.

Download:

* Downloading a file with given **file content ID**: HTTP GET `<RhetosSite>/LightDMS/DownloadPreview/{{ID}}?filename={{filename}}`
  * The *ID* parameter is GUID formatted file content ID.
  * The *filename* query parameter is a name that the browser will offer to the user when saving the downloaded file.
  * Example format `http://localhost/Rhetos/LightDMS/DownloadPreview/8EF65043-2E2A-424D-B76F-4DAA5A48CB3D?filename=somefile.txt`
  * Parametrized format `http://localhost/Rhetos/LightDMS/DownloadPreview?id=8EF65043-2E2A-424D-B76F-4DAA5A48CB3D&filename=somefile.txt` (since 1.8.0)

* Downloading a file with given **document version ID**: HTTP GET `<RhetosSite>/LightDMS/Download/{{ID}}`
  * The *ID* parameter is GUID formatted document version ID.
  * Example format `http://localhost/Rhetos/LightDMS/Download/8EF65043-2E2A-424D-B76F-4DAA5A48CB3D`
  * Parametrized format `http://localhost/Rhetos/LightDMS/Download?id=8EF65043-2E2A-424D-B76F-4DAA5A48CB3D` (since 1.8.0)

### Storage options

LightDMS allows the following storage options:

1. Simple BLOB storage in the database table
2. Database FILESTREAM storage
    * This is a large performance improvement over the simple BLOB storage.
    * The files are accessed through the database API but are physically stored as file on a server disk.
3. Azure Blob Storage (currently download-only)
    * This option currently works as an extension of the FILESTREAM storage.
    The files are uploaded to FILESTREAM.
    A custom scheduled process is expected to migrate the files to Azure Blob Storage (archive).
    LightDMS will then download the archived file from Azure.
4. Document storage with Amazon S3 API (currently download-only)

## Database preparation

### Enable FILESTREAM on your application's database

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

### Activate FILESTREAM usage in LightDMS

Option A:
If you have enabled FILESTREAM on your database, it will **automatically** be used by LightDMS
**after the next execution of *DeployPackages.exe***.

Option B:
If you have already executed *DeployPackages.exe* to deploy LightDMS package before enabling FILESTREAM on the database,
and you do not want to execute *DeployPackages.exe* again,
the FILESTREAM usage in LightDMS can be activated by running the following SQL script on the database:

```SQL
DECLARE @Error INT = 0;
BEGIN TRAN;

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

COMMIT;
GO
IF @@TRANCOUNT > 0 ROLLBACK;
```

## Installation and configuration

Installing this package to a Rhetos web application:

1. Add 'Rhetos.LightDMS' NuGet package, available at the [NuGet.org](https://www.nuget.org/) on-line gallery.
2. Extend Rhetos services configuration (at `services.AddRhetosHost`) with the LightDMS service: `.AddLightDMS()`

## Building and testing the source code

**Note:** This package is already available at the [NuGet.org](https://www.nuget.org/) online gallery.
You don't need to build it from source in order to use it in your application.

To build the package from source, run `Build.bat`.
The build output is a NuGet package in the "Install" subfolder.

For contributions guidelines see [How to Contribute](https://github.com/Rhetos/Rhetos/wiki/How-to-Contribute) on Rhetos wiki.
