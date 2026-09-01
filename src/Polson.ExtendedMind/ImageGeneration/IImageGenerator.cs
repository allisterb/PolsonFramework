namespace Polson.ExtendedMind.ImageGeneration;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// The transport seam beneath <see cref="AssetRequisitionToolkit"/>: everything the requisition
/// surface needs from a cloud image model, and nothing else.
/// </summary>
/// <remarks>
/// <para>
/// This exists so the <b>successful</b> requisition path can be tested. Every failure path is free
/// to exercise — a refusal never reaches the network, a bad key fails at the service — but a success
/// bills for an image, so the one path that produces an asset was the one path with no offline test.
/// A fake implementation returning fixed bytes covers the whole of what happens *after* generation:
/// budget spend, cache write, tiling measurement and repair, provenance, and the run-record event.
/// </para>
/// <para>
/// Deliberately narrow. <see cref="ImageGenerator"/> also owns model probing, PNG header reading and
/// the content hash, and none of those are the seam — the hash in particular stays static on the
/// concrete type, because a substitutable cache key would let two implementations disagree about
/// what "the same request" means.
/// </para>
/// <para>
/// Not exposed to the JavaScript sandbox, for the reason given on <see cref="ImageGenerator"/>: an
/// unconstrained generate call would let an agent buy a finished picture, leaving no code trace for
/// another agent to read.
/// </para>
/// </remarks>
public interface IImageGenerator
{
    /// <summary>Model used when a call does not name one.</summary>
    string Model { get; }

    /// <summary>
    /// Generates one image. Implementations must not throw for a remote fault; the outcome is
    /// carried in <see cref="ImageGenerationResult.Success"/> and <see cref="ImageGenerationResult.Failure"/>.
    /// </summary>
    Task<ImageGenerationResult> GenerateImage(
        string prompt,
        string? model = null,
        string? aspectRatio = null,
        IReadOnlyList<byte[]>? conditionOn = null,
        CancellationToken ct = default);
}
