using System;
using System.IO;
using System.Threading.Tasks;

namespace Rhetos.LightDMS.Storage
{
    public class DatabaseStorage : IStorageProvider
    {
        public Task UploadStream(Stream inputStream, Guid id)
        {
            throw new NotImplementedException();
        }
    }
}
