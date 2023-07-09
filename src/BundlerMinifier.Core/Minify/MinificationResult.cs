using System.Collections.Generic;
using JetBrains.Annotations;

namespace BundlerMinifier;

[PublicAPI]
public class MinificationResult
{
    public MinificationResult(string fileName, string content, string sourceMap)
    {
        this.FileName = fileName;
        this.MinifiedContent = content;
        this.SourceMap = sourceMap;
    }

    public string FileName { get; set; }

    public string MinifiedContent { get; set; }

    /// <summary>
    /// The source map string produced by the compiler.
    /// </summary>
    public string SourceMap { get; set; }

    /// <summary>
    /// A collection of any errors reported by the compiler.
    /// </summary>
    public List<MinificationError> Errors { get; } = new List<MinificationError>();

    /// <summary>
    /// Checks if the compilation resulted in errors.
    /// </summary>
    public bool HasErrors => this.Errors.Count > 0;

    public bool Changed { get; set; }
}