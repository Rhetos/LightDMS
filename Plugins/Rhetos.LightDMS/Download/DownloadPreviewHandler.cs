/*
    Copyright (C) 2014 Omega software d.o.o.

    This file is part of Rhetos.

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU Affero General Public License as
    published by the Free Software Foundation, either version 3 of the
    License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU Affero General Public License for more details.

    You should have received a copy of the GNU Affero General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

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
            new DownloadHelper().HandleDownload(context, null, id);
            _performanceLogger.Write(sw, "Rhetos.LightDMS: Downloaded file (LightDMS.FileContent.ID = " + id + ") Executed.");
        }
    }
}
