namespace Polson.Drawing.Mesh;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SkiaSharp;

/// <summary>
/// <c>Character.drape</c>: a character's hanging garment simulated as cloth while the body moves from rest into a
/// pose, by Blender's cloth solver (<c>src/blender/cloth_drape.py</c>, run by <see cref="BlenderDriver.Drape"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>What hangs is decided from the garment labels the build projected</b> (<c>garments.json</c>), not from the
/// geometry. A vertex hangs when it is labelled <c>garment</c> or <c>lower</c>, lies below the crotch, and belongs
/// to a connected stretch of such vertices that is the outermost layer there: met first by a ray through the middle
/// between the legs, or first and last by a ray through a leg that crosses the leg inside it. Nothing below the
/// lowest such hit, the hem, hangs. That keeps trousers rigged, under an open coat or where the views' masks ran down
/// them. Everything else is the body, which the cloth collides with.
/// </para>
/// <para>
/// <b>The body moves through five keys</b>, the pose's angles and hip move scaled by 0.2, 0.4, 0.6, 0.8 and 1, so
/// a leg swings up rather than cutting through the garment in one frame. See
/// <c>docs/internal/character-rigging-modes.md</c> §6 for the recipe and what it measured.
/// </para>
/// <para>
/// <b>Cached on the request</b> in <c>characters/&lt;name&gt;/drapes/</c>: the pose, the settings, which vertices
/// hang, and the cloth script's own hash. Rebuilding the character replaces the folder and the cache with it. The key is the request,
/// never the geometry, since a simulation reproduces the shape and not the bytes.
/// </para>
/// </remarks>
internal static class ClothDrape
{
    #region Methods
    /// <summary>Drapes <paramref name="character"/>, from its folder <paramref name="dir"/>, in <paramref name="pose"/>.</summary>
    internal static Dictionary<string, object?> Drape(string dir, string name, FaceMesh character, object? pose, IDictionary? settings)
    {
        var posed = character.Pose(pose);
        Dictionary<string, object?> Unchanged(string note) => new()
        {
            ["mesh"] = posed, ["draped"] = false, ["vertices"] = 0, ["seconds"] = 0.0, ["cached"] = false, ["note"] = note
        };

        var manifest = JsonNode.Parse(File.ReadAllText(Path.Combine(dir, CharacterBuilder.Manifest)))!;
        if (manifest["garments"] is not JsonObject garments)
            return Unchanged($"'{name}' was built before the garment step, so it is not known what hangs. Rebuild it with " +
                             "GenerateCharacter, and the build will find its garments.");
        if (garments["hangs"]?.GetValue<bool>() != true)
            return Unchanged(garments["hangs"] is null
                ? $"Whether anything hangs on '{name}' could not be read when it was built: {garments["reason"]}."
                : $"Nothing hangs between '{name}''s legs, so there is no cloth to drape; the pose is unchanged.");
        var labelsPath = Path.Combine(dir, CharacterBuilder.GarmentsJson);
        if (!File.Exists(labelsPath))
            return Unchanged($"'{name}' has no garment labels ({CharacterBuilder.GarmentsJson}), so nothing can drape.");

        var labels = JsonNode.Parse(File.ReadAllText(labelsPath))!;
        var plan = Hanging(character,
            [.. labels["labels"]!.AsArray().Select(n => n!.GetValue<int>())],
            [.. labels["names"]!.AsArray().Select(n => n!.GetValue<string>())]);
        if (plan.Count == 0)
            return Unchanged($"No labelled garment on '{name}' hangs below the crotch across the legs: {plan.Why}");

        var settingsJson = Settings(settings);
        var key = Key(pose, settingsJson, plan.Mask);
        var cacheDir = Path.Combine(dir, "drapes");
        var cachePath = Path.Combine(cacheDir, key + ".json");
        var cached = File.Exists(cachePath);
        var started = DateTime.UtcNow;
        if (!cached)
        {
            var request = Request(character, pose, plan, settingsJson);
            var work = Path.Combine(cacheDir, "run-" + key);
            try
            {
                using var result = BlenderDriver.Drape(request, work);
                Directory.CreateDirectory(cacheDir);
                File.WriteAllText(cachePath, result.RootElement.GetRawText());
            }
            finally
            {
                if (Directory.Exists(work)) Directory.Delete(work, true);
            }
        }

        using var doc = JsonDocument.Parse(File.ReadAllText(cachePath));
        var root = doc.RootElement;
        var vertices = (SKPoint3[])posed.Vertices.Clone();
        var ids = root.GetProperty("indices").EnumerateArray().Select(e => e.GetInt32()).ToArray();
        var positions = root.GetProperty("positions").EnumerateArray().ToArray();
        for (var i = 0; i < ids.Length; i++)
        {
            var p = positions[i];
            vertices[ids[i]] = new SKPoint3((float)p[0].GetDouble(), (float)p[1].GetDouble(), (float)p[2].GetDouble());
        }
        return new()
        {
            ["mesh"] = posed.WithVertices(vertices),
            ["draped"] = true,
            ["vertices"] = ids.Length,
            ["seconds"] = Math.Round((DateTime.UtcNow - started).TotalSeconds, 1),
            ["cached"] = cached,
            ["note"] = null
        };
    }

