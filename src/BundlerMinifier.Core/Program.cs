using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace BundlerMinifier;

[UsedImplicitly]
class Program
{
    const string c_DefaultConfigFileName = "bundleconfig.json";

    static bool GetConfigFileFromArgs(IReadOnlyList<string> args, out string configPath)
    {
        int index = args.Count - 1;
        bool fileExists;
        bool fallbackExists = fileExists = File.Exists(c_DefaultConfigFileName);

        if (index > -1)
        {
            fileExists = File.Exists(args[index]);

            if (BundleHandler.TryGetBundles(args[index], out _))
            {
                configPath = args[index];
                return true;
            }
        }

        if (BundleHandler.TryGetBundles(c_DefaultConfigFileName, out _))
        {
            configPath = new FileInfo(c_DefaultConfigFileName).FullName;
            return false;
        }

        if (args.Count > 0)
        {
            Console.WriteLine(!fileExists
                ? $"A configuration file called {args[index]} could not be found".Red().Bright()
                : $"Configuration file {args[index]} has errors".Red().Bright());
        }

        if (!fallbackExists)
        {
            Console.WriteLine($"A configuration file called {c_DefaultConfigFileName} could not be found".Red()
                .Bright());
        }
        else
        {
            Console.WriteLine($"Configuration file {c_DefaultConfigFileName} has errors".Red().Bright());
        }

        configPath = null;
        return false;
    }

    static int Main(params string[] args)
    {
        int readConfigsUntilIndex = args.Length;
        if (GetConfigFileFromArgs(args, out var configPath))
        {
            --readConfigsUntilIndex;
        }

        if (configPath == null)
        {
            ShowHelp();
            return 0;
        }

        Console.WriteLine($"Bundling with configuration from {configPath}".Green().Bright());

        var processor = new BundleFileProcessor();
        EventHookups(processor);

        List<string> configurations = [];
        bool isClean = false;
        bool isWatch = false;
        bool isNoColor = false;
        bool isHelp = false;
        bool useParallel = true;

        for (int i = 0; i < readConfigsUntilIndex; ++i)
        {
            bool currentArgIsClean = string.Equals(args[i], "clean", StringComparison.OrdinalIgnoreCase);
            bool currentArgIsWatch = string.Equals(args[i], "watch", StringComparison.OrdinalIgnoreCase);
            bool currentArgIsNoColor = string.Equals(args[i], "--no-color", StringComparison.OrdinalIgnoreCase);
            bool currentArgIsHelp = string.Equals(args[i], "help", StringComparison.OrdinalIgnoreCase);
            bool currentArgDisableParallel = string.Equals(args[i], "--disable-parallel", StringComparison.OrdinalIgnoreCase);
            currentArgIsHelp |= string.Equals(args[i], "-h", StringComparison.OrdinalIgnoreCase);
            currentArgIsHelp |= string.Equals(args[i], "--help", StringComparison.OrdinalIgnoreCase);
            currentArgIsHelp |= string.Equals(args[i], "help", StringComparison.OrdinalIgnoreCase);
            currentArgIsHelp |= string.Equals(args[i], "-?", StringComparison.OrdinalIgnoreCase);

            if (currentArgIsHelp)
            {
                isHelp = true;
                break;
            }

            if (currentArgIsClean)
            {
                isClean = true;
            }
            else if (currentArgIsWatch)
            {
                isWatch = true;
            }
            else if (currentArgIsNoColor)
            {
                isNoColor = true;
            }
            else
            {
                configurations.Add(args[i]);
            }

            useParallel = !currentArgDisableParallel;
        }

        if (isNoColor)
        {
            StringExtensions.NoColor = true;
        }

        if (isHelp)
        {
            ShowHelp();
            return 0;
        }

        if (isClean && isWatch)
        {
            Console.WriteLine("The clean and watch options may not be used together.".Red().Bright());
            return -1;
        }

        if (isWatch)
        {
            bool isWatching = Watcher.Configure(processor, configurations, configPath, useParallel);

            if (!isWatching)
            {
                Console.WriteLine("No output file names were matched".Red().Bright());
                return -1;
            }

            Console.WriteLine("Watching... Press [Enter] to stop".LightGray().Bright());
            Console.ReadLine();
            Watcher.Stop();
            return 0;
        }

        if (configurations.Count == 0)
        {
            return Run(processor, configPath, null, isClean, useParallel);
        }

        foreach (string config in configurations)
        {
            int runResult = Run(processor, configPath, config, isClean, useParallel);

            if (runResult < 0)
            {
                return runResult;
            }
        }

        return 0;
    }

