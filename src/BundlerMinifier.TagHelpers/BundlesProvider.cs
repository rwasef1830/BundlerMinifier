using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Microsoft.AspNetCore.Hosting;
#if NETSTANDARD2_0
using IWebHostEnvironment = Microsoft.AspNetCore.Hosting.IHostingEnvironment;
#endif

namespace BundlerMinifier.TagHelpers
{
    public class BundleProvider : IBundleProvider, IDisposable
    {
        readonly object _lock = new object();
        readonly string _configurationPath;
        IList<Bundle> _bundles;
        FileSystemWatcher _fileWatcher;


        public BundleProvider() : this(null)
        {
        }

        public BundleProvider(IWebHostEnvironment hostingEnvironment)
            : this(@"bundleconfig.json", hostingEnvironment)
        {
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameterInConstructor")]
        public BundleProvider(string configurationPath, IWebHostEnvironment hostingEnvironment)
        {
            if (configurationPath == null)
            {
                throw new ArgumentNullException(nameof(configurationPath));
            }

            string filePath;
            if (hostingEnvironment != null && string.IsNullOrWhiteSpace(Path.GetDirectoryName(configurationPath)))
            {
                filePath = Path.Combine(hostingEnvironment.ContentRootPath, configurationPath);
            }
            else
            {
                filePath = configurationPath;
            }

            var fullPath = Path.GetFullPath(filePath);
            var directory = Path.GetDirectoryName(fullPath);
            var fileName = Path.GetFileName(fullPath);
            this._configurationPath = fullPath;

            if (directory == null)
            {
                return;
            }

            var watcher = new FileSystemWatcher(directory);
            watcher.EnableRaisingEvents = true;
            watcher.IncludeSubdirectories = false;
            watcher.Filter = fileName;
            watcher.Changed += (sender, args) => this.Reset();
            watcher.Created += (sender, args) => this.Reset();
            watcher.Deleted += (sender, args) => this.Reset();
            this._fileWatcher = watcher;
        }

        void Reset()
        {
            this._bundles = null;
        }

        void LoadBundles()
        {
            if (this._bundles != null)
            {
                return;
            }

            lock (this._lock)
            {
                if (this._bundles != null)
                {
                    return;
                }

                if (!BundleHandler.TryGetBundles(this._configurationPath, out var bundles))
                {
                    throw new Exception($"Unable to load bundles from {this._configurationPath}.");
                }

                var result = new List<Bundle>();
                foreach (var bundle in bundles)
                {
                    var b = new Bundle
                    {
                        Name = bundle.OutputFileName,
                        OutputFileUrl = bundle.GetAbsoluteOutputFile(),
                        InputFileUrls = bundle.GetAbsoluteInputFiles().ToList()
                    };
                    result.Add(b);
                }

                this._bundles = result;
            }
        }

        [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
        public Bundle GetBundle(string name)
        {
            this.LoadBundles();

            var bundle = this._bundles.FirstOrDefault(
                b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
            return bundle;
        }

        public void Dispose()
        {
            if (this._fileWatcher == null)
            {
                return;
            }

            this._fileWatcher.Dispose();
            this._fileWatcher = null;
        }
    }
}