    /// <summary>Which vertices hang: labelled garment, below the crotch, in a stretch that is the outermost layer there.</summary>
    internal static HangPlan Hanging(FaceMesh character, int[] labels, string[] names)
    {
        if (labels.Length > character.VertexCount)
            throw new InvalidOperationException(
                $"The garment labels are for {labels.Length} vertices and the character has {character.VertexCount}; rebuild it.");
        var cloth = new HashSet<int>(names.Select((n, i) => (n, i)).Where(x => x.n is "garment" or "lower").Select(x => x.i));
        var v = character.Vertices;
        int n = labels.Length;

        Vector3 Joint(string part)
        {
            var at = CharacterIk.Where(character, null, part, null);
            return new Vector3(Convert.ToSingle(at["x"], CultureInfo.InvariantCulture),
                               Convert.ToSingle(at["y"], CultureInfo.InvariantCulture),
                               Convert.ToSingle(at["z"], CultureInfo.InvariantCulture));
        }
        // The legs' axes from the knees: a rigger may root both thighs at the middle of the pelvis, as UniRig did the
        // warden's, which would put both "legs" on the centre line.
        Vector3 left = Joint("leftThigh"), right = Joint("rightThigh"), leftKnee = Joint("leftShin"), rightKnee = Joint("rightShin");
        var hips = (left + right) / 2f;

        float y0 = float.MaxValue, y1 = float.MinValue;
        for (var i = 0; i < n; i++) { y0 = MathF.Min(y0, v[i].Y); y1 = MathF.Max(y1, v[i].Y); }
        var height = y1 - y0;

        // The crotch: straight down from between the hip joints to the first surface, the legs modelled inside the
        // garment. A gown with no legs inside gives nothing near, so it is kept within 12% of the height of the hips.
        var positions = v.Select(p => new Vector3(p.X, p.Y, p.Z)).ToArray();
        var idx = character.Indices;
        var tris = Enumerable.Range(0, idx.Length / 3).Select(t => ((int)idx[3 * t], (int)idx[(3 * t) + 1], (int)idx[(3 * t) + 2])).ToArray();
        var hipY = MathF.Min(left.Y, right.Y);
        var crotch = SkinWeights.CastDown(hips, positions, tris) ?? hipY - (0.05f * height);
        var top = Math.Clamp(crotch, hipY - (0.12f * height), hipY);

        // Welded, since a reconstructed mesh splits vertices along its UV seams.
        var rep = new int[n];
        var at = new Dictionary<(long, long, long), int>();
        var q = 1e5 / height;
        for (var i = 0; i < n; i++)
            rep[i] = at.TryGetValue(((long)Math.Round(v[i].X * q), (long)Math.Round(v[i].Y * q), (long)Math.Round(v[i].Z * q)), out var r)
                ? r : at[((long)Math.Round(v[i].X * q), (long)Math.Round(v[i].Y * q), (long)Math.Round(v[i].Z * q))] = i;

        bool Candidate(int i) => i < n && cloth.Contains(labels[i]) && v[i].Y < top;
        var parent = Enumerable.Range(0, n).ToArray();
        int Find(int i) { while (parent[i] != i) i = parent[i] = parent[parent[i]]; return i; }
        foreach (var (a, b, c) in tris)
            foreach (var (u, w) in new[] { (a, b), (b, c), (c, a) })
                if (Candidate(u) && Candidate(w)) parent[Find(rep[u])] = Find(rep[w]);

        // What is outermost below the crotch: rays front to back and back to front, from just below it down to the
        // floor. Through the middle, legs standing apart let a ray through, so the first surface it meets is a skirt or
        // a coat's skirts. Through each leg's axis the garment must be a layer with the leg modelled inside it: at
        // least four surfaces crossed, the same one first and last. That keeps a trouser leg rigged even where the
        // views' masks ran down it, and it finds a coat reconstructed as two tubes, one round each leg, as the
        // warden's was, which the middle ray passes between. The lowest height where a ray still meets a garment is
        // the hem, and nothing below it hangs.
        var crosses = new HashSet<int>();
        var hem = top;
        var far = 10f * height;
        int? Comp(int t) => new[] { tris[t].Item1, tris[t].Item2, tris[t].Item3 }.Where(Candidate).Select(i => (int?)Find(rep[i])).FirstOrDefault();
        for (var y = top - (0.03f * height); y > y0; y -= 0.01f * height)
        {
            var found = false;
            foreach (var dir in new[] { -Vector3.UnitZ, Vector3.UnitZ })
                if (Hits(new Vector3(hips.X, y, hips.Z) - (far * dir), dir, positions, tris) is [var first, ..] && Comp(first) is { } c)
                { crosses.Add(c); found = true; }
            foreach (var x in new[] { leftKnee.X, rightKnee.X })
                if (Hits(new Vector3(x, y, hips.Z) - (far * -Vector3.UnitZ), -Vector3.UnitZ, positions, tris) is { Count: >= 4 } through
                    && Comp(through[0]) is { } outer && Comp(through[^1]) == outer)
                { crosses.Add(outer); found = true; }
            if (found) hem = y;
        }

        var mask = new bool[character.VertexCount];
        var count = 0;
        for (var i = 0; i < n; i++)
            if (Candidate(i) && v[i].Y >= hem - (0.02f * height) && crosses.Contains(Find(rep[i]))) { mask[i] = true; count++; }
        var candidates = Enumerable.Range(0, n).Count(Candidate);
        return new HangPlan(mask, top, count, count == 0
            ? $"{candidates} labelled vertices lie below the crotch, and none of them is the outermost layer between or round the legs."
            : null);
    }

