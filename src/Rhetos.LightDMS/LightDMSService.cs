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
using Rhetos.Logging;
using Rhetos.Utilities;
using System.Threading.Tasks;
using Rhetos.Host.AspNet;

namespace Rhetos.LightDMS
{
    public class LightDMSService
    {
        private readonly ILogProvider _logProvider;
        private readonly ConnectionString _connectionString;

        public LightDMSService(IRhetosComponent<ILogProvider> logProvider, IRhetosComponent<ConnectionString> connectionString)
        {
            _logProvider = logProvider.Value;
            _connectionString = connectionString.Value;
        }

        public async Task ProcessDownloadRequestAsync(HttpContext context)
        {
            await new DownloadHandler(_logProvider, _connectionString).ProcessRequest(context);
        }

        public async Task ProcessDownloadPreviewRequestAsync(HttpContext context)
        {
            await new DownloadPreviewHandler(_logProvider, _connectionString).ProcessRequest(context);
        }

        public async Task ProcessUploadRequestAsync(HttpContext context)
        {
            await new UploadHandler(_logProvider, _connectionString).ProcessRequest(context);
        }
    }
}
