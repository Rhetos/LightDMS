using System;
using System.Web;
using System.Web.Routing;

namespace Rhetos.LightDMS
{
    public class LightDMSRouteHandler : IRouteHandler
    {
        private string _operation;
        public LightDMSRouteHandler(string operation)
        {
            _operation = operation;
        }

        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            if (_operation == "Upload")
                return Activator.CreateInstance(typeof(UploadHandler)) as IHttpHandler;
            if (_operation == "Download")
                return Activator.CreateInstance(typeof(DownloadHandler)) as IHttpHandler;
            throw new InvalidOperationException("Only Upload & Download operations are allowed.");
        }
    }
}