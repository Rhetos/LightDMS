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

using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Rhetos.LightDMS;
using System.Collections.Generic;
using System.Reflection;

namespace Rhetos
{
    /// <summary>
    /// Adds the LightDMS Web API to the application.
    /// </summary>
    /// <remarks>
    /// It registers <see cref="LightDmsController"/> and <see cref="LightDmsService"/> to the <see cref="IServiceCollection"/>.
    /// </remarks>
    public static class LightDmsRhetosServiceCollectionBuilderExtensions
    {
        public static RhetosServiceCollectionBuilder AddLightDMS(this RhetosServiceCollectionBuilder builder)
        {
            builder.Services.AddScoped<LightDmsService>();

            builder.Services
                .AddControllers()
                .ConfigureApplicationPartManager(p =>
                {
                    p.FeatureProviders.Add(new LightDmsApiControllerFeatureProvider());
                });

            builder.AddDashboardSnippet<LightDmdDashboardSnippet>();

            return builder;
        }
    }

    internal class LightDmsApiControllerFeatureProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Add(typeof(LightDmsController).GetTypeInfo());
        }
    }
}