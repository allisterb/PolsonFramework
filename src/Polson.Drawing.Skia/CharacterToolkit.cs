namespace Polson.Drawing.Skia;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Characters built by the <c>GenerateCharacter</c> tool: posable, with semantic joint names and a face.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// <para>
/// <b>Read-only, like <c>Research</c>.</b> Building a character takes minutes — a mesh reconstructed
/// from the views, then rigged on a GPU — which no script can wait for, so the tool builds it outside
/// the sandbox and a script only loads what is finished. Each lives in <c>characters/&lt;name&gt;/</c>.
/// </para>
/// </remarks>
public class CharacterToolkit
{
    #region Constructors
    public CharacterToolkit(string? projectRoot = null) => this.projectRoot = projectRoot;
    #endregion

    #region Methods
    /// <summary>The names of the finished characters in this project.</summary>
    public string[] List()
    {
        var root = ProjectPath.Resolve(projectRoot, Folder, "name", "Read");
        return Directory.Exists(root)
            ? [.. Directory.GetDirectories(root)
                .Where(d => File.Exists(Path.Combine(d, CharacterBuilder.Manifest)))
                .Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)]
            : [];
    }

    /// <summary>
    /// A character, ready to pose and draw: <c>Character.load('mara').pose({ head: { yDeg: 30 } })</c>.
    /// </summary>
    /// <remarks>
    /// The mesh carries the rig, <c>mesh.jointMap</c> for its body-part names, and the face built from
    /// its turnaround already in place, so <c>Mesh.draw</c>'s <c>expression</c> acts on it. Loaded once
    /// per session and reused, so calling this in every script costs nothing after the first.
    /// </remarks>
    public FaceMesh Load(string name)
    {
        var dir = Dir(name, out var display);
        var stamp = File.GetLastWriteTimeUtc(Path.Combine(dir, CharacterBuilder.Manifest));
        if (Cache.TryGetValue(dir, out var hit) && hit.Stamp == stamp) return hit.Mesh;

        var mesh = CharacterBuilder.Assemble(dir, display);
        Cache[dir] = (stamp, mesh);
        return mesh;
    }

    /// <summary>What was recorded when the character was built: its files, joint names, and any warnings.</summary>
    public Dictionary<string, object?> Info(string name)
    {
        var dir = Dir(name, out _);
        using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(dir, CharacterBuilder.Manifest)));
        return (Dictionary<string, object?>)ToClr(doc.RootElement)!;
    }
    /// <summary>The clips <see cref="Retarget"/> can take a pose from: <c>{ name, seconds, file }</c> each.</summary>
    /// <remarks>
    /// From the project's <c>poses/</c> folder first, then the pose library. Where two files carry a
    /// clip of the same name, the first found wins.
    /// </remarks>
    public object?[] Clips() =>
        [.. PoseRetarget.Clips(projectRoot).Select(c => (object?)new Dictionary<string, object?>
        {
            ["name"] = c.Name,
            ["seconds"] = Math.Round((double)c.Seconds, 3),
            ["file"] = Path.GetFileName(c.File)
        })];

    /// <summary>
    /// A pose for <paramref name="character"/> taken from one frame of a recorded clip:
    /// <c>tomas.pose(Character.retarget(tomas, 'Idle_Rail_Call', { at: 0.5 }))</c>.
    /// </summary>
    /// <remarks>
    /// Keyed by body part, so it passes straight to <c>pose(...)</c> and one entry can be replaced
    /// before it does. <c>at</c> is a fraction of the clip, <c>time</c> is seconds; neither means the
    /// first frame. <paramref name="character"/> is a mesh from <c>Character.load</c>, or a name.
    /// </remarks>
    public Dictionary<string, object?> Retarget(object character, string clip, object? options = null)
    {
        var mesh = character switch
        {
            FaceMesh m => m,
            string name => Load(name),
            _ => throw new ArgumentException("Character.retarget needs a character from Character.load(name), or its name.", nameof(character))
        };
        if (mesh.Rig is not { } rig)
            throw new ArgumentException("Character.retarget needs a rigged character; this mesh carries no skeleton.", nameof(character));
        ArgumentException.ThrowIfNullOrWhiteSpace(clip);

        var clips = PoseRetarget.Clips(projectRoot);
        if (clips.Count == 0)
            throw new InvalidOperationException(
                $"No pose clips are available. Put .glb clips in the project's {PoseRetarget.ProjectFolder}/ folder, " +
                "or point Poses:Library at a folder of them.");
        var found = clips.FirstOrDefault(c => c.Name == clip)
            ?? throw new ArgumentException($"No clip '{clip}'. Nearest: {string.Join(", ", Nearest(clip, clips.Select(c => c.Name)))}. " +
                                           "Character.clips() lists them all.", nameof(clip));

        var opt = JsInterop.AsDict(options);
        float? at = null, time = null;
        if (opt != null)
            foreach (var key in opt.Keys)
            {
                var k = Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture);
                var v = Convert.ToSingle(opt[key!], System.Globalization.CultureInfo.InvariantCulture);
                switch (k)
                {
                    case "at": at = v; break;
                    case "time": time = v; break;
                    default: throw new ArgumentException($"Character.retarget has no option '{k}'. It takes at (0 to 1) or time (seconds).");
                }
            }
        if (at.HasValue && time.HasValue)
            throw new ArgumentException("Character.retarget takes at or time, not both: they are two ways of naming one moment.");
        if (at is < 0f or > 1f || (at.HasValue && !float.IsFinite(at.Value)))
            throw new ArgumentException($"'at' is a fraction of the clip, 0 to 1; got {at}.");
        if (time is { } tt && (!float.IsFinite(tt) || tt < 0f || tt > found.Seconds))
            throw new ArgumentException($"'time' is in seconds within the clip, 0 to {found.Seconds:0.###}; got {tt}.");

        return PoseRetarget.Retarget(rig, found, time ?? (at ?? 0f) * found.Seconds);
    }
    /// <summary>
    /// Moves hands and feet to goals and points the head, on top of a pose:
    /// <c>Character.reach(tomas, pose, { rightHand: { page: { x, y } } }, drawOptions)</c>.
    /// </summary>
    /// <remarks>
    /// Returns <c>{ pose, reached, miss }</c>: the new pose, whether each goal was reached, and how far
    /// short each fell in model units. A goal out of reach leaves the limb at full stretch toward it
    /// rather than tearing it, and says so in <c>reached</c>.
    /// </remarks>
    public Dictionary<string, object?> Reach(object character, object? pose, object goals, object? draw = null) =>
        CharacterIk.Reach(Character(character, "reach"), pose, goals, draw);

    /// <summary>
    /// Where a body part's joint is under a pose: <c>{ x, y, z }</c> in model space, or <c>{ x, y, depth }</c>
    /// on the page when given the options you pass to <c>Mesh.draw</c>.
    /// </summary>
    public Dictionary<string, object?> Where(object character, object? pose, string part, object? draw = null) =>
        CharacterIk.Where(Character(character, "where"), pose, part, draw);
    #endregion

    #region Private
    FaceMesh Character(object character, string call) => character switch
    {
        FaceMesh m => m,
        string name => Load(name),
        _ => throw new ArgumentException($"Character.{call} needs a character from Character.load(name), or its name.", nameof(character))
    };

    /// <summary>The closest few names, by edit distance, for a clip that was not found.</summary>
    static IEnumerable<string> Nearest(string wanted, IEnumerable<string> names) =>
        names.OrderBy(n => Distance(wanted.ToLowerInvariant(), n.ToLowerInvariant())).Take(4);

    static int Distance(string a, string b)
    {
        var d = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) d[j] = j;
        for (var i = 1; i <= a.Length; i++)
        {
            var prev = d[0];
            d[0] = i;
            for (var j = 1; j <= b.Length; j++)
            {
                var keep = d[j];
                d[j] = Math.Min(Math.Min(d[j] + 1, d[j - 1] + 1), prev + (a[i - 1] == b[j - 1] ? 0 : 1));
                prev = keep;
            }
        }
        return d[b.Length];
    }

    string Dir(string name, out string display)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!ValidName.IsMatch(name))
            throw new ArgumentException($"'{name}' is not a character name: use letters, digits, '-' and '_'.", nameof(name));

        display = $"{Folder}/{name}";
        var dir = ProjectPath.Resolve(projectRoot, display, nameof(name), "Read");
        if (!File.Exists(Path.Combine(dir, CharacterBuilder.Manifest)))
        {
            var known = List();
            throw new ArgumentException(
                $"No finished character '{name}'. "
                + (known.Length > 0 ? $"This project has: {string.Join(", ", known)}." : "This project has none yet.")
                + " Build one with the GenerateCharacter tool.", nameof(name));
        }
        return dir;
    }

    /// <remarks>
    /// Arrays become CLR arrays, not lists: the engine hands a script an array as a real JS array, and a
    /// list as a wrapper that <c>JSON.stringify</c> cannot serialise, so <c>JSON.stringify(info.warnings)</c>
    /// killed the script it was in (lastlight2, 2026-09-25).
    /// </remarks>
    static object? ToClr(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => ToClr(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().Select(ToClr).ToArray(),
        JsonValueKind.String => e.GetString(),
        JsonValueKind.Number => e.GetDouble(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        _ => null
    };

    readonly string? projectRoot;

    /// <summary>The folder characters live in, under the project.</summary>
    public const string Folder = "characters";

    internal static readonly Regex ValidName = new("^[A-Za-z0-9][A-Za-z0-9_-]{0,63}$", RegexOptions.Compiled);

    static readonly ConcurrentDictionary<string, (DateTime Stamp, FaceMesh Mesh)> Cache = new(StringComparer.OrdinalIgnoreCase);
    #endregion
}
