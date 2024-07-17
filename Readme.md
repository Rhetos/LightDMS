# LightDMS

LightDMS is a lightweight document storage and versioning plugin for [Rhetos development platform](https://github.com/Rhetos/Rhetos).

- It supports different storage methods: (MS SQL table, MS SQL filestream, S3 storage, Azure BLOB storage).
- It provides web API for upload, download and document versioning.

See [rhetos.org](http://www.rhetos.org/) for more information on Rhetos.

1. [Features](#features)
   1. [File web API](#file-web-api)
   2. [Storage options](#storage-options)
2. [Installation and configuration](#installation-and-configuration)
3. [Optimize database storage with FILESTREAM](#optimize-database-storage-with-filestream)
   1. [1. Enable FILESTREAM on your application's database](#1-enable-filestream-on-your-applications-database)
   2. [2. Activate FILESTREAM usage in LightDMS](#2-activate-filestream-usage-in-lightdms)
   3. [3. Database cleanup after dbupdate](#3-database-cleanup-after-dbupdate)
4. [How to contribute](#how-to-contribute)
   1. [Build](#build)
   2. [Test](#test)
      1. [Prerequisites](#prerequisites)
      2. [Configure and run test](#configure-and-run-test)
      3. [How testing works](#how-testing-works)

## Features

### File web API

Examples in this article will assume that your application's base URI is `https://localhost:5000`.

LightDMS plugin provides the following web API methods.
Check out your application's Rhetos dashboard (for example <https://localhost:5000/rhetos>) for sample code and demonstration of the LightDMS web API.

Upload:

* Uploading a file: `<baseURI>/LightDMS/Upload`
  * Example format `https://localhost:5000/LightDMS/Upload/8EF65043-2E2A-424D-B76F-4DAA5A48CB3D`
  * Response contains file content ID. Note that one LightDMS document may be related to many files, one for each version of the document.

Download:

* Downloading a file with given **file content ID**: HTTP GET `<baseURI>/LightDMS/DownloadPreview/{{ID}}?filename={{filename}}`
  * The *ID* parameter is GUID formatted file content ID.
  * The *filename* query parameter is a name that the browser will offer to the user when saving the downloaded file.
  * Example format `https://localhost:5000/LightDMS/DownloadPreview/8EF65043-2E2A-424D-B76F-4DAA5A48CB3D?filename=somefile.txt`
  * Parametrized format `https://localhost:5000/LightDMS/DownloadPreview?id=8EF65043-2E2A-424D-B76F-4DAA5A48CB3D&filename=somefile.txt` (since 1.8.0)

* Downloading a file with given **document version ID**: HTTP GET `<baseURI>/LightDMS/Download/{{ID}}`
  * The *ID* parameter is GUID formatted document version ID.
  * Example format `https://localhost:5000/LightDMS/Download/8EF65043-2E2A-424D-B76F-4DAA5A48CB3D`
  * Parametrized format `https://localhost:5000/LightDMS/Download?id=8EF65043-2E2A-424D-B76F-4DAA5A48CB3D` (since 1.8.0)

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

## Installation and configuration

Installing this package to a Rhetos application:

1. Add 'Rhetos.LightDMS' NuGet package, available at the [NuGet.org](https://www.nuget.org/) on-line gallery.
2. Extend Rhetos services configuration (at `services.AddRhetosHost`) with the LightDMS service: `.AddLightDMS<STORAGE PROVIDER IMPLEMENTATION>()`.
   Select the storage provider implementation with the generic type parameter:
   * `.AddLightDMS<Rhetos.LightDMS.Storage.DatabaseStorage>()` for simple BLOB storage in the **database** table or FILESTREAM storage (if enabled in database).
   * `.AddLightDMS<Rhetos.LightDMS.Storage.AzureStorageClient>()` for **Azure Blob Storage**
   * `.AddLightDMS<Rhetos.LightDMS.Storage.S3StorageClient>()` for **Amazon S3 storage**
   * or implement a custom `IStorageProvider` and use it as AddLightDMS generic type parameter.

On download, LightDMS will automatically use a storage where each file was uploaded.

For **Azure Blob Storage**, set the configuration in section `Rhetos:LightDMS:Azure`.

```js
"Rhetos": {
  "LightDMS": {
    "Azure": {
      "StorageContainer": "...",
      "StorageConnectionVariable": "..."
    }
  }
}
```

For **Amazon S3 storage**, set the configuration in section `Rhetos:LightDMS:S3`.

```js
"Rhetos": {
  "LightDMS": {
    "S3": {
      "Key": "S3 Secret Key",
      "AccessKeyID": "S3 Access Key",
      "ServiceURL": "S3 Service url",
      "BucketName": "S3 Bucket",
      "DestinationFolder": "Optional folder of files",
      "CertificateSubject": "S3 CertificateSubject",
      "ForcePathStyle": true // if your bucket is url path not on subdomain
    }
  }
}
```

## Optimize database storage with FILESTREAM

When using **database** storage, instead of Azure Blob Storage or Amazon S3, is it advised to optimize your database by enabling FILESTREAM storage for files:

### 1. Enable FILESTREAM on your application's database

1. Enable FileStream on SQL Server instance -
   SQL Server Configuration Manager [Steps](https://msdn.microsoft.com/en-us/library/cc645923.aspx)

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

### 2. Activate FILESTREAM usage in LightDMS

**Option A:**

If you have enabled FILESTREAM on your database, it will **automatically** be used by LightDMS
on the **next deployment** of the Rhetos app.

Instead of the full deployment, you can execute `DeployPackages.exe` or `rhetos.exe dbupdate`.

**Option B:**

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

### 3. Database cleanup after dbupdate

If your application have **used simple database storage before enabling FILESTREAM**, and it already contains
some files in the database, your should execute this cleanup script to reclaim the old file storage space.

**After** the Rhetos app deployment (rhetos dbupdate), execute the following SQL script to clean up database:

```SQL
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
```

## How to contribute

Contributions are very welcome. The easiest way is to fork this repo, and then
make a pull request from your fork. The first time you make a pull request, you
may be asked to sign a Contributor Agreement.
For more info see [How to Contribute](https://github.com/Rhetos/Rhetos/wiki/How-to-Contribute) on Rhetos wiki.

### Build

**Note:** This package is already available at the [NuGet.org](https://www.nuget.org/) online gallery.
You don't need to build it from source in order to use it in your application.

To build the package from source, run `Build.bat`.
The build output is a NuGet package in the "Install" subfolder.

### Test

#### Prerequisites

* PowerShell
* Docker with Linux container mode (for Azure Blob and S3 Storage emulators)
* MS SQL Server instance with FILESTREAM enabled

#### Configure and run test

1. Enable FileStream on the test SQL Server instance -
   SQL Server Configuration Manager [Steps](https://msdn.microsoft.com/en-us/library/cc645923.aspx)
   - For unit tests, all 3 checkboxes must be enabled.
   - Do not create the test database manually (delete it if not configured properly).
     It will be created by the test scripts.

2. Create and configure test settings file `.\test-config.json`, with the following content.
   Enter two database names that do not already exist on the test SQL Server.
   The test script will create and configure this two databases from the configuration file,
   if not created already.
   * Note: **remove comments** from the created 'test-config.json' file.

    ```js
    {
      // SQL server credential that has DDL grants on master database
      // This credential will be used to create and configure necessary databases
      "SqlServerCredential": "Integrated Security=true",
      "SqlServerName": "localhost",
      // Name of the database WITH FILESTREAM enabled
      // You should only modify it if you find the name is duplicate with your existing database
      "FileStreamDatabaseName": "rhetos_lightdms_test_fs",
      // Absolute path to the folder where the file stream will store
      "FileStreamFileLocation": "C:\\LightDMS_Test_Files\\",
      // Name of the database WITHOUT FILESTREAM
      // You should only modify it if you find the name is duplicate with your existing database
      "VarBinaryDatabaseName": "rhetos_lightdms_test_varbin"
    }
    ```

3. Once you have everything configured properly, you can run the test:

    ```batch
    Clean.bat
    Build.bat
    powershell .\Test.ps1
    ```

#### How testing works

1. `Test.ps1` interacts with SQL databases and storage emulators to prepare necessary file contents
2. `Test.ps1` interacts with `TestApp` via `WebApplicationFactory` to perform assertions. Learn more about integration test with ASP.NET Core at: <https://docs.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-5.0>
3. `TestApp` interacts with databases and storage emulators to perform its core functionalities.

```bash
                             ┌┬─────────────┬┐
                             ││  SQL server ││
                             ├┴─────────────┴┤
                             │               │
                       ┌─────┤►FILESTREAM DB │
   ┌─────────┐         │     │               │
   │ TestApp ├───┐     │     ├───────────────┤
   └───▲─────┘   │     ├─────┤►VarBinary DB  │
       │         │     │     │               │
       │         └─────┤     └───────────────┘
       │               │
       │               │     ┌┬─────────────┬┐
       │         ┌─────┤     ││   Docker    ││
       │         │     │     ├┴─────────────┴┤
   ┌───┴────┐    │     │     │               │
   │  Test  ├────┘     ├─────┼───►Azurite    │
   └────────┘          │     ├───────────────┤
                       │     │               │
                       └─────┼───►S3Ninja    │
                             │               │
                             └───────────────┘
```
