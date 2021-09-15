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
        private readonly LightDmsOptions _lightDMSOptions;

        public DefaultBlobConnectionStringResolver(LightDmsOptions lightDMSOptions)
        {
            _lightDMSOptions = lightDMSOptions;
        }

        public string Resolve()
        {
            var storageConnectionVariable = _lightDMSOptions.StorageConnectionVariable;
            if (string.IsNullOrWhiteSpace(storageConnectionVariable))
                throw new FrameworkException("Azure Blob Storage connection variable name missing.");

            return Environment.GetEnvironmentVariable(storageConnectionVariable, EnvironmentVariableTarget.Machine);
        }
    }
}
