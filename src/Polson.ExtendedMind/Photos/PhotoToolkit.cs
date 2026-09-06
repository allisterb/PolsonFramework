namespace Polson.ExtendedMind.Photos;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Polson;

/// <summary>
/// The reference-photograph surface, exposed to scripts as <c>Photo</c>.
/// </summary>
/// <remarks>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>Photo.of(...)</c> reaches <see cref="Of"/>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </para>
/// <para>
/// <b>Why this is not a generic image fetcher.</b> Three things stand between a name and bytes, and
/// each of them is a decision an agent under goal pressure would otherwise skip. <i>Identity</i>:
/// resolving to the wrong subject is the failure that looks like success, so an ambiguous name is
/// refused and <see cref="PhotoOptions.Expect"/> lets a caller assert what it thinks it is asking
/// for. <i>Terms</i>: a photograph with no stated licence cannot be credited or entered in a ledger,
/// so it is refused by default rather than delivered and forgotten. <i>Provenance</i>: what does
/// arrive carries its licence, its photographer and any non-copyright restriction, and
/// <see cref="PhotoAsset.CreditLine"/> renders them.
/// </para>
/// <para>
/// <b>These are photographs of real people and places, and that is the point of the constraints.</b>
/// A likeness is not raw material in the sense <c>Assets.material(...)</c> means — it depicts
/// someone, the depiction is someone else's work, and the subject may hold rights of their own that
/// no copyright licence settles (see <see cref="PhotoLicence.Restrictions"/>). The gates here are
/// what let the studio use one honestly.
/// </para>
/// <para>
/// The counterpart to <c>Assets</c>, deliberately: same failure-as-a-value contract, same readable
/// budget, same shared library. Where <c>Assets</c> <i>generates</i> substance that code turns into
/// form, this <i>retrieves</i> a likeness that code cannot synthesise and must not invent.
/// </para>
/// </remarks>
public class PhotoToolkit : Runtime
{
    #region Constructors
    /// <param name="source">Null disables the surface; every call returns <see cref="PhotoFailure.NotConfigured"/>.</param>
    /// <param name="budget">Delivered photographs allowed this run. Resolution is never charged.</param>
    /// <param name="requester">Agent role, for attribution in the run record.</param>
    public PhotoToolkit(IPhotoSource? source, PhotoBudget budget, string requester = "unknown")
    {
        ArgumentNullException.ThrowIfNull(budget);

        this.source = source;
        this.requester = requester;
        Budget = budget;
    }
    #endregion

    #region Properties
    /// <summary>Remaining allowance, readable by scripts so an agent can plan rather than hit a wall.</summary>
    public PhotoBudget Budget { get; }

    /// <summary>
    /// Every photograph delivered this session, by any agent.
    /// </summary>
    /// <remarks>
    /// Shared for the reason <c>Assets.library</c> is: a second agent that cannot see the first
    /// one's material re-fetches a near-duplicate and the piece reads as a collage. It is also where
    /// a credits block is assembled from — every attribution the finished artwork owes is in here.
    /// </remarks>
    public IReadOnlyList<PhotoAsset> Library => library;

    /// <summary>Whether a source is configured at all. False means every call will refuse.</summary>
    public bool IsAvailable => source is not null;
    #endregion

    #region Methods
    /// <summary>
    /// Resolves a name to a subject and its terms <b>without fetching pixels</b>.
    /// <c>Photo.resolve('Zendaya', { expect: 'actress' })</c>.
    /// </summary>
    /// <remarks>
    /// Free, and never charged against the budget. Use it to check identity or licence before
    /// committing — <c>subject.description</c> says what was found, <c>subject.alternatives</c> says
    /// what else it could have been, and <c>subject.licence</c> carries the terms.
    /// </remarks>
    public async Task<SubjectMatch> Resolve(string subject, PhotoOptions? options = null)
    {
        if (source is null)
        {
            return new SubjectMatch
            {
                Query = subject ?? string.Empty,
                Failure = PhotoFailure.NotConfigured,
                Error = "No photograph source is configured.",
            };
        }

        return await source.Resolve(subject ?? string.Empty, options ?? new PhotoOptions());
    }

