﻿using System;
using System.IO;
using System.Linq;
using Xunit.Abstractions;

namespace Templates.Test.Helpers
{
    internal static class TemplatePackageInstaller
    {
        private static object _templatePackagesReinstallationLock = new object();
        private static bool _haveReinstalledTemplatePackages;

        private static readonly string[] _templatePackages = new[]
        {
            "Microsoft.DotNet.Web.ItemTemplates",
            "Microsoft.DotNet.Web.ProjectTemplates.2.0",
            "Microsoft.DotNet.Web.Spa.ProjectTemplates",
            "Microsoft.AspNetCore.SpaTemplates",
        };

        public static void EnsureTemplatePackagesWereReinstalled(ITestOutputHelper output)
        {
            lock (_templatePackagesReinstallationLock)
            {
                if (!_haveReinstalledTemplatePackages)
                {
                    ReinstallTemplatePackages(output);
                    _haveReinstalledTemplatePackages = true;
                }
            }
        }

        private static void ReinstallTemplatePackages(ITestOutputHelper output)
        {
            // Remove any previous or prebundled version of the template packages
            foreach (var packageName in _templatePackages)
            {
                var proc = ProcessEx.Run(
                    output,
                    Directory.GetCurrentDirectory(),
                    "dotnet",
                    $"new --uninstall {packageName}");
                proc.WaitForExit(assertSuccess: true);
            }

            VerifyCannotFindTemplate(output, "ASP.NET Core Empty");

            // Locate the artifacts directory containing the built template packages
            var solutionDir = FindAncestorDirectoryContaining("Templating.sln");
            var artifactsDir = Path.Combine(solutionDir, "artifacts", "build");
            var builtPackages = Directory.GetFiles(artifactsDir, "*.nupkg");
            foreach (var packagePath in builtPackages)
            {
                if (_templatePackages.Any(name => Path.GetFileName(packagePath).StartsWith(name, StringComparison.OrdinalIgnoreCase)))
                {
                    output.WriteLine($"Installing templates package {packagePath}...");
                    var proc = ProcessEx.Run(
                        output,
                        Directory.GetCurrentDirectory(),
                        "dotnet",
                        $"new --install \"{packagePath}\"");
                    proc.WaitForExit(assertSuccess: true);
                }
            }
        }

        private static void VerifyCannotFindTemplate(ITestOutputHelper output, string templateName)
        {
            // Verify we really did remove the previous templates
            var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("D"));
            Directory.CreateDirectory(tempDir);
            try
            {
                var proc = ProcessEx.Run(
                    output,
                    tempDir,
                    "dotnet",
                    $"new \"{templateName}\"");
                proc.WaitForExit(assertSuccess: false);
                if (!proc.Error.Contains($"No templates matched the input template name: {templateName}."))
                {
                    throw new InvalidOperationException($"Failed to uninstall previous templates. The template '{templateName}' could still be found.");
                }
            }
            finally
            {
                Directory.Delete(tempDir);
            }
        }

        private static string FindAncestorDirectoryContaining(string filename)
        {
            var dir = Directory.GetCurrentDirectory();
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir, filename)))
                {
                    return dir;
                }

                dir = Directory.GetParent(dir)?.FullName;
            }

            throw new InvalidOperationException($"Could not find any ancestor directory containing {filename} at or above {Directory.GetCurrentDirectory()}");
        }
    }
}