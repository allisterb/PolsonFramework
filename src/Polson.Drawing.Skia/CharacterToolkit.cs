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
    #endregion

    #region Private
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

    static object? ToClr(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.Object => e.EnumerateObject().ToDictionary(p => p.Name, p => ToClr(p.Value)),
        JsonValueKind.Array => e.EnumerateArray().Select(ToClr).ToList(),
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
