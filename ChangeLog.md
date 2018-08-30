# LightDMS release notes

## NEXT (to be released)

### Internal improvements

* Improved error handling.
* DocumentVersionExt changed from SqlQueryable to Browse, for better extensibility.

## 1.3.0 (2018-06-29)

### New features

* Support for Azure Blob Storage (file download and migration).

### Breaking changes

* Removed property DocumentVersionExt.FileExtension.

## 1.2.13 (2017-01-30)

### New features

* Support for database storage without filestream.
* Added file upload time (FileContent.CreatedDate).

### Internal improvements

* Bugfix: Filenames contain space character were not interpreted correctly when downloading in browser.
* Bugfix: UTF-8 filenames were not interpreted correctly when downloading in browser.
* Bugfix: Incorrect file extension information for Unicode file names.
* Miscellaneous deployment and run-time performance improvements.

## 1.2.4 (2017-01-30)

### Initial features

* Web API for file upload/download from database filestream storage.
* Download preview, for downloading uploaded file content before submitting the final metadata record.
