namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Polson.Animation;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;
using Polson.ExtendedMind.DocumentProcessing;
using Polson.ExtendedMind.ParallelSearch;
using SkiaSharp;
using Svg;

/// <summary>
/// Checks a finished piece against what it set and where that came from: <c>Verify.film(comp, video)</c> for a film,
/// <c>Verify.image(canvas)</c> for a still.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three things, and only one is a model's.</b> What the piece shows is read from the piece itself — every text a
/// composition sets and the seconds it is on screen (<c>comp.texts()</c>), or every line a canvas or paper drew and where.
/// Where each came from is looked up mechanically — the brief, the pages of the project's documents, the session's Qloo
/// answers and research, or a figure the script says it computed. Only the reading is a model's: Nemotron transcribes the
/// encoded film or the rendered image, and the transcription is compared with the ground truth. A text set but not read
/// is <b>missed</b> — usually too brief or too small; a reading of something never set is a <b>stray</b>, either the
/// watcher misreading or a real fault in the piece (a half-drawn figure, a frame of the wrong scene, a label painted over).
/// </para>
/// <para>
/// <b>A stray is not reported until it has been looked at.</b> The watcher misread $974M as $744M on a report film, and
/// the frame showed $974M. So each stray's frame (or, on a still, a crop around the line it resembles) is saved, and the
/// stray stays <i>pending</i> until the agent has looked and called it with <c>Verify.stray(n, 'misread' | 'real')</c>.
/// The studio shows a stray only then.
/// </para>
/// <para>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase spelling onto them,
/// so a script calling <c>Verify.film(...)</c> reaches <c>Film(...)</c>.
/// </para>
/// </remarks>
public sealed partial class VerifyToolkit
{
    #region Constructors
    /// <param name="video">The watcher; unconfigured, a check answers why.</param>
    /// <param name="projectRoot">Where the piece, the brief and the documents are; nothing outside it is read or written.</param>
    /// <param name="session">Holds the Qloo answers and research this session fetched, and the latest check for <see cref="Stray"/>.</param>
    /// <param name="record">Records <c>film.verify</c> / <c>image.verify</c> and their strays for the studio's Verification view.</param>
    /// <param name="facts">Checks the <c>sources</c> option's stated facts on the web; null and they are recorded as unchecked.</param>
    public VerifyToolkit(VideoReader? video, string? projectRoot, SessionContext? session,
        Action<string, IReadOnlyDictionary<string, object?>>? record = null, FactChecker? facts = null)
    {
        this.video = video;
        this.projectRoot = projectRoot;
        this.session = session;
        this.record = record;
        this.facts = facts;
    }
    #endregion

