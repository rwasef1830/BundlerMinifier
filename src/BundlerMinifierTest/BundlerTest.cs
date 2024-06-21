using System;
using System.IO;
using System.Linq;
using BundlerMinifier;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BundlerMinifierTest;

[TestClass]
public class BundlerTest
{
    const string c_TestBundle = "../../../artifacts/test1.json";
    const string c_TestBundleRecursion = "../../../artifacts/test11.json";
    const string c_TestBundleRecursionFails = "../../../artifacts/test12.json";
        
    BundleFileProcessor _processor;
    Guid _guid;

    [TestInitialize]
    public void Setup()
    {
        this._processor = new BundleFileProcessor();
        this._guid = Guid.NewGuid();
    }

    [TestCleanup]
    public void Cleanup()
    {
        File.Delete("../../../artifacts/" + this._guid + ".json");
        File.Delete("../../../artifacts/foo.js");
        File.Delete("../../../artifacts/foo.js.gz");
        File.Delete("../../../artifacts/foo.min.js");
        File.Delete("../../../artifacts/foo.min.js.map");
        File.Delete("../../../artifacts/foo.css");
        File.Delete("../../../artifacts/foo.min.css");
        File.Delete("../../../artifacts/foo.html");
        File.Delete("../../../artifacts/foo.min.html");
        File.Delete("../../../artifacts/minify.min.js");
        File.Delete("../../../artifacts/minify.min.js.gz");
        File.Delete("../../../artifacts/encoding/encoding.js");
        File.Delete("../../../artifacts/encoding/encoding.min.js");
        File.Delete("../../../artifacts/file3.min.html");
        File.Delete("../../../artifacts/file3.min.js");
        File.Delete("../../../artifacts/file4.min.html");
        File.Delete("../../../artifacts/test7.min.js");
        File.Delete("../../../artifacts/test8.min.js");
    }

    [TestMethod]
    public void IsSupported()
    {
        var files1 = new[] { "file.js", "file2.js" };
        var result1 = BundleFileProcessor.IsSupported(files1);
        Assert.IsTrue(result1);

        var files2 = new[] { "file.js", "file2.css" };
        var result2 = BundleFileProcessor.IsSupported(files2);
        Assert.IsFalse(result2);

        var files3 = new[] { null, "file2.css" };
        var result3 = BundleFileProcessor.IsSupported(files3);
        Assert.IsTrue(result3);
    }

    [TestMethod]
    public void GetBundles()
    {
        var bundles = BundleHandler.GetBundles(c_TestBundle);
        Assert.AreEqual(4, bundles.Count());
    }

    [TestMethod]
    public void GetBundles_Recursion()
    {
        var bundles = BundleHandler.GetBundles(c_TestBundleRecursion).ToList();
            
        var bundleA = bundles.Single(x => x.OutputFileName == "test11a.min.js");
        Assert.AreEqual(1, bundleA.InputFiles.Count);
        Assert.AreEqual("file1.js", bundleA.InputFiles[0]);
            
        var bundleB = bundles.Single(x => x.OutputFileName == "test11b.min.js");
        Assert.AreEqual(2, bundleB.InputFiles.Count);
        Assert.AreEqual("file1.js", bundleB.InputFiles[0]);
        Assert.AreEqual("file2.js", bundleB.InputFiles[1]);
            
        var bundleC = bundles.Single(x => x.OutputFileName == "test11c.min.js");
        Assert.AreEqual(3, bundleC.InputFiles.Count);
        Assert.AreEqual("file3.js", bundleC.InputFiles[0]);
        Assert.AreEqual("file1.js", bundleC.InputFiles[1]);
        Assert.AreEqual("file2.js", bundleC.InputFiles[2]);
            
        Assert.AreEqual(3, bundles.Count);
    }
        
    [TestMethod]
    public void GetBundles_Recursion_Circular_Fails()
    {
        var bundles = BundleHandler.GetBundles(c_TestBundleRecursionFails).ToList();
        Assert.AreEqual(0, bundles.Count);
    }

    [TestMethod]
    public void AddBundles()
    {
        var bundle = new Bundle
        {
            IncludeInProject = true,
            OutputFileName = this._guid + ".js"
        };
        bundle.InputFiles.AddRange(new[] { "file1.js", "file2.js" });

        string filePath = "../../../artifacts/" + this._guid + ".json";
        BundleHandler.AddBundle(filePath, bundle);

        var bundles = BundleHandler.GetBundles(filePath);
        Assert.AreEqual(1, bundles.Count());
    }

    [TestMethod]
    public void AddBundleToExisting()
    {
        var bundle = new Bundle
        {
            IncludeInProject = true,
            OutputFileName = this._guid + ".js"
        };
        bundle.InputFiles.AddRange(new[] { "file1.js", "file2.js" });

        string filePath = "../../../artifacts/" + this._guid + ".json";
        File.Copy(c_TestBundle, filePath);
        BundleHandler.AddBundle(filePath, bundle);

        var bundles = BundleHandler.GetBundles(filePath);
        Assert.AreEqual(5, bundles.Count());
    }

