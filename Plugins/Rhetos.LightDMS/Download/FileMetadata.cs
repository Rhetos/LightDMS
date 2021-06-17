/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

using System;
using System.IO;
using System.Net;

namespace Rhetos.LightDMS
{
    public class FileMetadata
    {
        public Guid FileContentId { get; set; }
        public string FileName { get; set; }
        public bool AzureStorage { get; set; }
        public bool S3Storage { get; set; }
        public long Size { get; set; }
    }

    public class FileDownloadResult
    {
        public FileMetadata Metadata { get; set; }
    }

    public class FileUploadResult
    {
        public Guid? ID { get; set; }
        public HttpStatusCode StatusCode { get; set; }
        public string Error { get; set; }
        public Exception Exception { get; set; }
    }
}