/*
    Copyright (C) 2016 Omega software d.o.o.

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
using Rhetos.Host.AspNet;
using Rhetos.Logging;
using Rhetos.Utilities;
using System.Threading.Tasks;

namespace Rhetos.LightDMS
{
    public class LightDMSService
    {
        private readonly ILogProvider _logProvider;
        private readonly ConnectionString _connectionString;
        private readonly IContentTypeProvider _contentTypeProvider;
        private readonly LightDMSOptions _lightDMSOptions;

        public LightDMSService(
            IRhetosComponent<ILogProvider> logProvider,
            IRhetosComponent<ConnectionString> connectionString,
            IRhetosComponent<LightDMSOptions> lightDMSOptions,
            IContentTypeProvider contentTypeProvider)
        {
            _logProvider = logProvider.Value;
            _connectionString = connectionString.Value;
            _lightDMSOptions = lightDMSOptions.Value;
            _contentTypeProvider = contentTypeProvider;
        }

        public async Task ProcessDownloadRequestAsync(HttpContext context)
        {
            await new DownloadHandler(_logProvider, _connectionString, _contentTypeProvider, _lightDMSOptions).ProcessRequest(context);
        }

        public async Task ProcessDownloadPreviewRequestAsync(HttpContext context)
        {
            await new DownloadPreviewHandler(_logProvider, _connectionString, _contentTypeProvider, _lightDMSOptions).ProcessRequest(context);
        }

        public async Task ProcessUploadRequestAsync(HttpContext context)
        {
            await new UploadHandler(_logProvider, _connectionString).ProcessRequest(context);
        }
    }
}
