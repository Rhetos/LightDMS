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
using System.Diagnostics;
using System.Web;

namespace Rhetos.LightDMS
{
    public class DownloadHandler : IHttpHandler
    {
        private readonly ILogger _performanceLogger;

        public DownloadHandler()
        {
            var logProvider = new NLogProvider();
            _performanceLogger = logProvider.GetLogger("Performance.LightDMS");
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
            var id = DownloadHelper.GetId(context);
            if (id == null)
            {
                Respond.BadRequest(context, "The 'id' parameter is missing or incorrectly formatted.");
                return;
            }

            var sw = Stopwatch.StartNew();
            new DownloadHelper().HandleDownload(context, id, null);
            _performanceLogger.Write(sw, "Downloaded file (DocumentVersionID = " + id + ") Executed.");
        }
    }
}