    #region Methods
    /// <summary>
    /// The film checked against what it set and where that came from: <c>{ ok, passed, items, strays, counts, summary }</c>,
    /// or <c>{ ok: false, failureName, error, remedy }</c> when the watcher could not watch (<c>Pending</c>: run it again
    /// unchanged). <paramref name="options"/>: <c>{ computed?: { 'text as set': 'its working' } }</c> for figures the script
    /// computed from sourced ones; <c>{ sources?: { 'text as set': { about, claim } } }</c> for a fact no document holds,
    /// checked on the web.
    /// </summary>
    public async Task<Dictionary<string, object?>> Film(MotionComposition comp, string videoPath, object? options = null)
    {
        ArgumentNullException.ThrowIfNull(comp);
        if (string.IsNullOrWhiteSpace(videoPath)) throw new ArgumentException("Verify.film needs the encoded film: Verify.film(comp, 'output.mp4').");
        var (computed, _, ignored, scale, stated) = Options(options, "Verify.film", allowName: false);
        if (video is null || !video.IsAvailable)
            return Failed("NotConfigured", "No reader is configured, so nothing can watch the film.", NoReader);

        var full = ProjectPath.Resolve(projectRoot, videoPath, "video", "Read");
        if (!File.Exists(full)) throw new FileNotFoundException($"Verify.film: '{videoPath}' is not in the project. Encode it first.", full);

        // A film over the watcher's limit is watched as a smaller copy. becool2's grainy 1080 master was 65 MB, and the agent
        // made a 634 × 356 copy by hand after two refusals (2026-10-25). Strays' frames are still cut from the film itself.
        var watched = videoPath;
        long? over = null;
        var limit = video.MaxClipBytes;
        if (new FileInfo(full).Length is var size && size > limit)
        {
            watched = $"artifacts/verify/{Path.GetFileNameWithoutExtension(full)}-watch.mp4";
            if (FFmpeg.WatchCopy(full, comp.Duration, ProjectPath.Resolve(projectRoot, watched, "video", "Write"), WatchHeight, limit) is { } why)
                return Failed("TooLarge", $"{videoPath} is {Megabytes(size)} MB, over the watcher's {Megabytes(limit)} MB, "
                    + $"and no smaller copy could be made: {why}", "Encode a shorter film, or check it in parts encoded as separate files.");
            over = size;
        }

        var answer = await video.Ask(watched, Question(comp.Duration), new DocumentOptions { Schema = Schema(comp.Duration) });
        if (!answer.Success)
            return Failed(answer.FailureName, answer.Error, answer.FailureName == "Pending"
                ? "The watcher is still reading; it finishes into the cache. Run the same call again, unchanged."
                : answer.Remedy);

        // Compared as prepared (a LaTeX reading as its linear form, a split equation joined), shown as read.
        var parsed = FilmReadBack.ParseReading(answer.Text);
        var prepared = MathReading.Prepare([.. parsed.Select(r => r.Text)]);
        var read = prepared.Select(p => new FilmReadBack.Reading(p.Compare, parsed[p.First].From, parsed[p.Last].To)).ToArray();
        var rawOf = prepared.GroupBy(p => p.Compare).ToDictionary(g => g.Key, g => g.First().Raw);
        string? Raw(string? compared) => compared is null ? null : rawOf.GetValueOrDefault(compared, compared);
        var texts = comp.Texts().OfType<Dictionary<string, object?>>().ToArray();
        var expected = texts
            .Select(t => new FilmReadBack.Shown((string)t["text"]!, t["from"] as double?, t["to"] as double?, t["seconds"] as double? ?? 0,
                [.. ((object?[])t["intervals"]!).OfType<double[]>()]))
            .ToArray();
        var sources = new FilmSources(projectRoot, session);
        // Read in the unit the texts best match, should the watcher have answered in another (see Question).
        var timeScale = FilmReadBack.TimeScale(expected, read, comp.Duration);
        if (timeScale != 1)
            read = [.. read.Select(r => r with { From = r.From * timeScale, To = r.To * timeScale })];
        var match = FilmReadBack.Match(expected, read);
        // A counting number caught mid-count is on the film, though not a claim. The watcher samples frames of its own, so it
        // reads values no frame of ours drew exactly ($380M and $924M on a count to $974M, 2026-10-23): a reading counts as
        // mid-count when it is one figure, written in the number's format, in its range, while it counted.
        var counting = comp.Counting();
        bool MidCount(FilmReadBack.Reading r) =>
            FilmReadBack.MatchFigures(r.Text) is [var figure]
            && double.TryParse(figure, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && counting.Any(c => c.Covers(value, r.From, FilmReadBack.Slack)
                && FilmReadBack.Normal(FilmReadBack.Canon(c.Format.Format(value))) == FilmReadBack.Normal(FilmReadBack.Canon(r.Text)));
        // A citation (`desc: 'source…'`) is where a figure came from, not a claim on screen, and is set at footnote size: the
        // watcher misreads small type ("2026" as "2025"), and a page number read wrong is the watcher's error, not the
        // film's. So it is neither graded nor raised as a stray; the figures it cites are checked as themselves.
        var citations = texts.Select(t => t.GetValueOrDefault("desc") is string d && d.TrimStart().StartsWith("source", StringComparison.OrdinalIgnoreCase)).ToArray();
        var cited = expected.Where((_, i) => citations[i]).Select(e => e.Text).ToHashSet();
        match = match with { Strays = [.. match.Strays.Where(s => !MidCount(s.Reading) && !(s.Near is { } near && cited.Contains(near)))] };

        var items = expected.Select((e, i) =>
        {
            // An axis label comp.axes drew is a ruler, not a claim: sourced as a scale without being declared.
            var source = citations[i] ? (Kind: "citation", Detail: (string?)"source line")
                : texts[i].GetValueOrDefault("kind") as string is "tick" or "axis" ? (Kind: "scale", Detail: (string?)"axis label")
                : sources.Classify(e.Text, computed, scale);
            var seen = match.Seen[i];
            return new Dictionary<string, object?>
            {
                ["text"] = e.Text,
                ["from"] = e.From,
                ["to"] = e.To,
                ["seconds"] = e.Seconds,
                ["source"] = source.Kind,
                ["detail"] = source.Detail,
                ["seen"] = seen is not null,
                ["readAs"] = Raw(seen?.Text),
                ["readAt"] = seen?.From,
                ["status"] = e.From is null ? "never shown" : citations[i] ? "citation" : seen is not null ? "seen"
                    : FilmReadBack.Unreadable(e.Text, Nemotron) ? "unchecked" : "missed",
            };
        }).ToArray();

        var strays = match.Strays.Select((s, i) =>
        {
            var at = Math.Clamp((s.Reading.From ?? 0) + 0.25, 0, comp.Duration);
            // Named for the film: becool3 checked films B then C, and C's `stray-1.png` overwrote B's before it was looked at.
            var frame = $"artifacts/verify/{Path.GetFileNameWithoutExtension(full)}-stray-{i + 1}.png";
            var error = FFmpeg.Frame(full, at, ProjectPath.Resolve(projectRoot, frame, "frame", "Write"));
            return new ReadBackStray(i + 1, Raw(s.Reading.Text)!, s.Reading.From, error is null ? frame : null, error, s.Near,
                FilmReadBack.Unreadable(s.Reading.Text, Nemotron) ? "unreadable script" : s.Kind);
        }).ToList();

        await Attest(items, stated, t => sources.Wholly(t, computed, scale));
        return Hold(new FilmCheck(videoPath, comp.Duration, items, strays, answer.Provenance?.Model, answer.Provenance?.Endpoint)
            { Hash = Hash(full), Ignored = ignored, UnreadableScripts = Scripts, TimeScale = timeScale, Watched = over is null ? null : watched, FilmBytes = over, Limit = limit });
    }

    /// <summary>
    /// A still checked the same way: every line of text <paramref name="source"/> drew (a canvas, its context, or a paper),
    /// where each came from, and whether a reader of the rendered image read it as set. The image read is the source's
    /// pixels now, saved as <c>artifacts/verify/&lt;name&gt;.png</c>; so check the piece as delivered, after everything is
    /// drawn. <paramref name="options"/>: <c>{ computed?, sources?, name? }</c>, <c>name</c> defaulting to <c>'image'</c>.
    /// </summary>
    public async Task<Dictionary<string, object?>> Image(object source, object? options = null)
    {
        var (computed, name, ignored, scale, stated) = Options(options, "Verify.image", allowName: true);
        var still = Still.Of(source);
        if (video is null || !video.IsAvailable)
            return Failed("NotConfigured", "No reader is configured, so nothing can read the image.", NoReader);

        var imagePath = $"artifacts/verify/{name}.png";
        var full = ProjectPath.Resolve(projectRoot, imagePath, "image", "Write");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        using (var bitmap = still.Flattened())
        using (var data = bitmap.Encode(SKEncodedImageFormat.Png, 100))
            File.WriteAllBytes(full, data.ToArray());

        var answer = await video.Ask(imagePath, ImageQuestion, new DocumentOptions { Schema = ImageSchema, MimeType = "image/png" });
        if (!answer.Success)
            return Failed(answer.FailureName, answer.Error, answer.FailureName == "Pending"
                ? "The reader is still reading; it finishes into the cache. Run the same script again, unchanged."
                : answer.Remedy);

        // Compared as prepared (a LaTeX reading as its linear form), shown as read.
        var prepared = MathReading.Prepare([.. FilmReadBack.ParseReading(answer.Text).Select(r => r.Text)]);
        var read = prepared.Select(p => p.Compare).ToArray();
        var rawOf = prepared.GroupBy(p => p.Compare).ToDictionary(g => g.Key, g => g.First().Raw);
        string? Raw(string? compared) => compared is null ? null : rawOf.GetValueOrDefault(compared, compared);
        var page = SKRect.Create(0, 0, still.Width, still.Height);
        var lines = still.Lines.Where(l => FilmReadBack.Normal(l.Text).Length > 0).ToArray();
        var shown = lines.GroupBy(l => FilmReadBack.Normal(l.Text))
            .Select(g => (Text: g.First().Text.Trim(), Lines: g.ToArray(), On: g.Where(l => l.Box.IntersectsWith(page)).ToArray()))
            .ToArray();
        var onCanvas = shown.Where(s => s.On.Length > 0).ToArray();
        var match = ImageReadBack.Match([.. onCanvas.Select(s => s.Text)], read);
        var seenOf = onCanvas.Select((s, i) => (s.Text, Seen: match.Seen[i], Closest: match.Closest[i])).ToDictionary(x => x.Text);
        var sources = new FilmSources(projectRoot, session);

        var items = shown.Select(s =>
        {
            var src = sources.Classify(s.Text, computed, scale);
            var at = s.On.FirstOrDefault() ?? s.Lines[0];
            var (_, seen, closest) = seenOf.GetValueOrDefault(s.Text);
            return new Dictionary<string, object?>
            {
                ["text"] = s.Text,
                ["count"] = s.Lines.Length,
                ["box"] = Box(at.Box),
                ["boxes"] = s.On.Take(MaxBoxes).Select(l => (object?)Box(l.Box)).ToArray(),
                ["size"] = Math.Round(at.Size, 1),
                ["source"] = src.Kind,
                ["detail"] = src.Detail,
                ["seen"] = seen is not null,
                ["readAs"] = Raw(seen),
                ["closest"] = seen is null ? Raw(closest) : null,
                ["status"] = s.On.Length == 0 ? "off the canvas" : seen is not null ? "seen" : FilmReadBack.Unreadable(s.Text, Nemotron) ? "unchecked" : "missed",
            };
        }).ToArray();

        using var pixels = SKBitmap.Decode(File.ReadAllBytes(full));
        var strays = match.Strays.Select((s, i) =>
        {
            // A crop around the line the reading resembles, where to look; otherwise the whole image is where.
            var near = onCanvas.Where(o => o.Text == s.Near).SelectMany(o => o.On).FirstOrDefault();
            var kind = FilmReadBack.Unreadable(s.Reading.Text, Nemotron) ? "unreadable script" : s.Kind;
            if (near is null) return new ReadBackStray(i + 1, Raw(s.Reading.Text)!, null, imagePath, null, s.Near, kind);
            var crop = $"artifacts/verify/{name}-stray-{i + 1}.png";
            var error = Crop(pixels, near.Box, ProjectPath.Resolve(projectRoot, crop, "crop", "Write"));
            return new ReadBackStray(i + 1, Raw(s.Reading.Text)!, null, error is null ? crop : imagePath, error, s.Near, kind);
        }).ToList();

        await Attest(items, stated, t => sources.Wholly(t, computed, scale));
        return Hold(new ImageCheck(imagePath, still.Kind, still.Width, still.Height, items, strays, answer.Provenance?.Model, answer.Provenance?.Endpoint)
            { Hash = Hash(full), Ignored = ignored, UnreadableScripts = Scripts });
    }

    /// <summary>
    /// A derivation checked step by step: each step equals the one before it, as <c>Equation.equivalent</c> decides.
    /// <c>{ ok, passed, name, steps, given, pairs, counts, summary }</c>; each pair is <c>{ from, to, a, b, verdict, detail,
    /// at, values, provided, approximate, read }</c>. <paramref name="steps"/> are LaTeX strings or <c>comp.equation</c>
    /// handles, in order. A leading <c>=</c>, and the <c>&amp;</c> of an aligned line, are dropped, so the lines of a
    /// derivation can be given as written. Two equations in a row are a step of a solution. An equation beside an
    /// expression, or with two <c>=</c>, is a chain and is checked as its sides: <c>steps</c> is then the chain compared,
    /// <c>given</c> the steps as given, and a pair with <c>from</c> equal to <c>to</c> is within a step. When the latest read-back read
    /// the steps, the steps as read are checked too (<c>read</c>), which does not change <c>passed</c>.
    /// <paramref name="options"/>: <c>{ name?, domain?, integers?, samples?, budget? }</c>, <c>budget</c> being the
    /// milliseconds for the whole derivation (15,000).
    /// </summary>
    public Dictionary<string, object?> Derivation(object steps, object? options = null)
    {
        var result = DerivationCheck(steps, options, readBack: true);
        record?.Invoke("derivation.verify", new Dictionary<string, object?>
        {
            ["name"] = result["name"], ["steps"] = result["steps"], ["pairs"] = result["pairs"], ["counts"] = result["counts"],
            ["passed"] = result["passed"],
        });
        record?.Invoke("check", new Dictionary<string, object?>
        {
            ["claim"] = DerivationClaim((string)result["name"]!), ["passed"] = result["passed"], ["detail"] = result["summary"], ["by"] = "verify",
        });
        return result;
    }
    /// <summary>
    /// A stray of the latest check settled after looking at its frame or crop: <paramref name="verdict"/> <c>'misread'</c>
    /// (it shows what the piece set; the reader misread it) or <c>'real'</c> (it shows what was read: a fault in the piece).
    /// Returns the check as it now stands. Refused in the script that ran the check: nothing has been looked at yet.
    /// </summary>
    public Dictionary<string, object?> Stray(int number, string verdict, string? note = null) =>
        Settle((session?.ReadBack ?? last) ?? throw new InvalidOperationException("Verify.stray: run Verify.film or Verify.image first; there is no check to settle."),
            number, verdict, note);

    /// <summary>
    /// A stray of one piece's latest check, whichever was checked last: <paramref name="piece"/> is the film as it was
    /// checked (<c>'artifacts/final_b.mp4'</c>, or <c>'final_b'</c>), or a still's <c>name</c>.
    /// </summary>
    public Dictionary<string, object?> Stray(string piece, int number, string verdict, string? note = null)
    {
        var held = session?.ReadBacks ?? checks;
        var key = Key(piece);
        var check = held.Values.FirstOrDefault(c => Key(c.Name) == key || c.Name.Equals(piece?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"Verify.stray: '{piece}' has not been checked"
                + (held.Count == 0 ? "; run Verify.film or Verify.image first." : $". Checked: {string.Join(", ", held.Values.Select(c => $"'{c.Name}'"))}."));
        return Settle(check, number, verdict, note);
    }
    #endregion

    #region Internal methods
    /// <summary>A piece's name as <c>Verify.stray</c> matches it: its file name without the folder or extension, lower case.</summary>
    internal static string Key(string? name) => Path.GetFileNameWithoutExtension((name ?? "").Trim()).ToLowerInvariant();

    /// <summary>
    /// The derivation check itself, recorded by neither: <see cref="Derivation"/> records it, and a claim's check calls it
    /// from inside <see cref="Claim"/>'s sandbox, where the claim is what is recorded. <paramref name="readBack"/> checks
    /// the steps as the latest read-back read them too.
    /// </summary>
    internal Dictionary<string, object?> DerivationCheck(object steps, object? options, bool readBack)
    {
        var (name, check, budget) = DerivationOptions(options);
        var given = (steps as System.Collections.IEnumerable is { } e && steps is not string ? e.Cast<object?>() : [])
            .Select(s => s switch
            {
                string latex => latex,
                MotionEquation equation => equation.Latex,
                _ => throw new ArgumentException("Verify.derivation's steps are LaTeX strings or comp.equation handles."),
            }).Select(Step).ToArray();
        if (given.Length is < 2 or > MaxSteps)
            throw new ArgumentException($"Verify.derivation needs 2 to {MaxSteps} steps, in order; it was given {given.Length}.");

        // The steps as the latest read-back read them, matched by linear form: read as set, or — misread, and so not seen —
        // the reading closest to it on a still, or the stray recorded beside it on a film.
        var linear = given.Select(s => EquationLayout.LinearFromLatex(s) ?? s).ToArray();
        var latest = readBack ? session?.ReadBack ?? last : null;
        var read = linear.Select(l =>
        {
            var item = latest?.ItemList.FirstOrDefault(i => (string?)i["text"] == l);
            var reading = item?["readAs"] as string ?? item?.GetValueOrDefault("closest") as string
                ?? latest?.Strays.FirstOrDefault(s => s.Near == l)?.Read;
            return reading is null ? null : Step(MathReading.Unwrap(reading));
        }).ToArray();

        // A step that is an equation beside one that is an expression is a chain, `∫₀² x² dx = [x³/3]₀²` then `2³/3 − 0³/3`,
        // and is checked as its sides: each side against the next, so the equation's own claim is checked as well as the
        // continuation. ftcexplainer1's plan wrote one (2026-10-23) and was told an equation and an expression do not compare.
        // A step with two `=` is a chain on its own. Equations among equations stay steps of a solution.
        var sides = given.Select(Sides).ToArray();
        bool Split(int i) => sides[i].Length > 2
            || sides[i].Length == 2 && (i > 0 && sides[i - 1].Length == 1 || i + 1 < given.Length && sides[i + 1].Length == 1);
        var chain = given.SelectMany((s, i) => Split(i) ? sides[i].Select(p => (Latex: p, Step: i)) : [(Latex: s, Step: i)]).ToArray();
        var readChain = given.SelectMany((_, i) => !Split(i) ? [read[i]]
            : read[i] is { } r && Sides(r) is var rs && rs.Length == sides[i].Length ? rs.Select(x => (string?)x)
            : Enumerable.Repeat((string?)null, sides[i].Length)).ToArray();

        var perPair = Math.Clamp(budget / (chain.Length - 1), MinPairMs, EquationCheck.DefaultBudgetMs);
        var options1 = check with { BudgetMs = perPair };
        var pairs = Enumerable.Range(0, chain.Length - 1).Select(i =>
        {
            var v = EquationCheck.Check(chain[i].Latex, chain[i + 1].Latex, options1);
            var pair = Pair(v);
            pair["from"] = chain[i].Step + 1;
            pair["to"] = chain[i + 1].Step + 1;
            pair["read"] = readChain[i] is { } ra && readChain[i + 1] is { } rb ? Pair(EquationCheck.Check(ra, rb, options1)) : null;
            return pair;
        }).ToArray();
        var chained = chain.Select(c => EquationLayout.LinearFromLatex(c.Latex) ?? c.Latex).ToArray();

        var count = (string kind) => pairs.Count(p => (string?)p["verdict"] == kind);
        var failing = pairs.Where(p => (string?)p["verdict"] is not ("proved" or "agrees")).ToArray();
        var readWrong = pairs.Where(p => p["read"] is Dictionary<string, object?> r && (string?)r["verdict"] == "differs").ToArray();
        var summary = $"{given.Length} steps"
            + (chain.Length == given.Length ? "" : $", {chain.Length} expressions with each equation's sides apart")
            + $", {pairs.Length} checked: {count("proved")} proved, {count("agrees")} agree without proof"
            + (count("differs") == 0 ? "" : $", {count("differs")} differ")
            + (count("unsettled") == 0 ? "" : $", {count("unsettled")} unsettled")
            + (count("unread") == 0 ? "" : $", {count("unread")} unread") + "."
            + string.Concat(failing.Take(4).Select(p => $" {Label(p)} {p["verdict"]}: {p["detail"]}{At(p)}"))
            + (failing.Length == 0 ? "" : " Fix the step, or give the domain it holds on.")
            + (readWrong.Length == 0 ? "" : $" As the watcher read them, "
                + $"{string.Join(", ", readWrong.Select(Label))} differ: the reading, not the derivation, is wrong there; see the read-back's strays.");
        var counts = new Dictionary<string, object?>
        {
            ["proved"] = count("proved"), ["agrees"] = count("agrees"), ["differs"] = count("differs"),
            ["unsettled"] = count("unsettled"), ["unread"] = count("unread"),
        };
        var passed = failing.Length == 0;
        return new()
        {
            ["ok"] = true, ["passed"] = passed, ["name"] = name, ["steps"] = chained.Cast<object?>().ToArray(),
            ["given"] = linear.Cast<object?>().ToArray(), ["pairs"] = pairs.Cast<object?>().ToArray(), ["counts"] = counts,
            ["summary"] = summary,
        };

        static string Step(string latex) => AlignedLine().Replace(latex.Replace("&", ""), "").Trim();

        static string Label(Dictionary<string, object?> p) => Equals(p["from"], p["to"]) ? $"Step {p["from"]}'s sides" : $"Step {p["from"]} → {p["to"]}";

        static string At(Dictionary<string, object?> p) => p["at"] is Dictionary<string, object?> at && at.Count > 0
            ? $" (at {string.Join(", ", at.Select(kv => $"{kv.Key} = {kv.Value}"))})"
            : "";

        static Dictionary<string, object?> Pair(EquationCheck.Verdict v) => new()
        {
            ["a"] = v.A, ["b"] = v.B, ["verdict"] = v.Kind, ["detail"] = v.Detail, ["provided"] = v.Provided,
            ["approximate"] = v.Approximate,
            ["at"] = v.At is { Count: > 0 } at ? at.ToDictionary(p => p.Key, p => (object?)p.Value) : null,
            ["values"] = v.ValueA is { } x && v.ValueB is { } y ? new object?[] { Real(x), Real(y) } : null,
        };

        static object Real(System.Numerics.Complex c) => Math.Abs(c.Imaginary) < 1e-12 * Math.Max(1, c.Magnitude)
            ? Math.Round(c.Real, 12) : c.ToString(CultureInfo.InvariantCulture);
    }

    #endregion

    #region Private methods
    /// <summary>Settles one stray of <paramref name="check"/>: refused in the script that ran the check.</summary>
    private Dictionary<string, object?> Settle(ReadBackCheck check, int number, string verdict, string? note)
    {
        var others = (session?.ReadBacks ?? checks).Count > 1;
        var stray = check.Strays.FirstOrDefault(s => s.Number == number)
            ?? throw new ArgumentException($"Verify.stray: there is no stray {number} in '{check.Name}'; its check has {check.Strays.Count}."
                + (others ? " To settle another piece's, name it: Verify.stray('artifacts/final_b.mp4', n, 'misread')." : ""));
        if (madeHere.Contains(check))
            throw new InvalidOperationException($"Verify.stray: look at the {check.Evidence} first. A stray is settled in a later script than the check, "
                + $"after you have seen {stray.Frame ?? check.Name} (return Skia.Image.load('{stray.Frame ?? check.Name}') from a script to see it).");
        var v = verdict?.Trim().ToLowerInvariant();
        if (v is not ("misread" or "real"))
            throw new ArgumentException($"Verify.stray's verdict is 'misread' (the {check.Evidence} shows what the {check.Piece} set) or 'real' (a fault in the {check.Piece}).");
        stray.Verdict = v;
        stray.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        record?.Invoke($"{check.Piece}.stray", new Dictionary<string, object?>
        {
            [check.NameKey] = check.Name, ["n"] = number, ["read"] = stray.Read, ["at"] = stray.At, ["frame"] = stray.Frame,
            ["near"] = stray.Near, ["verdict"] = v, ["note"] = stray.Note,
        });
        var result = check.Result();
        RecordCheck(check, result);
        return result;
    }

    /// <summary>
    /// The check held for <see cref="Stray"/>, recorded, and returned as the script receives it. Checking the same bytes
    /// again keeps the verdicts already given to the same readings, so a re-render that changed nothing is not re-settled.
    /// </summary>
    private Dictionary<string, object?> Hold(ReadBackCheck check)
    {
        var held = session?.ReadBacks ?? checks;
        if (held.GetValueOrDefault(check.Name) is { } before && before.Hash == check.Hash && check.Hash is not null)
            foreach (var s in check.Strays)
                if (before.Strays.FirstOrDefault(b => b.Verdict is not null && b.Read == s.Read && b.Near == s.Near) is { } settled)
                    (s.Verdict, s.Note) = (settled.Verdict, settled.Note);
        held[check.Name] = check;
        if (session is not null) session.ReadBack = check;
        else last = check;
        madeHere.Add(check);
        record?.Invoke($"{check.Piece}.verify", check.Event());
        var result = check.Result();
        RecordCheck(check, result);
        return result;
    }

    /// <summary>
    /// The read-back's verdict as a judged <c>check</c>, recorded by the check itself — as it stands after the reading
    /// and after each stray is settled — so the record's verdict is the one the check reached, not one written for it.
    /// </summary>
    private void RecordCheck(ReadBackCheck check, Dictionary<string, object?> result) =>
        record?.Invoke("check", new Dictionary<string, object?>
        {
            ["claim"] = ReadBackClaim(check.Name), ["passed"] = result["passed"], ["detail"] = result["summary"], ["judged"] = true,
            ["by"] = "verify",
        });

    /// <summary>The claim a read-back records: reserved for <c>Verify</c>; <c>Stage.check</c> refuses it.</summary>
    internal static string ReadBackClaim(string name) => $"{ReadBackPrefix}{name}: every text has a source and was read as set";

    internal const string ReadBackPrefix = "Read-back of ";

    /// <summary>The claim a derivation check records: reserved for <c>Verify</c>; <c>Stage.check</c> refuses it.</summary>
    internal static string DerivationClaim(string name) => $"{DerivationPrefix}{name}: every step follows from the last";

    internal const string DerivationPrefix = "Derivation ";

    /// <summary>The derivation's options: its name, and what <c>Equation.equivalent</c> takes, with a budget for the whole.</summary>
    private static (string Name, EquationCheck.Options Check, int BudgetMs) DerivationOptions(object? options)
    {
        var o = JsInterop.AsDict(options);
        var forCheck = new System.Collections.Hashtable();
        string name = "derivation";
        var budget = DerivationBudgetMs;
        if (o is not null)
            foreach (var key in o.Keys.Cast<object>().Select(k => k.ToString()!))
                switch (key)
                {
                    case "name": name = o[key]?.ToString()?.Trim() is { Length: > 0 } n ? n : name; break;
                    case "budget": budget = (int)Math.Clamp(Convert.ToDouble(o[key], CultureInfo.InvariantCulture), 1000, MaxDerivationBudgetMs); break;
                    case "domain" or "integers" or "samples": forCheck[key] = o[key]; break;
                    default: throw new ArgumentException($"Verify.derivation's options are {{ name, domain, integers, samples, budget }}; '{key}' is not one.");
                }
        return (name, EquationCheck.ReadOptions(forCheck, "Verify.derivation"), budget);
    }

    /// <summary>
    /// LaTeX's sides at each <c>=</c> outside every brace and bracket, so <c>\sum_{k=1}</c> keeps its own; one side for an
    /// expression, and the whole when a side would be empty.
    /// </summary>
    internal static string[] Sides(string latex)
    {
        var parts = new List<string>();
        int depth = 0, start = 0;
        for (var i = 0; i < latex.Length; i++)
        {
            var c = latex[i];
            if (c == '\\')      // a set's \{ … \} brackets; no command contains =
            {
                if (i + 1 < latex.Length) depth += latex[++i] switch { '{' => 1, '}' => -1, _ => 0 };
                continue;
            }
            if (c is '{' or '(' or '[') depth++;
            else if (c is '}' or ')' or ']') depth--;
            else if (c == '=' && depth == 0) { parts.Add(latex[start..i].Trim()); start = i + 1; }
        }
        parts.Add(latex[start..].Trim());
        return parts.Count > 1 && parts.All(p => p.Length > 0) ? [.. parts] : [latex];
    }

    /// <summary>An aligned derivation's line: a leading <c>=</c>, and a trailing <c>\\</c>.</summary>
    [GeneratedRegex(@"^\s*=\s*|\s*\\\\\s*$")]
    private static partial Regex AlignedLine();

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))[..32];

