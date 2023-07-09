using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;

namespace BundlerMinifier
{
    class ChangeHandler : IEquatable<ChangeHandler>
    {
        static readonly string[] s_IgnorePatterns = { "node_modules".AsPathSegment(), "bower_components".AsPathSegment(), "jspm_packages".AsPathSegment() };
        readonly string _configFile;
        readonly BundleFileProcessor _processor;

        public ChangeHandler(BundleFileProcessor processor, string configFile, Bundle bundle)
        {
            this._processor = processor;
            this._configFile = configFile;
            this.Bundle = bundle;
        }

        public Bundle Bundle { get; }

        public bool Equals(ChangeHandler other)
        {
            return other != null
                && string.Equals(this.Bundle.OutputFileName, other.Bundle.OutputFileName, StringComparison.Ordinal)
                && this.Bundle.InputFiles.Count == other.Bundle.InputFiles.Count
                && SetCompare(this.Bundle.InputFiles, other.Bundle.InputFiles, StringComparer.Ordinal)
                && string.Equals(this.Bundle.SourceMapRootPath, other.Bundle.SourceMapRootPath, StringComparison.Ordinal)
                && this.Bundle.SourceMap == other.Bundle.SourceMap
                && SetCompare(this.Bundle.Minify, other.Bundle.Minify, (l, r) => string.Equals(l.Key, r.Key, StringComparison.Ordinal) && Equals(l.Value, r.Value));
        }

        class DelegateToComparer<T> : IEqualityComparer<T>
        {
            readonly Func<T, T, bool> _equals;
            readonly Func<T, int> _getHashCode;

            public DelegateToComparer(Func<T, T, bool> equals, Func<T, int> getHashCode)
            {
                this._equals = equals;
                this._getHashCode = getHashCode ?? (x => 0);
            }

            public bool Equals(T x, T y)
            {
                return this._equals(x, y);
            }

            public int GetHashCode(T obj)
            {
                return this._getHashCode(obj);
            }
        }

        static bool SetCompare<T>(IEnumerable<T> left, IEnumerable<T> right, Func<T, T, bool> comparer, Func<T, int> getHashCode = null)
        {
            return SetCompare(left, right, new DelegateToComparer<T>(comparer, getHashCode));
        }

        static bool SetCompare<T>(IEnumerable<T> left, IEnumerable<T> right, IEqualityComparer<T> comparer)
        {
            if(left == null && right == null)
            {
                return true;
            }

            if(left != null != (right != null))
            {
                return false;
            }

            HashSet<T> ls = new HashSet<T>(left, comparer);
            HashSet<T> rs = new HashSet<T>(right, comparer);

            ls.SymmetricExceptWith(rs);
            return ls.Count == 0;
        }

        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(this.Bundle.OutputFileName);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as ChangeHandler);
        }

        public bool FilesChanged(FileSystemEventArgs e)
        {
            if (!IsFileValid(e.FullPath))
            {
                return false;
            }

            if (!BundleFileProcessor.IsFileConfigured(this._configFile, e.FullPath).Any())
            {
                return false;
            }

            var inputs = this.Bundle.GetAbsoluteInputFiles();
            var inputLastModified = inputs.Count > 0 ? inputs.Max(File.GetLastWriteTimeUtc) : DateTime.MaxValue;

            if ((this.Bundle.GetAbsoluteInputFiles().Count > 1 || this.Bundle.InputFiles.FirstOrDefault() != this.Bundle.OutputFileName)
                && inputLastModified > File.GetLastWriteTimeUtc(this.Bundle.GetAbsoluteOutputFile()))
            {
                return this._processor.Process(this._configFile, new[] { this.Bundle });
            }

            return false;
        }

        [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
        static bool IsFileValid(string file)
        {
            string fileName = Path.GetFileName(file);

            // VS adds ~ to temp file names so let's ignore those
            if (fileName.Contains('~') || fileName.Contains(".min."))
            {
                return false;
            }

            if (s_IgnorePatterns.Any(p => file.IndexOf(p, StringComparison.Ordinal) > -1))
            {
                //var fsw = (FileSystemWatcher)sender;
                //fsw.EnableRaisingEvents = false;
                return false;
            }

            if (!BundleFileProcessor.IsSupported(file))
            {
                return false;
            }

            return true;
        }
    }
}
