namespace Polson.Tests.ExtendedMind;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using global::Polson.Tests;
using Polson.ExtendedMind.ObjectGeneration;
using Xunit;
using Xunit.Abstractions;

/// <summary>Does a four-view TURNAROUND do for a character what two elevations could not for a prop?</summary>
/// <remarks>
/// <para>
/// <b>A probe, not a regression test.</b> It spends four real generations, so it is gated on
/// <c>POLSON_TRELLIS_TURNAROUND=1</c> as well as on a reachable container — an ordinary run of this
/// project's tests must not quietly burn GPU minutes.
/// </para>
/// <para>
/// <b>What it is testing, and why the answer is not already known.</b> Multi-image input was measured
/// on a drawn chest of known proportions and did <i>not</i> triangulate: the best two-view answer was
/// no better than one view's, because <b>the API carries no camera-pose field</b> and nothing can say
/// "this second image is the side, at 90°". That result stands. What it does not settle is whether
/// four <i>canonically posed, consistently framed</i> views of a <i>character</i> behave differently —
/// the failure there was an absent pose, and a front/back/left/right turnaround is the one input whose
/// pose a model might plausibly infer from content alone.
/// </para>
/// <para>
/// <b>Four runs differing only in their input</b>, one fixed non-zero seed throughout, so any
/// difference is attributable to the images rather than to sampling. <see cref="Baseline"/> is the
/// control. The two <c>stochastic</c> runs are reversed copies of one another: that algorithm
/// <i>cycles</i> images and was measured to be order-dependent, so if the reversal tracks the last
/// run rather than the first, passing an array is in effect single-view conditioning on
/// <c>image[0]</c> and the extra three views bought nothing.
/// </para>
/// <para>
/// Textured, unusually — <c>no_texture</c> is the cheaper default and is normally right for a studio
/// that inks geometry. Here the texture <i>is</i> a measurement: the back view supplies braids and a
/// slung crossbow that the front view cannot show, so whether they appear on the mesh's back is the
/// most direct evidence of whether the back image was used at all.
/// </para>
/// </remarks>
public class TrellisTurnaroundProbe : TestsRuntime
{
    #region Fields
    readonly ITestOutputHelper output;
    readonly string? baseUrl;

    /// <summary>Views cut from the supplied sheet, and where the generated models are left.</summary>
    /// <remarks>
    /// Under <c>bin/</c> deliberately: it is gitignored, so neither the source artwork nor a
    /// multi-megabyte GLB is committed, which is how <c>potrace</c> and the Blender build are carried.
    /// </remarks>
    static readonly string Dir = Path.Combine(RepoRoot(), "bin", "trellis", "turnaround");

    /// <summary>Fixed and non-zero. <b>Seed 0 means RANDOM</b>, which would make this unrepeatable.</summary>
    const long Seed = 7;
    #endregion

    #region Constructors
    public TrellisTurnaroundProbe(ITestOutputHelper output)
    {
        this.output = output;
        this.baseUrl = config["Trellis:BaseUrl"];
    }
    #endregion

