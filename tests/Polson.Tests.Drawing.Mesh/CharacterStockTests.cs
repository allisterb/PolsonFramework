namespace Polson.Tests.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Polson.Drawing.Skia;
using Xunit;
using Xunit.Abstractions;

/// <summary><c>Character.stock(name)</c> — a rigged stock body with its parts named, no build needed.</summary>
/// <remarks>
/// The male and female bodies and the CC0 clips are fetched into the Mesh project's <c>Library/</c> by
/// <c>tools/bootstrap.py</c>, and these fail rather than skip without them: a checkout that has not been set up
/// should say so, not report a pass.
/// </remarks>
[Collection(TomasRig.Name)]
public class CharacterStockTests : TestsRuntime
{
    const string Lastlight = @"C:\Projects\PolsonRuns\lastlight3";

    readonly ITestOutputHelper output;

    public CharacterStockTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void TestTheLibraryWasFetchedAndCopied()
    {
        const string setup = "the stock bodies and clips are not next to the test assembly: run tools/bootstrap.py and rebuild";
        Assert.True(CharacterStock.Shipped("stock") is not null && CharacterStock.Shipped("poses") is not null
                    && Directory.GetFiles(CharacterStock.Shipped("stock")!, "*.glb").Length > 0, setup);
        Assert.Contains(CharacterStock.Shipped("stock"), CharacterStock.Folders(null));
        Assert.Contains(CharacterStock.Shipped("poses"), PoseRetarget.Folders(null));
        Assert.Subset(new HashSet<string> { "male", "female" }, CharacterStock.Names(null).ToHashSet());
        Assert.Contains(PoseRetarget.Clips(null), c => c.Name == "Crouch_Idle");
    }

    [Fact]
    public void TestStocksListTheirLicences()
    {
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
        var ex = Assert.Throws<ArgumentException>(() => new CharacterToolkit(null).Stock("mael"));
        Assert.Contains("male", ex.Message);
    }

    /// <summary>A project's own <c>stock/</c> comes first, and a body on another skeleton is refused by what it lacks.</summary>
    [Fact]
    public void TestAProjectStockComesFirstAndMustBeOnTheSkeleton()
    {
        var rigged = Path.Combine(Lastlight, "characters", "tomas", "rigged.glb");
        if (!File.Exists(rigged)) { output.WriteLine("NOT RUN: lastlight3 not on disk"); return; }

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
