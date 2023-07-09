using System;
using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
#if NETSTANDARD2_0
using IWebHostEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
#endif

namespace BundlerMinifier.TagHelpers
{
    [PublicAPI]
    public static class BundleExtensions
    {
        [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
        public static IServiceCollection AddBundles(this IServiceCollection services,
            Action<BundleOptions> configure = null)
        {
            services.AddSingleton<IBundleProvider, BundleProvider>();
            services.AddTransient(serviceProvider =>
            {
                var env = serviceProvider.GetService<IWebHostEnvironment>();

                var options = new BundleOptions();
                options.Configure(env);
                configure?.Invoke(options);

                return options;
            });

            return services;
        }
    }
}
