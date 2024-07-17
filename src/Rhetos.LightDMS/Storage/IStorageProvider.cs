using System;
using System.IO;
using System.Threading.Tasks;

namespace Rhetos.LightDMS.Storage
{
    public interface IStorageProvider
    {
        public Task UploadStream(Stream inputStream, Guid id);
    }
}
