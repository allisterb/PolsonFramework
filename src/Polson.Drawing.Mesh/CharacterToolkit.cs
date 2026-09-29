namespace Polson.Drawing.Mesh;

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
    /// <para>
    /// The mesh carries the rig, <c>mesh.jointMap</c> for its body-part names, and the face built from
    /// its turnaround already in place, so <c>Mesh.draw</c>'s <c>expression</c> acts on it. Loaded once
    /// per session and reused, so calling this in every script costs nothing after the first.
    /// </para>
    /// <para>
    /// <c>{ rig: 'unirig' }</c> or <c>{ rig: 'solver' }</c> loads that rig of the character instead of its default,
    /// where it was built with both: the same body skinned two ways, which fail differently. <c>Character.info(name).rig</c>
    /// says which it has and which is the default.
    /// </para>
    /// </remarks>
    public FaceMesh Load(string name, object? options = null)
    {
        var dir = Dir(name, out var display);
        string? rig = null;
        if (JsInterop.AsDict(options) is { } opt)
            foreach (var key in opt.Keys)
            {
                var k = Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture);
                if (k != "rig") throw new ArgumentException($"Character.load has no option '{k}'. It takes rig.");
                rig = Convert.ToString(opt[key!], System.Globalization.CultureInfo.InvariantCulture);
            }

        var manifestPath = Path.Combine(dir, CharacterBuilder.Manifest);
        var stamp = File.GetLastWriteTimeUtc(manifestPath);
        rig ??= CharacterBuilder.DefaultRig(System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(manifestPath))!.AsObject());
        var key2 = dir + "|" + rig;
        if (Cache.TryGetValue(key2, out var hit) && hit.Stamp == stamp) return hit.Mesh;

        var mesh = CharacterBuilder.Assemble(dir, display, rig);
        Cache[key2] = (stamp, mesh);
        return mesh;
    }

    /// <summary>
    /// A stock body, rigged and with its body parts named: <c>Character.stock('female')</c>. No build needed.
    /// </summary>
    /// <remarks>
    /// Mesh2Motion's human models, on the skeleton the pose clips were recorded on: <c>male</c> and <c>female</c> come
    /// with the studio (fetched by <c>tools/bootstrap.py</c>), and a project's <c>stock/</c> folder or <c>models/stock/</c> can add others. It poses, retargets, reaches, places and reshapes like a built character. One
    /// without a texture draws as a wireframe. <c>Character.stocks()</c> lists them with their licences.
    /// </remarks>
    public FaceMesh Stock(string name = "male") => CharacterStock.Load(projectRoot, name);

    /// <summary>The stock bodies here: <c>{ name, licence, author, file }</c>, with <c>licence</c> null where it is not known.</summary>
    /// <remarks>
    /// Licences are per model, as Mesh2Motion states them: most are CC0; <c>sophia</c> is CC-BY-SA and <c>jay</c>,
    /// <c>sintel</c> and <c>bunny</c> are CC-BY, so a drawing made over them credits the author.
    /// </remarks>
    public object?[] Stocks() =>
        [.. CharacterStock.List(projectRoot).Select(s => (object?)new Dictionary<string, object?>
        {
            ["name"] = s.Name,
            ["licence"] = s.Terms?.Licence,
            ["author"] = s.Terms?.Author,
            ["file"] = Path.GetFileName(s.File)
        })];

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
    /// A pose for <paramref name="character"/> taken from a recorded clip or from a body found in a picture:
    /// <c>tomas.pose(Character.retarget(tomas, 'Idle_Rail_Call', { at: 0.5 }))</c>, or
    /// <c>Character.retarget(tomas, Character.detect(photo))</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Keyed by body part, so it passes straight to <c>pose(...)</c> and one entry can be replaced before it
    /// does. <paramref name="character"/> is a mesh from <c>Character.load</c>, or a name.
    /// </para>
    /// <para>
    /// <b>From a clip:</b> <c>at</c> is a fraction of the clip, <c>time</c> is seconds; neither means the first
    /// frame. The hips move in place with the clip — a crouch lowers them — unless <c>moveHips</c> is false.
    /// </para>
    /// <para>
    /// <b>From a detection:</b> the body's 3D landmarks set the trunk, head and limbs, including what points at
    /// or away from the camera. The picture's angle is kept, so drawing at <c>yawDeg: 0</c> shows the pose as
    /// photographed; <c>faceFront: true</c> turns it to face the character's front instead. The hips drop until
    /// the lower foot is on the floor, and a second foot level with it in the picture is planted too, unless
    /// <c>moveHips</c> is false. A planted foot is laid flat, because the detector reads flat feet as pointing
    /// down; <c>flatFeet: false</c> keeps its reading, for a figure on its toes.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> Retarget(object character, object source, object? options = null)
    {
        var mesh = Character(character, "retarget");
        if (mesh.Rig is not { } rig)
            throw new ArgumentException("Character.retarget needs a rigged character; this mesh carries no skeleton.", nameof(character));
        var opt = JsInterop.AsDict(options);

        if (source is BodyDetection body)
        {
            bool faceFront = false, moveHips = true, flatFeet = true;
            if (opt != null)
                foreach (var key in opt.Keys)
                {
                    var k = Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture);
                    var on = Convert.ToBoolean(opt[key!], System.Globalization.CultureInfo.InvariantCulture);
                    switch (k)
                    {
                        case "faceFront": faceFront = on; break;
                        case "moveHips": moveHips = on; break;
                        case "flatFeet": flatFeet = on; break;
                        default: throw new ArgumentException($"Character.retarget from a detection has no option '{k}'. It takes faceFront, moveHips and flatFeet.");
                    }
                }
            return LandmarkRetarget.Retarget(mesh, body, faceFront, moveHips, flatFeet);
        }

        if (source is not string clip)
            throw new ArgumentException(
                $"Character.retarget takes a clip name (Character.clips()) or a body from Character.detect(image), and got {source?.GetType().Name ?? "nothing"}.",
                nameof(source));
        ArgumentException.ThrowIfNullOrWhiteSpace(clip);

        var clips = PoseRetarget.Clips(projectRoot);
        if (clips.Count == 0)
            throw new InvalidOperationException(
                $"No pose clips are available. Put .glb clips in the project's {PoseRetarget.ProjectFolder}/ folder, " +
                "or point Poses:Library at a folder of them.");
        var found = clips.FirstOrDefault(c => c.Name == clip)
            ?? throw new ArgumentException($"No clip '{clip}'. Nearest: {string.Join(", ", Nearest(clip, clips.Select(c => c.Name)))}. " +
                                           "Character.clips() lists them all.", nameof(source));

        float? at = null, time = null;
        var moveHipsClip = true;
        if (opt != null)
            foreach (var key in opt.Keys)
            {
                var k = Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture);
                if (k == "moveHips") { moveHipsClip = Convert.ToBoolean(opt[key!], System.Globalization.CultureInfo.InvariantCulture); continue; }
                var v = Convert.ToSingle(opt[key!], System.Globalization.CultureInfo.InvariantCulture);
                switch (k)
                {
                    case "at": at = v; break;
                    case "time": time = v; break;
                    default: throw new ArgumentException($"Character.retarget has no option '{k}'. It takes at (0 to 1) or time (seconds), and moveHips.");
                }
            }
        if (at.HasValue && time.HasValue)
            throw new ArgumentException("Character.retarget takes at or time, not both: they are two ways of naming one moment.");
        if (at is < 0f or > 1f || (at.HasValue && !float.IsFinite(at.Value)))
            throw new ArgumentException($"'at' is a fraction of the clip, 0 to 1; got {at}.");
        if (time is { } tt && (!float.IsFinite(tt) || tt < 0f || tt > found.Seconds))
            throw new ArgumentException($"'time' is in seconds within the clip, 0 to {found.Seconds:0.###}; got {tt}.");

        return PoseRetarget.Retarget(rig, found, time ?? (at ?? 0f) * found.Seconds, moveHipsClip);
    }

    /// <summary>
    /// Finds a body in a picture — a photograph, a sheet, a drawing — for <c>Character.retarget</c> to pose a
    /// character from: <c>const body = Character.detect(photo); if (body.found) …</c>.
    /// </summary>
    /// <remarks>
    /// Needs the optional MediaPipe backend, as <c>Face.detect</c> does; <c>Character.canDetect</c> says whether it
    /// is here. Not finding a body is a result — read <c>found</c> — and only a missing backend throws.
    /// </remarks>
    public BodyDetection Detect(object image, int timeoutMs = 60000)
    {
        var bitmap = image switch
        {
            SkiaBitmapWrapper b => b,
            SkiaCanvas c => c.Bitmap,
            null => throw new ArgumentNullException(nameof(image)),
            _ => throw new ArgumentException($"Character.detect takes a bitmap or a canvas, and got {image.GetType().Name}.", nameof(image))
        };
        return BodyDetector.Detect(bitmap, timeoutMs);
    }

    /// <summary>Whether <see cref="Detect"/> will work here.</summary>
    public bool CanDetect => BodyDetector.Available;

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
    /// <summary>
    /// The options that stand a character in a frame, for <c>Mesh.draw</c> and <c>Character.reach</c>:
    /// <c>Mesh.draw(ctx, tomas.pose(p), Character.place(tomas, panel, { at: { x: 0.3, y: 0.92 }, height: 0.7 }))</c>.
    /// </summary>
    /// <remarks>
    /// <c>frame</c> is any <c>{ x, y, width, height }</c>, such as a panel from <c>Layout.grid</c>.
    /// <c>at</c> is where the feet stand, as fractions of the frame, and <c>height</c> how tall the
    /// character stands, as a share of the frame's height — standing, whatever the pose, so a crouch
    /// comes out shorter rather than enlarged.
    /// </remarks>
    public Dictionary<string, object?> Place(object character, object frame, object? options = null) =>
        CharacterIk.Place(Character(character, "place"), frame, options);

    /// <summary>
    /// A character's proportions, each a share of its standing height: <c>torso</c>, <c>neck</c>,
    /// <c>head</c>, <c>upperArm</c>, <c>forearm</c>, <c>thigh</c>, <c>shin</c>, <c>shoulders</c>, <c>hips</c>.
    /// </summary>
    /// <remarks>
    /// Measured between the named joints, and the head from its joint to the top of the mesh, so hair and a
    /// hat count. What <c>mesh.proportion({ like })</c> copies.
    /// </remarks>
    public Dictionary<string, object?> Proportions(object character) =>
        CharacterProportion.Measure(Character(character, "proportions"))
            .ToDictionary(kv => kv.Key, kv => (object?)Math.Round(kv.Value, 4));

    /// <summary>
    /// A character posed with its hanging garment simulated as cloth:
    /// <c>Mesh.draw(ctx, Character.drape(warden, pose).mesh, draw)</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <paramref name="pose"/> is what <c>pose(...)</c> takes. Returns <c>{ mesh, draped, vertices, seconds, cached,
    /// note }</c>: <c>mesh</c> is always drawable. When nothing hangs, or the character was built before garments
    /// were found, it is the plain pose, <c>draped</c> is false and <c>note</c> says why.
    /// </para>
    /// <para>
    /// Runs Blender, about 5 to 20 s the first time for a pose, then free: the result is cached with the character.
    /// <c>Character.canDrape</c> says whether Blender is here. <paramref name="options"/> takes <c>settings</c>,
    /// the cloth solver's (<c>mass</c>, <c>stretch</c>, <c>bending</c>, <c>pinStiffness</c> and others; see the SDK
    /// reference), and an unknown one is refused.
    /// </para>
    /// </remarks>
    public Dictionary<string, object?> Drape(object character, object? pose, object? options = null)
    {
        var mesh = character switch
        {
            string n => Load(n),
            FaceMesh m => m,
            _ => throw new ArgumentException("Character.drape needs a character from Character.load(name), or its name.", nameof(character))
        };
        // The cache key is the folder and the rig, so it says which character this is and which rig's labels to use.
        var cached = Cache.FirstOrDefault(kv => ReferenceEquals(kv.Value.Mesh, mesh)).Key
            ?? throw new ArgumentException("Character.drape needs the character itself, from Character.load(name), or its name; " +
                                           "not a posed or reshaped mesh. Pass the pose as the second argument.", nameof(character));
        var name = Path.GetFileName(cached[..cached.LastIndexOf('|')]);
        var rig = cached[(cached.LastIndexOf('|') + 1)..];
        if (!BlenderDriver.CanDrape)
            throw new InvalidOperationException("Character.drape needs Blender, and it is not installed here. Check Character.canDrape first.");

        var opt = JsInterop.AsDict(options);
        System.Collections.IDictionary? settings = null;
        if (opt != null)
            foreach (var key in opt.Keys)
            {
                var k = Convert.ToString(key, System.Globalization.CultureInfo.InvariantCulture);
                if (k != "settings")
                    throw new ArgumentException($"Character.drape has no option '{k}'. It takes settings.");
                settings = JsInterop.AsDict(opt[key!]);
            }
        return ClothDrape.Drape(Dir(name, out _), name, rig, mesh, pose, settings);
    }

    /// <summary>Whether <see cref="Drape"/> will work here: Blender and the cloth script are installed.</summary>
    public bool CanDrape => BlenderDriver.CanDrape;
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
