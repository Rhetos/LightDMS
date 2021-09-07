using System;
using System.IO;
using System.Net.Http;
using System.Text;

namespace Rhetos.LightDMS.IntegrationTest.Utilities
{
    internal class UploadSuccessResponse
    {
        public Guid ID { get; set; }
    }

    internal static class TestDataUtilities
    {
        static byte[] fileContent = Encoding.UTF8.GetBytes("A test file content");

        public static HttpRequestMessage GenerateUploadRequest(int fileCount = 1)
        {
            var content = new MultipartFormDataContent();

            for (var i = 0; i < fileCount; ++i)
            {
                var memoryStream = new MemoryStream(fileContent);
                var streamContent = new StreamContent(memoryStream);
                content.Add(streamContent, $"file{i}", $"testfile{i}.txt");
            }

            var request = new HttpRequestMessage(HttpMethod.Post, "LightDMS/Upload")
            { 
                Content = content
            };

            return request;
        }
    }
}
