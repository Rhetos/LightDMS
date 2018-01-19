using Rhetos.Logging;
using System;
using System.Diagnostics;
using System.Linq;
using System.Web;

namespace Rhetos.LightDMS
{
    public class DownloadHandler : IHttpHandler
    {
        private ILogger _performanceLogger;
        private ILogger _logger;

        public DownloadHandler()
        {
            var logProvider = new NLogProvider();
            _performanceLogger = logProvider.GetLogger("Performance");
            _logger = logProvider.GetLogger(GetType().Name);
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
            var sw = Stopwatch.StartNew();
            new DownloadHelper().HandleDownload(context, id, null);
            _performanceLogger.Write(sw, "Rhetos.LightDMS: Downloaded file (DocumentVersionID = " + id + ") Executed.");
        }
    }
}
