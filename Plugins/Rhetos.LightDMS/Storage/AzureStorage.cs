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

using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Rhetos.LightDMS;
using System;
using System.Threading.Tasks;

namespace Rhetos.LightDms.Storage
{
    public class AzureStorageClient
    {
        private readonly LightDmsOptions _lightDmsOptions;

        public AzureStorageClient(LightDmsOptions lightDmsOptions)
        {
            _lightDmsOptions = lightDmsOptions;
        }

        public async Task<CloudBlobContainer> GetCloudBlobContainer()
        {
            var storageConnectionVariable = _lightDmsOptions.StorageConnectionVariable;
            string storageConnectionString;
            if (!string.IsNullOrWhiteSpace(storageConnectionVariable))
                storageConnectionString = Environment.GetEnvironmentVariable(storageConnectionVariable, EnvironmentVariableTarget.Machine);
            else
                //variable name has to be defined if AzureStorage bit is set to true
                throw new FrameworkException("Azure Blob Storage connection variable name missing.");

            if (string.IsNullOrEmpty(storageConnectionString))
                throw new FrameworkException("Azure Blob Storage environment variable missing.");

            if (!CloudStorageAccount.TryParse(storageConnectionString, out CloudStorageAccount storageAccount))
                throw new FrameworkException("Invalid Azure Blob Storage connection string.");

            CloudBlobClient cloudBlobClient = storageAccount.CreateCloudBlobClient();
            var storageContainerName = _lightDmsOptions.StorageContainer;
            if (string.IsNullOrWhiteSpace(storageContainerName))
                throw new FrameworkException("Azure blob storage container name is missing from configuration.");

            CloudBlobContainer cloudBlobContainer = cloudBlobClient.GetContainerReference(storageContainerName);
            if (!await cloudBlobContainer.ExistsAsync())
                throw new FrameworkException("Azure blob storage container doesn't exist.");

            return cloudBlobContainer;
        }
    }
}