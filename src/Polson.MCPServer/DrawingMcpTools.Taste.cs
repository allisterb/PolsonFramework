namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

using Polson.ExtendedMind.Taste;

public partial class DrawingMcpTools
{
    #region Properties
    /// <summary>
    /// The Qloo client every session is a scope of, configured once at startup from <c>ApiKeys:Qloo</c>. Null
    /// when no key is configured, and the <c>Taste</c> tool says so plainly.
    /// </summary>
    /// <remarks>
    /// One client for the process, not one per session: Qloo's five-a-second limit and its monthly quota belong
    /// to the key, so the pacing and the last-known quota must be shared. Each call runs on
    /// <see cref="QlooClient.Scoped"/> with the session's own budget.
    /// </remarks>
    public static QlooClient? TasteClient { get; set; }

    /// <summary>Most results the tool returns in one reply. The full answer is kept for scripts either way.</summary>
    internal const int MaxTasteResults = 50;
    #endregion

    #region Methods
    [McpServerTool(Name = "Taste")]
    [Description("Asks Qloo's taste graph what an AUDIENCE actually loves — the artists, films, shows, brands and places " +
        "that over-index for a place, an age band, a gender or a community — and what those things share. Use it " +
        "BEFORE designing for an audience, so references, palette and imagery rest on measured taste rather than on " +
        "guesses about a group. Every answer carries the Qloo request it came from: cite it.\n\n" +
        "OPERATIONS (op):\n" +
        "- 'resolve': a name to Qloo ids, before using it as a signal. kind 'entity' (default; narrow with type), " +
        "'tag' (e.g. 'warm palette'), or 'audience' (e.g. 'Latino', 'Retirement'). Returns CANDIDATES. When `ambiguous` " +
        "is true, choose by disambiguation — and ASK THE DIRECTOR if the choice would change the result. Never pass an " +
        "id you have not resolved.\n" +
        "- 'insights': what an audience likes, of one type. Give `type` (artist, movie, tv_show, brand, book, podcast, " +
        "video_game, place, destination, person) and `audience` as JSON, e.g. {\"location\":\"Lagos\",\"age\":\"55_and_older\"}. " +
        "Extra Qloo parameters go in `query` as JSON. A location is REQUIRED with an age or gender: without one, Qloo " +
        "returns roughly the all-time popular list, not this audience. A place NAME is fuzzy-matched — 'Lagos' is also " +
        "in Portugal and Spain — so check `locality` in the reply, or resolve it first (op 'resolve', type 'locality') " +
        "and pass the chosen id as the location.\n" +
        "- 'profile': START HERE for an audience. What it loves that its PLACE does not: for each domain (default " +
        "movie, tv_show, artist; or `type`, comma-separated), the audience's list minus the place's baseline — the " +
        "DISTINCTIVE references — plus the place's aesthetic vocabulary. Give `audience` with a location. Cite the " +
        "distinctive references as the evidence for a direction; derive the look from what those references look and " +
        "sound like. `placeAesthetics` are the PLACE's: Qloo does not vary tags by age, so never present them as this " +
        "audience's. `pace` counts its distinctive films and shows tagged fast and slow; with two profiles, " +
        "Taste.contrast(a, b) tests the difference and leads its summary with it. About 7 requests; a second audience in " +
        "the same place reuses the cached baseline.\n" +
        "- 'describe': the tags a set of entities share, from `entities` (ids). Give `type` as tag kinds — " +
        "'style', 'theme,archetype', 'plot' — or 'film' for all the kinds that describe a film. With no `type`, " +
        "entities this session knows are films or shows get 'film', said in `note`: UNFILTERED, Qloo's top tags over six " +
        "films were a credit card, a hotel rating and 'Identifies as women-owned' — tags from every domain, places and " +
        "brands included. 'all' asks for that overview anyway. 'aesthetic' and 'motif' are on no film: " +
        "asked for over films they return fashion words. WEAK EVIDENCE: measured, two halves of one audience's titles shared none of their tags, so with a dozen titles " +
        "the tags follow which titles you picked. Use it to explore, never as the basis of a direction.\n" +
        "- 'demographics': who an entity appeals to, by age band and gender, from `entities`. Use it to check a " +
        "reference really skews to your audience before you borrow from it.\n" +
        "- 'compare': what separates two sets of entities: `entities` against `versus`. One row per tag, with how many " +
        "entities on each side carry it (a, b): `tagsA`/`tagsB` lean to one side, most leaning first — read those; " +
        "`shared` both carry. Qloo's own scores barely vary, so they are not used. `take` caps each list; the whole " +
        "answer is Taste.get(id).comparison.\n" +
        "- 'audiences': Qloo's audience ids under a type, e.g. type 'communities', 'life_stage', 'global_issues'.\n" +
        "- 'status': this session's allowance and the key's monthly quota. Free.\n\n" +
        "WHAT QLOO IS NOT. It holds measured taste, not culture's rules: it does not know which colours mean mourning " +
        "or which numbers are unlucky. Those come from research, cited separately — never present a Qloo result as one. " +
        "Results are AGGREGATE affinities: never describe them as facts about a person, and never infer health, beliefs " +
        "or identity from them.\n\n" +
        "COST. Requests are metered (5 a second, 10,000 a month for the whole studio) and this session has an " +
        "allowance (`budget` in every reply). A repeated question is answered from the cache and costs nothing, so " +
        "ask the same thing the same way. A query with an unknown or misspelt parameter is REFUSED before sending — " +
        "Qloo would otherwise ignore it silently and answer with nothing — so a refusal is free advice.\n\n" +
        "Every answer stays readable from later scripts as the `Taste` global: Taste.get('t3').entities, " +
        "Taste.latest.cite() for the caption line.")]
    public Task<JsonObject> Taste(
        [Description("'profile', 'resolve', 'insights', 'describe', 'demographics', 'compare', 'audiences' or 'status'.")] string op,
        [Description("resolve: the name or words to look up.")] string? name = null,
        [Description("resolve: 'entity' (default), 'tag' or 'audience'.")] string? kind = null,
        [Description("insights and compare: the result type — artist, movie, tv_show, brand, book, podcast, video_game, place, destination, person, or a full urn. resolve: narrows an entity search, or for kind 'tag' the tag kind. describe: tag kinds to keep, comma-separated, e.g. 'style,theme'; 'film' for the kinds that describe a film; 'all' for every kind, places and brands included. audiences: the audience type, e.g. 'communities'.")] string? type = null,
        [Description("insights: the audience, as JSON: {\"location\":\"Mexico City\",\"age\":\"55_and_older\",\"gender\":\"female\",\"audiences\":[\"<audience id>\"]}. location is a place name, or a locality id from resolve (type 'locality'). Age bands: 24_and_younger, 25_to_29, 30_to_34, 35_to_44, 45_to_54, 55_and_older. location is required with age or gender.")] string? audience = null,
        [Description("insights: further Qloo Insights parameters as a JSON object, e.g. {\"filter.popularity.min\":0.5,\"signal.interests.entities\":[\"<id>\"],\"feature.explainability\":true}. Checked before sending.")] string? query = null,
        [Description("describe, demographics, compare: Qloo entity ids, comma-separated, from 'resolve' or an earlier answer.")] string? entities = null,
        [Description("compare: the second set of entity ids, comma-separated.")] string? versus = null,
        [Description("How many results (1-50; default 10, 25 for describe). compare: rows per list in the reply.")] int? take = null,
        [Description("What this is for, in your own words. Recorded, and how a script finds it later with Taste.find(...).")] string? label = null,
        RequestContext<CallToolRequestParams>? context = null,
        CancellationToken cancellationToken = default)
    => RecordedAsync(nameof(Taste), async () =>
    {
        var session = Registry.GetOrCreate(GetSessionId(context?.Server));
        var registry = session.Taste;
        op = (op ?? string.Empty).Trim().ToLowerInvariant();
        label = Trimmed(label) ?? $"{op} {Trimmed(name) ?? Trimmed(type) ?? Trimmed(entities) ?? string.Empty}".Trim();

        if (op == "status") return TasteStatus(registry.Budget);

        if (TasteClient is null)
        {
            return new JsonObject
            {
                ["ok"] = false,
                ["error"] = "No Qloo API key is configured, or Qloo:Enabled is false, so taste data is unavailable for "
                          + "this whole run.",
                ["remedy"] = "Do not guess what an audience likes and present it as measured. Design from the brief, and "
                           + "tell the director the taste grounding could not be fetched.",
            };
        }

        var client = TasteClient.Scoped(registry.Budget);
        if (op == "profile") return await TasteProfile(session, client, label, audience, type, query, take, cancellationToken);

        var count = Math.Clamp(take ?? (op == "describe" ? 25 : 10), 1, MaxTasteResults);
        var (call, refusal, note) = TastePlan(client, op, name, kind, type, audience, query, entities, versus, count, cancellationToken, registry);

        if (refusal is not null || call is null)
        {
            refusal ??= "Nothing to do.";
            Events.Append("taste.refused", session.Stage, null, new Dictionary<string, object?> { ["op"] = op, ["label"] = label, ["reason"] = refusal });
            return new JsonObject
            {
                ["ok"] = false, ["op"] = op, ["error"] = refusal, ["note"] = "Nothing was sent or spent.",
                ["budget"] = TasteBudgetNode(registry.Budget),
            };
        }

        var response = await call();
        var result = registry.Add(op, label, response);
        Events.Append(response.Success ? "taste.query" : "taste.failed", session.Stage, null, new Dictionary<string, object?>
        {
            ["id"] = result.Id,
            ["op"] = op,
            ["label"] = label,
            ["request"] = response.Request,
            ["failure"] = response.Success ? null : response.Failure.ToString(),
            ["fromCache"] = response.FromCache,
            ["results"] = result.Entities.Length + result.Tags.Length + result.Audiences.Length + result.Demographics.Length,
            ["entities"] = response.Success && op == "insights" && result.Entities.Length > 0 ? TasteListed(result) : null,
            ["warnings"] = response.Warnings.Count,
            ["spent"] = registry.Budget.Spent,
            ["remaining"] = registry.Budget.Remaining,
            ["monthlyRemaining"] = registry.Budget.MonthlyRemaining,
        });

        var reply = TasteReply(op, result, response, registry.Budget, Trimmed(name), count, (TasteIds(entities).Length, TasteIds(versus).Length));
        if (note is not null) reply["note"] = note;
        return reply;
    });
    #endregion