    /// <summary>
    /// Delivers a reference photograph. <c>await Photo.of('Zendaya', { expect: 'actress', width: 600 })</c>.
    /// </summary>
    /// <remarks>
    /// Pipeline, in order — note that everything refusable is refused before the budget is touched:
    /// <list type="number">
    /// <item>Resolve the subject. Ambiguous names and failed <see cref="PhotoOptions.Expect"/>
    /// assertions stop here, costing nothing.</item>
    /// <item>Check the terms. An unstated licence, or one outside
    /// <see cref="PhotoOptions.RequireLicence"/>, stops here too.</item>
    /// <item>Session cache lookup on (source, file, width). A hit costs nothing and is not charged.</item>
    /// <item>Charge the budget, then fetch. The source decodes what arrives rather than trusting the
    /// Content-Type, so undecodable bytes are a named failure and never a blank picture.</item>
    /// <item>Record the delivery in the library and the run record.</item>
    /// </list>
    /// </remarks>
    public async Task<PhotoAsset> Of(string subject, PhotoOptions? options = null)
    {
        var query = (subject ?? string.Empty).Trim();
        var opts = options ?? new PhotoOptions();

        if (source is null)
        {
            return Refuse(query, PhotoFailure.NotConfigured, "No photograph source is configured.",
                new SubjectMatch { Query = query, Failure = PhotoFailure.NotConfigured });
        }

        var match = await source.Resolve(query, opts);
        if (!match.Success)
        {
            // A resolution failure is a refusal, not a spend: nothing was fetched and the remedy is
            // to reword or choose another subject rather than to retry.
            return Refuse(query, match.Failure, match.Error, match);
        }

        if (LicenceRefusal(match.Licence, opts) is (PhotoFailure failure, string reason))
        {
            return Refuse(query, failure, reason, match);
        }

        var width = WikimediaPhotoSource.ClampWidth(opts.Width);
        var id = Address(source.Name, match.File ?? query, width);

        if (cache.TryGetValue(id, out var cached))
        {
            Budget.CacheHits += 1;
            RequisitionScope.Record(new RequisitionRecord(
                "photo", query, Success: true, Failure: null, Reason: null,
                Model: source.Name, FromCache: true, Refused: false));

            return cached with { FromCache = true };
        }

        if (!Budget.CanAfford())
        {
            return Refuse(query, PhotoFailure.BudgetExhausted,
                $"The photograph allowance is spent ({Budget.Spent} of {Budget.Total}).", match);
        }

        // Charged before the call, not after. A fetch that fails still consumed the host's request,
        // and an allowance that only counts successes is one an unlucky run can overrun without limit.
        Budget.Spent += 1;

        var fetched = await source.Fetch(match, opts);
        if (!fetched.Success)
        {
            RequisitionScope.Record(new RequisitionRecord(
                "photo", query, Success: false, Failure: fetched.Failure.ToString(),
                Reason: fetched.Error, Model: source.Name, FromCache: false, Refused: false));

            Warn("Photograph fetch for {Subject} failed: {Failure} — {Error}",
                query, fetched.Failure, fetched.Error ?? "(no detail)");

            return new PhotoAsset
            {
                Subject = match,
                Licence = match.Licence,
                Source = source.Name,
                Failure = fetched.Failure,
                Error = fetched.Error,
            };
        }

        Budget.BytesFetched += fetched.Bytes.Length;

        var asset = new PhotoAsset
        {
            Success = true,
            Id = id,
            Bytes = fetched.Bytes,
            Width = fetched.Width,
            Height = fetched.Height,
            MimeType = fetched.MimeType,
            Subject = match,
            Licence = match.Licence,
            Source = source.Name,
            SourceUrl = fetched.SourceUrl,
            FetchedUtc = DateTime.UtcNow,
            Requester = requester,
        };

        cache[id] = asset;
        library.Add(asset);

        RequisitionScope.Record(new RequisitionRecord(
            "photo", query, Success: true, Failure: null,
            Reason: null, Model: source.Name, FromCache: false, Refused: false));

        Info("Photograph {Subject} -> {Title}, {Width}x{Height}, {Licence}, {Remaining} left",
            query, match.Title ?? "?", fetched.Width, fetched.Height,
            match.Licence?.Name ?? "(no licence)", Budget.Remaining);

        return asset;
    }

