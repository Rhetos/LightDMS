using System.ComponentModel.Composition;

namespace Rhetos.LightDMS
{
    [Export(typeof(Rhetos.IHomePageSnippet))]
    public class RhetosHomePageDemo : Rhetos.IHomePageSnippet
    {
        public string Html
        {
            get
            {
                return
@"        <h2>LightDMS demo interface</h2>
    <div class=""row"">
        <div class=""col-md-4""></div>
        <div class=""col-md-4"">
            <h4>Upload demo</h4>
            <form action=""lightdms/upload"" method=""post"" enctype=""multipart/form-data"">
                <label for=""file-web-api"">Select file</label>
                <input type=""file"" name=""file-web-api"" id=""file-web-api"" />
                <br />
                <input type=""submit"" name=""submit"" value=""Upload"" />
            </form>
        </div><div class=""col-md-4"">
            <h4>Download demo</h4>
            <label for=""file-id"">FileID</label>
            <input type=""text"" name=""file-id"" id=""file-id"" />
            <br />
            <button onclick=""window.location = 'lightdms/Download/' + document.getElementById('file-id').value"">Download</button>
        </div>
    </div>
";
            }
        }
    }
}
