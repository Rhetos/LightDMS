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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.StaticFiles;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Rhetos.LightDMS
{
    public class DownloadHandler
    {
        private readonly ILogger _performanceLogger;
        private readonly ILogProvider _logProvider;
        private readonly ConnectionString _connectionString;
        private readonly IContentTypeProvider _contentTypeProvider;
        private readonly LightDMSOptions _lightDMSOptions;

        public DownloadHandler(ILogProvider logProvider,
            ConnectionString connectionString,
            IContentTypeProvider contentTypeProvider,
            LightDMSOptions lightDMSOptions)
        {
            _performanceLogger = logProvider.GetLogger("Performance.LightDMS");
            _logProvider = logProvider;
            _connectionString = connectionString;
            _contentTypeProvider = contentTypeProvider;
            _lightDMSOptions = lightDMSOptions;
        }

        public async Task ProcessRequest(HttpContext context)
        {
            var id = DownloadHelper.GetId(context);
            if (id == null)
            {
                await Respond.BadRequest(context, "The 'id' parameter is missing or incorrectly formatted.");
                return;
            }

            var sw = Stopwatch.StartNew();
            await new DownloadHelper(_logProvider, _connectionString, _contentTypeProvider, _lightDMSOptions).HandleDownload(context, id, null);
            _performanceLogger.Write(sw, "Downloaded file (DocumentVersionID = " + id + ") Executed.");
        }
    }
}
