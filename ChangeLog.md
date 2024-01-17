# Rhetos.LightDMS release notes

## 5.3.0 (2024-01-17)

* Bugfix: Download fails if the filename contains a comma character.

## 5.2.0 (2023-03-16)

* Updated Azure.Storage.Blobs dependency.
  Note that Azure.RequestFailedException might occur on testing if the old version of Azurite container is used.

## 5.1.0 (2022-04-08)

* Bugfix: Missing NuGet dependency on Azure.Storage.Blobs.

## 5.0.0 (2022-03-25)

### Breaking changes

* Migrated from .NET Framework to .NET 5 and Rhetos 5.
* Removed MimeTypeHelper class. Use the Microsoft.AspNetCore.StaticFiles.FileExtensionContentTypeProvider class instead.
* To configure the StorageContainer and StorageConnectionVariable use the `Rhetos:LightDMS:StorageContainer` and `Rhetos:LightDMS:StorageConnectionVariable` keys.
* Azure S3 configuration settings are read from configuration section `Rhetos:LightDMS:S3` instead of `LightDMS:S3`.
  ```json
  "Rhetos": {
    "LightDMS": {
      "S3": {
        "Key": "S3 Secret Key",
        "AccessKeyID": "S3 Access Key",
        "ServiceURL": "S3 Service url",
        "BucketName": "S3 Bucket",
        "DestinationFolder": "Optional folder of files",
        "ForcePathStyle": set = true if your bucket is url path not on subdomain
      }
    }
  }
  ```

### New features

* Implemented option to directly upload to Azure or S3 storage;
  In app settings set `Rhetos:LightDMS:UploadTarget` value to `Database`, `Azure` or `S3`.

## 1.10.0 (2024-01-17)

(The following changes as not available in releases v5.0 - v5.2. They are included in 5.3.0 and later.)

* Configurable Azure S3 storage option "LightDms.S3.CertificateSubject", instead of the hardcoded value.
  The default value is backward compatible.

## 1.9.0 (2021-07-05)

### New features

* Support for external document storage with Amazon S3 API.

### Internal improvements

* Removed unnecessary System.Web.Mvc.dll reference.
* Internal refactoring to uniformly handle download and upload stream.

## 1.8.0 (2021-03-05)

### Internal improvements

* Download and DownloadPreview supports `id` as a query parameter instead of the route path, to simplify network analysis.

## 1.7.0 (2019-04-03)

### Internal improvements

* Bugfix: Old data migration script fails when upgrading from version 1.5.0 to 1.6.0 (issue #2).

## 1.6.0 (2019-01-22)

### Internal improvements

* Bugfix: ArgumentNullException when downloading from FILESTREAM.
  Introduced in v1.4.0.
  Fixes Rhetos/LightDMS#1
* Compatibility with Rhetos v2.10 polymorphic property implementation (The property X is not implemented in the polymorphic subtype).
* Improved error handling to detect the response blocking issues.
* Standardized error responses and logging, to match Rhetos framework.

## 1.5.1 (2019-01-11)

### Internal improvements

* Bugfix: DataMigration scripts are not applied when deploying LightDMS package v1.5.0 (issue Rhetos#80).

## 1.5.0 (2018-09-21)

### Internal improvements

* Extensibility: *DocumentVersionExt* changed to SqlQueryable with extension points to allow customizations and usage in database.

## 1.4.0 (2018-09-04)

### Internal improvements

* Improved error handling.
* Improved documentation and homepage snippet.
* *DocumentVersionExt* changed from SqlQueryable to Browse, for better extensibility.

## 1.3.0 (2018-06-29)

### New features

* Support for Azure Blob Storage (file download and migration).

### Breaking changes

* Removed property DocumentVersionExt.FileExtension.

## 1.2.13 (2018-03-01)

### New features

* Support for database storage without FILESTREAM.
* Added file upload time (FileContent.CreatedDate).

### Internal improvements

* Bugfix: Filenames contain space character were not interpreted correctly when downloading in browser.
* Bugfix: UTF-8 filenames were not interpreted correctly when downloading in browser.
* Bugfix: Incorrect file extension information for Unicode file names.
* Miscellaneous deployment and run-time performance improvements.

## 1.2.4 (2017-01-30)

### Initial features

* Web API for file upload/download from database FILESTREAM storage.
* Download preview, for downloading uploaded file content before submitting the final metadata record.