    #region Internal methods
    internal const string TasteTypeList = "artist, movie, tv_show, brand, book, podcast, video_game, place, destination, person";

    /// <summary>
    /// An insights answer's entities as the studio's Audiences view lists them: the item number a direction cites, the name
    /// and the tag names, so a cited tag can be shown on the entity that carries it. Kept in the project's run record only;
    /// no id, score or request.
    /// </summary>
    internal static object?[] TasteListed(TasteResult result) =>
        [.. result.Entities.OfType<Dictionary<string, object?>>().Select((e, i) => (object?)new Dictionary<string, object?>
        {
            ["rank"] = i + 1,
            ["name"] = e.GetValueOrDefault("name") as string ?? "?",
            ["tags"] = (e.GetValueOrDefault("tags") as object?[] ?? []).OfType<Dictionary<string, object?>>()
                .Select(t => t.GetValueOrDefault("name") as string).OfType<string>().ToArray(),
        })];

    /// <summary>Domains a profile reads when none are named: what an audience watches and listens to.</summary>
    internal const string TasteProfileDomains = "movie,tv_show,artist";

    /// <summary>
    /// Popularity percentile a profile asks for unless the query sets one. Measured: none returned the long tail
    /// (titles no one would recognise), 0.9 the all-time list; 0.5 kept both recognisable and distinctive.
    /// </summary>
    internal const double TasteProfileFloor = 0.5;

