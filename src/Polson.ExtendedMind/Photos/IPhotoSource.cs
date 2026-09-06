namespace Polson.ExtendedMind.Photos;

using System.Threading;
using System.Threading.Tasks;

/// <summary>
/// Where reference photographs come from.
/// </summary>
/// <remarks>
/// <para>
/// Split in two on purpose. <see cref="Resolve"/> answers "which subject, and on what terms" without
/// moving any pixels; <see cref="Fetch"/> spends the bandwidth. That split is what lets the identity
/// and licence gates run <i>before</i> anything is charged, and it lets an agent check a name as
/// often as it likes for free.
/// </para>
/// <para>
/// An interface rather than a concrete client so the toolkit's gates, budget and run-record
/// behaviour can be tested without a network, and so a second source can be added behind the same
/// contract. Note that adding one is not merely more coverage: a source that cannot report a
/// licence — an image search, say — cannot satisfy <see cref="PhotoLicence"/> honestly, and would
/// have to return <see cref="PhotoFailure.LicenceUnstated"/> for everything it finds.
/// </para>
/// </remarks>
public interface IPhotoSource
{
    /// <summary>Short identifier recorded on every delivery, e.g. <c>wikimedia</c>.</summary>
    string Name { get; }

    /// <summary>Resolves a name to a subject, a lead photograph and its terms. Fetches no pixels.</summary>
    Task<SubjectMatch> Resolve(string subject, PhotoOptions options, CancellationToken cancellationToken = default);

    /// <summary>Fetches the bytes for an already-resolved match.</summary>
    Task<PhotoFetch> Fetch(SubjectMatch match, PhotoOptions options, CancellationToken cancellationToken = default);
}

/// <summary>Raw outcome of a byte fetch, before the toolkit dresses it as a <see cref="PhotoAsset"/>.</summary>
public sealed record PhotoFetch
{
    public bool Success { get; init; }

    public byte[] Bytes { get; init; } = [];

    public int Width { get; init; }

    public int Height { get; init; }

    public string MimeType { get; init; } = "image/jpeg";

    public string? SourceUrl { get; init; }

    public PhotoFailure Failure { get; init; } = PhotoFailure.None;

    public string? Error { get; init; }
}
