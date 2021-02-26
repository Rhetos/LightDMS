﻿/*
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

using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Rhetos.LightDMS
{
    public class LightDMSController : ControllerBase
    {
        private readonly LightDMSService _lightDMSService;

        public LightDMSController(LightDMSService lightDMSService)
        {
            _lightDMSService = lightDMSService;
        }

        [HttpPost]
        [Route("LightDMS/Upload")]
        public async Task Upload()
        {
            await _lightDMSService.ProcessUploadRequestAsync(HttpContext);
        }

        [HttpPost]
        [Route("LightDMS/Download/{id}")]
        public async Task Download()
        {
            await _lightDMSService.ProcessDownloadRequestAsync(HttpContext);
        }

        [HttpPost]
        [Route("LightDMS/DownloadPreview/{id}")]
        public async Task DownloadPreview()
        {
            await _lightDMSService.ProcessDownloadPreviewRequestAsync(HttpContext);
        }
    }
}