    /// <summary>The triangles a ray crosses, nearest first, as indices into <paramref name="tris"/>.</summary>
    static List<int> Hits(Vector3 from, Vector3 dir, Vector3[] p, (int A, int B, int C)[] tris)
    {
        var hits = new List<(float T, int I)>();
        for (var k = 0; k < tris.Length; k++)
        {
            // Möller-Trumbore.
            var tri = tris[k];
            Vector3 v0 = p[tri.A], e1 = p[tri.B] - v0, e2 = p[tri.C] - v0;
            var h = Vector3.Cross(dir, e2);
            var det = Vector3.Dot(e1, h);
            if (MathF.Abs(det) < 1e-12f) continue;
            var f = 1f / det;
            var s = from - v0;
            var u = f * Vector3.Dot(s, h);
            if (u is < 0f or > 1f) continue;
            var q = Vector3.Cross(s, e1);
            var w = f * Vector3.Dot(dir, q);
            if (w < 0f || u + w > 1f) continue;
            var t = f * Vector3.Dot(e2, q);
            if (t > 1e-6f) hits.Add((t, k));
        }
        return [.. hits.OrderBy(h => h.T).Select(h => h.I)];
    }

    /// <summary>A pose with every angle and the hip move scaled by <paramref name="s"/>.</summary>
    internal static Dictionary<string, object?> Scaled(object? pose, double s)
    {
        var outp = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (JsInterop.AsDict(pose) is not { } d) return outp;
        foreach (DictionaryEntry e in d)
            outp[Convert.ToString(e.Key, CultureInfo.InvariantCulture)!] = e.Value switch
            {
                double x => x * s,
                float x => (float)(x * s),
                int x => x * s,
                long x => x * s,
                _ when JsInterop.AsDict(e.Value) is not null => Scaled(e.Value, s),
                _ => e.Value
            };
        return outp;
    }
    #endregion