    #region Probe
    [Fact]
    public async Task Turnaround_FourViewsAgainstOne()
    {
        if (Environment.GetEnvironmentVariable("POLSON_TRELLIS_TURNAROUND") != "1")
        {
            this.output.WriteLine("NOT RUN: set POLSON_TRELLIS_TURNAROUND=1. This spends four generations.");
            return;
        }

        if (string.IsNullOrWhiteSpace(this.baseUrl))
        {
            this.output.WriteLine("NOT RUN: set 'Trellis:BaseUrl' to a NIM container.");
            return;
        }

        using var client = new TrellisClient(this.baseUrl, config["ApiKeys:NvidiaNIM"]);

        if (!await client.IsReadyAsync())
        {
            this.output.WriteLine($"NOT RUN: {this.baseUrl} did not answer /v1/health/ready.");
            return;
        }

        var caps = await client.GetCapabilitiesAsync();
        this.output.WriteLine($"profile {caps.Profile ?? "(unknown)"}, modes {caps.ModeList()}, " +
                              $"image array {caps.AcceptsImageArray}, algos [{string.Join(", ", caps.MultiImageAlgorithms)}]");

        //: Refuse early rather than spending four ~90 s waits to be told. A `base:text` container
        //: has no image encoder at all, so there is nothing for this probe to do against one.
        Assert.True(caps.Accepts("image"), $"this variant takes {caps.ModeList()}, not 'image' — load a *:image container");

        var front = Uri("view_front.png");
        var back = Uri("view_back.png");
        var left = Uri("view_left.png");
        var right = Uri("view_right.png");

        (string Name, object Image, string? Algo)[] runs =
        [
            ("1-front-only",        front,                                  null),
            ("2-four-multidiff",    new[] { front, back, left, right },     "multidiffusion"),
            ("3-four-stochastic",   new[] { front, back, left, right },     "stochastic"),
            ("4-reversed-stoch",    new[] { right, left, back, front },     "stochastic")
        ];

        var rows = new List<string>();
        foreach (var (name, image, algo) in runs)
        {
            var result = await client.GenerateAsync(
                new TrellisRequest
                {
                    Mode = "image",
                    Image = image,
                    MultiImageAlgorithm = algo,
                    Seed = Seed,
                    OutputFormat = "glb"
                },
                attempts: 2);

            if (!result.Success)
            {
                this.output.WriteLine($"{name}: FAILED {result.FailureName} — {result.Remedy} {result.Error}");
                rows.Add($"{name,-20} FAILED {result.FailureName}");
                continue;
            }

            var path = Path.Combine(Dir, $"{name}.glb");
            File.WriteAllBytes(path, result.Bytes);

            var m = Measure(result.Bytes);
            rows.Add($"{name,-20} {result.Ms / 1000.0,6:F1}s {result.Bytes.Length,9:N0}B " +
                     $"{m.Verts,7:N0}v {m.Tris,7:N0}t  extents {m.W:F3} x {m.H:F3} x {m.D:F3}  " +
                     $"depth/width {m.Depth / Math.Max(m.Wide, 1e-6):F3}");
        }

        this.output.WriteLine("");
        foreach (var row in rows) this.output.WriteLine(row);

        Assert.All(rows, r => Assert.DoesNotContain("FAILED", r));
    }
    #endregion

    #region Helpers
    static string Uri(string file) =>
        "data:image/png;base64," + Convert.ToBase64String(File.ReadAllBytes(Path.Combine(Dir, file)));

    /// <summary>Walk up to the directory carrying the solution, so the path survives any bin depth.</summary>
    static string RepoRoot()
    {
        var d = new DirectoryInfo(AppContext.BaseDirectory);
        while (d is not null && d.GetFiles("*.sln").Length == 0) d = d.Parent;
        return d?.FullName ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// Extents straight off the GLB's own <c>POSITION</c> accessor bounds — orientation-independent,
    /// so it needs no guess about which axis is up. <b>A generated mesh's orientation is not
    /// guaranteed</b>; the chest came back long along Z, which is why <c>Wide</c> and <c>Depth</c> are
    /// derived by sorting rather than by assuming X is width.
    /// </summary>
    static (int Verts, int Tris, double W, double H, double D, double Wide, double Depth) Measure(byte[] glb)
    {
        //: 12-byte header, then length-prefixed chunks; the first is JSON. Parsed by hand because
        //: this project does not reference a glTF library from the ExtendedMind side.
        var jsonLength = BitConverter.ToInt32(glb, 12);
        var json = Encoding.UTF8.GetString(glb, 20, jsonLength);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var accessors = root.GetProperty("accessors");

        double[] lo = [double.MaxValue, double.MaxValue, double.MaxValue];
        double[] hi = [double.MinValue, double.MinValue, double.MinValue];
        int verts = 0, tris = 0;

        foreach (var mesh in root.GetProperty("meshes").EnumerateArray())
        {
            foreach (var prim in mesh.GetProperty("primitives").EnumerateArray())
            {
                var pos = accessors[prim.GetProperty("attributes").GetProperty("POSITION").GetInt32()];
                verts += pos.GetProperty("count").GetInt32();
                if (prim.TryGetProperty("indices", out var idx))
                {
                    tris += accessors[idx.GetInt32()].GetProperty("count").GetInt32() / 3;
                }

                for (var i = 0; i < 3; i++)
                {
                    lo[i] = Math.Min(lo[i], pos.GetProperty("min")[i].GetDouble());
                    hi[i] = Math.Max(hi[i], pos.GetProperty("max")[i].GetDouble());
                }
            }
        }

        double[] span = [hi[0] - lo[0], hi[1] - lo[1], hi[2] - lo[2]];

        //: glTF is Y-up, so height is anchored; of the two horizontal axes the longer is width.
        var horizontal = new[] { span[0], span[2] }.OrderByDescending(v => v).ToArray();
        return (verts, tris, span[0], span[1], span[2], horizontal[0], horizontal[1]);
    }
    #endregion
}
