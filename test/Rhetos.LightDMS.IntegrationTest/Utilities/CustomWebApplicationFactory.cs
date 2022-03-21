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