    #region Private
    static string Request(FaceMesh character, object? pose, HangPlan plan, string settingsJson)
    {
        var keys = new List<SKPoint3[]> { character.Pose(null).Vertices };
        foreach (var s in new[] { 0.2, 0.4, 0.6, 0.8 }) keys.Add(character.Pose(Scaled(pose, s)).Vertices);
        keys.Add(character.Pose(pose).Vertices);

        using var buffer = new MemoryStream();
        using (var w = new Utf8JsonWriter(buffer))
        {
            w.WriteStartObject();
            w.WriteStartArray("triangles");
            var idx = character.Indices;
            for (var t = 0; t < idx.Length; t += 3)
            {
                w.WriteStartArray();
                w.WriteNumberValue(idx[t]); w.WriteNumberValue(idx[t + 1]); w.WriteNumberValue(idx[t + 2]);
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WriteStartArray("mask");
            foreach (var m in plan.Mask) w.WriteBooleanValue(m);
            w.WriteEndArray();
            w.WriteNumber("top", plan.Top);
            w.WriteStartArray("keys");
            foreach (var k in keys)
            {
                w.WriteStartArray();
                foreach (var p in k)
                {
                    w.WriteStartArray();
                    w.WriteNumberValue(Math.Round(p.X, 5)); w.WriteNumberValue(Math.Round(p.Y, 5)); w.WriteNumberValue(Math.Round(p.Z, 5));
                    w.WriteEndArray();
                }
                w.WriteEndArray();
            }
            w.WriteEndArray();
            w.WritePropertyName("settings");
            w.WriteRawValue(settingsJson);
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>The settings as a JSON object; only numbers and booleans, and the script refuses an unknown name.</summary>
    static string Settings(IDictionary? settings)
    {
        var o = new SortedDictionary<string, object>(StringComparer.Ordinal);
        if (settings is not null)
            foreach (DictionaryEntry e in settings)
            {
                var k = Convert.ToString(e.Key, CultureInfo.InvariantCulture)!;
                o[k] = e.Value switch
                {
                    bool b => b,
                    double or float or int or long => Convert.ToDouble(e.Value, CultureInfo.InvariantCulture),
                    _ => throw new ArgumentException($"Character.drape's setting '{k}' must be a number or a boolean.")
                };
            }
        return JsonSerializer.Serialize(o);
    }

    /// <summary>The cache key: the pose and settings, canonically written, which vertices hang, and the cloth script's own hash.</summary>
    static string Key(object? pose, string settingsJson, bool[] mask)
    {
        var bits = new byte[(mask.Length + 7) / 8];
        for (var i = 0; i < mask.Length; i++) if (mask[i]) bits[i / 8] |= (byte)(1 << (i % 8));
        var text = Canonical(pose) + "|" + settingsJson + "|" + ScriptHash() + "|" + Convert.ToHexStringLower(SHA256.HashData(bits));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..20];
    }

    static string Canonical(object? value) => JsInterop.AsDict(value) is { } d
        ? "{" + string.Join(",", d.Keys.Cast<object>().Select(k => Convert.ToString(k, CultureInfo.InvariantCulture)!)
            .Order(StringComparer.Ordinal).Select(k => k + ":" + Canonical(d[k]))) + "}"
        : value switch
        {
            null => "null",
            bool b => b ? "true" : "false",
            double or float or int or long => Math.Round(Convert.ToDouble(value, CultureInfo.InvariantCulture), 4).ToString("R", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""
        };

    static string ScriptHash() => BlenderDriver.ClothScript is { } path
        ? Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)))[..12]
        : "none";
    #endregion
}

/// <summary>Which vertices hang, where the garment's top is, and why none do when none do.</summary>
internal sealed record HangPlan(bool[] Mask, float Top, int Count, string? Why);
