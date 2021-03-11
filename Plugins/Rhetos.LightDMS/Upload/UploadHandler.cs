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

using System.Net;
using System.Web;

namespace Rhetos.LightDMS
{
    public class UploadHandler : IHttpHandler
    {
        public bool IsReusable => false;

        public void ProcessRequest(HttpContext context)
        {
            if (context.Request.Files.Count != 1)
            {
                Respond.BadRequest(context, "Exactly one file has to be sent as request in Multipart format. There were " + context.Request.Files.Count + " files in upload request.");
                return;
            }

            var uploadHelper = new UploadHelper();
            var fileUploadResult = uploadHelper.UploadStream(context.Request.Files[0].InputStream);

            switch (fileUploadResult.StatusCode)
            {
                case HttpStatusCode.OK:
                    Respond.Ok(context, new { fileUploadResult.ID });
                    break;
                case HttpStatusCode.BadRequest:
                    Respond.BadRequest(context, fileUploadResult.Error);
                    break;
                default:
                    Respond.InternalError(context, fileUploadResult.Exception);
                    break;
            }
        }
    }
}
