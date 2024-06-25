using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace BundlerMinifier;

[PublicAPI]
public class Watcher
{
    static readonly List<ChangeHandler> s_ChangeHandlers = new List<ChangeHandler>();
    static FileSystemWatcher s_Listener;
    static string s_ConfigPath;
    static bool s_WatchingAll;
    static BundleFileProcessor s_Processor;
    static bool s_UseParallel;

    [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    public static bool Configure(
        BundleFileProcessor processor,
        List<string> configurations,
        string configPath,
        bool useParallel)
    {
        s_Processor = processor;
        s_UseParallel = useParallel;

        if (!BundleHandler.TryGetBundles(configPath, out var bundles))
        {
            return false;
        }

        if (configurations.Count > 0)
        {
            foreach (string config in configurations)
            {
                var bundle = bundles.FirstOrDefault(x =>
                    string.Equals(x.OutputFileName, config, StringComparison.OrdinalIgnoreCase));

                if (bundle != null)
                {
                    s_ChangeHandlers.Add(new ChangeHandler(processor, configPath, bundle, s_UseParallel));
                }
            }
        }
        else
        {
            foreach (var bundle in bundles)
            {
                s_ChangeHandlers.Add(new ChangeHandler(processor, configPath, bundle, s_UseParallel));
            }

            s_WatchingAll = true;
        }

        if (s_ChangeHandlers.Count > 0)
        {
            ConfigureWatcher(configPath);
        }

        return s_ChangeHandlers.Count > 0;
    }

    static void ConfigureWatcher(string configPath)
    {
        s_ConfigPath = configPath;
        string basePath = new FileInfo(configPath).Directory?.FullName ?? string.Empty;
        var fsw = new FileSystemWatcher(basePath);

        fsw.Changed += FilesChanged;
        fsw.Renamed += FilesChanged;

        fsw.IncludeSubdirectories = true;
        fsw.NotifyFilter = NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName;
        fsw.EnableRaisingEvents = true;
        s_Listener = fsw;
    }

    public static void Stop()
    {
        var fsw = s_Listener;

        if (fsw == null)
        {
            return;
        }

        s_Listener = null;
        fsw.Changed -= FilesChanged;
        fsw.Changed -= FilesChanged;

        try
        {
            fsw.Dispose();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    static async void FilesChanged(object sender, FileSystemEventArgs e)
    {
        const int maxRetries = 10;
        var fsw = (FileSystemWatcher)sender;
        fsw.EnableRaisingEvents = false;
        int retries = 0;
        bool suppressOutputMessage = false;

        while (retries < maxRetries)
        {
            await Task.Delay(100);

            try
            {
                if (string.Equals(e.FullPath, s_ConfigPath, StringComparison.OrdinalIgnoreCase))
                {
                    bool changed = ReloadConfig();
                    suppressOutputMessage = !changed;

                    if (changed)
                    {
                        Console.WriteLine("Configuration reloaded".Green().Bright());
                    }
                }
                else
                {
                    bool anyRan = false;

                    foreach (var handler in s_ChangeHandlers)
                    {
                        anyRan |= handler.FilesChanged(e);
                    }

                    if (!anyRan)
                    {
                        suppressOutputMessage = true;
                    }
                }

                break;
            }
            catch
            {
                ++retries;
            }
        }

        if (retries >= maxRetries)
        {
            Console.WriteLine("An error occurred while processing".Red().Bright());
        }

        fsw.EnableRaisingEvents = true;

        if (!suppressOutputMessage)
        {
            Console.WriteLine("Watching... Press [Enter] to stop".LightGray().Bright());
        }
    }

    [SuppressMessage("ReSharper", "HeapView.ClosureAllocation")]
    [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
    static bool ReloadConfig()
    {
        bool anyChanges = false;

        if (!BundleHandler.TryGetBundles(s_ConfigPath, out var bundles))
        {
            throw new Exception("Unable to load bundles.");
        }

        var oldHandlers = s_ChangeHandlers.ToList();

        if (!s_WatchingAll)
        {
            foreach (var handler in oldHandlers)
            {
                var bundle = bundles.FirstOrDefault(x => string.Equals(x.OutputFileName,
                    handler.Bundle.OutputFileName, StringComparison.OrdinalIgnoreCase));

                if (bundle != null)
                {
                    var newHandler = new ChangeHandler(s_Processor, bundle.FileName, bundle, s_UseParallel);

                    if (newHandler.Equals(handler))
                    {
                        continue;
                    }

                    s_ChangeHandlers.Remove(handler);
                    s_ChangeHandlers.Add(newHandler);
                    s_Processor.Process(s_ConfigPath, new[] { bundle }, s_UseParallel);
                    anyChanges = true;
                }
                else
                {
                    s_ChangeHandlers.Remove(handler);
                    Console.WriteLine(
                        $"Cannot find configuration {handler.Bundle.OutputFileName}".Orange().Bright());
                }
            }
        }
        else
        {
            HashSet<Bundle> bundlesToProcess = [..bundles];

            foreach (var handler in oldHandlers)
            {
                var bundle = bundles.FirstOrDefault(x =>
                    string.Equals(
                        x.OutputFileName,
                        handler.Bundle.OutputFileName,
                        StringComparison.OrdinalIgnoreCase));

                if (bundle == null)
                {
                    continue;
                }

                bundlesToProcess.Remove(bundle);
                var newHandler = new ChangeHandler(s_Processor, bundle.FileName, bundle, s_UseParallel);

                if (newHandler.Equals(handler))
                {
                    continue;
                }

                s_ChangeHandlers.Remove(handler);
                s_ChangeHandlers.Add(newHandler);
                s_Processor.Process(s_ConfigPath, new[] { bundle }, s_UseParallel);
                anyChanges = true;
            }

            foreach (var bundle in bundlesToProcess)
            {
                s_ChangeHandlers.Add(new ChangeHandler(s_Processor, s_ConfigPath, bundle, s_UseParallel));
                s_Processor.Process(s_ConfigPath, new[] { bundle }, s_UseParallel);
                anyChanges = true;
            }
        }

        return anyChanges;
    }
}