    /// <summary>
    /// The taste profile (design doc §4.3): per domain, what the audience loves that its place's baseline does not
    /// list, plus the place's aesthetic vocabulary. Every request is kept, and refusals cost nothing.
    /// </summary>
    /// <remarks>
    /// Built on <b>membership</b>, not on affinity: measured, Qloo's affinity saturates near the top for anything in
    /// a list (audience minus baseline came to ±0.01), while the lists themselves separate audiences clearly. The
    /// tags come from the place, not the audience, because Qloo ignores age for tag results.
    /// </remarks>
    private async Task<JsonObject> TasteProfile(
        SessionContext session, QlooClient client, string label, string? audience, string? types, string? extra, int? take,
        CancellationToken ct)
    {
        var registry = session.Taste;
        JsonObject Refuse(string reason)
        {
            Events.Append("taste.refused", session.Stage, null, new Dictionary<string, object?> { ["op"] = "profile", ["label"] = label, ["reason"] = reason });
            return new JsonObject { ["ok"] = false, ["op"] = "profile", ["error"] = reason, ["note"] = "Nothing was sent or spent.", ["budget"] = TasteBudgetNode(registry.Budget) };
        }

        var domains = (Trimmed(types) ?? TasteProfileDomains).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => (Given: t, Urn: TasteType(t))).ToArray();
        if (domains.FirstOrDefault(d => d.Urn is null || !d.Urn.StartsWith("urn:entity:", StringComparison.Ordinal)) is { Given: { } bad })
            return Refuse($"profile domain '{bad}' is not an entity type. Use {TasteTypeList}.");

        var count = Math.Clamp(take ?? 30, 5, MaxTasteResults);
        var (template, problem) = TasteQuery(domains[0].Urn!, audience, extra, count);
        if (template is null) return Refuse(problem!);
        var place = template.Names.FirstOrDefault(n => n.StartsWith("signal.location", StringComparison.Ordinal) && n != "signal.location.radius");
        if (place is null)
            return Refuse("A profile needs the audience's place: audience {\"location\": \"...\"}. Its contrast is against that place's baseline.");
        if (!template.Has("filter.popularity.min")) template["filter.popularity.min"] = TasteProfileFloor;

        var responses = new List<QlooResponse>();
        var domainNodes = new List<object?>();
        var distinctiveAll = new List<object?>();
        QlooLocality? locality = null;

        // **What the audience is set against.** An age, gender or audience against its place: what this group favours that
        // the place does not. A place alone against the world: with nothing to take away, the place's own baseline is the
        // audience itself, and coffee_1 (2026-10-30, Tokyo and Lagos at any age) came back with every title shared and
        // nothing distinctive, so its run had no field to design from.
        var demographic = template.Names.Any(n => n.StartsWith("signal.demographics.", StringComparison.Ordinal) && template[n] is not null);
        var against = demographic ? "place" : "world";

        foreach (var (given, urn) in domains)
        {
            var mine = template.Clone().Set("filter.type", urn);
            var baseline = mine.Clone();
            var prefix = demographic ? "signal.demographics." : "signal.location";
            foreach (var n in baseline.Names.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).ToArray()) baseline[n] = null;

            var a = await client.Insights(mine, ct);
            var b = await client.Insights(baseline, ct);
            responses.Add(a);
            responses.Add(b);
            locality ??= a.Locality ?? b.Locality;
            if (!a.Success || !b.Success) break;

