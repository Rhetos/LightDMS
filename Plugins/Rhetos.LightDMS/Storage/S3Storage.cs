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

            var client = new AmazonS3Client(accessKeyID, key, s3Config);
            return client;
        }

        public static void DownloadBlob(AmazonS3Client client, string path, string blobName, string bucketName)
        {
            using (TransferUtility fileTransferUtility = new TransferUtility(client))
            {
                fileTransferUtility.Download(path + @"\" + blobName, bucketName, blobName);
            }
        }
    }
}