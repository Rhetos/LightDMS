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
