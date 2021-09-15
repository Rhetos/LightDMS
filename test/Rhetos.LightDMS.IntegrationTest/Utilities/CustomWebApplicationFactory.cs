using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class CustomWebApplicationFactory<TStartup>
        : WebApplicationFactory<TStartup> where TStartup : class
    {
        private readonly Action<IServiceCollection> _serviceBuilder;
        private readonly Action<ContainerBuilder> _rhetosContainerBuilder;

        public CustomWebApplicationFactory() 
        {
            _serviceBuilder = (services) => { };
            _rhetosContainerBuilder = (builder) => { };
        }

        public CustomWebApplicationFactory(Action<IServiceCollection> serviceBuilder)
            : this()
        {
            _serviceBuilder = serviceBuilder;
        }

        public CustomWebApplicationFactory(Action<ContainerBuilder> rhetosContainerBuilder)
            : this()
        {
            _rhetosContainerBuilder = rhetosContainerBuilder;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
            {
                services.AddRhetosHost(ConfigureRhetos);
                _serviceBuilder.Invoke(services);
            });
        }

        private void ConfigureRhetos(IServiceProvider serviceProvider, IRhetosHostBuilder rhetosHostBuilder)
        {
            var rootPath = Path.Combine("..", "..", "..", "..", "Rhetos.LightDMS.TestApp", "bin", "Debug", "net5.0");
            rhetosHostBuilder.UseRootFolder(rootPath);
            rhetosHostBuilder.ConfigureContainer(_rhetosContainerBuilder);
        }
    }
}