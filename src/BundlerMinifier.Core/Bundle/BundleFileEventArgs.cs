using System;
using JetBrains.Annotations;

namespace BundlerMinifier;

[PublicAPI]
public class BundleFileEventArgs : EventArgs
{
    public BundleFileEventArgs(string outputFileName, Bundle bundle, string baseFolder, bool containsChanges)
    {
        this.ContainsChanges = containsChanges;
        this.OutputFileName = outputFileName;
        this.Bundle = bundle;
        this.BaseFolder = baseFolder;
    }

    public bool ContainsChanges { get; set; }

    public Bundle Bundle { get; set; }

    public string OutputFileName { get; set; }

    public string BaseFolder { get; set; }

}