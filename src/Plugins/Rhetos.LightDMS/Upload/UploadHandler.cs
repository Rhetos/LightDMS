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
using Rhetos.Logging;
using System.Net;
using System.Threading.Tasks;

namespace Rhetos.LightDMS
{
    public class UploadHandler
    {
        private readonly Respond _respond;
        private readonly UploadHelper _uploadHelper;

        public UploadHandler(ILogProvider logProvider, UploadHelper uploadHelper)
        {
            _respond = new Respond(logProvider);
            _uploadHelper = uploadHelper;
        }

        public async Task ProcessRequest(HttpContext context)
        {
            if (context.Request.Form.Files.Count != 1)
            {
                await _respond.BadRequest(context, "Exactly one file has to be sent as request in Multipart format. There were " + context.Request.Form.Files.Count + " files in upload request.");
                return;
            }

            var fileUploadResult = await _uploadHelper.UploadStream(context.Request.Form.Files[0].OpenReadStream());

            switch (fileUploadResult.StatusCode)
            {
                case HttpStatusCode.OK:
                    await _respond.Ok(context, new { fileUploadResult.ID });
                    break;
                case HttpStatusCode.BadRequest:
                    await _respond.BadRequest(context, fileUploadResult.Error);
                    break;
                default:
                    await _respond.InternalError(context, fileUploadResult.Exception);
                    break;
            }
        }
    }
}
