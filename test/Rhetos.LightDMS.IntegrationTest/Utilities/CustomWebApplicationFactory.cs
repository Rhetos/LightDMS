using Autofac;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Rhetos.Logging;
using Rhetos.Utilities;
using System;
using System.IO;
using Xunit.Abstractions;

namespace Rhetos.LightDMS.IntegrationTest
{
    public class CustomWebApplicationFactory<TStartup>
        : WebApplicationFactory<TStartup> where TStartup : class
    {
        private readonly Action<IServiceCollection> _serviceBuilder = services => { };
        private readonly Action<ContainerBuilder> _rhetosContainerBuilder = builder => { };
        private readonly ILogProvider _logProvider = new ConsoleLogProvider();

        public CustomWebApplicationFactory() 
        {
        }

        public CustomWebApplicationFactory(
            ITestOutputHelper testOutput,
            Action<IServiceCollection> configureServices = null,
            Action<ContainerBuilder> configureRhetos = null)
            : this()
        {
            _serviceBuilder += configureServices;
            _rhetosContainerBuilder += configureRhetos;

            if (testOutput != null)
            {
                _logProvider = new TestLogProvider(testOutput);
                _rhetosContainerBuilder += builder => builder.RegisterInstance(_logProvider);
            }
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
            rhetosHostBuilder.UseBuilderLogProvider(_logProvider);
        }
    }
}