    /// <summary>
    /// The options: <c>computed</c> split into the entries whose working shows arithmetic, which are used, and those whose
    /// working does not (<c>'Period covered from FT methodology'</c>), which are set aside and named in the summary. A
    /// citation is not a computation, and a figure it labels is looked for in its source instead.
    /// </summary>
    private static (Dictionary<string, string> Computed, string Name, string[] Ignored, HashSet<string> Scale, Dictionary<string, (string About, string[] Claims)> Stated)
        Options(object? options, string method, bool allowName)
    {
        var o = JsInterop.AsDict(options);
        if (o is null) return ([], "image", [], [], []);
        var allowed = allowName ? "{ computed, sources, scale, name }" : "{ computed, sources, scale }";
        foreach (var key in o.Keys)
            if (key?.ToString() is not ("computed" or "sources" or "name" or "scale") || (key.ToString() == "name" && !allowName))
                throw new ArgumentException($"{method}'s options are {allowed}; '{key}' is not one.");
        var stated = new Dictionary<string, (string About, string[] Claims)>(StringComparer.Ordinal);
        if (o.Contains("sources") && o["sources"] is not null)
        {
            var given = JsInterop.AsDict(o["sources"])
                ?? throw new ArgumentException($"{method}'s sources map each text as set to the fact it states: {{ sources: {{ '{SourcesExample}' }} }}.");
            foreach (var key in given.Keys.Cast<object>())
            {
                var fact = JsInterop.AsDict(given[key]);
                var about = fact?.Contains("about") == true ? fact["about"]?.ToString()?.Trim() : null;
                // One claim, or a list for a line that states several facts (a billing line: one claim per name).
                string[] claims = fact?.Contains("claim") != true ? [] : fact["claim"] switch
                {
                    string s => [s.Trim()],
                    System.Collections.IEnumerable list => [.. list.Cast<object?>().Select(c => c?.ToString()?.Trim() ?? "")],
                    _ => [],
                };
                if (string.IsNullOrEmpty(about) || claims.Length == 0 || claims.Any(string.IsNullOrEmpty)
                    || fact!.Keys.Cast<object>().Any(k => k.ToString() is not ("about" or "claim")))
                    throw new ArgumentException($"{method}'s sources entry for '{key}' is {{ about, claim }}: what it is about, in English, "
                        + $"and the fact as a full sentence, or a list of them for a line stating several — {{ sources: {{ '{SourcesExample}' }} }}.");
                stated[key.ToString()!.Trim()] = (about, claims);
            }
        }
        HashSet<string> scale = o.Contains("scale") && o["scale"] is System.Collections.IEnumerable labels and not string
            ? [.. labels.Cast<object?>().Select(l => l?.ToString()?.Trim() ?? "").Where(l => l.Length > 0)]
            : o.Contains("scale") && o["scale"] is not null
                ? throw new ArgumentException($"{method}'s scale is a list of the axis labels as drawn: {{ scale: ['0%', '1,000%'] }}.")
                : [];
        var computed = JsInterop.AsDict(o.Contains("computed") ? o["computed"] : null) is { } c
            ? c.Keys.Cast<object>().ToDictionary(k => k.ToString()!, k => c[k]?.ToString() ?? "", StringComparer.Ordinal)
            : [];
        var name = o.Contains("name") ? o["name"]?.ToString() ?? "" : "image";
        if (!SafeName().IsMatch(name))
            throw new ArgumentException($"{method}'s name is a file name for the read image, letters, digits, '-' and '_': 'final_a', not '{name}'.");
        var ignored = computed.Where(c => !Arithmetic().IsMatch(c.Value)).Select(c => c.Key).ToArray();
        foreach (var key in ignored) computed.Remove(key);
        return (computed, name, ignored, scale, stated);
    }

    private const string SourcesExample = "DIRECTED BY F. GARY GRAY': { about: 'Be Cool (2005 film)', claim: 'F. Gary Gray is credited as director in the on-screen credits of Be Cool (2005).' }";

