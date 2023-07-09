using System;
using JetBrains.Annotations;

namespace BundlerMinifier;

[PublicAPI]
public class MinifyFileEventArgs : EventArgs
{
    public MinifyFileEventArgs(string originalFile, string resultFile, bool containsChanges)
    {
        this.ContainsChanges = containsChanges;
        this.OriginalFile = originalFile;
        this.ResultFile = resultFile;
    }

    public MinifyFileEventArgs(string originalFile, string resultFile, Bundle bundle, bool containsChanges)
        : this(originalFile, resultFile, containsChanges)
    {
        this.Bundle = bundle;
    }

    public bool ContainsChanges { get; set; }

    public string OriginalFile { get; private set; }

    public string ResultFile { get; private set; }

    public Bundle Bundle { get; private set; }

    /// <summary>
    /// A collection of any errors reported by the compiler.
    /// </summary>
    public MinificationResult Result { get; set; }
}