using System.IO;
using System.Net.Http;

namespace Rhetos.LightDMS.IntegrationTest.Utilities
{
    internal static class TestDataUtilities
    {
        public static HttpRequestMessage GenerateUploadRequest()
        {
            using var fileStream = File.OpenRead("testfile.txt");

            var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file1", "testfile.txt");

            var request = new HttpRequestMessage(HttpMethod.Post, "LightDMS/Upload")
            { 
                Content = content 
            };

            return request;
        }
    }
}
