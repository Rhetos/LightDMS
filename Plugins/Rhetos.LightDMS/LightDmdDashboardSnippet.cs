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

using Rhetos.Host.AspNet.Dashboard;

namespace Rhetos.LightDMS
{
    [Options("Rhetos:LightDMS")]
    public class LightDmdDashboardSnippet : IDashboardSnippet
    {
        public string DisplayName => "LightDMS";

        public int Order => 200;

        public string RenderHtml() =>
@"    <form action=""lightdms/upload"" method=""post"" enctype=""multipart/form-data"">
        <label for=""file-web-api"">Upload file:</label>
        <input type=""file"" name=""file-web-api"" id=""file-web-api"" />
        <input type=""submit"" name=""submit"" value=""Upload"" />
    </form>
    <div>
        <label for=""FileContentID"">Download by FileContentID:</label>
        <input type=""text"" name=""FileContentID"" id=""FileContentID"" />
        <button onclick=""window.location = 'lightdms/DownloadPreview/' + document.getElementById('FileContentID').value
            + '?filename=' + (document.getElementById('file-web-api').value.replace(/^.*[\\\/]/, '') || document.getElementById('FileContentID').value)"">
            Download preview</button>
    </div>
    <div>
        <label for=""DocumentVersionID"">Download by DocumentVersionID:</label>
        <input type=""text"" name=""DocumentVersionID"" id=""DocumentVersionID"" />
        <button onclick=""window.location = 'lightdms/Download/' + document.getElementById('DocumentVersionID').value"">Download</button>
    </div>
";

    }
}
