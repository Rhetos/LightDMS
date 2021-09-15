using Rhetos.LightDMS.IntegrationTest.Utilities;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class HardcodedAzureBlobConnectionStringResolver : IAzureBlobConnectionStringResolver
    {
        public string Resolve()
        {
            return TestDataUtilities.BLOB_CONN_STRING;
        }
    }
}