    /// <summary>
    /// Each text the lookup left as <c>own words</c> or <c>unsourced figure</c> that the <c>sources</c> option states a fact
    /// for, checked on the web and recorded as <c>web</c>: the verdict and its sources in <c>detail</c>, the whole check in
    /// <c>web</c>. A text sourced only by its figures keeps that source and has its words checked too, the verdict added to
    /// its <c>detail</c>. A text the brief or a document holds word for word is not sent: the filing outranks the web.
    /// </summary>
    private async Task Attest(Dictionary<string, object?>[] items, Dictionary<string, (string About, string[] Claims)> stated, Func<string, bool> wholly)
    {
        if (stated.Count == 0) return;
        foreach (var item in items)
        {
            var (text, kind) = ((string)item["text"]!, (string?)item["source"]);
            if (!stated.TryGetValue(text.Trim(), out var fact)) continue;
            // Own words are checked and become `web`. A line sourced only by its figures keeps that source, and its words
            // are checked as well: becool5's "2005 · Directed by F. Gary Gray" was sourced by the brief's 2005 alone.
            var own = kind is "own words" or "unsourced figure";
            if (!own && (kind is not ("brief" or "document" or "research" or "computed") || wholly(text))) continue;
            var checks = new List<FactCheck>();
            foreach (var claim in fact.Claims)
                checks.Add(facts is null
                    ? FactCheck.Unchecked(fact.About, claim, "no ApiKeys:Parallel is configured")
                    : await facts.Check(fact.About, claim));
            // A line stating several facts is as good as its weakest: one wrong name in a billing line fails the line.
            var weakest = checks.MinBy(c => Strength(c.Verdict) * 2 + (c.Probability ?? 1))!;
            var detail = checks.Count == 1 ? weakest.Detail()
                : $"{checks.Count} claims, the weakest {weakest.Detail()}" + (weakest.Verdict == FactCheck.Supported ? "" : $" ('{weakest.Claim}')");
            (item["source"], item["detail"]) = own ? ("web", detail) : (kind, $"{item["detail"]}; web: {detail}");
            item["web"] = new Dictionary<string, object?>
            {
                ["about"] = fact.About, ["claim"] = string.Join(" · ", fact.Claims), ["verdict"] = weakest.Verdict,
                ["probability"] = weakest.Probability, ["error"] = weakest.Error,
                ["checks"] = checks.Select(c => (object?)new Dictionary<string, object?>
                    { ["claim"] = c.Claim, ["verdict"] = c.Verdict, ["probability"] = c.Probability }).ToArray(),
                ["sources"] = checks.SelectMany(c => c.Sources).DistinctBy(s => s.Url).Select(s => (object?)new Dictionary<string, object?>
                    { ["url"] = s.Url, ["title"] = s.Title, ["recognised"] = s.Recognised }).ToArray(),
            };
        }

        // Lowest first: not supported, unsettled, not checked, supported.
        static int Strength(string verdict) => verdict switch
        {
            FactCheck.NotSupported => 0,
            FactCheck.Unsettled => 1,
            FactCheck.Supported => 3,
            _ => 2,
        };
    }

    internal static string Megabytes(long bytes) => (bytes / (1024 * 1024.0)).ToString("0.#", CultureInfo.InvariantCulture);

    private static Dictionary<string, object?> Failed(string? name, string? error, string? remedy) =>
        new() { ["ok"] = false, ["failureName"] = name, ["error"] = error, ["remedy"] = remedy };

    private static double[] Box(SKRect r) => [Math.Round(r.Left, 1), Math.Round(r.Top, 1), Math.Round(r.Width, 1), Math.Round(r.Height, 1)];

    /// <summary>The line's box with room around it, cut from the read image; null, or why not.</summary>
    private static string? Crop(SKBitmap? pixels, SKRect box, string path)
    {
        if (pixels is null) return "the read image could not be decoded";
        var pad = Math.Max(box.Height * 1.5f, 24f);
        var r = SKRectI.Round(SKRect.Create(box.Left - pad, box.Top - pad, box.Width + 2 * pad, box.Height + 2 * pad));
        r.Intersect(new SKRectI(0, 0, pixels.Width, pixels.Height));
        if (r.Width <= 0 || r.Height <= 0) return "the line is off the image";
        using var subset = new SKBitmap();
        if (!pixels.ExtractSubset(subset, r)) return "the crop could not be cut";
        using var data = subset.Encode(SKEncodedImageFormat.Png, 100);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data.ToArray());
        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9_-]{1,64}$")]
    private static partial Regex SafeName();

    /// <summary>Working that shows arithmetic: a digit and an operator, a minus only when spaced as one.</summary>
    [GeneratedRegex(@"\d.*([+−×*/÷^=]|\s-\s)|([+−×*/÷^=]|\s-\s).*\d")]
    private static partial Regex Arithmetic();
    #endregion

    #region Fields
    /// <summary>
    /// What the watcher is asked. Every text, as written, and when, in seconds of a film it is told the length of: the
    /// transcription the ground truth is held against.
    /// </summary>
    /// <remarks>
    /// "The second it first appears" with no length and no unit came back from Nemotron on nightshift1 (2026-10-24) as every
    /// true time divided by ten, then by a hundred on a second encode — 5 s as 0.5, then as 0.05, as if a clock's 0:05 had
    /// been read as a decimal — and every text was reported missed and stray. So the unit, the range and an example are
    /// stated, and the schema bounds the numbers.
    /// </remarks>
    internal static string Question(double duration) =>
        "List every piece of text that appears on screen in this video, each exactly as it is written — keep numbers, units, "
        + "currency signs and punctuation as shown — with when it first appears and when it disappears. "
        + $"The video is {Seconds(duration)} seconds long. Give from and to as plain numbers of seconds from the start, between "
        + $"0 and {Seconds(duration)}: a line on screen from five seconds in until eight and a half is from 5, to 8.5 — not "
        + "0.05, not 0:05, not a fraction of the length. Give lines that appear at different times as separate items. Small "
        + "text counts: axis and tick labels, legends, footnotes. Write any mathematical expression or equation as one item, "
        + "however many lines it spans, in LaTeX. Only text: do not describe pictures.";

    internal static string Schema(double duration) =>
        "{\"type\":\"object\",\"required\":[\"items\"],\"properties\":{\"items\":{\"type\":\"array\",\"items\":{\"type\":\"object\","
        + "\"required\":[\"text\",\"from\",\"to\"],\"properties\":{\"text\":{\"type\":\"string\"},"
        + $"\"from\":{{\"type\":\"number\",\"minimum\":0,\"maximum\":{Seconds(duration)},\"description\":\"seconds from the start\"}},"
        + $"\"to\":{{\"type\":\"number\",\"minimum\":0,\"maximum\":{Seconds(duration)},\"description\":\"seconds from the start\"}}}}}}}}}}}}";

    private static string Seconds(double s) => Math.Round(s, 2).ToString(CultureInfo.InvariantCulture);

    /// <summary>What the reader of a still is asked: every line, as written. A line repeated may be listed once.</summary>
    internal const string ImageQuestion =
        "List every line of text in this image, each exactly as it is written — keep numbers, units, currency signs and "
        + "punctuation as shown. Give each line as a separate item, from the top of the image to the bottom; small print, axis "
        + "labels and footnotes count. A line that appears in several places may be listed once. Write any mathematical "
        + "expression or equation as one item, however many lines it spans, in LaTeX. Only text: do not describe pictures, "
        + "and never complete or correct a line you cannot read.";

    internal const string ImageSchema =
        "{\"type\":\"object\",\"required\":[\"items\"],\"properties\":{\"items\":{\"type\":\"array\",\"items\":{\"type\":\"object\","
        + "\"required\":[\"text\"],\"properties\":{\"text\":{\"type\":\"string\"}}}}}}";

    /// <summary>
    /// The watch copy's greatest height. becool2's copies read 8 of 8 texts at 356 px tall and 3 of 8 at 270, where its
    /// small type fell to 7 px; 720 keeps 1080's type at two thirds and fits 15 s in the limit at about 3.5 Mbps.
    /// </summary>
    internal const int WatchHeight = 720;

    /// <summary>Boxes given per text: an axis label repeated across small multiples needs a few to find, not all of them.</summary>
    private const int MaxBoxes = 12;

    /// <summary>A derivation's length and time: 30 steps, 15 s for the whole by default (25 s at most, under the 30 s script limit), at least 0.4 s a step.</summary>
    private const int MaxSteps = 30, DerivationBudgetMs = 15000, MaxDerivationBudgetMs = 25000, MinPairMs = 400;

    private readonly VideoReader? video;
    private readonly string? projectRoot;
    private readonly SessionContext? session;
    private readonly Action<string, IReadOnlyDictionary<string, object?>>? record;
    private readonly FactChecker? facts;
    private ReadBackCheck? last;

    /// <summary>What to configure when there is no reader.</summary>
    internal const string NoReader = "Verify:Reader gemini (the default) needs ApiKeys:GoogleAgentPlatform; nemotron needs ApiKeys:NvidiaNIM or "
        + "ApiKeys:NebiusTokenFactory with NebiusTokenFactory:BaseUrl.";

    /// <summary>Whether this reader is Nemotron, which cannot be trusted with Arabic or Hindi's Devanagari (see <see cref="FilmReadBack.Unreadable"/>).</summary>
    private bool Nemotron => video?.Reader == VideoReader.NemotronReader;

    /// <summary>The scripts a reader may leave unchecked, as a check's summary names them; Gemini leaves none.</summary>
    private const string Scripts = "Arabic or Hindi";

    /// <summary>Each piece's latest check without a session, as <see cref="SessionContext.ReadBacks"/> holds them with one.</summary>
    private readonly Dictionary<string, ReadBackCheck> checks = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>The checks this script ran: their strays cannot have been looked at yet.</summary>
    private readonly HashSet<ReadBackCheck> madeHere = [];
    #endregion

    #region Types
    /// <summary>One line of text as drawn on a still, in the read image's pixels.</summary>
    private sealed record Placed(string Text, SKRect Box, float Size);

    /// <summary>A still to check: its lines as drawn and its pixels, from a canvas, its context, or a paper.</summary>
    private sealed class Still
    {
        public required string Kind { get; init; }
        public required int Width { get; init; }
        public required int Height { get; init; }
        public required Placed[] Lines { get; init; }
        public required Func<SKBitmap> Pixels { get; init; }

        /// <summary>The pixels over white: a transparent ground reads as black to a model and is never what a page shows.</summary>
        public SKBitmap Flattened()
        {
            using var source = Pixels();
            var flat = new SKBitmap(new SKImageInfo(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            using var canvas = new SKCanvas(flat);
            canvas.Clear(SKColors.White);
            using var image = SKImage.FromBitmap(source);
            canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Nearest));
            return flat;
        }

        public static Still Of(object source) => source switch
        {
            SkiaCanvas c => FromCanvas(c),
            CanvasRenderingContext2D ctx => FromCanvas(ctx.Canvas),
            SnapPaper p => FromPaper(p),
            _ => throw new ArgumentException("Verify.image checks what a canvas or a paper drew: Verify.image(canvas) — pass the canvas, its context, or the paper, after everything is drawn."),
        };

        private static Still FromCanvas(SkiaCanvas c) => new()
        {
            Kind = "canvas", Width = c.Width, Height = c.Height,
            Lines = [.. c.DrawnText.Select(l => new Placed(l.Words, l.Bounds, l.SizePaper))],
            Pixels = () => c.SkBitmap.Copy(),
        };

        /// <summary>A paper's lines are in paper units; they are mapped onto the pixels it renders to.</summary>
        private static Still FromPaper(SnapPaper p)
        {
            var (w, h) = (p.Width > 0 ? (int)MathF.Ceiling(p.Width) : 800, p.Height > 0 ? (int)MathF.Ceiling(p.Height) : 600);
            var document = PosterTypeAudit.Parse(p.ToString());
            var page = document.ViewBox is { Width: > 0, Height: > 0 } vb ? SKRect.Create(vb.MinX, vb.MinY, vb.Width, vb.Height) : SKRect.Create(0, 0, w, h);
            var (sx, sy) = (w / page.Width, h / page.Height);
            return new()
            {
                Kind = "paper", Width = w, Height = h,
                Lines = [.. PosterTypeAudit.Lines(document).Select(l => new Placed(l.Words,
                    SKRect.Create((l.Bounds.Left - page.Left) * sx, (l.Bounds.Top - page.Top) * sy, l.Bounds.Width * sx, l.Bounds.Height * sy), l.SizePaper * sy))],
                Pixels = () => SKBitmap.Decode(p.ToImageBytes(w, h, "png", 100)),
            };
        }
    }
    #endregion
}

/// <summary>One reading of something the piece never set, held until the agent has looked at its frame or crop.</summary>
public sealed class ReadBackStray(int number, string read, double? at, string? frame, string? frameError, string? near, string kind)
{
    public int Number { get; } = number;
    public string Read { get; } = read;

    /// <summary>The film second it was read at; null on a still.</summary>
    public double? At { get; } = at;

    /// <summary>Where to look: the film's frame, or a crop of the still around the line it resembles (else the whole still).</summary>
    public string? Frame { get; } = frame;
    public string? FrameError { get; } = frameError;

    /// <summary>The text set nearest to it, when the reading resembles one: a different figure in a line the piece did set.</summary>
    public string? Near { get; } = near;

    /// <summary><c>'different figure'</c>, <c>'never set'</c>, or <c>'unreadable script'</c> — a reading of Arabic or Hindi by
    /// Nemotron, which cannot be trusted with them, so only the frame says whether it is the line as set.</summary>
    public string Kind { get; } = kind;

    /// <summary>Null until looked at; then <c>'misread'</c> or <c>'real'</c>.</summary>
    public string? Verdict { get; internal set; }

    public string? Note { get; internal set; }