    [TestMethod]
    public void Process()
    {
        this._processor.Process(c_TestBundle, useParallel: false);

        // JS
        string jsResult = File.ReadAllText(new FileInfo("../../../artifacts/foo.min.js").FullName);
        Assert.IsTrue(jsResult.StartsWith("var file1=1,file2=2"));
        Assert.IsTrue(new FileInfo("../../../artifacts/foo.min.js.map").Exists);

        // CSS
        string cssResult = File.ReadAllText(new FileInfo("../../../artifacts/foo.min.css").FullName);
        Assert.AreEqual("body{background:url('/test.png')}body{display:block}body{background:url(test2/image.png?foo=hat)}", cssResult);

        // HTML
        string htmlResult = File.ReadAllText("../../../artifacts/foo.min.html");
        Assert.AreEqual(@"<div>hatæ</div><span tabindex=2><i>hat</i></span>", htmlResult);
    }

    [TestMethod]
    public void Process_WithMissingFiles_ShouldThrow()
    {
        var ex = Assert.ThrowsException<AggregateException>(() =>
            this._processor.Process(c_TestBundle.Replace("test1", "test10"), useParallel: false));
        Assert.AreEqual(ex.InnerException?.GetType(), typeof(FileNotFoundException));
    }

    [TestMethod]
    public void Minify()
    {
        var bundles = BundleHandler.GetBundles(c_TestBundle);
        this._processor.Process(c_TestBundle, bundles.Where(b => b.OutputFileName == "minify.min.js"), useParallel: false);

        string cssResult = File.ReadAllText(new FileInfo("../../../artifacts/minify.min.js").FullName);
        Assert.AreEqual("var i=1,y=3,o={value:1},o2={...o,newValue:2};\n//# sourceMappingURL=minify.min.js.map", cssResult);

        string map = File.ReadAllText(new FileInfo("../../../artifacts/minify.min.js.map").FullName);
        Assert.IsTrue(map.Contains("minify.js"));
    }

    [TestMethod]
    public void JustGzip()
    {
        this._processor.Process(c_TestBundle.Replace("test1", "test3"), useParallel: false);
        Assert.IsFalse(File.Exists("../../../artifacts/foo.min.js"));
        Assert.IsTrue(File.Exists("../../../artifacts/foo.js.gz"));
        Assert.IsTrue(File.Exists("../../../artifacts/foo.js.br"));
        Assert.IsTrue(File.Exists("../../../artifacts/foo.js.zstd"));
        Assert.IsTrue(File.Exists("../../../artifacts/minify.min.js"));
        Assert.IsTrue(File.Exists("../../../artifacts/minify.min.js.gz"));
        Assert.IsTrue(File.Exists("../../../artifacts/minify.min.js.zstd"));
    }

    [TestMethod]
    public void ProcessWithDirectory()
    {
        this._processor.Process(c_TestBundle.Replace("test1", "test2"), useParallel: false);

        // JS
        string jsResult = File.ReadAllText("../../../artifacts/foo.min.js");
        Assert.AreEqual("var file1=1,file2=2;", jsResult);
    }

    [TestMethod]
    public void InvalidCss()
    {
        this._processor.Process(c_TestBundle.Replace("test1", "error"), useParallel: false);

        bool result = File.Exists("../../../artifacts/error.min.css");
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void PreserveKnockoutContainerlessBindings()
    {
        this._processor.Process(c_TestBundle.Replace("test1", "test4"), useParallel: false);

        string htmlResult = File.ReadAllText("../../../artifacts/file3.min.html");
        Assert.AreEqual("<div><!--ko if:observable--><p></p><!--/ko--></div>", htmlResult);
    }

    [TestMethod]
    public void PreserveJavaScript0EvalStatements()
    {
        this._processor.Process(c_TestBundle.Replace("test1", "test5"), useParallel: false);

        string jsResult = File.ReadAllText("../../../artifacts/file3.min.js");
        Assert.AreEqual("(function(n){n()})(function(){\"use strict\";var n=(0,eval)(\"this\");console.log(n)});", jsResult);
    }

    [TestMethod]
    public void KeepOneSpaceWhenCollapsingHtml()
    {
        this._processor.Process(c_TestBundle.Replace("test1", "test6"), useParallel: false);

        string htmlResult = File.ReadAllText("../../../artifacts/file4.min.html");
        Assert.AreEqual("<div class=\"bold\"><span><i class=\"fa fa-phone\"></i></span> <span>DEF</span></div>", htmlResult);
    }

    [TestMethod]
    public void PreventDoubleProcessing()
    {
        var bundle = c_TestBundle.Replace("test1", "test7");

        var result = this._processor.Process(bundle, useParallel: false);
        Assert.IsTrue(result);
        const string filePath = "../../../artifacts/test7.min.js";
        Assert.IsTrue(File.Exists(filePath));
        var firstFileTime = File.GetLastWriteTimeUtc(filePath);

        result = this._processor.Process(bundle, useParallel: false);
        Assert.IsFalse(result);
        var secondFileTime = File.GetLastWriteTimeUtc(filePath);
        Assert.AreEqual(firstFileTime, secondFileTime);
    }

    [TestMethod]
    public void SupportNewSyntax()
    {
        this._processor.Process(c_TestBundle.Replace("test1", "test8"), useParallel: false);

        string jsResult = File.ReadAllText("../../../artifacts/test8.min.js");

        Assert.AreEqual("function test(n){for(const t of n)console.log(t)}test([1,2,3,4]);", jsResult);
    }

    [TestMethod]
    public void SupportDoubleAsteriskOperator()
    {
        this._processor.Process(c_TestBundle.Replace("test1", "test9"), useParallel: false);

        string jsResult = File.ReadAllText("../../../artifacts/test9.min.js");

        Assert.AreEqual("function r(n,t){return n**t}let x=2**5.1,y=3**x,z=2**81,p=21;", jsResult);
    }
}
