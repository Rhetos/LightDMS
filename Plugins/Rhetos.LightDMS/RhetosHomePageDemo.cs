using Rhetos.Utilities;
using System.ComponentModel.Composition;
using System.IO;
using System.Text;

namespace Rhetos.LightDMS
{
    [Export(typeof(Rhetos.IHomePageSnippet))]
    public class RhetosHomePageDemo : Rhetos.IHomePageSnippet
    {
        private string _snippet;

        public string Html
        {
            get
            {
                if (_snippet == null)
                {
                    string filePath = Path.Combine(Paths.ResourcesFolder, "LightDMS", "HomePageSnippet.html");
                    _snippet = File.ReadAllText(filePath, Encoding.Default);
                }
                return _snippet;
            }
        }
    }
}
