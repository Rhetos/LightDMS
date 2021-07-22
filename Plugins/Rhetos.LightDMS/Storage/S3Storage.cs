using Amazon.S3;
using Amazon.S3.Transfer;
using Rhetos.LightDMS;

namespace Rhetos.LightDms.Storage
{
    public class S3StorageClient
    {
        private readonly S3Options _s3Options;

        public S3StorageClient(S3Options s3Options)
        {
            _s3Options = s3Options;
        }

        public AmazonS3Client GetAmazonS3Client()
        {
            var key = _s3Options.Key;
            var accessKeyID = _s3Options.AccessKeyID;
            var s3Config = new AmazonS3Config()
            {
                ServiceURL = _s3Options.ServiceURL
            };
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(accessKeyID) || string.IsNullOrWhiteSpace(s3Config.ServiceURL))
                throw new FrameworkException("Invalid S3 storage configuration parameters.");

            var client = new AmazonS3Client(accessKeyID, key, s3Config);
            return client;
        }
    }
}