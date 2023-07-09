#if NETSTANDARD2_0
using IWebHostEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
#else
using Microsoft.Extensions.Hosting;
#endif
using Microsoft.AspNetCore.Hosting;

namespace BundlerMinifier.TagHelpers
{
    public class BundleOptions
    {
        public bool UseBundles { get; set; }
        public bool UseMinifiedFiles { get; set; }
        public bool AppendVersion { get; set; }        

        internal void Configure(IWebHostEnvironment env)
        {
            if (env == null)
            {
                return;
            }

            var isDevelopment = env.IsDevelopment();
            this.UseBundles = !isDevelopment;
            this.UseMinifiedFiles = !isDevelopment;
            this.AppendVersion = !isDevelopment;
        }
    }
}
