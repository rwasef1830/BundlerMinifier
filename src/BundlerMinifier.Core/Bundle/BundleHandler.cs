using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace BundlerMinifier;

[PublicAPI]
public static class BundleHandler
{
    [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
    public static void AddBundle(string configFile, Bundle newBundle)
    {
        IEnumerable<Bundle> existing = GetBundles(configFile)
            .Where(x => !x.OutputFileName.Equals(newBundle.OutputFileName));

        List<Bundle> bundles = new List<Bundle>();

        bundles.AddRange(existing);
        bundles.Add(newBundle);
        newBundle.FileName = configFile;

        var settings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            DefaultValueHandling = DefaultValueHandling.Ignore
        };

        string content = JsonConvert.SerializeObject(bundles, settings);
        File.WriteAllText(configFile, content + Environment.NewLine);
    }

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
    public static void RemoveBundle(string configFile, Bundle bundleToRemove)
    {
        IEnumerable<Bundle> bundles = GetBundles(configFile);
        List<Bundle> newBundles = new List<Bundle>();

        if (!bundles.Contains(bundleToRemove))
        {
            return;
        }

        newBundles.AddRange(bundles.Where(b => !b.Equals(bundleToRemove)));
        string content = JsonConvert.SerializeObject(newBundles, Formatting.Indented);
        File.WriteAllText(configFile, content);
    }

    public static bool TryGetBundles(string configFile, out IEnumerable<Bundle> bundles)
    {
        try
        {
            if (string.IsNullOrEmpty(configFile) || !File.Exists(configFile))
            {
                bundles = Enumerable.Empty<Bundle>();
                return false;
            }

            var bundleFileInfo = new FileInfo(configFile);
            configFile = bundleFileInfo.FullName;
            string content = File.ReadAllText(configFile);
            bundles = JArray.Parse(content).ToObject<Bundle[]>();

            foreach (var bundle in bundles)
            {
                bundle.FileName = configFile;
                bundle.MostRecentWrite = bundleFileInfo.LastWriteTimeUtc;
            }

            var bundlesByOutputPath = bundles.ToDictionary(x => x.OutputFileName, x => x);
            var bundleOutputPathReferenceSet = new HashSet<string>();

            foreach (var (_, bundle) in bundlesByOutputPath)
            {
                bundleOutputPathReferenceSet.Clear();
                bundleOutputPathReferenceSet.Add(bundle.OutputFileName);
                    
                for (int i = 0; i < bundle.InputFiles.Count; i++)
                {
                    var inputFile = bundle.InputFiles[i];
                    if (!bundlesByOutputPath.TryGetValue(inputFile, out var referencedBundle))
                    {
                        continue;
                    }

                    if (!bundleOutputPathReferenceSet.Add(inputFile))
                    {
                        // Circular reference
                        return false;
                    }
                        
                    bundle.InputFiles.RemoveAt(i);
                    bundle.InputFiles.InsertRange(i, referencedBundle.InputFiles);
                    i--;
                }
            }

            return true;
        }
        catch
        {
            bundles = null;
            return false;
        }
    }

    public static IEnumerable<Bundle> GetBundles(string configFile)
    {
        if (!TryGetBundles(configFile, out var bundles))
        {
            return Array.Empty<Bundle>();
        }
            
        return bundles;
    }

    public static void ProcessBundle(string baseFolder, Bundle bundle)
    {
        var mostRecentWrite = bundle.MostRecentWrite;
        var sb = new StringBuilder();
        List<string> inputFiles = bundle.GetAbsoluteInputFiles();

        foreach (var input in inputFiles)
        {
            string file = Path.Combine(baseFolder, input);

            if (!File.Exists(file))
            {
                continue;
            }

            string content;

            if (input.EndsWith(".css", StringComparison.OrdinalIgnoreCase) && AdjustRelativePaths(bundle))
            {
                content = CssRelativePath.Adjust(file, bundle.GetAbsoluteOutputFile());
            }
            else
            {
                content = FileHelpers.ReadAllText(file);
            }
            var lastWriteFile = File.GetLastWriteTimeUtc(file);
            if (mostRecentWrite < lastWriteFile)
            {
                mostRecentWrite = lastWriteFile;
            }

            // adding new line only if there are more than 1 files
            // otherwise we are preserving file integrity
            if (sb.Length > 0)
            {
                sb.AppendLine();
            }

            sb.Append(content);
        }

        bundle.MostRecentWrite = mostRecentWrite;
        bundle.Output = sb.ToString();
    }

    static bool AdjustRelativePaths(Bundle bundle)
    {
        if (!bundle.Minify.ContainsKey("adjustRelativePaths"))
        {
            return true;
        }

        return bundle.Minify["adjustRelativePaths"].ToString() == "True";
    }
}