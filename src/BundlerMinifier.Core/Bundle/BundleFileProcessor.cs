using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace BundlerMinifier;

[PublicAPI]
public class BundleFileProcessor
{
    static readonly string[] s_Supported = [".JS", ".CSS", ".HTML", ".HTM"];

    public static bool IsSupported(params string[] files)
    {
        files = files.Where(f => !string.IsNullOrEmpty(f)).ToArray();

        if (!files.Any())
        {
            return false;
        }

        string ext = Path.GetExtension(files.First())?.ToUpperInvariant();

        foreach (string file in files)
        {
            string fileExt = Path.GetExtension(file).ToUpperInvariant();

            if (!s_Supported.Contains(fileExt) || !fileExt.Equals(ext, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public bool Process(string fileName, IEnumerable<Bundle> bundles = null, bool useParallel = false)
    {
        var info = new FileInfo(fileName);
        bundles ??= BundleHandler.GetBundles(fileName);
        bool result = false;

        Parallel.ForEach(
            bundles,
            new ParallelOptions { MaxDegreeOfParallelism = useParallel ? Environment.ProcessorCount : 1 },
            bundle =>
            {
                var localResult = this.ProcessBundle(info.Directory?.FullName, bundle);
                lock (bundles)
                {
                    result |= localResult;
                }
            });

        return result;
    }

    public static void Clean(string fileName, IEnumerable<Bundle> bundles = null)
    {
        var info = new FileInfo(fileName);
        bundles ??= BundleHandler.GetBundles(fileName);

        foreach (var bundle in bundles)
        {
            CleanBundle(info.Directory?.FullName, bundle);
        }
    }

    public void SourceFileChanged(string bundleFile, string sourceFile)
    {
        var bundles = BundleHandler.GetBundles(bundleFile);
        string bundleFileFolder = Path.GetDirectoryName(bundleFile),
            sourceFileFolder = Path.GetDirectoryName(sourceFile);

        foreach (var bundle in bundles)
        {
            foreach (string input in bundle.GetAbsoluteInputFiles())
            {
                if (input.Equals(sourceFile, StringComparison.OrdinalIgnoreCase) ||
                    input.Equals(sourceFileFolder, StringComparison.OrdinalIgnoreCase))
                {
                    this.ProcessBundle(bundleFileFolder, bundle);
                }
            }
        }
    }

    public static IEnumerable<Bundle> IsFileConfigured(string configFile, string sourceFile)
    {
        List<Bundle> list = new List<Bundle>();

        try
        {
            var configs = BundleHandler.GetBundles(configFile);

            foreach (var bundle in configs)
            {
                foreach (string input in bundle.GetAbsoluteInputFiles())
                {
                    if (input.Equals(sourceFile, StringComparison.OrdinalIgnoreCase) && !list.Contains(bundle))
                    {
                        list.Add(bundle);
                    }
                }
            }

            return list;
        }
        catch (Exception)
        {
            return list;
        }
    }

    bool ProcessBundle(string baseFolder, Bundle bundle)
    {
        this.OnProcessing(bundle, baseFolder);
        bool changed = false;

        if (bundle.GetAbsoluteInputFiles(true).Count > 1 || bundle.InputFiles.FirstOrDefault() != bundle.OutputFileName)
        {
            BundleHandler.ProcessBundle(baseFolder, bundle);

            if (!bundle.IsMinificationEnabled || !bundle.OutputIsMinFile)
            {
                string outputFile = bundle.GetAbsoluteOutputFile();
                bool containsChanges = FileHelpers.HasFileContentChanged(outputFile, bundle.Output);

                if (containsChanges)
                {
                    this.OnBeforeBundling(bundle, baseFolder, true);
                    var outputFileDirectory = Directory.GetParent(outputFile);
                    outputFileDirectory?.Create();

                    File.WriteAllText(outputFile, bundle.Output, new UTF8Encoding(false));
                    this.OnAfterBundling(bundle, baseFolder, true);
                    changed = true;
                }
            }
        }

        MinificationResult minResult = null;
        var minFile = BundleMinifier.GetMinFileName(bundle.GetAbsoluteOutputFile());
        if (bundle.IsMinificationEnabled)
        {
            var outputWriteTime = File.GetLastWriteTimeUtc(minFile);
            var minifyChanged = bundle.MostRecentWrite >= outputWriteTime;

            if (minifyChanged)
            {
                minResult = BundleMinifier.MinifyBundle(bundle);

                // If no change is detected, then the minFile is not modified, so we need to update the write time manually
                if (!minResult.Changed && File.Exists(minFile))
                {
                    File.SetLastWriteTimeUtc(minFile, DateTime.UtcNow);
                }

                changed |= minResult.Changed;

                if (bundle.SourceMap && !string.IsNullOrEmpty(minResult.SourceMap))
                {
                    string mapFile = minFile + ".map";
                    bool smChanges = FileHelpers.HasFileContentChanged(mapFile, minResult.SourceMap);

                    if (smChanges)
                    {
                        this.OnBeforeWritingSourceMap(minFile, mapFile, true);
                        File.WriteAllText(mapFile, minResult.SourceMap, new UTF8Encoding(false));
                        this.OnAfterWritingSourceMap(minFile, mapFile, true);
                        changed = true;
                    }
                }
            }
            else
            {
                this.OnMinificationSkipped(bundle, baseFolder, false);
            }
        }

        if (minResult?.HasErrors ?? false)
        {
            throw new Exception("Minification failed.");
        }

        if (!bundle.IsGzipEnabled)
        {
            return changed;
        }

        var fileToGzip = bundle.IsMinificationEnabled ? minFile : bundle.GetAbsoluteOutputFile();

        if (minResult == null)
        {
            BundleMinifier.CompressFile(fileToGzip, bundle, false, File.ReadAllText(fileToGzip));
        }
        else
        {
            BundleMinifier.CompressFile(fileToGzip, bundle, minResult.Changed, minResult.MinifiedContent);
        }

        return changed;
    }

    static void CleanBundle(string baseFolder, Bundle bundle)
    {
        string outputFile = bundle.GetAbsoluteOutputFile();
        baseFolder = baseFolder.DemandTrailingPathSeparatorChar();
        if (!bundle.GetAbsoluteInputFiles().Contains(outputFile, StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(outputFile))
            {
                FileHelpers.RemoveReadonlyFlagFromFile(outputFile);
                File.Delete(outputFile);
                Console.WriteLine($"Deleted {FileHelpers.MakeRelative(baseFolder, outputFile).Cyan().Bright()}");
            }
        }

        string minFile = BundleMinifier.GetMinFileName(bundle.GetAbsoluteOutputFile());
        string mapFile = minFile + ".map";
        var compressedFileNames = new[]
        {
            minFile + ".gz",
            minFile + ".br",
            minFile + ".zstd"
        };

        if (File.Exists(minFile))
        {
            FileHelpers.RemoveReadonlyFlagFromFile(minFile);
            File.Delete(minFile);
            Console.WriteLine($"Deleted {FileHelpers.MakeRelative(baseFolder, minFile).Cyan().Bright()}");
        }

        if (File.Exists(mapFile))
        {
            FileHelpers.RemoveReadonlyFlagFromFile(mapFile);
            File.Delete(mapFile);
            Console.WriteLine($"Deleted {mapFile.Cyan().Bright()}");
        }

        foreach (var compressFileName in compressedFileNames)
        {
            if (!File.Exists(compressFileName))
            {
                return;
            }

            FileHelpers.RemoveReadonlyFlagFromFile(compressFileName);
            File.Delete(compressFileName);
            Console.WriteLine($"Deleted {compressFileName.Cyan().Bright()}");
        }
    }

    public event EventHandler<BundleFileEventArgs> Processing;

    protected void OnProcessing(Bundle bundle, string baseFolder)
    {
        this.Processing?.Invoke(this,
            new BundleFileEventArgs(bundle.GetAbsoluteOutputFile(), bundle, baseFolder, false));
    }

    public event EventHandler<BundleFileEventArgs> BeforeBundling;

    protected void OnBeforeBundling(Bundle bundle, string baseFolder, bool containsChanges)
    {
        this.BeforeBundling?.Invoke(this,
            new BundleFileEventArgs(bundle.GetAbsoluteOutputFile(), bundle, baseFolder, containsChanges));
    }


    public event EventHandler<BundleFileEventArgs> AfterBundling;

    protected void OnAfterBundling(Bundle bundle, string baseFolder, bool containsChanges)
    {
        this.AfterBundling?.Invoke(this,
            new BundleFileEventArgs(bundle.GetAbsoluteOutputFile(), bundle, baseFolder, containsChanges));
    }

    public event EventHandler<MinifyFileEventArgs> BeforeWritingSourceMap;

    protected void OnBeforeWritingSourceMap(string file, string mapFile, bool containsChanges)
    {
        this.BeforeWritingSourceMap?.Invoke(this, new MinifyFileEventArgs(file, mapFile, containsChanges));
    }

    public event EventHandler<MinifyFileEventArgs> AfterWritingSourceMap;

    protected void OnAfterWritingSourceMap(string file, string mapFile, bool containsChanges)
    {
        this.AfterWritingSourceMap?.Invoke(this, new MinifyFileEventArgs(file, mapFile, containsChanges));
    }

    public event EventHandler<BundleFileEventArgs> MinificationSkipped;

    protected void OnMinificationSkipped(Bundle bundle, string baseFolder, bool containsChanges)
    {
        this.MinificationSkipped?.Invoke(this,
            new BundleFileEventArgs(bundle.GetAbsoluteOutputFile(), bundle, baseFolder, containsChanges));
    }
}
