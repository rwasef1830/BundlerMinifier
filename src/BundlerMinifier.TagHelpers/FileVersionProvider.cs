using System;
using Microsoft.AspNetCore.Antiforgery.Internal;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;

namespace BundlerMinifier.TagHelpers
{
    // On ASP.NET Core 2.1, FileVersionProvider can be resolved from DI
    // However this class disappeared in .NET Core 2.2 because they introduced IFileVersionProvider
    // The problem is that FileVersionProvider became internal. So using Meziantou.AspNetCore.BundleTagHelpers
    // in .NET Core 2.2 will crash at runtime if we keep using FileVersionProvider from 2.1.
    // We can't change the code to use IFileVersionProvider instead as it will not be backward compatible with 2.1.
    class FileVersionProvider
    {
        const string c_VersionKey = "v";
        static readonly char[] s_QueryStringAndFragmentTokens = { '?', '#' };
        readonly IFileProvider _fileProvider;
        readonly IMemoryCache _cache;
        readonly PathString _requestPathBase;

        /// <summary>
        /// Creates a new instance of <see cref="BundlerMinifier.TagHelpers.FileVersionProvider" />.
        /// </summary>
        /// <param name="fileProvider">The file provider to get and watch files.</param>
        /// <param name="cache"><see cref="IMemoryCache"/> where versioned urls of files are cached.</param>
        /// <param name="requestPathBase">The base path for the current HTTP request.</param>
        public FileVersionProvider(
            IFileProvider fileProvider,
            IMemoryCache cache,
            PathString requestPathBase)
        {
            this._fileProvider = fileProvider ?? throw new ArgumentNullException(nameof(fileProvider));
            this._cache = cache ?? throw new ArgumentNullException(nameof(cache));
            this._requestPathBase = requestPathBase;
        }

        /// <summary>
        /// Adds version query parameter to the specified file path.
        /// </summary>
        /// <param name="path">The path of the file to which version should be added.</param>
        /// <returns>Path containing the version query string.</returns>
        /// <remarks>
        /// The version query string is appended with the key "v".
        /// </remarks>
        public string AddFileVersionToPath(string path)
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            var resolvedPath = path;

            var queryStringOrFragmentStartIndex = path.IndexOfAny(s_QueryStringAndFragmentTokens);
            if (queryStringOrFragmentStartIndex != -1)
            {
                resolvedPath = path[..queryStringOrFragmentStartIndex];
            }

            if (Uri.TryCreate(resolvedPath, UriKind.Absolute, out var uri) && !uri.IsFile)
            {
                // Don't append version if the path is absolute.
                return path;
            }

            if (this._cache.TryGetValue(path, out string value))
            {
                return value;
            }

            var cacheEntryOptions = new MemoryCacheEntryOptions();
            cacheEntryOptions.AddExpirationToken(this._fileProvider.Watch(resolvedPath));
            var fileInfo = this._fileProvider.GetFileInfo(resolvedPath);

            if (!fileInfo.Exists && this._requestPathBase.HasValue &&
                resolvedPath.StartsWith(this._requestPathBase.Value, StringComparison.OrdinalIgnoreCase))
            {
                var requestPathBaseRelativePath = resolvedPath[this._requestPathBase.Value.Length..];
                cacheEntryOptions.AddExpirationToken(this._fileProvider.Watch(requestPathBaseRelativePath));
                fileInfo = this._fileProvider.GetFileInfo(requestPathBaseRelativePath);
            }

            value = fileInfo.Exists ? QueryHelpers.AddQueryString(path, c_VersionKey, GetHashForFile(fileInfo)) :
                // if the file is not in the current server.
                path;

            value = this._cache.Set(path, value, cacheEntryOptions);

            return value;
        }

        static string GetHashForFile(IFileInfo fileInfo)
        {
            using var sha256 = CryptographyAlgorithms.CreateSHA256();
            using var readStream = fileInfo.CreateReadStream();
            var hash = sha256.ComputeHash(readStream);
            return WebEncoders.Base64UrlEncode(hash);
        }
    }
}
