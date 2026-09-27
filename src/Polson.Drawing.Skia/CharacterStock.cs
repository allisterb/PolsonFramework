namespace Polson.Drawing.Skia;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;

/// <summary>
/// Stock bodies: rigged humans on the skeleton the pose clips were recorded on, with their body parts named, for
/// posing, placing and drawing as guides without building a character.
/// </summary>
/// <remarks>
/// <para>
/// <b>Not a script global.</b> A script reaches this through <c>Character.stock(name)</c> and
/// <c>Character.stocks()</c>.
/// </para>
/// <para>
/// <b>Where they come from.</b> Mesh2Motion's human variations (Scott Petrovic's app, which ships them), each a
/// separate artist's model re-rigged onto one skeleton. They are not in the repository: fetch the ones wanted into
/// <c>models/stock/</c> (the command is in <c>.gitignore</c>), or put a project's own in its <c>stock/</c> folder.
/// </para>
/// <para>
/// <b>Licences are per model, not per collection.</b> <see cref="Known"/> carries what Mesh2Motion's own source
/// states for each; most are CC0, and four are CC-BY or CC-BY-SA with an author to credit. A file not in that table
/// is reported with no licence, which means unknown rather than free.
/// </para>
/// </remarks>
public static class CharacterStock
{
    #region Types
    /// <summary>What Mesh2Motion states for one of its human variations.</summary>
    internal sealed record Terms(string Licence, string Author);
    #endregion

    #region Fields
    /// <summary>Mesh2Motion's human variations, from its <c>src/lib/RigModelVariations.ts</c>.</summary>
    internal static readonly Dictionary<string, Terms> Known = new(StringComparer.OrdinalIgnoreCase)
    {
        ["male"] = new("CC0", "Quaternius"),
        ["female"] = new("CC0", "Quaternius"),
        ["zombie"] = new("CC0", "Kenney.nl"),
        ["female_8"] = new("CC0", "elbolilloduro"),
        ["female_9"] = new("CC0", "elbolilloduro"),
        ["female_31"] = new("CC0", "elbolilloduro"),
        ["male_5"] = new("CC0", "elbolilloduro"),
        ["male_6"] = new("CC0", "elbolilloduro"),
        ["male_10"] = new("CC0", "elbolilloduro"),
        ["male_15"] = new("CC0", "elbolilloduro"),
        ["male_32"] = new("CC0", "elbolilloduro"),
        ["doctor_m"] = new("CC0", "elbolilloduro"),
        ["swat_male"] = new("CC0", "elbolilloduro"),
        ["police_male"] = new("CC0", "elbolilloduro"),
        ["police_female"] = new("CC0", "elbolilloduro"),
        ["hazmat_female"] = new("CC0", "elbolilloduro"),
        ["hazmat_suit_male"] = new("CC0", "elbolilloduro"),
        ["killer_4"] = new("CC0", "elbolilloduro"),
        ["killer_5"] = new("CC0", "elbolilloduro"),
        ["killer_6"] = new("CC0", "elbolilloduro"),
        ["killer_7"] = new("CC0", "elbolilloduro"),
        ["monster"] = new("CC0", "elbolilloduro"),
        ["monster_3"] = new("CC0", "elbolilloduro"),
        ["monster_4"] = new("CC0", "elbolilloduro"),
        ["monster_5"] = new("CC0", "elbolilloduro"),
        ["sophia"] = new("CC-BY-SA 4.0", "Tysan Tan"),
        ["jay"] = new("CC-BY", "Blender Studio"),
        ["sintel"] = new("CC-BY", "Blender Studio"),
        ["bunny"] = new("CC-BY", "Blender Studio"),
    };

