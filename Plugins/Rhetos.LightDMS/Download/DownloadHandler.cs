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
    public class DownloadHandler : IHttpHandler
    {
        private ILogger _performanceLogger;

        public DownloadHandler()
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
            var sw = Stopwatch.StartNew();
            DownloadHelper.HandleDownload(context, @"
                        SELECT fc.Content.PathName(),
                                GET_FILESTREAM_TRANSACTION_CONTEXT(), 
                                FileSize = DATALENGTH(Content), 
                                Name = dv.FileName
                        FROM LightDMS.DocumentVersion dv
                            INNER JOIN LightDMS.FileContent fc ON dv.FileContentID = fc.ID
                            INNER JOIN LightDMS.DocumentVersionExt dvext ON dvext.ID = dv.ID
                        WHERE dv.ID = '"+ id + "'");
            _performanceLogger.Write(sw, "Rhetos.LightDMS: Downloaded file (DocumentVersionID = " + id + ") Executed.");
        }
    }
}
