using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Minimatch;
using Newtonsoft.Json;

namespace BundlerMinifier;

public class Bundle
{
    [JsonIgnore]
    public string FileName { get; set; }

    [JsonProperty("outputFileName")]
    public string OutputFileName { get; set; }

    [JsonProperty("inputFiles")]
    public List<string> InputFiles { get; } = new List<string>();

    [JsonProperty("minify")]
    public Dictionary<string, object> Minify { get; } = new Dictionary<string, object> { { "enabled", true } };

    [JsonProperty("includeInProject")]
    public bool IncludeInProject { get; set; } = true;

    [JsonProperty("sourceMap")]
    public bool SourceMap { get; set; }

    [JsonProperty("sourceMapRootPath")]
    public string SourceMapRootPath { get; set; }

    internal string Output { get; set; }

    internal bool IsMinificationEnabled => this.Minify.ContainsKey("enabled") &&
                                           this.Minify["enabled"]?.ToString()?.Equals("true",
                                               StringComparison.OrdinalIgnoreCase) == true;

    internal bool IsGzipEnabled => this.Minify.ContainsKey("gzip") &&
                                   this.Minify["gzip"]?.ToString()?.Equals(
                                       "true", StringComparison.OrdinalIgnoreCase) == true;

    [JsonIgnore]
    public bool OutputIsMinFile => Path.GetFileName(this.OutputFileName)?.Contains(".min.") == true;

    public DateTime MostRecentWrite { get; set; }

    /// <summary>
    /// Converts the relative output file to an absolute file path.
    /// </summary>
    public string GetAbsoluteOutputFile()
    {
        string folder = new FileInfo(this.FileName).DirectoryName ?? string.Empty;
        return Path.Combine(folder, this.OutputFileName.NormalizePath());
    }

    /// <summary>
    /// Returns a list of absolute file paths of all matching input files.
    /// </summary>
    /// <param name="notifyOnPatternMiss">Writes to the Console if any input file is missing on disk.</param>
    [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
    public List<string> GetAbsoluteInputFiles(bool notifyOnPatternMiss = false)
    {
        List<string> files = new List<string>();

        if (!this.InputFiles.Any())
        {
            return files;
        }

        string folder = new DirectoryInfo(Path.GetDirectoryName(this.FileName) ?? string.Empty).FullName;
        string ext = Path.GetExtension(this.InputFiles.First());
        var options = new Options { AllowWindowsPaths = true };

        foreach (string inputFile in this.InputFiles.Where(f => !f.StartsWith("!", StringComparison.Ordinal)))
        {
            int globIndex = inputFile.IndexOf('*');

            if (globIndex > -1)
            {
                string relative = string.Empty;
                int last = inputFile.LastIndexOf('/', globIndex);

                if (last > -1)
                {
                    relative = inputFile[..(last + 1)];
                }

                var output = this.GetAbsoluteOutputFile();
                var outputMin = BundleMinifier.GetMinFileName(output);

                string searchDir = new FileInfo(Path.Combine(folder, relative).NormalizePath()).FullName;
                var allFiles = Directory.EnumerateFiles(searchDir, "*" + ext, SearchOption.AllDirectories)
                    .Select(f => f.Replace(folder + FileHelpers.PathSeparatorChar, ""));

                var matches = Minimatcher.Filter(allFiles, inputFile, options).Select(f => Path.Combine(folder, f));
                matches = matches.Where(match => match != output && match != outputMin).ToList();

                if (notifyOnPatternMiss && !matches.Any())
                {
                    Console.WriteLine($"  No files matched the pattern {inputFile}".Orange().Bright());
                }

                files.AddRange(matches.Where(f => !files.Contains(f)).OrderBy(f => f));
            }
            else
            {
                string fullPath = Path.Combine(folder, inputFile.NormalizePath());

                if (Directory.Exists(fullPath))
                {
                    var dir = new DirectoryInfo(fullPath);
                    const SearchOption search = SearchOption.TopDirectoryOnly;
                    var dirFiles = dir.GetFiles("*" + Path.GetExtension(this.OutputFileName), search);
                    var collected = dirFiles.Select(f => f.FullName).Where(f => !files.Contains(f)).ToList();

                    if (notifyOnPatternMiss && collected.Count == 0)
                    {
                        Console.WriteLine($"  No files were found in {inputFile}".Orange().Bright());
                    }

                    files.AddRange(collected);
                }
                else
                {
                    files.Add(fullPath);

                    if (!notifyOnPatternMiss || File.Exists(fullPath))
                    {
                        continue;
                    }

                    Console.WriteLine($"  {inputFile} was not found".Orange().Bright());
                    throw new FileNotFoundException(inputFile);
                }
            }
        }

        // Remove files starting with a !
        foreach (string inputFile in this.InputFiles)
        {
            int globIndex = inputFile.IndexOf('!');

            if (globIndex != 0)
            {
                continue;
            }

            var allFiles = files.Select(f => f.Replace(folder + FileHelpers.PathSeparatorChar, ""));
            var matches = Minimatcher.Filter(allFiles, inputFile, options).Select(f => Path.Combine(folder, f));
            files = matches.ToList();
        }

        return files;
    }

    public override bool Equals(object obj)
    {
        if (obj == null)
        {
            return false;
        }

        if (obj.GetType() != this.GetType())
        {
            return false;
        }

        if (obj == this)
        {
            return true;
        }

        var other = (Bundle)obj;

        return this.GetHashCode() == other.GetHashCode();
    }

    [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
    public override int GetHashCode()
    {
        return this.OutputFileName?.GetHashCode() ?? 0;
    }

    /// <summary>For the JSON.NET serializer</summary>
    public bool ShouldSerializeIncludeInProject()
    {
        var config = new Bundle();
        return this.IncludeInProject != config.IncludeInProject;
    }

    /// <summary>For the JSON.NET serializer</summary>
    public bool ShouldSerializeMinify()
    {
        var config = new Bundle();
        return !DictionaryEqual(this.Minify, config.Minify, null);
    }

    static bool DictionaryEqual<TKey, TValue>(
        IDictionary<TKey, TValue> first, IDictionary<TKey, TValue> second,
        IEqualityComparer<TValue> valueComparer)
    {
        if (first == second)
        {
            return true;
        }

        if (first == null || second == null)
        {
            return false;
        }

        if (first.Count != second.Count)
        {
            return false;
        }

        valueComparer ??= EqualityComparer<TValue>.Default;

        foreach (var kvp in first)
        {
            if (!second.TryGetValue(kvp.Key, out var secondValue))
            {
                return false;
            }

            if (!valueComparer.Equals(kvp.Value, secondValue))
            {
                return false;
            }
        }

        return true;
    }
}