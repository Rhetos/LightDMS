using Autofac;
using Rhetos.Utilities;
using System;

namespace Rhetos.LightDMS.IntegrationTest.Utilities
{
    public static class TestScopeContainerBuilderExtensions
    {
        /// <summary>
        /// This method uses a shallow copy of the original options instance for configuration.
        /// It supports <paramref name="configure"/> action that directly modifies properties of the options class.
        /// </summary>
        /// <remarks>
        /// Since options classes are usually singletons, the <paramref name="configure"/> action should not modify any object that is referenced
        /// by the options class instance, because it might affect configuration of other unit tests.
        /// </remarks>
        public static ContainerBuilder ConfigureOptions<TOptions>(this ContainerBuilder builder, Action<TOptions> configure) where TOptions : class
        {
            builder.RegisterDecorator<TOptions>((context, parameters, originalOptions) =>
            {
                TOptions modifiedOptions = CsUtility.ShallowCopy(originalOptions);
                configure.Invoke(modifiedOptions);
                return modifiedOptions;
            });
            return builder;
        }
    }
}
