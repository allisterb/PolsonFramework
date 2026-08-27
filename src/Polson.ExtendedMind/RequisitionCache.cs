namespace Polson.ExtendedMind;

using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

/// <summary>
/// File-backed store of 1024 masters, keyed by content address.
/// </summary>
/// <remarks>
/// <para>
/// The highest-leverage piece of the feature. Generation is metered and non-deterministic, and the
/// loop this exists to shorten is retry-heavy: without a cache every re-run of a script re-bills for
/// pixels already bought, and no script is reproducible across runs.
/// </para>
/// <para>
/// The master is stored at native size rather than at the size delivered. A requisition always buys
/// 1024 regardless of what was asked for, so holding the master makes every other size free — an
/// agent asking for 256 and later 512 pays once.
/// </para>
/// </remarks>
public class RequisitionCache : Runtime, IRequisitionCache
{
    #region Constructors
    public RequisitionCache(string? directory = null)
    {
        this.directory = directory ?? Path.Combine(CamelDir, "assets");
        Directory.CreateDirectory(this.directory);
    }
    #endregion

    #region Methods
    public async Task<ImageGenerationResult?> Get(string hash)
    {
        var (image, meta) = PathsFor(hash);
        if (!File.Exists(image) || !File.Exists(meta))
        {
            return null;
        }

        try
        {
            var record = ReadRecord(await File.ReadAllTextAsync(meta));
            if (record is null)
            {
                return null;
            }

            var bytes = await File.ReadAllBytesAsync(image);

            // A truncated or corrupted cache entry must miss rather than deliver garbage.
            if (!ImageGenerator.TryReadPngSize(bytes, out var w, out var h))
            {
                Warn("Discarding undecodable cache entry {Hash}", hash);
                return null;
            }

            return new ImageGenerationResult
            {
                Success = true,
                ImageBytes = bytes,
                Width = w,
                Height = h,
                MimeType = "image/png",
                Model = record.Model,
                Prompt = record.Prompt,
                Hash = hash,
                GeneratedUtc = record.GeneratedUtc,
                FromCache = true,
                Charged = false,
            };
        }
        catch (Exception ex)
        {
            Warn("Cache read failed for {Hash}: {Message}", hash, ex.Message);
            return null;
        }
    }

    public async Task Put(ImageGenerationResult image)
    {
        ArgumentNullException.ThrowIfNull(image);

        if (!image.Success || image.ImageBytes is not { Length: > 0 })
        {
            return;
        }

        var (imagePath, metaPath) = PathsFor(image.Hash);

        try
        {
            await File.WriteAllBytesAsync(imagePath, image.ImageBytes);
            await File.WriteAllTextAsync(metaPath, WriteRecord(new CacheRecord
            {
                Model = image.Model,
                Prompt = image.Prompt,
                GeneratedUtc = image.GeneratedUtc,
                Width = image.Width,
                Height = image.Height,
            }));
        }
        catch (Exception ex)
        {
            // A cache write failure must not fail the requisition; the asset is already in hand.
            Warn("Cache write failed for {Hash}: {Message}", image.Hash, ex.Message);
        }
    }

    /// <summary>
    /// Near-duplicate lookup by token overlap over stored prompts.
    /// </summary>
    /// <remarks>
    /// A deliberately weak stand-in. Token overlap catches rewordings that share vocabulary
    /// ("weathered oak planks" against "weathered oak planking") and misses synonymy entirely
    /// ("old wooden boards"). Closing that gap needs an embedding or a judgement call, which is the
    /// concrete job an Asset Manager role does that no type constraint can.
    /// </remarks>
    public async Task<ImageGenerationResult?> FindSimilar(string descriptor, double threshold = 0.9)
    {
        var wanted = Tokenise(descriptor);
        if (wanted.Count == 0)
        {
            return null;
        }

        foreach (var meta in Directory.EnumerateFiles(this.directory, "*.json"))
        {
            try
            {
                var record = ReadRecord(await File.ReadAllTextAsync(meta));
                if (record is null)
                {
                    continue;
                }

                var have = Tokenise(record.Prompt);
                if (have.Count == 0)
                {
                    continue;
                }

                var overlap = (double)wanted.Intersect(have).Count() / Math.Max(wanted.Count, have.Count);
                if (overlap >= threshold)
                {
                    return await Get(Path.GetFileNameWithoutExtension(meta));
                }
            }
            catch (Exception ex)
            {
                Warn("Skipping unreadable cache record {File}: {Message}", meta, ex.Message);
            }
        }

        return null;
    }

    /// <summary>
    /// Writes the sidecar by hand rather than by reflection.
    /// </summary>
    /// <remarks>
    /// <c>JsonSerializer</c>'s reflection path is disabled outright in trimmed and AOT-published
    /// hosts, where it throws rather than degrading — and a cache that silently stores no metadata
    /// misses every subsequent lookup and quietly re-bills for every asset. Five fields do not
    /// justify that failure mode.
    /// </remarks>
    static string WriteRecord(CacheRecord record)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("model", record.Model);
            writer.WriteString("prompt", record.Prompt);
            writer.WriteString("generatedUtc", record.GeneratedUtc.ToString("O"));
            writer.WriteNumber("width", record.Width);
            writer.WriteNumber("height", record.Height);
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    static CacheRecord? ReadRecord(string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return new CacheRecord
        {
            Model = root.TryGetProperty("model", out var m) ? m.GetString() ?? string.Empty : string.Empty,
            Prompt = root.TryGetProperty("prompt", out var p) ? p.GetString() ?? string.Empty : string.Empty,
            GeneratedUtc = root.TryGetProperty("generatedUtc", out var g) && g.TryGetDateTime(out var when)
                ? when
                : DateTime.MinValue,
            Width = root.TryGetProperty("width", out var w) ? w.GetInt32() : 0,
            Height = root.TryGetProperty("height", out var h) ? h.GetInt32() : 0,
        };
    }

    (string Image, string Meta) PathsFor(string hash) =>
        (Path.Combine(this.directory, hash + ".png"), Path.Combine(this.directory, hash + ".json"));

    static HashSet<string> Tokenise(string text) =>
        [.. text.ToLowerInvariant()
                .Split([' ', ',', '.', ';', ':', '\n', '\r', '\t', '-'], StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 3)];
    #endregion

    #region Fields
    readonly string directory;
    #endregion

    #region Types
    /// <summary>Internal, not private: System.Text.Json cannot bind a private nested type.</summary>
    internal sealed class CacheRecord
    {
        public string Model { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public DateTime GeneratedUtc { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
    #endregion
}
