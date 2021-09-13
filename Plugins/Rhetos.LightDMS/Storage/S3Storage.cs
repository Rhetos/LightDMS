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

using Amazon.S3;
using Amazon.S3.Transfer;

namespace Rhetos.LightDms.Storage
{
    public static class S3StorageClient
    {
        public static AmazonS3Client GetAmazonS3Client()
        {
            var key = System.Configuration.ConfigurationManager.AppSettings.Get("LightDms.S3.Key");
            var accessKeyID = System.Configuration.ConfigurationManager.AppSettings.Get("LightDms.S3.AccessKeyID");
            var s3Config = new AmazonS3Config()
            {
                ServiceURL = System.Configuration.ConfigurationManager.AppSettings.Get("LightDms.S3.ServiceURL")
            };
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(accessKeyID) || string.IsNullOrWhiteSpace(s3Config.ServiceURL))
                throw new FrameworkException("Invalid S3 storage configuration parameters.");

            var client = new AmazonS3Client(accessKeyID, key, s3Config);
            return client;
        }
    }
}