    /// <summary>
    /// Every distinct credit the artwork owes, ready to set as a block.
    /// </summary>
    /// <remarks>
    /// A convenience that exists because forgetting it is the likely failure: attribution is owed
    /// per photograph, the obligation is easy to satisfy and easy to overlook, and an agent that has
    /// to assemble the list by hand will sometimes not.
    /// </remarks>
    public string[] Credits() => [.. library
        .Select(a => a.CreditLine())
        .Where(line => line.Length > 0)
        .Distinct(StringComparer.Ordinal)];
    #endregion

    #region Fields
    private readonly IPhotoSource? source;
    private readonly string requester;
    private readonly List<PhotoAsset> library = [];
    private readonly Dictionary<string, PhotoAsset> cache = [];
    #endregion

    #region Private methods
    /// <summary>The terms gate. Returns null when the licence is acceptable.</summary>
    private static (PhotoFailure, string)? LicenceRefusal(PhotoLicence? licence, PhotoOptions options)
    {
        if (licence is null || !licence.IsStated)
        {
            return options.AllowUnstatedLicence
                ? null
                : (PhotoFailure.LicenceUnstated, "The source states no licence for this file.");
        }

        if (options.RequireLicence is not { Length: > 0 } allowed) return null;

        var name = licence.Name!;
        return allowed.Any(a => Matches(name, a))
            ? null
            : (PhotoFailure.LicenceNotAllowed,
               $"'{name}' is not among the allowed licences ({string.Join(", ", allowed)}).");
    }

    /// <summary>
    /// Whether a licence name satisfies an allowed prefix, matched at a word boundary.
    /// </summary>
    /// <remarks>
    /// <b>A bare <c>StartsWith</c> is wrong here, and wrong in the direction that matters.</b>
    /// <c>"CC BY-SA 4.0".StartsWith("CC BY")</c> is <c>true</c> — the hyphen does not break a prefix
    /// — so a caller who allowed <c>CC BY</c> precisely to avoid a share-alike obligation would have
    /// been handed exactly what they excluded, silently and with a correct-looking credit line. So
    /// the prefix must be followed by nothing at all or by a space: <c>CC BY 3.0</c> passes,
    /// <c>CC BY-SA 4.0</c> and <c>CC BY-NC 2.0</c> do not, and <c>CC BY-SA</c> can still be allowed
    /// by naming it.
    /// </remarks>
    private static bool Matches(string name, string allowed)
    {
        if (allowed.Length == 0 || name.Length < allowed.Length) return false;
        if (!name.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) return false;

        return name.Length == allowed.Length || char.IsWhiteSpace(name[allowed.Length]);
    }

    /// <summary>
    /// A refusal: nothing fetched, nothing charged.
    /// </summary>
    /// <remarks>
    /// Recorded with <c>Refused: true</c> so the run record distinguishes "we declined this" from
    /// "the source failed us" — a run that spent its time rewording queries is a different run from
    /// one the network kept dropping.
    /// <para>
    /// The budget is deliberately <b>not</b> pushed to <see cref="RequisitionScope.RecordBudget"/>:
    /// that slot is single and last-one-wins, and it belongs to the generation allowance. A photo
    /// budget written there would overwrite what a run spent on assets.
    /// </para>
    /// </remarks>
    private PhotoAsset Refuse(string query, PhotoFailure failure, string? reason, SubjectMatch match)
    {
        RequisitionScope.Record(new RequisitionRecord(
            "photo", query, Success: false, Failure: failure.ToString(), Reason: reason,
            Model: source?.Name, FromCache: false, Refused: true));

        return new PhotoAsset
        {
            Subject = match,
            Licence = match.Licence,
            Source = source?.Name ?? string.Empty,
            Failure = failure,
            Error = reason,
        };
    }

    /// <summary>Content address of a delivery. Stable across runs, so it can key a durable cache later.</summary>
    private static string Address(string source, string file, int width)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{source} {file} {width}"));
        return Convert.ToHexStringLower(bytes)[..16];
    }
    #endregion
}
