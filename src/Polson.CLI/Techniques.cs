namespace Polson.CLI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

/// <summary>
/// The techniques a director can request or deny for one production: the catalogue, its validation, the section it
/// writes into the instructions, and the check of a finished run against it.
/// </summary>
/// <remarks>
/// <para>
/// <b>What an agent does follows what its instructions require, not what they offer.</b> poster6 and nightshift3
/// (2026-10-25) both built the kit and the mock-ups their workflows required, and neither took the alternative grid
/// offered beside the other routes; poster6 named it as what it would do with more time. A request turns an option into
/// an instruction for one production without making it compulsory for every one; a denial takes a technique away where a
/// default would make every film the same — Manual 25's mystery-to-certainty is good advice, and so every film used it.
/// </para>
/// <para>
/// The catalogue is data (<c>ProjectTemplate/_shared/techniques.json</c>) so that the CLI, the instructions and the
/// studio's form read one list. A technique says how its use shows in the run record — a pattern in the scripts, an
/// artifact's name, a pattern in <c>direction.md</c> — or that nothing measures it, in which case the report says it was
/// judged, not measured.
/// </para>
/// </remarks>
internal static class Techniques
{
    #region Types
    /// <summary>One technique: its id, name, workflows, source, summary, the two instructions, and its evidence.</summary>
    internal sealed record Technique(string Id, string Name, string[] Workflows, string Source, string Summary, string Request,
        string Deny, Evidence Evidence);

    /// <summary>How use shows: a pattern over the run's scripts, over the names in <c>artifacts/</c>, over <c>direction.md</c>, or judged.</summary>
    internal sealed record Evidence(string? Scripts, string? Artifacts, string? Direction, string? Judged);

    /// <summary>A director's choices: requested and denied ids, each list in catalogue order.</summary>
    internal sealed record Choices(string[] Request, string[] Deny)
    {
        public static readonly Choices None = new([], []);

        public bool IsEmpty => Request.Length == 0 && Deny.Length == 0;
    }

    /// <summary>What a run did with one choice: <c>used</c>, <c>not used</c>, or <c>judged</c>, and what showed it.</summary>
    internal sealed record Outcome(string Id, string Name, bool Requested, string Status, string Detail);
    #endregion

    #region Properties
    /// <summary>The catalogue, in its order.</summary>
    internal static IReadOnlyList<Technique> All => _all ??= Load();
    #endregion

    #region Methods
    /// <summary>The techniques that apply to <paramref name="workflow"/>; a <c>_qloo</c> twin takes its base workflow's.</summary>
    internal static Technique[] For(string workflow) =>
        [.. All.Where(t => t.Workflows.Contains(ProjectGenerator.BaseWorkflow(workflow), StringComparer.Ordinal))];

    /// <summary>
    /// The director's choices checked against the catalogue and the workflow: each id known, applying to the workflow,
    /// and in one list only. Null and an error message when not, the message naming the ids that would do.
    /// </summary>
    internal static (Choices? Choices, string? Error) Parse(string workflow, IEnumerable<string> request, IEnumerable<string> deny)
    {
        static string[] Ids(IEnumerable<string> given) =>
            [.. given.SelectMany(g => g.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).Select(g => g.ToLowerInvariant()).Distinct()];

        var (asked, refused) = (Ids(request), Ids(deny));
        if (asked.Length == 0 && refused.Length == 0) return (Choices.None, null);

        var offered = For(workflow);
        if (offered.Length == 0)
            return (null, $"The '{workflow}' workflow has no techniques to request or deny. They apply to: {string.Join(", ", All.SelectMany(t => t.Workflows).Distinct())}.");
        foreach (var (ids, flag) in new[] { (asked, "--request"), (refused, "--deny") })
            foreach (var id in ids)
            {
                if (All.FirstOrDefault(t => t.Id == id) is not { } known)
                    return (null, $"{flag} {id}: no such technique. For '{workflow}': {string.Join(", ", offered.Select(t => t.Id))}.");
                if (!known.Workflows.Contains(ProjectGenerator.BaseWorkflow(workflow)))
                    return (null, $"{flag} {id}: {known.Name} is for {string.Join(", ", known.Workflows)}, not '{workflow}'. For '{workflow}': {string.Join(", ", offered.Select(t => t.Id))}.");
            }
        var both = asked.Intersect(refused).ToArray();
        if (both.Length > 0) return (null, $"{string.Join(", ", both)}: both requested and denied. Choose one.");

        string[] Ordered(string[] ids) => [.. All.Where(t => ids.Contains(t.Id)).Select(t => t.Id)];
        return (new Choices(Ordered(asked), Ordered(refused)), null);
    }

    /// <summary>The choices a project's manifest recorded, or none.</summary>
    internal static Choices FromManifest(JsonNode? manifest)
    {
        if (manifest?["techniques"] is not JsonObject t) return Choices.None;
        static string[] List(JsonNode? n) => n is JsonArray a ? [.. a.Select(x => x?.GetValue<string>() ?? "").Where(x => x.Length > 0)] : [];
        return new Choices(List(t["request"]), List(t["deny"]));
    }

    /// <summary>The manifest's field: <c>{ request, deny }</c>, or null for no choices.</summary>
    internal static Dictionary<string, object?>? ToManifest(Choices choices) => choices.IsEmpty ? null
        : new() { ["request"] = choices.Request, ["deny"] = choices.Deny };

