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
using System.Threading.Tasks;

namespace Rhetos.LightDMS
{
    public class LightDmsService
    {
        private readonly IRhetosComponent<DownloadHandler> _downloadHandler;
        private readonly IRhetosComponent<DownloadPreviewHandler> _downloadPreviewHandler;
        private readonly IRhetosComponent<UploadHandler> _uploadHandler;

        public LightDmsService(
            IRhetosComponent<DownloadHandler> downloadHandler,
            IRhetosComponent<DownloadPreviewHandler> downloadPreviewHandler,
            IRhetosComponent<UploadHandler> uploadHandler)
        {
            _downloadHandler = downloadHandler;
            _downloadPreviewHandler = downloadPreviewHandler;
            _uploadHandler = uploadHandler;
        }

        public async Task ProcessDownloadRequestAsync(HttpContext context)
        {
            await _downloadHandler.Value.ProcessRequest(context);
        }

        public async Task ProcessDownloadPreviewRequestAsync(HttpContext context)
        {
            await _downloadPreviewHandler.Value.ProcessRequest(context);
        }

        public async Task ProcessUploadRequestAsync(HttpContext context)
        {
            await _uploadHandler.Value.ProcessRequest(context);
        }
    }
}
