using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Encodings.Web;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Caching.Memory;
#if NETSTANDARD2_0
using IWebHostEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
#endif

namespace BundlerMinifier.TagHelpers
{
    [PublicAPI]
    [HtmlTargetElement("bundle")]
    public class BundleTagHelper : TagHelper
    {
        readonly IBundleProvider _bundleProvider;
        readonly BundleOptions _options;
        readonly IWebHostEnvironment _hostingEnvironment;
        readonly IMemoryCache _cache;
        readonly HtmlEncoder _htmlEncoder;
        readonly IUrlHelperFactory _urlHelperFactory;
        FileVersionProvider _fileVersionProvider;

        public BundleTagHelper(IWebHostEnvironment hostingEnvironment, IMemoryCache cache, HtmlEncoder htmlEncoder,
            IUrlHelperFactory urlHelperFactory, BundleOptions options = null, IBundleProvider bundleProvider = null)
        {
            if (hostingEnvironment == null)
            {
                throw new ArgumentNullException(nameof(hostingEnvironment));
            }

            if (cache == null)
            {
                throw new ArgumentNullException(nameof(cache));
            }

            if (htmlEncoder == null)
            {
                throw new ArgumentNullException(nameof(htmlEncoder));
            }

            if (urlHelperFactory == null)
            {
                throw new ArgumentNullException(nameof(urlHelperFactory));
            }

            if (options == null)
            {
                options = new BundleOptions();
                options.Configure(hostingEnvironment);
            }

            this._bundleProvider = bundleProvider ?? new BundleProvider(hostingEnvironment);
            this._options = options;
            this._hostingEnvironment = hostingEnvironment;
            this._cache = cache;
            this._htmlEncoder = htmlEncoder;
            this._urlHelperFactory = urlHelperFactory;
        }

        [HtmlAttributeNotBound]
        [ViewContext]
        public ViewContext ViewContext { get; set; }

        [HtmlAttributeName("name")]
        public string BundleName { get; set; }

        public override void Process(TagHelperContext context, TagHelperOutput output)
        {
            output.SuppressOutput();
            var bundle = this._bundleProvider.GetBundle(this.BundleName);
            if (bundle == null)
            {
                return;
            }

            var files = this.GetFiles(bundle);
            foreach (var file in files)
            {
                var src = this.GetSrc(file);
                if (src == null)
                {
                    continue;
                }

                if (this._options.AppendVersion)
                {
                    src = this.GetVersionedSrc(src);
                }

                if (bundle.OutputFileUrl.EndsWith(".js", StringComparison.OrdinalIgnoreCase))
                {
                    output.Content.AppendHtmlLine(
                        $"<script src=\"{this._htmlEncoder.Encode(src)}\" type=\"text/javascript\"></script>");
                }
                else if (bundle.OutputFileUrl.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
                {
                    output.Content.AppendHtmlLine(
                        $"<link href=\"{this._htmlEncoder.Encode(src)}\" rel=\"stylesheet\" />");
                }
            }
        }

        IEnumerable<string> GetFiles(Bundle bundle)
        {
            if (!this._options.UseBundles)
            {
                return bundle.InputFileUrls;
            }

            var bundlePath = bundle.OutputFileUrl;
            if (!this._options.UseMinifiedFiles)
            {
                return new[] { bundlePath };
            }

            var extension = Path.GetExtension(bundlePath);
            if (extension == null)
            {
                return new[] { bundlePath };
            }

            var minifiedPath = Path.ChangeExtension(bundlePath, ".min" + extension);
            if (File.Exists(minifiedPath))
            {
                bundlePath = minifiedPath;
            }

            return new[] { bundlePath };

        }

        string GetVersionedSrc(string srcValue)
        {
            this.EnsureFileVersionProvider();

            if (this._options.AppendVersion)
            {
                srcValue = this._fileVersionProvider.AddFileVersionToPath(srcValue);
            }

            return srcValue;
        }

        void EnsureFileVersionProvider()
        {
            this._fileVersionProvider ??= new FileVersionProvider(
                this._hostingEnvironment.WebRootFileProvider, 
                this._cache,
                this.ViewContext.HttpContext.Request.PathBase);
        }

        string GetSrc(string path)
        {
            var root = this._hostingEnvironment.WebRootPath
                .DemandTrailingPathSeparatorChar()
                .NormalizePath();
            
            var filePath = path.NormalizePath();
            if (!filePath.StartsWith(root))
            {
                return null;
            }

            var urlHelper = this._urlHelperFactory.GetUrlHelper(this.ViewContext);
            return urlHelper.Content("~/" + filePath[root.Length..].Replace('\\', '/'));
        }
    }
}
