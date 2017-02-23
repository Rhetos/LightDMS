using Rhetos.LightDms.Storage;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Data;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Web;

namespace Rhetos.LightDMS
{
    public class DownloadPreviewHandler : IHttpHandler
    {
        private ILogger _performanceLogger;

        public DownloadPreviewHandler()
        {
            var logProvider = Activator.CreateInstance<NLogProvider>();
            _performanceLogger = logProvider.GetLogger("Performance");
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }
        
        public void ProcessRequest(HttpContext context)
        {
            var id = Guid.Parse(context.Request.Url.LocalPath.Split('/').Last());

            var query = HttpUtility.ParseQueryString(context.Request.Url.Query);
            if (!query.AllKeys.Any(name => name.ToLower() == "filename")) {
                context.Response.ContentType = "application/json;";
                context.Response.Write(Newtonsoft.Json.JsonConvert.SerializeObject(new { error = "Fetching file preview requires filename as query parameter." }));
                context.Response.StatusCode = 400;
                return;
            }

            var sw = Stopwatch.StartNew();
            DownloadHelper.HandleDownload(context, null, id);
            _performanceLogger.Write(sw, "Rhetos.LightDMS: Downloaded file (LightDMS.FileContent.ID = " + id + ") Executed.");
        }
    }
}