    internal Dictionary<string, object?> Dict() => new()
    {
        ["n"] = Number, ["read"] = Read, ["at"] = At, ["frame"] = Frame, ["frameError"] = FrameError, ["near"] = Near,
        ["kind"] = Kind, ["verdict"] = Verdict, ["note"] = Note,
    };
}

/// <summary>A piece's latest check: what it set, where each came from, what the reader read, and its strays.</summary>
public abstract class ReadBackCheck(string name, Dictionary<string, object?>[] items, List<ReadBackStray> strays, string? model, string? endpoint)
{
    /// <summary>The film, or the still as read.</summary>
    public string Name { get; } = name;
    public List<ReadBackStray> Strays { get; } = strays;

    /// <summary>The scripts its reader cannot be trusted to read, as the summary names them.</summary>
    public string UnreadableScripts { get; init; } = "Arabic or Hindi";

    /// <summary>The bytes read, so checking them again can keep the verdicts already given.</summary>
    public string? Hash { get; init; }

    /// <summary>The <c>computed</c> keys set aside because their working shows no arithmetic.</summary>
    public string[] Ignored { get; init; } = [];

    /// <summary><c>'film'</c> or <c>'image'</c>: names the events, <c>film.verify</c> / <c>image.verify</c>.</summary>
    internal abstract string Piece { get; }

    /// <summary>The field that carries <see cref="Name"/>: <c>video</c> or <c>image</c>.</summary>
    internal abstract string NameKey { get; }

    /// <summary>What a stray is checked against: <c>frame</c> or <c>crop</c>.</summary>
    internal abstract string Evidence { get; }

    protected Dictionary<string, object?>[] Items { get; } = items;

    /// <summary>The items, for a later check that reads what this one read: <c>Verify.derivation</c>.</summary>
    internal IReadOnlyList<Dictionary<string, object?>> ItemList => Items;
    protected string? Model { get; } = model;
    protected string? Endpoint { get; } = endpoint;

    /// <summary>As recorded for the studio: every item, and every stray with its state.</summary>
    internal virtual Dictionary<string, object?> Event() => new()
    {
        [NameKey] = Name, ["model"] = Model, ["endpoint"] = Endpoint,
        ["items"] = Items.Cast<object?>().ToArray(),
        ["strays"] = Strays.Select(s => (object?)s.Dict()).ToArray(),
        ["counts"] = Counts(),
    };

    /// <summary>As the script receives it, with a summary and whether it passed.</summary>
    internal Dictionary<string, object?> Result()
    {
        var counts = Counts();
        var unsourced = Items.Count(i => (string?)i["source"] == "unsourced figure");
        var pending = Strays.Count(s => s.Verdict is null);
        var real = Strays.Count(s => s.Verdict == "real");
        var bySource = Items.GroupBy(i => (string)i["source"]!).OrderByDescending(g => g.Count()).Select(g => $"{g.Count()} {g.Key}");
        var missed = Items.Where(i => (string?)i["status"] == "missed").ToArray();
        var evidence = pending == 1 ? $"its {Evidence}" : $"their {Evidence}s";
        var web = Items.Select(i => (Text: (string)i["text"]!, Web: i.GetValueOrDefault("web") as Dictionary<string, object?>))
            .Where(w => w.Web is not null).Select(w => (w.Text, Verdict: w.Web!["verdict"] as string, Error: w.Web["error"] as string)).ToArray();
        var unsupported = web.Where(w => w.Verdict == FactCheck.NotSupported).ToArray();
        var unsettled = web.Where(w => w.Verdict == FactCheck.Unsettled).ToArray();
        var notChecked = web.Where(w => w.Verdict is FactCheck.NotChecked or FactCheck.Failed or FactCheck.Refused).ToArray();
        string Texts(IEnumerable<(string Text, string? Verdict, string? Error)> ws) => string.Join(", ", ws.Take(4).Select(w => $"'{Clip(w.Text)}'"));
        string Facts(int n, string what) => $" {n} stated fact{(n == 1 ? "" : "s")} {what}";
        var summary = $"{Items.Length} texts {Where}: {counts["seen"]} seen by the reader"
            + (missed.Length == 0 ? "" : $", {missed.Length} missed ({string.Join("; ", missed.Take(4).Select(m => $"\"{Clip((string)m["text"]!)}\" {MissedNote(m)}"))})")
            + $". Sources: {string.Join(", ", bySource)}."
            + (unsourced == 0 ? "" : $" {unsourced} figure{(unsourced == 1 ? " has" : "s have")} no source: find it in a document or the brief, pass its working as computed, or, for a fact rather than arithmetic, state it in sources.")
            + (unsupported.Length == 0 ? "" : Facts(unsupported.Length, $"not supported by the sources the web check read ({Texts(unsupported)}): correct it, or find it in a document; a fact nothing supports comes off."))
            + (unsettled.Length == 0 ? "" : Facts(unsettled.Length, $"the sources leave unsettled ({Texts(unsettled)}): state it as they do if you can; otherwise the director sees it marked unsettled."))
            + (notChecked.Length == 0 ? "" : Facts(notChecked.Length, $"not checked on the web ({notChecked[0].Error ?? "no decision"}): recorded as unchecked."))
            + (pending == 0 ? "" : $" {pending} reading{(pending == 1 ? "" : "s")} of text the {Piece} never set: look at {evidence} ({string.Join(", ", Strays.Where(s => s.Verdict is null).Select(s => s.Frame ?? "none saved"))}), then Verify.stray('{Name}', n, 'misread' | 'real').")
            + (real == 0 ? "" : $" {real} confirmed fault{(real == 1 ? "" : "s")} in the {Piece}: fix and {Again} again.")
            + (Ignored.Length == 0 ? "" : $" {Ignored.Length} computed entr{(Ignored.Length == 1 ? "y was" : "ies were")} not used, as the working shows no arithmetic ({string.Join(", ", Ignored.Take(4).Select(k => $"'{Clip(k)}'"))}): a figure from a document is found on its page without one, and a fact no document holds goes in sources.")
            + (Empty ? $" Nothing was checked: the {Piece} set no text and the reader read none, so this read-back vouches for nothing. "
                + $"Pass the canvas or paper the {Piece} was drawn on, after everything is drawn; a {Piece} with no words needs no read-back." : "")
            + FilmReadBack.UncheckedNote(counts["unchecked"] is int u ? u : 0, Evidence, UnreadableScripts)
            + (Inconclusive(counts) ? $" The reader saw {counts["seen"]} of the {(int)counts["seen"]! + (int)counts["missed"]!} texts it "
                + $"could check, under half, so this read-back cannot vouch for the {Piece}: hold texts longer or set them larger and "
                + $"{Again} again. If the {Evidence}s show them plainly, the reader is at fault: say so in direction.md." : "")
            + Note;
        var result = new Dictionary<string, object?>
        {
            ["ok"] = true,
            // The claim is that every text "was read as set": a reader that saw under half of them has not shown that.
            // nvdatenk1 passed with 0 of 92 seen, because coverage was never part of the verdict.
            // Nor has one that read nothing: coffee_1 failed on 15 unsourced figures, then passed on "0 texts on the image".
            ["passed"] = unsourced == 0 && unsupported.Length == 0 && pending == 0 && real == 0 && !Inconclusive(counts) && !Empty,
            [NameKey] = Name,
            ["items"] = Items.Cast<object?>().ToArray(),
            ["strays"] = Strays.Select(s => (object?)s.Dict()).ToArray(),
            ["counts"] = counts,
            ["model"] = Model,
            ["summary"] = summary,
        };
        return result;
    }

    /// <summary>Where the texts are, for the summary: <c>on screen</c>.</summary>
    protected abstract string Where { get; }

    /// <summary>Anything the reading itself needs said, appended to the summary; empty by default.</summary>
    internal virtual string Note => "";

    /// <summary>What a fault asks for: <c>encode</c> or <c>render</c>.</summary>
    protected abstract string Again { get; }

    /// <summary>Why a missed text may have been missed: its seconds on screen, or its size.</summary>
    protected abstract string MissedNote(Dictionary<string, object?> item);

    /// <summary>Whether the reader saw under half of the texts it could check (at least three), so the check says little.</summary>
    /// <summary>The piece set no text and the reader read none: nothing was compared, so nothing can pass.</summary>
    private bool Empty => Items.Length == 0 && Strays.Count == 0;

    private static bool Inconclusive(Dictionary<string, object?> counts) =>
        (int)counts["seen"]! + (int)counts["missed"]! is var checkable && checkable >= 3 && (int)counts["seen"]! * 2 < checkable;

    private Dictionary<string, object?> Counts() => new()
    {
        ["shown"] = Items.Length,
        ["seen"] = Items.Count(i => (string?)i["status"] == "seen"),
        ["missed"] = Items.Count(i => (string?)i["status"] == "missed"),
        ["citations"] = Items.Count(i => (string?)i["status"] == "citation"),
        ["unchecked"] = Items.Count(i => (string?)i["status"] == "unchecked"),
        ["unsourced"] = Items.Count(i => (string?)i["source"] == "unsourced figure"),
        ["strays"] = Strays.Count,
        ["pending"] = Strays.Count(s => s.Verdict is null),
        ["real"] = Strays.Count(s => s.Verdict == "real"),
    };

    private static string Clip(string s) => s.Length > 40 ? s[..39] + "…" : s.Replace('\n', ' ');
}

/// <summary>A film's latest check.</summary>
public sealed class FilmCheck(string video, double duration, Dictionary<string, object?>[] items, List<ReadBackStray> strays, string? model, string? endpoint)
    : ReadBackCheck(video, items, strays, model, endpoint)
{
    public string Video => Name;
    internal override string Piece => "film";
    internal override string NameKey => "video";
    internal override string Evidence => "frame";
    protected override string Where => "on screen";
    protected override string Again => "encode";

    /// <summary>What the watcher's times were multiplied by to be seconds: 1 unless it answered in another unit.</summary>
    public double TimeScale { get; init; } = 1;

    /// <summary>The smaller copy the watcher read, when the film was over its limit; null when it read the film.</summary>
    public string? Watched { get; init; }

    /// <summary>The film's size when a copy was read in its place.</summary>
    public long? FilmBytes { get; init; }

    /// <summary>The watcher's limit, in bytes.</summary>
    public long Limit { get; init; }

    internal override string Note => (TimeScale == 1 ? ""
        : $" The watcher's times came back in the wrong unit and were read ×{TimeScale:0.##} to match the film; its seconds are approximate.")
        + (Watched is null ? ""
        : $" Read from {Watched}, a copy at most {VerifyToolkit.WatchHeight} px tall: {Video} is {VerifyToolkit.Megabytes(FilmBytes ?? 0)} MB, "
            + $"over the watcher's {VerifyToolkit.Megabytes(Limit)} MB. "
            + "Small type reads less reliably there; look at the film's frame before calling a missed line a fault.");

    internal override Dictionary<string, object?> Event()
    {
        var e = base.Event();
        e["duration"] = duration;
        if (TimeScale != 1) e["timeScale"] = TimeScale;
        if (Watched is not null) (e["watched"], e["filmBytes"]) = (Watched, FilmBytes);
        return e;
    }

    protected override string MissedNote(Dictionary<string, object?> item) => $"on screen {item["seconds"]:0.#} s";
}

/// <summary>A still's latest check.</summary>
public sealed class ImageCheck(string image, string source, int width, int height, Dictionary<string, object?>[] items, List<ReadBackStray> strays, string? model, string? endpoint)
    : ReadBackCheck(image, items, strays, model, endpoint)
{
    internal override string Piece => "image";
    internal override string NameKey => "image";
    internal override string Evidence => "crop";
    protected override string Where => "on the image";
    protected override string Again => "render";

    internal override Dictionary<string, object?> Event()
    {
        var e = base.Event();
        (e["source"], e["width"], e["height"]) = (source, width, height);
        return e;
    }

    protected override string MissedNote(Dictionary<string, object?> item) => $"set at {item["size"]:0.#} px";
}

/// <summary>The read-back's comparison: what the watcher read against what the composition set.</summary>
internal static class FilmReadBack
{
    #region Types
    /// <summary>A text the film set, and when it is on screen.</summary>
    internal sealed record Shown(string Text, double? From, double? To, double Seconds, double[][] Intervals);

    /// <summary>One item of the watcher's transcription.</summary>
    internal sealed record Reading(string Text, double? From, double? To);

    /// <summary>A reading that matched nothing set: <c>Near</c> the set text it resembles, if any.</summary>
    internal sealed record Unmatched(Reading Reading, string? Near, string Kind);