    /// <summary>Body-part names on Mesh2Motion's human skeleton, which every variation shares.</summary>
    internal static readonly Dictionary<string, string> Parts = new(StringComparer.Ordinal)
    {
        ["hips"] = "pelvis", ["spine"] = "spine_02", ["chest"] = "spine_03", ["neck"] = "neck_01", ["head"] = "head",
        ["leftUpperArm"] = "upperarm_l", ["leftForearm"] = "lowerarm_l", ["leftHand"] = "hand_l",
        ["rightUpperArm"] = "upperarm_r", ["rightForearm"] = "lowerarm_r", ["rightHand"] = "hand_r",
        ["leftThigh"] = "thigh_l", ["leftShin"] = "calf_l", ["leftFoot"] = "foot_l",
        ["rightThigh"] = "thigh_r", ["rightShin"] = "calf_r", ["rightFoot"] = "foot_r",
    };

    static readonly ConcurrentDictionary<string, (DateTime Stamp, FaceMesh Mesh)> Cache = new(StringComparer.OrdinalIgnoreCase);
    #endregion

    #region Properties
    /// <summary>A stock folder from <c>Characters:Stock</c>, overriding discovery.</summary>
    public static string? LibraryOverride { get; set; }

    /// <summary>The folder under the project whose stock bodies come first.</summary>
    public const string ProjectFolder = "stock";
    #endregion

    #region Methods
    /// <summary>Where stock bodies are looked for, in order: the project's <c>stock/</c>, then the library.</summary>
    internal static List<string> Folders(string? projectRoot)
    {
        var folders = new List<string>();
        if (!string.IsNullOrEmpty(projectRoot) && Directory.Exists(Path.Combine(projectRoot, ProjectFolder)))
            folders.Add(Path.Combine(projectRoot, ProjectFolder));
        if (!string.IsNullOrWhiteSpace(LibraryOverride))
        {
            if (Directory.Exists(LibraryOverride)) folders.Add(LibraryOverride);
        }
        else if (FaceDetector.Candidates("models", "stock").FirstOrDefault(Directory.Exists) is { } found)
            folders.Add(found);
        return folders;
    }

    /// <summary>The names of the stock bodies available, for the host to announce.</summary>
    public static string[] Names(string? projectRoot) => [.. List(projectRoot).Select(s => s.Name)];

    /// <summary>Every stock body available, first name wins: <c>{ name, licence, author, file }</c>.</summary>
    internal static List<(string Name, string File, Terms? Terms)> List(string? projectRoot)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<(string, string, Terms?)>();
        foreach (var folder in Folders(projectRoot))
            foreach (var file in Directory.GetFiles(folder, "*.glb").Order(StringComparer.Ordinal))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                if (seen.Add(name)) list.Add((name, file, Known.GetValueOrDefault(name)));
            }
        return list;
    }

    /// <summary>A stock body with its body parts named, loaded once and reused.</summary>
    internal static FaceMesh Load(string? projectRoot, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var all = List(projectRoot);
        if (all.Count == 0)
            throw new InvalidOperationException(
                $"No stock bodies are here. Fetch them into models/stock/ (the commands are in .gitignore), put .glb files " +
                $"on Mesh2Motion's human skeleton in the project's {ProjectFolder}/ folder, or set Characters:Stock.");
        var found = all.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        if (found.File is null)
            throw new ArgumentException($"No stock body '{name}'. Here: {string.Join(", ", all.Select(s => s.Name))}.", nameof(name));

        var stamp = File.GetLastWriteTimeUtc(found.File);
        if (Cache.TryGetValue(found.File, out var hit) && hit.Stamp == stamp) return hit.Mesh;

        var mesh = MeshGltf.Load(found.File, $"stock/{found.Name}");
        var rig = mesh.Rig;
        var missing = Parts.Values.Where(bone => rig is null || !rig.FileNodes.ContainsKey(bone)).ToArray();
        if (missing.Length > 0)
            throw new ArgumentException(
                $"Stock body '{found.Name}' is not on Mesh2Motion's human skeleton: it has no {string.Join(", ", missing)}. " +
                "Load it with Mesh.load instead, and name its parts yourself.", nameof(name));
        foreach (var (part, bone) in Parts) rig!.Aliases[part] = bone;

        Cache[found.File] = (stamp, mesh);
        return mesh;
    }
    #endregion
}
