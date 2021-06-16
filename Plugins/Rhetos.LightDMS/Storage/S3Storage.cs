using Amazon.S3;
using Amazon.S3.Transfer;

namespace Rhetos.LightDms.Storage
{
    public static class S3StorageClient
    {
        public static AmazonS3Client GetAmazonS3Client()
        {
            var key = System.Configuration.ConfigurationManager.AppSettings.Get("StorageKey");
            var accessKeyID = System.Configuration.ConfigurationManager.AppSettings.Get("StorageAccessKeyID");
            var s3Config = new AmazonS3Config()
            {
                ServiceURL = System.Configuration.ConfigurationManager.AppSettings.Get("ServiceURLS3")
            };
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(accessKeyID) || string.IsNullOrWhiteSpace(s3Config.ServiceURL))
                throw new FrameworkException("Invalid S3 storage configuration parameters.");

            var client = new AmazonS3Client(accessKeyID, key, s3Config);
            return client;
        }
    }
}