    /// <summary>The instructions' section for the director's choices; empty when there are none.</summary>
    internal static string Section(Choices choices)
    {
        if (choices.IsEmpty) return string.Empty;
        Technique Of(string id) => All.First(t => t.Id == id);
        var s = new StringBuilder();
        s.AppendLine("## Techniques the director chose for this production");
        s.AppendLine();
        s.AppendLine("The director set these for this production. **They override the workflow's own defaults wherever the two disagree,**");
        s.AppendLine("and they are not questions: in an autonomous run or not, do not ask about them.");
        s.AppendLine();
        if (choices.Request.Length > 0)
        {
            s.AppendLine("**Use** — each in the delivered piece. If one genuinely does not serve this brief, use it in one of the concepts all the");
            s.AppendLine("same and record in `direction.md` why that concept lost: never drop a requested technique silently.");
            s.AppendLine();
            foreach (var t in choices.Request.Select(Of)) s.AppendLine($"- **{t.Name}** ({t.Source}). {t.Request}");
            s.AppendLine();
        }
        if (choices.Deny.Length > 0)
        {
            s.AppendLine("**Do not use** — in no concept, comp, frame or delivered piece, even where the workflow or a manual recommends it.");
            s.AppendLine();
            foreach (var t in choices.Deny.Select(Of)) s.AppendLine($"- **{t.Name}** ({t.Source}). {t.Deny}");
            s.AppendLine();
        }
        s.AppendLine("Record them in `direction.md` under a heading **Techniques**: each requested one and where it is used, or why the");
        s.AppendLine("concept that used it lost; each denied one and what you did in its place.");
        return s.ToString();
    }

    /// <summary>
    /// What a finished run did with its choices, read from the record: the saved scripts, the names in <c>artifacts/</c>,
    /// and <c>direction.md</c>. A technique nothing measures is reported as judged.
    /// </summary>
    internal static Outcome[] Check(string dir, Choices choices)
    {
        if (choices.IsEmpty) return [];
        string Read(string path) => File.Exists(path) ? File.ReadAllText(path) : "";
        var scripts = string.Join("\n", Directory.Exists(Path.Combine(dir, "scripts"))
            ? Directory.GetFiles(Path.Combine(dir, "scripts"), "*.js").Select(Read) : []);
        var artifacts = Directory.Exists(Path.Combine(dir, "artifacts"))
            ? Directory.GetFiles(Path.Combine(dir, "artifacts")).Select(Path.GetFileName).OfType<string>().ToArray() : [];
        var direction = Read(Path.Combine(dir, "direction.md"));

        Outcome Judge(string id, bool requested)
        {
            var t = All.First(x => x.Id == id);
            var e = t.Evidence;
            if (e.Judged is { } what) return new(id, t.Name, requested, "judged", $"not measured: {what}");
            var found = new List<string>();
            if (e.Scripts is { } sp && Regex.Match(scripts, sp) is { Success: true } m) found.Add($"scripts: {m.Value.Trim()}");
            if (e.Artifacts is { } ap && artifacts.FirstOrDefault(a => Regex.IsMatch(a, ap, RegexOptions.IgnoreCase)) is { } art) found.Add($"artifacts/{art}");
            if (e.Direction is { } dp && Regex.Match(direction, dp, RegexOptions.IgnoreCase | RegexOptions.Multiline) is { Success: true } d) found.Add($"direction.md: {d.Value.Trim()}");
            return found.Count > 0
                ? new(id, t.Name, requested, "used", string.Join("; ", found))
                : new(id, t.Name, requested, "not used", $"no {Describe(e)} in the record");
        }
        return [.. choices.Request.Select(id => Judge(id, true)), .. choices.Deny.Select(id => Judge(id, false))];
    }
    #endregion

    #region Private methods
    private static string Describe(Evidence e) => string.Join(" or ", new[]
    {
        e.Scripts is null ? null : $"script matching /{e.Scripts}/",
        e.Artifacts is null ? null : $"artifact named /{e.Artifacts}/",
        e.Direction is null ? null : $"direction.md heading /{e.Direction}/",
    }.OfType<string>());

    private static List<Technique> Load()
    {
        var assembly = typeof(Techniques).Assembly;
        var resource = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("_shared.techniques.json", StringComparison.Ordinal))
            ?? throw new FileNotFoundException("The technique catalogue is not embedded: ProjectTemplate/_shared/techniques.json.");
        using var stream = assembly.GetManifestResourceStream(resource)!;
        var root = JsonNode.Parse(stream) ?? throw new InvalidDataException("techniques.json is empty.");
        string Text(JsonNode n, string key) => n[key]?.GetValue<string>() ?? throw new InvalidDataException($"techniques.json: a technique has no '{key}'.");
        return [.. (root["techniques"] as JsonArray ?? []).OfType<JsonNode>().Select(n => new Technique(
            Text(n, "id"), Text(n, "name"),
            [.. (n["workflows"] as JsonArray ?? []).Select(w => w!.GetValue<string>())],
            Text(n, "source"), Text(n, "summary"), Text(n, "request"), Text(n, "deny"),
            new Evidence(n["evidence"]?["scripts"]?.GetValue<string>(), n["evidence"]?["artifacts"]?.GetValue<string>(),
                n["evidence"]?["direction"]?.GetValue<string>(), n["evidence"]?["judged"]?.GetValue<string>())))];
    }
    #endregion

    #region Fields
    private static List<Technique>? _all;
    #endregion
}