    static void ShowHelp()
    {
#if DOTNET
        const string commandName = "dotnet bundle";
#else
            const string commandName = "BundlerMinifier";
#endif
        using (ColoredTextRegion.Create(s => s.Orange().Bright()))
        {
            Console.WriteLine($"Usage: {commandName} [[args]] [configPath]");
            Console.WriteLine(" Each arg in args can be one of the following:");
            Console.WriteLine("     - The name of an output to process (outputFileName in the configuration file)");
            Console.WriteLine("         If no outputs to process are specified, all ");
            Console.WriteLine("     - [ -? | -h | --help | help]        - Shows this help message");
            Console.WriteLine(
                "         All other arguments are ignored when one of the help switches are included");
            Console.WriteLine("     - clean                             - Deletes artifacts from previous runs");
            Console.WriteLine("         All other arguments are ignored when \"clean\" is included");
            Console.WriteLine("         Not compatible with \"watch\"");
            Console.WriteLine("     - watch                             - Deletes artifacts from previous runs");
            Console.WriteLine("         Watches files that would cause specified rules to run");
            Console.WriteLine("         Not compatible with \"clean\"");
            Console.WriteLine("     - --disable-parallel                - For watch and build, no parallel bundling.");
            Console.WriteLine("     - --no-color                        - Doesn't colorize output");
            Console.WriteLine("     - [ -? | -h | --help ] to show this help message");
            Console.WriteLine(
                $" The configPath parameter may be omitted if a {c_DefaultConfigFileName} file is in the working directory");
            Console.WriteLine(
                "     otherwise, this parameter must be the location of a file containing the definitions for how");
            Console.WriteLine("     the bundling and minification should be performed.");
        }
    }

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    static int Run(BundleFileProcessor processor, string configPath, string file, bool isClean, bool useParallel)
    {
        var configs = GetConfigs(configPath, file);

        if (configs == null || !configs.Any())
        {
            Console.WriteLine("No configurations matched".Orange().Bright());
            return -1;
        }

        try
        {
            if (isClean)
            {
                BundleFileProcessor.Clean(configPath, configs);
            }
            else
            {
                processor.Process(configPath, configs, useParallel);
            }

            return 0;
        }
        catch (Exception ex)
        {
            var exceptions = new List<Exception>();

            if (ex is AggregateException aggregateException)
            {
                exceptions.AddRange(aggregateException.InnerExceptions);
            }
            else
            {
                exceptions.Add(ex);
            }

            foreach (var exception in exceptions)
            {
                Console.WriteLine(exception.Message);
            }

            return -1;
        }
    }

    static void EventHookups(BundleFileProcessor processor)
    {
        // For console colors, see http://stackoverflow.com/questions/23975735/what-is-this-u001b9-syntax-of-choosing-what-color-text-appears-on-console

        processor.Processing += (_, e) =>
        {
            Console.WriteLine($"Processing {e.Bundle.OutputFileName.Cyan().Bright()}");
            FileHelpers.RemoveReadonlyFlagFromFile(e.Bundle.GetAbsoluteOutputFile());
        };
        processor.AfterBundling += (_, _) => { Console.WriteLine("  Bundled".Green().Bright()); };
        processor.BeforeWritingSourceMap += (_, e) => { FileHelpers.RemoveReadonlyFlagFromFile(e.ResultFile); };
        processor.AfterWritingSourceMap += (_, _) => { Console.WriteLine("  Sourcemapped".Green().Bright()); };

        BundleMinifier.BeforeWritingMinFile += (_, e) => { FileHelpers.RemoveReadonlyFlagFromFile(e.ResultFile); };
        BundleMinifier.AfterWritingMinFile += (_, _) => { Console.WriteLine("  Minified".Green().Bright()); };
        BundleMinifier.BeforeWritingGzipFile += (_, e) => { FileHelpers.RemoveReadonlyFlagFromFile(e.ResultFile); };
        BundleMinifier.AfterWritingGzipFile += (_, _) => { Console.WriteLine("  GZipped".Green().Bright()); };
        BundleMinifier.ErrorMinifyingFile += (_, e) =>
        {
            Console.WriteLine($"{string.Join(Environment.NewLine, e.Result.Errors)}");
        };
    }

    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
    static IEnumerable<Bundle> GetConfigs(string configPath, string file)
    {
        var configs = BundleHandler.GetBundles(configPath);

        if (configs == null || !configs.Any())
        {
            return null;
        }

        if (file == null)
        {
            return configs;
        }

        if (file.StartsWith("*"))
        {
            configs = configs.Where(c =>
                Path.GetExtension(c.OutputFileName)?.Equals(file[1..], StringComparison.OrdinalIgnoreCase) == true);
        }
        else
        {
            configs = configs.Where(c => c.OutputFileName.Equals(file, StringComparison.OrdinalIgnoreCase));
        }

        return configs;
    }
}