    internal sealed record Outcome(Reading?[] Seen, Unmatched[] Strays);
    #endregion

    #region Methods
    /// <summary>The watcher's items, from the schema'd answer; none when it is not that shape.</summary>
    internal static Reading[] ParseReading(string? json)
    {
        try
        {
            return [.. (JsonNode.Parse(json ?? "")?["items"]?.AsArray() ?? [])
                .Select(n => new Reading(n?["text"]?.GetValue<string>() ?? "", Num(n?["from"]), Num(n?["to"])))
                .Where(r => Normal(r.Text).Length > 0)];
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            return [];
        }

        static double? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
    }

    /// <summary>
    /// Each set text's reading, or null when none matched; and each reading that matched nothing. A reading matches a text
    /// on screen within <see cref="Slack"/> seconds of its interval whose words it shares — and whose figures it has
    /// exactly: the same line with a different figure is a stray, kind <c>'different figure'</c>, not a match. A text of
    /// several lines is seen when every line is read.
    /// </summary>
    /// <remarks>
    /// **A reading of several lines that matches nothing is read again line by line.** The watcher can transcribe a whole
    /// scene as one block: nvdatenk1's (2026-10-28) eight-line scorecard came back as one reading per scene, no set line
    /// was similar enough to a block, and a film whose every line was read scored 0 of 92 seen with nine strays, each of
    /// them only lines the film set. Matched first as read, so a block that is one set text of several lines still pairs
    /// whole; split only where that leaves it unmatched, and the split kept only when it sees more.
    /// </remarks>
    internal static Outcome Match(IReadOnlyList<Shown> shown, IReadOnlyList<Reading> read)
    {
        var asRead = MatchOnce(shown, read);
        var blocks = asRead.Strays.Select(u => u.Reading).Where(r => r.Text.Contains('\n')).ToHashSet();
        if (blocks.Count == 0) return Joined(shown, asRead);
        var split = MatchOnce(shown, [.. read.SelectMany(r => blocks.Contains(r) ? Lines(r) : [r])]);
        return Joined(shown, split.Seen.Count(s => s is not null) > asRead.Seen.Count(s => s is not null) ? split : asRead);

        static IEnumerable<Reading> Lines(Reading r) =>
            r.Text.Split('\n').Select(l => l.Trim()).Where(l => Normal(l).Length > 0).Select(l => r with { Text = l });
    }

    /// <summary>
    /// **A reading of several set texts run together on one line is each of them.** becool3 (2026-10-28) set its credits
    /// as three texts, one under another, and the watcher read them as one string joined by spaces: all three were missed
    /// and the string was a stray, and "HE LEFT THE MOVIES" was missed because the reading of both lines paired with the
    /// second. Each set text on screen with the reading and found in it word for word, figures and all, is seen; the
    /// reading stops being a stray when those texts account for nearly all of it.
    /// </summary>
    private static Outcome Joined(IReadOnlyList<Shown> shown, Outcome outcome)
    {
        var seen = outcome.Seen.ToArray();
        var strays = outcome.Strays.ToList();
        var normal = shown.Select(s => Normal(Canon(s.Text))).ToArray();
        foreach (var r in outcome.Seen.OfType<Reading>().Concat(outcome.Strays.Select(u => u.Reading)).Distinct().ToArray())
        {
            var read = $" {Normal(Canon(r.Text))} ";
            var parts = Enumerable.Range(0, shown.Count)
                .Where(i => shown[i].From is not null && normal[i].Length > 0 && InTime(shown[i], r) && read.Contains($" {normal[i]} "))
                .ToArray();
            if (parts.Length < 2) continue;
            foreach (var i in parts) seen[i] ??= r with { Text = shown[i].Text };
            var words = read.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
            if (parts.Sum(i => normal[i].Split(' ').Length) >= 0.8 * words)
                strays.RemoveAll(u => u.Reading == r);
        }
        return new Outcome(seen, [.. strays]);
    }

    private static Outcome MatchOnce(IReadOnlyList<Shown> shown, IReadOnlyList<Reading> read)
    {
        var used = new bool[read.Count];
        var seen = new Reading?[shown.Count];
        for (var i = 0; i < shown.Count; i++)
        {
            var s = shown[i];
            if (s.From is null) continue;
            var lines = s.Text.Split('\n').Select(l => l.Trim()).Where(l => Normal(l).Length > 0).ToArray();
            var whole = Best(s, s.Text, read, used);
            if (whole >= 0) { used[whole] = true; seen[i] = read[whole]; continue; }
            if (lines.Length < 2) continue;
            var parts = lines.Select(l => Best(s, l, read, used)).ToArray();
            if (parts.All(p => p >= 0) && parts.Distinct().Count() == parts.Length)
            {
                foreach (var p in parts) used[p] = true;
                seen[i] = new Reading(string.Join("\n", parts.Select(p => read[p].Text)), read[parts[0]].From, read[parts[^1]].To);
            }
        }

        // The same words and figures, read a little outside the interval, are the same text: the watcher's times drift.
        // uberpitch1 (2026-10-24) read six lines word for word 1.7–2.3 s before they arrived, and each was reported twice,
        // missed and stray, fourteen problems where there were none. Only an exact reading is paired this far out.
        for (var i = 0; i < shown.Count; i++)
        {
            var s = shown[i];
            if (seen[i] is not null || s.From is null) continue;
            var figures = MatchFigures(s.Text);
            var k = Enumerable.Range(0, read.Count).FirstOrDefault(k => !used[k] && InTime(s, read[k], DriftSlack)
                && Similarity(s.Text, read[k].Text) >= 0.95 && figures.SequenceEqual(MatchFigures(read[k].Text)), -1);
            if (k >= 0) { used[k] = true; seen[i] = read[k]; }
        }

        // A reading of one line of a text of several is that text, not a stray. A reading of the first line is near enough
        // the whole to pair with it above, which left the second line's reading over: lanternkeepermotion1 (2026-11-14) set
        // each two-line caption as one text, and every second line came back as text the film never set.
        for (var k = 0; k < read.Count; k++)
        {
            if (used[k]) continue;
            var figures = MatchFigures(read[k].Text);
            var i = Enumerable.Range(0, shown.Count).FirstOrDefault(i => seen[i] is not null && shown[i].Text.Contains('\n')
                && InTime(shown[i], read[k]) && shown[i].Text.Split('\n').Any(l => Normal(l).Length > 0
                    && Similarity(l, read[k].Text) >= 0.6 && figures.SequenceEqual(MatchFigures(l))), -1);
            if (i < 0) continue;
            used[k] = true;
            var had = seen[i]!;
            if (Similarity(read[k].Text, had.Text) < 0.95 && !Normal(had.Text).Contains(Normal(read[k].Text), StringComparison.Ordinal))
            {
                // Kept in the order the lines were set, so the reading reads as the text does.
                var lines = shown[i].Text.Split('\n');
                int Line(string t) => Array.FindIndex(lines, l => Similarity(l, t) >= 0.6) is var n and >= 0 ? n : int.MaxValue;
                var text = Line(read[k].Text) < Line(had.Text.Split('\n')[0]) ? read[k].Text + "\n" + had.Text : had.Text + "\n" + read[k].Text;
                seen[i] = had with { Text = text, From = Min(had.From, read[k].From), To = Max(had.To, read[k].To) };
            }
        }

        static double? Min(double? a, double? b) => a is null ? b : b is null ? a : Math.Min(a.Value, b.Value);
        static double? Max(double? a, double? b) => a is null ? b : b is null ? a : Math.Max(a.Value, b.Value);

        var strays = read.Select((r, k) => (r, k)).Where(x => !used[x.k]).Select(x =>
        {
            var near = shown.Where(s => s.From is not null && InTime(s, x.r)).Select(s => (s, score: Words(s.Text, x.r.Text)))
                .Where(p => p.score >= 0.5).OrderByDescending(p => p.score).Select(p => p.s.Text).FirstOrDefault();
            return new Unmatched(x.r, near, near is not null && MatchFigures(near).Length > 0 ? "different figure" : "never set");
        }).ToArray();
        return new Outcome(seen, strays);
    }

    /// <summary>
    /// What the watcher's times must be multiplied by to be seconds: 1, or 10, 60 or 100 where that unit matches clearly more
    /// of the set texts and puts no reading past the film's end.
    /// </summary>
    /// <remarks>
    /// The question states the unit (<see cref="VerifyToolkit.Question"/>); this is for when the watcher answers in another
    /// anyway. nightshift1's readings were every true time over ten, then over a hundred: in order and in proportion, so
    /// the transcription was right and only its unit wrong. A scale is taken only when it sees clearly more, so a correct
    /// reading with a few misses is never stretched to fit.
    /// </remarks>
    internal static double TimeScale(IReadOnlyList<Shown> shown, IReadOnlyList<Reading> read, double duration)
    {
        var times = read.SelectMany(r => new[] { r.From, r.To }).OfType<double>().ToArray();
        if (times.Length == 0 || duration <= 0) return 1;

        // How many texts a scale sees, and how far off its matched readings land: the true unit sees most and lands closest.
        (int Seen, double Error) Score(double k)
        {
            var seen = Match(shown, [.. read.Select(r => r with { From = r.From * k, To = r.To * k })]).Seen;
            var errors = shown.Zip(seen).Where(p => p.Second?.From is not null && p.First.From is not null)
                .Select(p => Math.Abs(p.Second!.From!.Value - p.First.From!.Value)).ToArray();
            return (errors.Length, errors.Length == 0 ? double.MaxValue : errors.Average());
        }

        var asRead = Score(1);
        var best = new[] { 10d, 60d, 100d }.Where(k => times.Max() * k <= duration * 1.1 + Slack)
            .Select(k => (Scale: k, Score: Score(k))).OrderByDescending(c => c.Score.Seen).ThenBy(c => c.Score.Error)
            .FirstOrDefault();
        // A margin, so one coincidence does not stretch a reading already in seconds. But where the reading as given sees
        // nothing at all, one line seen is enough: becool3's Hindi film (2026-10-28) was dated 0.02–0.1 s for texts at
        // 2–10 s, "0:09" written 0.09, and with its Devanagari lines misread only BE COOL could line up at ×100.
        var margin = asRead.Seen == 0 ? 0 : Math.Max(1, shown.Count / 5);
        return best.Scale > 0 && best.Score.Seen > asRead.Seen + margin ? best.Scale : 1;
    }

    /// <summary>The best unused reading of <paramref name="text"/> on screen with <paramref name="shown"/>, or -1.</summary>
    private static int Best(Shown shown, string text, IReadOnlyList<Reading> read, bool[] used)
    {
        var (best, bestScore) = (-1, 0.0);
        var figures = MatchFigures(text);
        for (var k = 0; k < read.Count; k++)
        {
            if (used[k] || !InTime(shown, read[k])) continue;
            if (!figures.SequenceEqual(MatchFigures(read[k].Text))) continue;
            var score = Similarity(text, read[k].Text);
            if (score > bestScore) (best, bestScore) = (k, score);
        }
        return bestScore >= 0.6 ? best : -1;
    }

    private static bool InTime(Shown s, Reading r, double slack = Slack) =>
        r.From is not { } at || s.Intervals.Length == 0 || s.Intervals.Any(iv => at >= iv[0] - slack && at <= iv[1] + slack);

    /// <summary>
    /// 1 for the same words in order; containment counts most of that when the shorter is at least half the longer;
    /// otherwise the share of words in common. Compared in <see cref="Canon"/> form, so <c>b²</c> and <c>b^2</c> agree.
    /// </summary>
    /// <remarks>Containment used to count at any length, and a split-off fragment, <c>2</c>, matched the line
    /// <c>aₙ = a₁ + (n − 2)d</c> (equation probe, 2026-10-23): a short reading inside a long line is not a reading of it.</remarks>
    internal static double Similarity(string a, string b)
    {
        var (na, nb) = (Normal(Canon(a)), Normal(Canon(b)));
        if (na.Length == 0 || nb.Length == 0) return 0;
        if (na == nb) return 1;
        // The same characters spaced differently: an equation's thin space, x² dx and x²dx. The figures are compared apart.
        if (na.Replace(" ", "") == nb.Replace(" ", "")) return 0.95;
        var ratio = (double)Math.Min(na.Length, nb.Length) / Math.Max(na.Length, nb.Length);
        if (ratio >= 0.5 && (na.Contains(nb, StringComparison.Ordinal) || nb.Contains(na, StringComparison.Ordinal)))
            return 0.6 + 0.4 * ratio;
        return Words(a, b);
    }

