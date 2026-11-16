namespace Polson.Tests.Engine;

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

/// <summary>
/// The four drawing libraries are MIT and the rest of the repository is AGPL-3.0. A library that references an AGPL
/// project is no longer MIT in any useful sense: the binary that uses it carries the AGPL code too. One accidental
/// reference is all it takes, and nothing else would say so.
/// </summary>
public class LicenceBoundaryTests : TestsRuntime
{
    #region Constants
    /// <summary>The MIT projects. Each may reference only these.</summary>
    private static readonly string[] Mit = ["Polson.Runtime", "Polson.Drawing.Svg", "Polson.Drawing.Skia", "Polson.Drawing.Mesh"];
    #endregion

    #region Tests
    [Theory]
    [InlineData("Polson.Runtime")]
    [InlineData("Polson.Drawing.Svg")]
    [InlineData("Polson.Drawing.Skia")]
    [InlineData("Polson.Drawing.Mesh")]
    public void TestAnMitLibraryReferencesOnlyMitLibraries(string name)
    {
        var references = Assembly.Load(new AssemblyName(name)).GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("Polson", StringComparison.Ordinal));

        Assert.Empty(references.Except(Mit));
    }

    [Theory]
    [InlineData("Polson.Runtime")]
    [InlineData("Polson.Drawing.Svg")]
    [InlineData("Polson.Drawing.Skia")]
    [InlineData("Polson.Drawing.Mesh")]
    public void TestAnMitProjectDeclaresOnlyMitProjects(string name)
    {
        var project = Path.Combine(RepoRoot(), "src", name, $"{name}.csproj");
        var referenced = Regex.Matches(File.ReadAllText(project), "<ProjectReference\\s+Include=\"([^\"]+)\"")
            .Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/')));

        Assert.Empty(referenced.Except(Mit));
        Assert.Contains("<PackageLicenseExpression>MIT</PackageLicenseExpression>", File.ReadAllText(project));
        Assert.True(File.Exists(Path.Combine(RepoRoot(), "src", name, "LICENSE")), $"src/{name}/LICENSE is missing.");
    }

    /// <summary>A file copied out of its directory still says what it is under, so every source file states it.</summary>
    [Theory]
    [InlineData("Polson.Runtime")]
    [InlineData("Polson.Drawing.Svg")]
    [InlineData("Polson.Drawing.Skia")]
    [InlineData("Polson.Drawing.Mesh")]
    public void TestEveryMitSourceFileSaysSo(string name)
    {
        var dir = Path.Combine(RepoRoot(), "src", name);
        var unmarked = Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !Path.GetRelativePath(dir, f).Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj"))
            .Where(f => !File.ReadLines(f).First().TrimStart('﻿').StartsWith("// SPDX-License-Identifier: MIT", StringComparison.Ordinal))
            .Select(f => Path.GetRelativePath(dir, f));

        Assert.Empty(unmarked);
    }
    #endregion

    #region Methods
    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Polson.sln"))) return dir.FullName;
        }
        throw new InvalidOperationException("No Polson.sln above " + AppContext.BaseDirectory);
    }
    #endregion
}
