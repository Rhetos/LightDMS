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

namespace Rhetos.LightDMS
{
    public interface IAzureBlobConnectionStringResolver
    {
        string Resolve();
    }

    /// <summary>
    /// For backward compatibility, it takes connection string from machine's environment variable.
    /// A better approach might be using options pattern,
    /// which is more native to dotnet therefore easier to extend and override.
    /// See more: https://docs.microsoft.com/en-us/dotnet/core/extensions/options
    /// </summary>
    public class DefaultBlobConnectionStringResolver : IAzureBlobConnectionStringResolver
    {
        private readonly AzureStorageOptions _azureStorageOptions;

        public DefaultBlobConnectionStringResolver(AzureStorageOptions azureStorageOptions)
        {
            _azureStorageOptions = azureStorageOptions;
        }

        public string Resolve()
        {
            var storageConnectionVariable = _azureStorageOptions.StorageConnectionVariable;
            if (string.IsNullOrWhiteSpace(storageConnectionVariable))
                throw new FrameworkException("Azure Blob Storage connection variable name missing.");

            return Environment.GetEnvironmentVariable(storageConnectionVariable, EnvironmentVariableTarget.Machine);
        }
    }
}