    /// <summary>The share of words two texts have in common, in <see cref="Canon"/> form.</summary>
    internal static double Words(string a, string b)
    {
        var (wa, wb) = (Normal(Canon(a)).Split(' ').ToHashSet(), Normal(Canon(b)).Split(' ').ToHashSet());
        return wa.Count == 0 || wb.Count == 0 ? 0 : wa.Intersect(wb).Count() / (double)wa.Union(wb).Count();
    }

    /// <summary>
    /// The figures a reader would compare, in <see cref="Canon"/> form and with their signs: <c>mc²</c> has the figure 2, so
    /// <c>mc³</c> is a different figure, and <c>−1</c> is not <c>1</c>. A minus is a sign when it is attached to the number
    /// and not to a word or number before it: <c>−1</c>, <c>(−3</c>, but not <c>n − 1</c> or <c>2016-19</c>. Sourcing keeps
    /// <see cref="Figures"/>, unsigned, where an exponent is not a claim and a page saying "fell 3%" sources −3%.
    /// </summary>
    /// <remarks>Unsigned, an axes film read the y axis's −1 as 1 and the check counted it seen (2026-10-23): a loss read as a
    /// gain would have passed the same way.</remarks>
    internal static string[] MatchFigures(string? text) =>
        [.. SignedFigure.Matches(Canon(text)).Select(m => m.Value.Replace(",", "").Replace("$", "").TrimEnd('.'))];

    /// <summary>
    /// A text with Unicode scripts spelled out and the minus sign as a hyphen: <c>x¹⁰ − aₙ</c> is <c>x^10 - a_n</c>. The
    /// linear form of an equation writes scripts as script characters, and a reader writes <c>^</c>; both compare alike.
    /// </summary>
    internal static string Canon(string? text)
    {
        if (string.IsNullOrEmpty(text)) return "";
        var sb = new StringBuilder(text.Length + 8);
        char? mode = null;
        foreach (var c in text)
        {
            char? mark = null;
            var plain = c;
            if (Superscript.TryGetValue(c, out var sup)) (mark, plain) = ('^', sup);
            else if (Subscript.TryGetValue(c, out var sub)) (mark, plain) = ('_', sub);
            if (mark is not null && mark != mode) sb.Append(mark.Value);
            sb.Append(plain == '−' ? '-' : plain);
            mode = mark;
        }
        return sb.ToString();
    }

    /// <summary>A text as compared: no case, accents or punctuation, straight quotes, single spaces.</summary>
    internal static string Normal(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var sb = new StringBuilder();
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(char.IsLetterOrDigit(c) || c is '%' or '$' or '.' ? char.ToLowerInvariant(c) : ' ');
        }
        // A figure and a word meet at a token boundary: 2dx is 2 dx, as x² dx is, so a thin space read or not is one reading.
        var split = Regex.Replace(sb.ToString(), @"(?<=\d)(?=\p{L})|(?<=\p{L})(?=\d)", " ");
        return Regex.Replace(Regex.Replace(split, @"(?<!\d)\.|\.(?!\d)", " "), @"\s+", " ").Trim();
    }

    /// <summary>
    /// Whether a text is in a script the reader cannot be trusted to read: with <paramref name="nemotron"/> (the Nemotron
    /// reader), Arabic and Hindi's Devanagari; with Gemini, none. In the language probe (2026-10-27) Nemotron read a correctly
    /// drawn Arabic heading as different words, and on becool3's Hindi film (2026-10-28) it read the credits as unrelated
    /// sentences, so a match or a miss in those scripts says nothing about the film. Such a text is <c>unchecked</c> rather
    /// than missed, and a reading of it is a stray of kind <c>'unreadable script'</c>: the studio's own eye on the frame is the
    /// check. Gemini read the same Hindi film almost exactly, and in the Arabic probe (2026-10-29) read six correct Arabic
    /// lines exactly and five of six deliberately wrong ones as drawn, a reversed line included; it corrected one transposed
    /// letter pair, a typo the read-back's word tolerance passes in any script.
    /// </summary>
    internal static bool Unreadable(string? text, bool nemotron = false) =>
        nemotron && (text ?? "").Any(c => (c is >= '؀' and <= 'ۿ' or >= 'ݐ' and <= 'ݿ' or >= 'ࢠ' and <= 'ࣿ'
            or >= 'ﭐ' and <= '﷿' or >= 'ﹰ' and <= '﻿' or >= 'ऀ' and <= 'ॿ') && char.IsLetter(c));

    /// <summary>What the summary says when texts were left unchecked for their script, <paramref name="scripts"/> naming them.</summary>
    internal static string UncheckedNote(int count, string piece, string scripts = "Arabic or Hindi") => count == 0 ? ""
        : $" {count} text{(count == 1 ? " is" : "s are")} in {scripts}, which the reader cannot be trusted to read: {(count == 1 ? "it is" : "they are")} "
          + $"unchecked, not missed. Look at each in its {piece} yourself and compare it with the line as set, and say in direction.md "
          + $"that the {scripts} was checked by eye and by translation back, not by the read-back.";

    /// <summary>The figures in a text, commas dropped, in order: "$40.4 billion (282 million)" is 40.4 and 282.</summary>
    internal static string[] Figures(string? text) =>
        [.. Figure.Matches(text ?? "").Select(m => m.Value.Replace(",", "").TrimEnd('.'))];

    /// <summary>A text with each figure as <c>#</c>: the shape a misread figure keeps. "Grew 2,205.8%" is "grew #%".</summary>
    internal static string Shape(string? text) => Figure.Replace(Normal(Canon(text)), "#");
    #endregion

    #region Fields
    /// <summary>How far a reading's time may sit outside a text's interval: the watcher samples about once a second.</summary>
    internal const double Slack = 1.5;

    /// <summary>How far an exact reading may sit outside its text's interval and still be that text (<see cref="Match"/>).</summary>
    internal const double DriftSlack = 4;

    private static readonly Dictionary<char, char> Superscript = new()
    {
        ['⁰'] = '0', ['¹'] = '1', ['²'] = '2', ['³'] = '3', ['⁴'] = '4', ['⁵'] = '5', ['⁶'] = '6', ['⁷'] = '7', ['⁸'] = '8', ['⁹'] = '9',
        ['⁺'] = '+', ['⁻'] = '-', ['⁼'] = '=', ['⁽'] = '(', ['⁾'] = ')', ['ⁿ'] = 'n', ['ⁱ'] = 'i',
    };

    private static readonly Dictionary<char, char> Subscript = new()
    {
        ['₀'] = '0', ['₁'] = '1', ['₂'] = '2', ['₃'] = '3', ['₄'] = '4', ['₅'] = '5', ['₆'] = '6', ['₇'] = '7', ['₈'] = '8', ['₉'] = '9',
        ['₊'] = '+', ['₋'] = '-', ['₌'] = '=', ['₍'] = '(', ['₎'] = ')', ['ₐ'] = 'a', ['ₑ'] = 'e', ['ₕ'] = 'h', ['ᵢ'] = 'i', ['ⱼ'] = 'j',
        ['ₖ'] = 'k', ['ₗ'] = 'l', ['ₘ'] = 'm', ['ₙ'] = 'n', ['ₒ'] = 'o', ['ₚ'] = 'p', ['ᵣ'] = 'r', ['ₛ'] = 's', ['ₜ'] = 't', ['ᵤ'] = 'u',
        ['ᵥ'] = 'v', ['ₓ'] = 'x',
    };

    private static readonly Regex Figure = new(@"\d[\d,]*(?:\.\d+)?", RegexOptions.Compiled);

    /// <summary>A figure with its sign, which may stand before a currency symbol: <c>−1</c>, <c>−$974</c>.</summary>
    private static readonly Regex SignedFigure = new(@"(?:(?<![\w.)\]}])-\$?)?\d[\d,]*(?:\.\d+)?", RegexOptions.Compiled);
    #endregion
}

/// <summary>
/// A still's read-back: what the reader read against the lines the canvas drew. No time to place a reading, so a reading
/// is placed by its words; the reader may list a repeated line once, so each distinct text needs reading once.
/// </summary>
internal static class ImageReadBack
{
    /// <summary>
    /// Each distinct text's reading, or null when none matched; and each reading that matched nothing. A reading matches a
    /// text whose words it shares and whose figures it has exactly, as on film. A reading that runs several drawn lines
    /// together (a wrapped paragraph, or a table row read across its columns) reads every line it contains, and is a stray
    /// only if it carries a figure none of the drawn lines it contains has. A text not read gets the reading closest to it,
    /// for the agent to judge: <c>ORORO</c> read as <c>OROO</c> is a size fault, not an absence.
    /// </summary>
    internal static (string?[] Seen, string?[] Closest, FilmReadBack.Unmatched[] Strays) Match(IReadOnlyList<string> texts, IReadOnlyList<string> read)
    {
        var used = new bool[read.Count];
        var seen = new string?[texts.Count];
        var padded = read.Select(r => " " + FilmReadBack.Normal(FilmReadBack.Canon(r)) + " ").ToArray();
        // Contained, and every one of the line's figures, signs included, is among the reading's: −1 is not inside "1".
        var readFigures = read.Select(FilmReadBack.MatchFigures).ToArray();
        bool Contains(int k, int i) =>
            padded[k].Contains(" " + FilmReadBack.Normal(FilmReadBack.Canon(texts[i])) + " ", StringComparison.Ordinal)
            && FilmReadBack.MatchFigures(texts[i]).All(readFigures[k].Contains);

        // Longest first, so a heading is not taken by a reading of a one-word label inside it.
        foreach (var i in Enumerable.Range(0, texts.Count).OrderByDescending(i => FilmReadBack.Normal(texts[i]).Length))
        {
            var figures = FilmReadBack.MatchFigures(texts[i]);
            var (best, score) = (-1, 0.0);
            for (var k = 0; k < read.Count; k++)
            {
                if (used[k] || !figures.SequenceEqual(FilmReadBack.MatchFigures(read[k]))) continue;
                var s = FilmReadBack.Similarity(texts[i], read[k]);
                if (s > score) (best, score) = (k, s);
            }
            if (score >= 0.6) (used[best], seen[i]) = (true, read[best]);
        }

        for (var i = 0; i < texts.Count; i++)
        {
            if (seen[i] is not null) continue;
            var k = Enumerable.Range(0, read.Count).FirstOrDefault(k => Contains(k, i), -1);
            if (k >= 0) seen[i] = read[k];
        }

        // Every drawn line a reading contains explains it, whether or not that line was also read elsewhere: the reader
        // lists a table's figures on their own and again inside each row.
        var strays = new List<FilmReadBack.Unmatched>();
        for (var k = 0; k < read.Count; k++)
        {
            if (used[k]) continue;
            var reading = new FilmReadBack.Reading(read[k], null, null);
            var shape = " " + FilmReadBack.Shape(read[k]) + " ";
            var contained = Enumerable.Range(0, texts.Count).Where(i => Contains(k, i)).ToArray();
            var have = contained.SelectMany(i => FilmReadBack.MatchFigures(texts[i])).ToList();
            var extra = FilmReadBack.MatchFigures(read[k]).Where(f => !have.Remove(f)).ToArray();
            if (contained.Length > 0 && extra.Length == 0) continue;

            // Beside the drawn figure whose shape it carries, unread first; else the line it most resembles.
            var shaped = Enumerable.Range(0, texts.Count).Where(i => FilmReadBack.MatchFigures(texts[i]).Length > 0
                    && shape.Contains(" " + FilmReadBack.Shape(texts[i]) + " ", StringComparison.Ordinal))
                .OrderBy(i => seen[i] is null ? 0 : 1).ThenByDescending(i => FilmReadBack.Shape(texts[i]).Length).ToArray();
            var near = shaped.Length > 0 ? texts[shaped[0]]
                : contained.Length > 0 ? texts[contained[0]]
                : texts.Select(t => (t, score: FilmReadBack.Words(t, read[k]))).Where(p => p.score >= 0.5)
                    .OrderByDescending(p => p.score).Select(p => p.t).FirstOrDefault();
            strays.Add(new(reading, near, near is not null && (extra.Length > 0 || FilmReadBack.MatchFigures(near).Length > 0) ? "different figure" : "never set"));
        }

        var closest = texts.Select((t, i) => seen[i] is not null ? null : Closest(t, read)).ToArray();
        return (seen, closest, [.. strays]);
    }

