namespace Polson.Drawing.Mesh;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

/// <summary>
/// The second rig a character build makes: Mesh2Motion's human skeleton fitted into the reconstructed body, and its
/// skin weights solved locally. No GPU and no rig service.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why a second rig.</b> UniRig needs the GPU machine, so without it a build had no rig at all. And the two fail
/// differently, which is the reason to keep both rather than fall back from one to the other: UniRig's long garments
/// move as rigid tubes and slabs, and this one's seams stretch more and can pull a curtain of torso after an arm
/// hanging near the thigh. See <c>docs/internal/character-rigging-modes.md</c> §2 and §4.
/// </para>
/// <para>
/// <b>What it takes.</b> The template, <c>Library/rigs/rig-human.glb</c> (Mesh2Motion, CC0, fetched by
/// <c>tools/bootstrap.py</c>), and the body detector, which finds the joints in a front render. The bones are the
/// template's own, so the body-part names are known without detecting anything: <see cref="CharacterStock.Parts"/>.
/// Weights are diffused over 12 steps with the arm plane on, the settings measured best on lastlight3.
/// </para>
/// </remarks>
public static class SolverRig
{
    #region Fields
    static string? _template;
    #endregion

    #region Properties
    /// <summary>An explicit path to the rig template, or null to use the library's.</summary>
    public static string? TemplateOverride
    {
        get => _template;
        set => _template = value;
    }

    /// <summary>The template that will be used, or null when none is here.</summary>
    public static string? Template => FaceDetector.Pick(_template, [Path.Combine(AppContext.BaseDirectory, "Library", "rigs", TemplateFile),
        .. FaceDetector.Candidates("models", "rigs/" + TemplateFile)]);

    /// <summary>Whether the solver can rig here: the template and the body detector.</summary>
    public static bool Available => Template is not null && BodyDetector.Available;

    /// <summary>What is missing, or null when nothing is.</summary>
    public static string? Missing
    {
        get
        {
            List<string> gaps = [];
            if (Template is null) gaps.Add($"Library/rigs/{TemplateFile} (tools/bootstrap.py)");
            if (BodyDetector.Missing is { } body) gaps.Add(body);
            return gaps.Count == 0 ? null : string.Join(", ", gaps);
        }
    }
    #endregion

    #region Methods
    /// <summary>Rigs the unrigged body at <paramref name="meshPath"/>, a GLB standing on the floor.</summary>
    /// <param name="notes">Where anything worth saying about the fit goes.</param>
    public static SolverRigResult Rig(string meshPath, List<string> notes)
    {
        ArgumentNullException.ThrowIfNull(notes);
        if (!Available) throw new InvalidOperationException($"The solver rig needs {Missing}.");

        var sw = Stopwatch.StartNew();
        var template = SkeletonFit.LoadTemplate(Template!);
        var body = MeshGltf.Load(meshPath, Path.GetFileName(meshPath));
        var fit = SkeletonFit.Fit(body, template);
        try
        {
            notes.AddRange(fit.Warnings.Select(w => "Solver rig: " + w));
            var glb = SkeletonFit.Rig(meshPath, fit, template, armPlane: true, diffuse: Diffusion);
            return new SolverRigResult(glb, fit.FrontSign, new Dictionary<string, string>(CharacterStock.Parts, StringComparer.Ordinal),
                                       body.VertexCount, (int)sw.ElapsedMilliseconds);
        }
        finally
        {
            fit.Render?.Dispose();
        }
    }
    #endregion

    #region Constants
    /// <summary>The template's file name.</summary>
    public const string TemplateFile = "rig-human.glb";

    /// <summary>Weight diffusion steps: 12 was measured best on lastlight3 (§2a of the design doc).</summary>
    const int Diffusion = 12;
    #endregion
}

/// <summary>A solved rig: the rigged GLB, which way the body faces, its body-part names, and what it cost.</summary>
public sealed record SolverRigResult(byte[] Glb, int FrontSign, Dictionary<string, string> Joints, int Vertices, int Ms);
