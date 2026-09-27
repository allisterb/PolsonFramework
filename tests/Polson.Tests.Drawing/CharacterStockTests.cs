namespace Polson.Tests.Drawing;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;
using Xunit.Abstractions;

/// <summary><c>Character.stock(name)</c> — a rigged stock body with its parts named, no build needed.</summary>
/// <remarks>Runs where stock bodies have been fetched into <c>models/stock/</c>, and says NOT RUN otherwise.</remarks>
[Collection(TomasRig.Name)]
public class CharacterStockTests : TestsRuntime
{
    const string Lastlight = @"C:\Projects\PolsonRuns\lastlight3";

    readonly ITestOutputHelper output;

    public CharacterStockTests(ITestOutputHelper output) => this.output = output;

    static bool Fetched() => CharacterStock.Names(null).Contains("male") && CharacterStock.Names(null).Contains("female");

    [Fact]
    public void TestStocksListTheirLicences()
    {
        if (!Fetched()) { output.WriteLine("NOT RUN: male and female not in models/stock/"); return; }
        var stocks = new CharacterToolkit(null).Stocks().Cast<Dictionary<string, object?>>().ToList();
        var male = stocks.Single(s => (string)s["name"]! == "male");
        Assert.Equal("CC0", male["licence"]);
        Assert.Equal("Quaternius", male["author"]);
        Assert.Equal("male.glb", male["file"]);
    }

    [Theory]
    [InlineData("male")]
    [InlineData("female")]
    public void TestAStockBodyPosesAndPlaces(string name)
    {
        if (!Fetched() || !PoseRetarget.Clips(null).Any(c => c.Name == "Crouch_Idle"))
        { output.WriteLine("NOT RUN: stock bodies or pose library not on disk"); return; }
        var kit = new CharacterToolkit(null);
        var body = kit.Stock(name);

        Assert.True(body.Posable);
        Assert.Equal(17, body.JointMap.Count);
        Assert.Same(body, kit.Stock(name));   // loaded once

        var crouch = body.Pose(kit.Retarget(body, "Crouch_Idle", new Dictionary<string, object?> { ["at"] = 0.5 }));
        var height = body.Vertices.Max(v => v.Y) - body.Vertices.Min(v => v.Y);
        var drop = body.Vertices.Max(v => v.Y) - crouch.Vertices.Max(v => v.Y);
        output.WriteLine($"{name}: {body.VertexCount} vertices, height {height:0.00}, crouch drops the top {drop / height * 100:0}%");
        Assert.True(drop > 0.2f * height);

        var frame = new Dictionary<string, object?> { ["x"] = 0, ["y"] = 0, ["width"] = 400, ["height"] = 600 };
        var draw = kit.Place(body, frame);
        Assert.True(Convert.ToSingle(draw["scale"]) > 0f);
    }

    [Fact]
    public void TestAnUnknownNameIsRefusedWithTheNames()
    {
        if (!Fetched()) { output.WriteLine("NOT RUN: stock bodies not in models/stock/"); return; }
        var ex = Assert.Throws<ArgumentException>(() => new CharacterToolkit(null).Stock("mael"));
        Assert.Contains("male", ex.Message);
    }

    /// <summary>A project's own <c>stock/</c> comes first, and a body on another skeleton is refused by what it lacks.</summary>
    [Fact]
    public void TestAProjectStockComesFirstAndMustBeOnTheSkeleton()
    {
        var rigged = Path.Combine(Lastlight, "characters", "tomas", "rigged.glb");
        if (!Fetched() || !File.Exists(rigged)) { output.WriteLine("NOT RUN: stock bodies or lastlight3 not on disk"); return; }

        var project = Path.Combine(Path.GetTempPath(), "polson-stock-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(project, "stock"));
        try
        {
            File.Copy(rigged, Path.Combine(project, "stock", "tomas.glb"));
            var kit = new CharacterToolkit(project);
            var names = kit.Stocks().Cast<Dictionary<string, object?>>().Select(s => (string)s["name"]!).ToList();
            Assert.Equal("tomas", names[0]);
            Assert.Null(kit.Stocks().Cast<Dictionary<string, object?>>().First()["licence"]);

            var ex = Assert.Throws<ArgumentException>(() => kit.Stock("tomas"));
            output.WriteLine(ex.Message);
            Assert.Contains("pelvis", ex.Message);
        }
        finally { Directory.Delete(project, recursive: true); }
    }
}
