# Bundler and Minifier

A .NET Core tool that let's you configure bundling and minification of JS, CSS and HTML files.
This is a simplified fork of https://github.com/madskristensen/BundlerMinifier that is kept
more up-to-date with NUglify and implements some missing features. It has been repackaged
into a .NET Core tool.

Install it by running `dotnet tool install BundlerMinifier.Core.Tool`
Invoke it by calling `dotnet tool run bundle`.

---------------------------------------

## Features

- Bundles CSS, JavaScript or HTML files into a single output file
- Support for globbing patterns
- Minify individual or bundled CSS, JavaScript and HTML files
- Minification options for each language is customizable
- Command line support
- Shortcut to update all bundles in solution
- Suppress output file generation
- Support NUglify killSwitch and ignoreErrorList directives for each bundle.

### A note about encoding

All files without a BOM (Byte Order Mark) is treated as UTF-8. If you
see strange characters in the output bundle files, you may want to consider
saving the input files as UTF-8 or an encoding that lets you specify a BOM.

### bundleconfig.json

Create a `bundleconfig.json` file at the root of the
project which is used to configure all bundling.

Here's an example of what that file looks like:

```js
[
  {
    "outputFileName": "output/bundle.css",
    "inputFiles": [
      "css/lib/**/*.css", // globbing patterns are supported
      "css/input/site.css"
    ],
    "minify": {
        "enabled": true,
        "commentMode": "all"
    }
  },
  {
    "outputFileName": "output/all.js",
    "inputFiles": [
      "js/*.js",
      "!js/ignore.js" // start with a ! to exclude files
    ]
  },
  {
    "outputFileName": "output/app.js",
    "inputFiles": [
      "input/main.js",
      "input/core/*.js" // all .js files in input/core/
    ]
  }
]
```

## Contribute
Check out the [contribution guidelines](.github/CONTRIBUTING.md)
if you want to contribute to this project.

## License
[Apache 2.0](LICENSE)
