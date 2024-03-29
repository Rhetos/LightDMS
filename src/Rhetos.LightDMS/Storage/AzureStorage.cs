﻿/*
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

using Azure.Storage.Blobs;
using Rhetos.LightDMS;
using System.Threading.Tasks;

namespace Rhetos.LightDms.Storage
{
    public class AzureStorageClient
    {
        private readonly LightDmsOptions _lightDmsOptions;
        private readonly IAzureBlobConnectionStringResolver _azureBlobConnectionStringResolver;

        public AzureStorageClient(LightDmsOptions lightDmsOptions, IAzureBlobConnectionStringResolver azureBlobConnectionStringResolver)
        {
            _lightDmsOptions = lightDmsOptions;
            _azureBlobConnectionStringResolver = azureBlobConnectionStringResolver;
        }

        public async Task<BlobContainerClient> GetBlobContainerClientAsync()
        {
            var storageConnectionString = _azureBlobConnectionStringResolver.Resolve();

            if (string.IsNullOrEmpty(storageConnectionString))
                throw new FrameworkException("Azure Blob Storage environment variable missing.");

            var storageContainerName = _lightDmsOptions.StorageContainer;
            if (string.IsNullOrWhiteSpace(storageContainerName))
                throw new FrameworkException("Azure blob storage container name is missing from configuration.");

            var client = new BlobContainerClient(storageConnectionString, storageContainerName);
            await client.CreateIfNotExistsAsync();

            return client;
        }
    }
}