            var baseIds = b.Entities.Select(e => e.EntityId).ToHashSet(StringComparer.Ordinal);
            var ranked = a.Entities.Select((e, i) => (Entity: e, Rank: i + 1)).ToArray();
            object?[] Objects(IEnumerable<(QlooEntity Entity, int Rank)> items) => [.. items.Select(x =>
            {
                var o = (Dictionary<string, object?>)TasteObjects.Entity(x.Entity)!;
                o["audienceRank"] = x.Rank;
                // A profile is read for its references, so of Qloo's properties it keeps only what visual research
                // reads: the picture Taste.images fetches, the look Taste.vocabulary tallies, and a film's year for the
                // research board. The rest stays in the single-call answers.
                o["properties"] = o.GetValueOrDefault("properties") is Dictionary<string, object?> all
                    ? all.Where(p => p.Key is "image" or "style_description" or "release_year").ToDictionary(p => p.Key, p => p.Value)
                    : null;
                return (object?)o;
            })];
            var distinctive = Objects(ranked.Where(x => !baseIds.Contains(x.Entity.EntityId)));
            var shared = Objects(ranked.Where(x => baseIds.Contains(x.Entity.EntityId)));
            distinctiveAll.AddRange(distinctive);
            domainNodes.Add(new Dictionary<string, object?>
            {
                ["type"] = urn, ["distinctive"] = distinctive, ["shared"] = shared,
                ["audienceRequest"] = a.Request, ["baselineRequest"] = b.Request,
            });
        }

        // Qloo echoes where it placed a location *name*, never one given as an id — and an id is what the tool tells an
        // agent to pass for an ambiguous place like Lagos. Both poster runs then had nothing in the reply to confirm the
        // place by. The id came from a `resolve` earlier in this session, so name it from there.
        var localityNote = (string?)null;
        if (locality is null && place == "signal.location" && template[place] is string placeId)
        {
            locality = ResolvedLocality(registry, placeId);
            localityNote = locality is null
                ? $"The location was given as the id {placeId}, which Qloo does not echo and this session never resolved. Resolve it (op 'resolve', type 'locality') to see which place it is."
                : "Named from this session's resolve: Qloo does not echo a place given by id.";
        }

        object?[] placeTags = [];
        string? tagsRequest = null;
        if (responses.All(r => r.Success))
        {
            var tq = new QlooQuery
            {
                ["filter.type"] = "urn:tag", [place] = template[place],
                ["filter.tag.types"] = new[] { "urn:tag:aesthetic_property:qloo", "urn:tag:motif:qloo" }, ["take"] = 20,
            };
            var tags = await client.Insights(tq, ct);
            responses.Add(tags);
            tagsRequest = tags.Request;
            placeTags = [.. tags.Tags.Select(TasteObjects.Tag)];
        }

        var profile = new Dictionary<string, object?>
        {
            ["audience"] = new Dictionary<string, object?>
            {
                ["location"] = template[place], ["age"] = template["signal.demographics.age"],
                ["gender"] = template["signal.demographics.gender"], ["audiences"] = template["signal.demographics.audiences"],
            },
            ["locality"] = locality is null ? null : new Dictionary<string, object?> { ["id"] = locality.EntityId, ["name"] = locality.Name, ["disambiguation"] = locality.Disambiguation },
            ["floor"] = template["filter.popularity.min"],
            ["domains"] = domainNodes.ToArray(),
            ["placeAesthetics"] = new Dictionary<string, object?>
            {
                ["tags"] = placeTags, ["request"] = tagsRequest,
                ["note"] = "The place's aesthetic vocabulary. Qloo does not vary it by age or gender, so it is never evidence about this audience.",
            },
            ["requests"] = responses.Select(r => r.Request).ToArray(),
        };

        profile["against"] = against;

        var notes = new List<string>();
        if (!demographic)
            notes.Add("This audience is a place with no age or gender, so its distinctive titles are what the place favours over the world's "
                      + "popular list, not what one group in it favours. Say so where you cite it.");
        if (domainNodes.Cast<Dictionary<string, object?>>().Any(d => ((object?[])d["distinctive"]!).Length == 0))
            notes.Add($"A domain has nothing distinctive: this audience's list matches the {(demographic ? "place's" : "world's")}. Say so rather than read a difference into it.");

        var result = registry.Add(id => TasteObjects.ProfileResult(id, label, profile, responses, [.. distinctiveAll], placeTags, locality, notes));

        // What the studio's Audiences view draws: who, where, each distinctive list and its pace — all of it, not its
        // top, because the pace counts are over the whole list and a reader must be able to find every title they
        // count (nightshift5 showed 9 of 21, leaning slow under a bar reading 71% fast). Titles
        // and ranks the run used as evidence — the run record lives in the project, never the repository (Qloo's
        // terms forbid storing responses in a public one) — and no request line, key or raw payload.
        var pace = result.Pace();
        var (fastTitles, slowTitles) = (((string[])pace["fastTitles"]!).ToHashSet(), ((string[])pace["slowTitles"]!).ToHashSet());
        Events.Append(result.Ok ? "taste.query" : "taste.failed", session.Stage, null, new Dictionary<string, object?>
        {
            ["id"] = result.Id, ["op"] = "profile", ["label"] = label, ["requests"] = responses.Count,
            ["fromCache"] = result.FromCache, ["distinctive"] = distinctiveAll.Count, ["placeTags"] = placeTags.Length,
            ["failure"] = result.Ok ? null : responses.First(r => !r.Success).Failure.ToString(),
            ["spent"] = registry.Budget.Spent, ["remaining"] = registry.Budget.Remaining, ["monthlyRemaining"] = registry.Budget.MonthlyRemaining,
            ["audience"] = new Dictionary<string, object?>
            {
                ["place"] = locality?.Name ?? template[place] as string, ["age"] = template["signal.demographics.age"] as string,
                ["gender"] = template["signal.demographics.gender"] as string,
            },
            ["against"] = against,
            ["domains"] = domainNodes.Cast<Dictionary<string, object?>>().Select(d => (object?)new Dictionary<string, object?>
            {
                ["type"] = d["type"],
                ["n"] = ((object?[])d["distinctive"]!).Length,
                ["shared"] = ((object?[])d["shared"]!).Length,
                ["top"] = ((object?[])d["distinctive"]!).Cast<Dictionary<string, object?>>().Select(e =>
                {
                    var name = e["name"] as string ?? "?";
                    return (object?)new Dictionary<string, object?>
                    {
                        ["name"] = name, ["rank"] = e["audienceRank"],
                        ["pace"] = fastTitles.Contains(name) ? "fast" : slowTitles.Contains(name) ? "slow" : null,
                    };
                }).ToArray(),
            }).ToArray(),
            ["pace"] = new Dictionary<string, object?> { ["fast"] = pace["fast"], ["slow"] = pace["slow"], ["n"] = pace["n"], ["fastShare"] = pace["fastShare"] },
        });

        var reply = new JsonObject
        {
            ["ok"] = result.Ok, ["op"] = "profile", ["id"] = result.Id, ["label"] = label, ["fromCache"] = result.FromCache,
            ["requests"] = responses.Count, ["against"] = against,
        };
        if (!result.Ok) { reply["error"] = result.Error; reply["remedy"] = result.Remedy; }
        if (locality is not null) reply["locality"] = new JsonObject { ["name"] = locality.Name, ["disambiguation"] = locality.Disambiguation };
        if (localityNote is not null)
        {
            reply["locality"] ??= new JsonObject { ["id"] = (string?)template[place] };
            ((JsonObject)reply["locality"]!)["note"] = localityNote;
        }
        reply["domains"] = new JsonArray([.. domainNodes.Cast<Dictionary<string, object?>>().Select(d => (JsonNode)new JsonObject
        {
            ["type"] = (string?)d["type"],
            ["distinctive"] = new JsonArray([.. ((object?[])d["distinctive"]!).Take(12).Cast<Dictionary<string, object?>>().Select(e => (JsonNode)new JsonObject
            {
                ["id"] = (string?)e["id"], ["name"] = (string?)e["name"], ["disambiguation"] = (string?)e["disambiguation"], ["rank"] = (int)e["audienceRank"]!,
            })]),
            ["distinctiveCount"] = ((object?[])d["distinctive"]!).Length,
            ["sharedCount"] = ((object?[])d["shared"]!).Length,
        })]);
        // The pace of the distinctive films and shows, in the reply itself: poster5's most decision-changing number was
        // the pace difference between two audiences, and it found it only by dumping Object.keys on a script result.
        reply["pace"] = new JsonObject
        {
            ["fast"] = (int)pace["fast"]!, ["slow"] = (int)pace["slow"]!, ["n"] = (int)pace["n"]!, ["fastShare"] = (double?)pace["fastShare"],
            ["note"] = "Of the distinctive films and shows, how many Qloo tags fast-paced and slow. With a second audience's profile, "
                     + "Taste.contrast(a, b).pace tests whether they differ (Fisher's exact p) and leads its summary.",
        };
        reply["placeAesthetics"] = new JsonArray([.. placeTags.Cast<Dictionary<string, object?>>().Select(t => (JsonNode?)(string?)t["name"])]);
        if (result.Warnings.Length > 0) reply["warnings"] = new JsonArray([.. result.Warnings.Select(w => (JsonNode?)w)]);
        reply["note"] = "Distinctive = in this audience's list and not in its place's baseline: cite these. placeAesthetics are the "
                      + "place's, the same for every age. The full profile is Taste.get('" + result.Id + "').profile.";
        reply["budget"] = TasteBudgetNode(registry.Budget);
        return reply;
    }

    /// <summary>
    /// The Qloo call one operation makes, or why it cannot be made, and a note for the reply when the tool chose
    /// something the caller did not ask for. Deciding is separate from calling so every refusal is found before
    /// anything is sent. <paramref name="registry"/> says which entities this session knows to be films.
    /// </summary>
    internal static (Func<Task<QlooResponse>>? Call, string? Refusal, string? Note) TastePlan(
        QlooClient client, string op, string? name, string? kind, string? type, string? audience, string? query,
        string? entities, string? versus, int count, CancellationToken ct, TasteRegistry? registry = null)
    {
        if (op == "describe") return TasteDescribePlan(client, type, entities, count, ct, registry);
        var (call, refusal) = TasteCall(client, op, name, kind, type, audience, query, entities, versus, count, ct);
        return (call, refusal, null);
    }

    /// <summary>
    /// <c>describe</c>: the tags a set of entities share. Without kinds, a set of films asks for the kinds that
    /// describe a film (<see cref="TasteDescribe"/>); anything else gets the overview, capped per kind, since over
    /// any entities Qloo returns tags from every domain and uncapped the commonest kind would crowd out the rest.
    /// </summary>
    private static (Func<Task<QlooResponse>>? Call, string? Refusal, string? Note) TasteDescribePlan(
        QlooClient client, string? type, string? entities, int count, CancellationToken ct, TasteRegistry? registry)
    {
        if (TasteIds(entities) is not { Length: > 0 } described) return (null, "describe needs entities: Qloo ids, comma-separated.", null);
        var (kinds, note) = TasteDescribe.Kinds(type, TasteTagTypes(type), [.. described.Select(id => registry?.TypeOf(id))]);

        var q = new QlooQuery { ["filter.type"] = "urn:tag", ["signal.interests.entities"] = described, ["take"] = count };
        if (kinds.Length > 0) q["filter.tag.types"] = kinds;
        // Kinds the caller named are taken as asked. The overview and the film kinds share the take, capped per kind:
        // measured, the six film kinds came back 6, 6, 6, 4, 2 and 1 of 25 (2026-10-18).
        if (kinds.Length == 0 || kinds.SequenceEqual(TasteDescribe.FilmKinds)) { q["diversify.by"] = "subtype"; q["diversify.take"] = Math.Max(3, count / 4); }
        return (async () => await client.Insights(q, ct), null, note);
    }

    private static (Func<Task<QlooResponse>>? Call, string? Refusal) TasteCall(
        QlooClient client, string op, string? name, string? kind, string? type, string? audience, string? query,
        string? entities, string? versus, int count, CancellationToken ct)
    {
        switch (op)
        {
            case "resolve":
            {
                if (Trimmed(name) is not string lookFor) return (null, "resolve needs a name.");
                return (Trimmed(kind) ?? "entity").ToLowerInvariant() switch
                {
                    "tag" => (async () => await client.Tags(lookFor, TasteTagType(type) is string tagType ? [tagType] : null, take: Math.Min(count, 10), cancellationToken: ct), null),
                    "audience" => (async () => await client.Audiences(TasteAudienceType(type), 100, lookFor, ct), null),
                    "entity" => (async () => await client.Search(lookFor,
                        TasteType(type) is string t && t.StartsWith("urn:entity:", StringComparison.Ordinal) ? [t] : null, Math.Min(count, 10), ct), null),
                    var other => (null, $"resolve kind is 'entity', 'tag' or 'audience'; '{other}' is not."),
                };
            }

            case "insights":
            {
                if (TasteType(type) is not string resultType) return (null, $"insights needs a type: {TasteTypeList}.");
                var (built, problem) = TasteQuery(resultType, audience, query, count);
                return built is null ? (null, problem) : (async () => await client.Insights(built, ct), null);
            }

            case "demographics":
            {
                if (TasteIds(entities) is not { Length: > 0 } who) return (null, "demographics needs entities: Qloo ids, comma-separated.");
                var tags = who.Where(i => i.StartsWith("urn:tag:", StringComparison.Ordinal)).ToArray();
                var q = new QlooQuery
                {
                    ["filter.type"] = "urn:demographics",
                    ["signal.interests.entities"] = who.Except(tags).ToArray(),
                    ["signal.interests.tags"] = tags,
                };
                return (async () => await client.Insights(q, ct), null);
            }

            case "compare":
            {
                if (TasteIds(entities) is not { Length: > 0 } left || TasteIds(versus) is not { Length: > 0 } right)
                    return (null, "compare needs entities and versus: two sets of Qloo ids, comma-separated.");
                return (async () => await client.Compare(left, right, TasteType(type), count, ct), null);
            }

            case "audiences":
                return (async () => await client.Audiences(TasteAudienceType(type), 100, null, ct), null);

            default:
                return (null, $"Unknown op '{op}'. Use profile, resolve, insights, describe, demographics, compare, audiences or status.");
        }
    }

    /// <summary>A short type name to its Qloo URN, or the URN as given; null when it is neither.</summary>
    /// <summary>A locality this session resolved, by its Qloo id — the name an id-given location never comes back with.</summary>
    private static QlooLocality? ResolvedLocality(TasteRegistry registry, string id) =>
        registry.All.Where(r => r.Op == "resolve")
            .SelectMany(r => r.Entities.OfType<Dictionary<string, object?>>())
            .Where(e => string.Equals(e.GetValueOrDefault("id") as string, id, StringComparison.OrdinalIgnoreCase))
            .Select(e => new QlooLocality(id, e.GetValueOrDefault("name") as string ?? id, e.GetValueOrDefault("disambiguation") as string, null, null))
            .FirstOrDefault();

    internal static string? TasteType(string? type) => Trimmed(type)?.ToLowerInvariant() switch
    {
        null => null,
        "artist" or "music" or "musician" => "urn:entity:artist",
        "movie" or "film" or "films" or "movies" => "urn:entity:movie",
        "tv" or "tv_show" or "show" or "television" => "urn:entity:tv_show",
        "brand" or "book" or "podcast" or "place" or "destination" or "person" => $"urn:entity:{type!.Trim().ToLowerInvariant()}",
        "video_game" or "game" => "urn:entity:video_game",
        "locality" or "city" or "location" => "urn:entity:locality",
        "tag" => "urn:tag",
        "demographics" => "urn:demographics",
        "heatmap" => "urn:heatmap",
        var urn when urn.StartsWith("urn:", StringComparison.Ordinal) => urn,
        _ => null,
    };

    private static string? TasteTagType(string? type) => TasteTagTypes(type).FirstOrDefault();

    /// <summary>
    /// Tag kinds, comma-separated, as URNs: <c>aesthetic</c> is <c>urn:tag:aesthetic_property:qloo</c>, another short
    /// name <c>x</c> is <c>urn:tag:x:qloo</c>, and a URN passes through.
    /// </summary>
    internal static string[] TasteTagTypes(string? types) =>
        (types ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(t => t.ToLowerInvariant() switch
            {
                "aesthetic" or "aesthetics" or "palette" => "urn:tag:aesthetic_property:qloo",
                var urn when urn.StartsWith("urn:tag:", StringComparison.Ordinal) => urn,
                var other when other.StartsWith("urn:", StringComparison.Ordinal) => null,
                var shortName => $"urn:tag:{shortName.Replace(' ', '_')}:qloo",
            })
            .OfType<string>().Distinct().ToArray();

    private static string? TasteAudienceType(string? type) => Trimmed(type) switch
    {
        null => null,
        var urn when urn.StartsWith("urn:audience:", StringComparison.Ordinal) => urn,
        var shortName => "urn:audience:" + shortName.ToLowerInvariant().Replace(' ', '_'),
    };

    private static string[] TasteIds(string? list) =>
        (list ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToArray();

    /// <summary>The Insights query from a type, an audience and extra parameters, or the reason it cannot be built.</summary>
    internal static (QlooQuery? Query, string? Problem) TasteQuery(string resultType, string? audience, string? extra, int take)
    {
        var q = new QlooQuery { ["filter.type"] = resultType, ["take"] = take };

        if (Trimmed(extra) is string json)
        {
            if (TasteObject(json, "query") is not (JsonObject parameters, null)) return (null, TasteObject(json, "query").Problem);
            foreach (var (key, value) in parameters) q[key] = TasteValue(value);
        }

        if (Trimmed(audience) is string who)
        {
            if (TasteObject(who, "audience") is not (JsonObject a, null)) return (null, TasteObject(who, "audience").Problem);
            foreach (var key in a.Select(p => p.Key))
                if (key is not ("location" or "age" or "gender" or "audiences"))
                    return (null, $"audience has no '{key}'. It takes location, age, gender and audiences; other Qloo parameters go in query.");
            // A resolved locality id goes in signal.location, which takes Qloo ids; a name is fuzzy-matched through
            // signal.location.query. "Lagos" matches Lagos in Nigeria, Portugal and Spain (2026-10-15), so an id is
            // the way to be sure.
            if (TasteValue(a["location"]) is string place)
                q[Guid.TryParse(place, out _) ? "signal.location" : "signal.location.query"] = place;
            q["signal.demographics.age"] = TasteValue(a["age"]) ?? q["signal.demographics.age"];
            q["signal.demographics.gender"] = TasteValue(a["gender"]) ?? q["signal.demographics.gender"];
            q["signal.demographics.audiences"] = TasteValue(a["audiences"]) ?? q["signal.demographics.audiences"];
        }

        if ((q.Has("signal.demographics.age") || q.Has("signal.demographics.gender"))
            && !q.Names.Any(n => n.StartsWith("signal.location", StringComparison.Ordinal)))
        {
            return (null, "An age or gender needs a location as well — audience {\"location\": \"...\"}. Without one Qloo "
                        + "returns roughly the all-time popular list rather than this audience's taste (measured 2026-10-15).");
        }

        return (q, null);
    }

    private static (JsonObject? Value, string? Problem) TasteObject(string json, string what)
    {
        try
        {
            return JsonNode.Parse(json) is JsonObject o ? (o, null) : (null, $"{what} must be a JSON object.");
        }
        catch (JsonException ex)
        {
            return (null, $"{what} is not valid JSON: {ex.Message}");
        }
    }

    /// <summary>A JSON value as <see cref="QlooQuery"/> takes one: arrays become lists, the rest their text.</summary>
    private static object? TasteValue(JsonNode? node) => node switch
    {
        null => null,
        JsonArray array => array.Select(TasteValue).Where(v => v is not null).ToArray(),
        JsonValue v when v.TryGetValue<bool>(out var b) => b,
        JsonValue v when v.TryGetValue<double>(out var d) => d,
        JsonValue v => v.ToString(),
        _ => node.ToJsonString(),
    };

    private static JsonObject TasteBudgetNode(QlooBudget budget) => new()
    {
        ["spent"] = budget.Spent,
        ["remaining"] = budget.Remaining,
        ["cacheHits"] = budget.CacheHits,
        ["monthlyRemaining"] = budget.MonthlyRemaining,
        ["monthlyReserve"] = budget.MonthlyReserve,
    };

    private static JsonObject TasteStatus(QlooBudget budget) => new()
    {
        ["ok"] = true,
        ["op"] = "status",
        ["configured"] = TasteClient is not null,
        ["budget"] = TasteBudgetNode(budget),
        ["note"] = budget.ReserveReached
            ? "The monthly reserve is reached: only cached answers are available."
            : "Repeated questions are answered from the cache and cost nothing.",
    };

    /// <summary>
    /// The reply the agent reads: compact, because it lands in the context window. The full answer — every tag,
    /// every property — is kept on the session for scripts.
    /// </summary>
    /// <param name="sides">For <c>compare</c>, how many entities each side named.</param>
    private static JsonObject TasteReply(string op, TasteResult result, QlooResponse response, QlooBudget budget, string? asked, int count,
        (int A, int B) sides = default)
    {
        var reply = new JsonObject
        {
            ["ok"] = result.Ok,
            ["op"] = op,
            ["id"] = result.Id,
            ["label"] = result.Label,
            ["request"] = result.Request,
            ["fromCache"] = result.FromCache,
        };
        if (result.CachedUtc is not null) reply["cachedUtc"] = result.CachedUtc;
        if (!result.Ok)
        {
            reply["error"] = result.Error;
            reply["remedy"] = result.Remedy;
        }
        if (result.Warnings.Length > 0) reply["warnings"] = new JsonArray([.. result.Warnings.Select(w => (JsonNode?)w)]);
        if (result.Locality is { } place)
            reply["locality"] = new JsonObject { ["name"] = place["name"] as string, ["disambiguation"] = place["disambiguation"] as string };

        switch (response)
        {
            case QlooEntitiesResponse found when op == "resolve":
                var candidates = found.Entities.Take(count).ToArray();
                reply["candidates"] = new JsonArray([.. candidates.Select(e => (JsonNode)new JsonObject
                {
                    ["id"] = e.EntityId, ["name"] = e.Name, ["type"] = e.Types.FirstOrDefault(),
                    ["disambiguation"] = e.Disambiguation, ["popularity"] = Round(e.Popularity),
                })]);
                var exact = candidates.Count(e => string.Equals(e.Name, asked, StringComparison.OrdinalIgnoreCase));
                reply["ambiguous"] = exact > 1 || (exact == 0 && candidates.Length > 1);
                break;
            case QlooTagsResponse:
            case QlooAudiencesResponse:
                reply["candidates"] = new JsonArray([.. (result.Tags.Length > 0 ? result.Tags : result.Audiences).Take(count).Select(TasteNode)]);
                reply["ambiguous"] = (result.Tags.Length > 0 ? result.Tags : result.Audiences).Length > 1;
                break;
            case QlooInsightsResponse insights:
                if (insights.Entities.Count > 0)
                {
                    reply["entities"] = new JsonArray([.. insights.Entities.Take(count).Select(e => (JsonNode)new JsonObject
                    {
                        ["id"] = e.EntityId, ["name"] = e.Name, ["type"] = e.Types.FirstOrDefault(),
                        ["disambiguation"] = e.Disambiguation, ["affinity"] = Round(e.Affinity), ["popularity"] = Round(e.Popularity),
                        ["tags"] = new JsonArray([.. e.Tags.Take(8).Select(t => (JsonNode?)t.Name)]),
                    })]);
                }
                if (insights.Tags.Count > 0)
                {
                    reply["tags"] = new JsonArray([.. insights.Tags.Take(count).Select(t => (JsonNode)new JsonObject
                    {
                        ["id"] = t.Id, ["name"] = t.Name, ["type"] = t.Type, ["affinity"] = Round(t.Affinity), ["popularity"] = Round(t.Popularity),
                    })]);
                }
                if (insights.Demographics.Count > 0)
                    reply["demographics"] = new JsonArray([.. insights.Demographics.Select(d => JsonNode.Parse(d.GetRawText()))]);
                break;
            case QlooCompareResponse compared when compared.Results is { } raw:
                // Never the raw answer: one row per tag per pairing came to 358 KB for seven entities a side (poster5),
                // which the host refused to deliver. Scripts keep the whole of it on the result.
                reply["comparison"] = TasteNode(TasteCompare.Summary(raw, count, result.Id, sides.A, sides.B));
                break;
        }

        reply["resultCount"] = result.Entities.Length + result.Tags.Length + result.Audiences.Length + result.Demographics.Length;
        reply["budget"] = TasteBudgetNode(budget);
        return reply;
    }

    private static JsonNode? TasteNode(object? value) => value is null ? null : JsonSerializer.SerializeToNode(value);

    private static double? Round(double? value) => value is double v ? Math.Round(v, 3) : null;
    #endregion
}