    /// <summary>The reading carrying most of a text's words, at least half of them; null when none does.</summary>
    private static string? Closest(string text, IReadOnlyList<string> read)
    {
        var words = FilmReadBack.Normal(text).Split(' ').ToHashSet();
        return read.Select(r => (r, share: words.Intersect(FilmReadBack.Normal(r).Split(' ')).Count() / (double)words.Count))
            .Where(p => p.share >= 0.5).OrderByDescending(p => p.share).ThenByDescending(p => FilmReadBack.Similarity(text, p.r))
            .Select(p => p.r).FirstOrDefault();
    }
}

/// <summary>
/// Readings of mathematics, made comparable with the equations that were set: the reader writes LaTeX, the record holds the
/// linear form, and both go through the same parser and linearizer (<see cref="EquationLayout.LinearFromLatex"/>).
/// </summary>
/// <remarks>
/// Measured 2026-10-23 on Nebius: Nemotron transcribed twelve typeset equations as exact LaTeX down to 16 px, reported
/// twelve deliberately wrong ones as drawn rather than as the textbook forms, and in one run of two split built-up
/// equations into visual rows (<c>x = \frac{-b \pm \sqrt{b^2 - 5ac}</c>, then <c>2a</c>). A fragment that will not parse
/// alone is joined to the next reading when together they do.
/// </remarks>
internal static partial class MathReading
{
    /// <summary>
    /// Each reading as shown (<c>Raw</c>, with a joined fragment's lines), as compared (<c>Compare</c>), and which readings
    /// it came from (<c>First</c>, and <c>Last</c> when a fragment was joined to the next).
    /// </summary>
    internal static List<(string Raw, string Compare, int First, int Last)> Prepare(IReadOnlyList<string> read)
    {
        var prepared = new List<(string, string, int, int)>();
        read = [.. read.Select(Unwrap)];
        for (var i = 0; i < read.Count; i++)
        {
            var r = read[i];
            if (!LooksLikeMath(r)) { prepared.Add((r, r, i, i)); continue; }
            if (EquationLayout.LinearFromLatex(r) is { } linear && !Incomplete(r)) { prepared.Add((r, linear, i, i)); continue; }
            if (i + 1 < read.Count && Join(r, read[i + 1]) is { } joined && EquationLayout.LinearFromLatex(joined) is { } whole)
            {
                prepared.Add((r + "\n" + read[i + 1], whole, i, i + 1));
                i++;
                continue;
            }
            prepared.Add((r, EquationLayout.LinearFromLatex(r) ?? r, i, i));
        }
        return prepared;
    }

    /// <summary>A command, a superscript, or a braced subscript: <c>\frac</c>, <c>b^2</c>, <c>x_{i}</c>.</summary>
    internal static bool LooksLikeMath(string reading) => EquationCheck.LooksLikeMath(reading);

    /// <summary>A reading as the reader meant it: delimiters off, JSON-eaten commands put back (see <c>EquationCheck.Unwrap</c>).</summary>
    internal static string Unwrap(string reading) => EquationCheck.Unwrap(reading);

    /// <summary>Unclosed braces, or a <c>\frac</c> with its numerator and no denominator: a fragment of a built-up equation.</summary>
    internal static bool Incomplete(string reading) => Depth(reading) > 0 || HalfFraction(reading);

    /// <summary>The fragment closed and the next reading set as what it lacks: a denominator, or the rest of a group.</summary>
    internal static string? Join(string fragment, string next)
    {
        var depth = Depth(fragment);
        if (depth < 0) return null;
        var closed = fragment + new string('}', depth);
        return HalfFraction(closed) ? closed + "{" + next + "}" : depth > 0 ? closed + " " + next : null;
    }

    private static int Depth(string s)
    {
        var depth = 0;
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] == '\\' && i + 1 < s.Length && s[i + 1] is '{' or '}') { i++; continue; }
            if (s[i] == '{') depth++;
            else if (s[i] == '}') depth--;
        }
        return depth;
    }

    /// <summary>The last <c>\frac</c> ends the text with one group: <c>= \frac{1}</c>.</summary>
    private static bool HalfFraction(string s)
    {
        var t = s.TrimEnd();
        var at = t.LastIndexOf(@"\frac", StringComparison.Ordinal);
        if (at < 0) return false;
        var i = at + 5;
        while (i < t.Length && t[i] == ' ') i++;
        if (i >= t.Length || t[i] != '{') return false;
        var depth = 0;
        for (; i < t.Length; i++)
        {
            if (t[i] == '{') depth++;
            else if (t[i] == '}' && --depth == 0) return i == t.Length - 1;
        }
        return false;
    }

}

/// <summary>Where a text on screen came from, looked up rather than asked: the brief, a document page, Qloo, research.</summary>
internal sealed class FilmSources
{
    #region Constructors
    internal FilmSources(string? projectRoot, SessionContext? session)
    {
        var briefText = ReadBrief(projectRoot);
        brief = Normal(briefText);
        briefFigures = [.. FilmReadBack.Figures(briefText)];
        pages = ReadPages(projectRoot);
        qloo = session?.Taste.All.Where(r => r.Ok)
            .SelectMany(r => r.Entities.OfType<Dictionary<string, object?>>()
                .Select(e => (Name: FilmReadBack.Normal(e.GetValueOrDefault("name") as string), r.Id)))
            .Where(x => x.Name.Length > 0).GroupBy(x => x.Name).ToDictionary(g => g.Key, g => g.First().Id) ?? [];
        // Figures taken as the brief's are, before Normal spaces out a thousands separator: matched against the normal text,
        // "$44,796 million" was "44 796 million", and anthropic_s_1_outside_cc's three 10-Q figures came back unsourced.
        research = session?.Research.All.Where(t => t.Status == "completed" && t.Result is not null)
            .Select(t => JsonSerializer.Serialize(t.Result, Unescaped) is var raw
                ? (t.Id, Text: Normal(raw), Figures: FilmReadBack.Figures(raw).ToHashSet()) : default).ToArray() ?? [];
    }
    #endregion

    #region Methods
    /// <summary>
    /// The source of a text set on screen: <c>(kind, detail)</c>, kind one of <c>scale</c> (an axis label the caller
    /// declared), <c>computed</c>, <c>brief</c>, <c>document</c>, <c>qloo</c>, <c>research</c>, <c>own words</c>, or
    /// <c>unsourced figure</c>. A text with figures is sourced figure by
    /// figure: one sentence may carry a document's figure and a computed one. Each figure is looked for on the documents'
    /// pages first, then among the figures of the <paramref name="computed"/> keys (so <c>{ 'Top 4.4%': '22 / 500' }</c>
    /// sources 4.4 wherever it is set), then research and the brief. The kind is the first of computed, document, research,
    /// brief among the figures' sources, and the detail names each.
    /// </summary>
    internal (string Kind, string? Detail) Classify(string text, IReadOnlyDictionary<string, string> computed, IReadOnlySet<string>? scale = null)
    {
        if (scale?.Contains(text.Trim()) == true) return ("scale", null);
        if (computed.TryGetValue(text, out var working) || computed.TryGetValue(text.Trim(), out working)) return ("computed", working);
        var n = Normal(text);
        var figures = FilmReadBack.Figures(text);
        if (n.Length > 0 && brief.Contains(n, StringComparison.Ordinal)) return ("brief", null);
        if (qloo.TryGetValue(n, out var handle)) return ("qloo", handle);

        if (figures.Length == 0)
        {
            foreach (var (doc, number, pageText, _) in pages)
                if (n.Length >= 4 && pageText.Contains(n, StringComparison.Ordinal)) return ("document", $"{doc} p. {number}");
            foreach (var (id, body, _) in research)
                if (n.Length >= 4 && body.Contains(n, StringComparison.Ordinal)) return ("research", id);
            return ("own words", null);
        }

        // Each figure where it is found: a computed key's figure, a document's page (one page holding them all named
        // first), research, the brief.
        var computedFigures = computed.SelectMany(c => FilmReadBack.Figures(c.Key).Select(f => (f, c.Value))).GroupBy(x => x.f)
            .ToDictionary(g => g.Key, g => g.First().Value);
        var ordered = pages.OrderBy(p => figures.All(p.Figures.Contains) ? 0 : 1).ToArray();
        (string? Kind, string Where) SourceOf(string f)
        {
            // The document first: a figure on its pages is the document's, whatever a computed entry says about it.
            foreach (var p in ordered)
                if (p.Figures.Contains(f)) return ("document", $"{p.Doc} p. {p.Page}");
            if (computedFigures.TryGetValue(f, out var how)) return ("computed", $"{f} = {how}");
            foreach (var r in research)
                if (r.Figures.Contains(f)) return ("research", r.Id);
            return briefFigures.Contains(f) ? ("brief", "brief.md") : (null, f);
        }
        var found = figures.Distinct().Select(SourceOf).ToArray();

        var missing = found.Where(x => x.Kind is null).Select(x => x.Where).ToArray();
        if (missing.Length > 0) return ("unsourced figure", $"no source for {string.Join(", ", missing)}");
        var kind = new[] { "computed", "document", "research", "brief" }.First(k => found.Any(x => x.Kind == k));
        return (kind, string.Join("; ", found.Select(x => x.Where).Distinct()));
    }

    /// <summary>
    /// Whether the text itself is found, not only its figures: declared a scale, a computed key, in the brief, a Qloo
    /// title, or on a document's page or in research word for word. A line sourced by its figures alone (the year in
    /// <c>2005 · Directed by F. Gary Gray</c>) still makes a claim in its words.
    /// </summary>
    internal bool Wholly(string text, IReadOnlyDictionary<string, string> computed, IReadOnlySet<string>? scale)
    {
        if (scale?.Contains(text.Trim()) == true || computed.ContainsKey(text) || computed.ContainsKey(text.Trim())) return true;
        var n = Normal(text);
        return n.Length > 0 && (brief.Contains(n, StringComparison.Ordinal) || qloo.ContainsKey(n)
            || n.Length >= 4 && (pages.Any(p => p.Text.Contains(n, StringComparison.Ordinal)) || research.Any(r => r.Text.Contains(n, StringComparison.Ordinal))));
    }
    #endregion

    #region Private methods
    private static string Normal(string? text) => FilmReadBack.Normal(text);

    /// <summary>The brief: the director's words between its markers, or the whole file.</summary>
    private static string ReadBrief(string? root)
    {
        if (root is null) return "";
        var path = Path.Combine(root, "brief.md");
        if (!File.Exists(path)) return "";
        var text = File.ReadAllText(path);
        var (a, b) = (text.IndexOf("BRIEF-BEGIN", StringComparison.Ordinal), text.IndexOf("BRIEF-END", StringComparison.Ordinal));
        return a >= 0 && b > a ? text[a..b] : text;
    }

    /// <summary>
    /// Every page of every document in <c>documents/</c> that <c>Documents.ask</c> has read as text, from its page cache
    /// (keyed by the file's hash, as the reader keys it): the document, its page number, the page's text and figures.
    /// </summary>
    private static (string Doc, int Page, string Text, HashSet<string> Figures)[] ReadPages(string? root)
    {
        if (root is null) return [];
        var docs = Path.Combine(root, "documents");
        var cache = Path.Combine(root, ".polson", "documents", "pages");
        if (!Directory.Exists(docs) || !Directory.Exists(cache)) return [];

        var found = new List<(string, int, string, HashSet<string>)>();
        foreach (var file in Directory.EnumerateFiles(docs))
        {
            var hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file)))[..32];
            var json = Path.Combine(cache, hash, "pages.json");
            if (!File.Exists(json)) continue;
            var pagesNode = JsonNode.Parse(File.ReadAllText(json))?["Pages"]?.AsArray() ?? [];
            var name = "documents/" + Path.GetFileName(file);
            for (var i = 0; i < pagesNode.Count; i++)
            {
                var text = pagesNode[i]?.GetValue<string>() ?? "";
                found.Add((name, i + 1, Normal(text), [.. FilmReadBack.Figures(text)]));
            }
        }
        return [.. found];
    }
    #endregion

    #region Fields
    private readonly string brief;
    private readonly HashSet<string> briefFigures;
    private readonly (string Doc, int Page, string Text, HashSet<string> Figures)[] pages;
    private readonly Dictionary<string, string> qloo;
    private readonly (string Id, string Text, HashSet<string> Figures)[] research;

    /// <summary>For reading a research result, never for writing one: its words as written, not as <c>’</c>.</summary>
    private static readonly JsonSerializerOptions Unescaped = new() { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    #endregion
}
