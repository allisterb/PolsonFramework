// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using SkiaSharp;

/// <summary>A bitmap as it prints: its pixels, and the box on the page it is drawn into, in points.</summary>
/// <param name="Bounds">The drawn box in PDF page space: points, origin at the bottom left of the media.</param>
/// <param name="StoredBytes">What the image takes in the file, compressed, with its alpha channel.</param>
/// <param name="ObjectNumber">The image's PDF object, so one image placed twice is counted once.</param>
public sealed record PlacedBitmap(int PixelWidth, int PixelHeight, float WidthPt, float HeightPt, SKRect Bounds, long StoredBytes = 0, int ObjectNumber = 0)
{
    /// <summary>Pixels per printed inch along the poorer of the two axes.</summary>
    public float EffectivePpi => MathF.Min(PixelWidth / (WidthPt / 72f), PixelHeight / (HeightPt / 72f));
}

/// <summary>
/// Finds every bitmap a PDF page draws and the size it prints at, by reading the page as a RIP does.
/// </summary>
/// <remarks>
/// <para>
/// <b>Measured on the PDF, not the SVG.</b> The SVG would need every transform on the way down, every
/// <c>&lt;use&gt;</c> clone and every nested viewport composed correctly to say how large an
/// <c>&lt;image&gt;</c> prints — and a mistake there reads as a measurement. The PDF has already composed
/// them: each image is drawn by a <c>Do</c> under the current transformation matrix, which maps the image's
/// unit square onto the page. That is the size it prints, whatever produced it.
/// </para>
/// <para>
/// Interprets only what placement needs — <c>q</c>, <c>Q</c>, <c>cm</c> and <c>Do</c>, recursing into form
/// XObjects with their <c>/Matrix</c>. A bitmap drawn some other way (a pattern fill, an inline image) is
/// not placed, and is counted in <see cref="Unplaced"/> rather than passed over.
/// </para>
/// <para>
/// Reads the structure Skia writes plus the incremental update <see cref="SvgPdfWriter"/> appends: classic
/// xref tables chained by <c>/Prev</c>, Flate-compressed content. It is not a general PDF reader.
/// </para>
/// </remarks>
public static partial class PdfRasterAudit
{
    #region Methods
    /// <summary>The bitmaps the file's pages draw, and how many image objects it holds that none of them placed.</summary>
    /// <exception cref="InvalidOperationException">The file's structure is not one this can read.</exception>
    public static (IReadOnlyList<PlacedBitmap> Placed, int Unplaced) Measure(string path)
    {
        var reader = new Reader(File.ReadAllBytes(path));
        var placed = new List<PlacedBitmap>();
        var placedObjects = new HashSet<int>();

        foreach (var page in reader.Pages())
        {
            var resources = reader.Dict(page, "Resources");
            var content = reader.Contents(page);
            Interpret(reader, content, resources, Matrix.Identity, placed, placedObjects, depth: 0);
        }

        var unplaced = reader.ImageObjects().Count(n => !placedObjects.Contains(n) && !reader.IsSoftMask(n));
        return (placed, unplaced);
    }

    private static void Interpret(Reader reader, byte[] content, Dictionary<string, string>? resources, Matrix start,
        List<PlacedBitmap> placed, HashSet<int> placedObjects, int depth)
    {
        if (depth > 12) return;

        var ctm = start;
        var stack = new Stack<Matrix>();
        var operands = new List<string>();

        foreach (var (token, isOperator) in Tokens(Encoding.Latin1.GetString(content)))
        {
            if (!isOperator)
            {
                operands.Add(token);
                continue;
            }

            switch (token)
            {
                case "q":
                    stack.Push(ctm);
                    break;
                case "Q":
                    if (stack.Count > 0) ctm = stack.Pop();
                    break;
                case "cm" when operands.Count >= 6 && operands.TakeLast(6).All(IsNumber):
                    var m = operands.TakeLast(6).Select(Number).ToArray();
                    ctm = new Matrix(m[0], m[1], m[2], m[3], m[4], m[5]).Then(ctm);
                    break;
                case "Do" when operands.Count >= 1 && operands[^1].StartsWith('/'):
                    var xobjects = reader.Dict(resources, "XObject");
                    if (xobjects is null || !xobjects.TryGetValue(operands[^1][1..], out var raw) || Reader.RefNumber(raw) is not { } number)
                        break;

                    var dict = reader.ObjectDict(number);
                    var subtype = reader.Name(dict, "Subtype");
                    if (subtype == "Image")
                    {
                        placedObjects.Add(number);
                        placed.Add(Place(reader, dict, ctm) with { StoredBytes = reader.StoredBytes(dict), ObjectNumber = number });
                    }
                    else if (subtype == "Form")
                    {
                        var formMatrix = reader.Numbers(dict, "Matrix") is { Length: 6 } f
                            ? new Matrix(f[0], f[1], f[2], f[3], f[4], f[5])
                            : Matrix.Identity;
                        var formResources = reader.Dict(dict, "Resources") ?? resources;
                        Interpret(reader, reader.Stream(number), formResources, formMatrix.Then(ctm), placed, placedObjects, depth + 1);
                    }
                    break;
            }

            operands.Clear();
        }
    }

    private static PlacedBitmap Place(Reader reader, Dictionary<string, string>? image, Matrix ctm)
    {
        var corners = new[] { ctm.Apply(0, 0), ctm.Apply(1, 0), ctm.Apply(0, 1), ctm.Apply(1, 1) };
        var bounds = new SKRect(corners.Min(p => p.X), corners.Min(p => p.Y), corners.Max(p => p.X), corners.Max(p => p.Y));
        return new PlacedBitmap(
            (int)(reader.Numbers(image, "Width")?.FirstOrDefault() ?? 0),
            (int)(reader.Numbers(image, "Height")?.FirstOrDefault() ?? 0),
            MathF.Sqrt(ctm.A * ctm.A + ctm.B * ctm.B),
            MathF.Sqrt(ctm.C * ctm.C + ctm.D * ctm.D),
            bounds);
    }

    /// <summary>A content stream's tokens: operands, and the operators that consume them.</summary>
    /// <remarks>
    /// Strings and dictionaries are read whole and skipped as single operands, so text drawn on the page cannot
    /// be mistaken for operators; array brackets are dropped, which leaves their elements as stray operands
    /// that the next operator discards.
    /// </remarks>
    private static IEnumerable<(string Token, bool IsOperator)> Tokens(string s)
    {
        var i = 0;
        while (i < s.Length)
        {
            var c = s[i];
            if (IsWhite(c) || c is '[' or ']' or '{' or '}') { i++; continue; }

            if (c == '%')
            {
                while (i < s.Length && s[i] is not '\n' and not '\r') i++;
                continue;
            }

            var start = i;
            if (c == '(')
            {
                SkipString(s, ref i);
                yield return (s[start..i], false);
            }
            else if (c == '<')
            {
                if (i + 1 < s.Length && s[i + 1] == '<') SkipDict(s, ref i);
                else i = s.IndexOf('>', i) is var close and >= 0 ? close + 1 : s.Length;
                yield return (s[start..i], false);
            }
            else if (c == '/')
            {
                i++;
                while (i < s.Length && IsRegular(s[i])) i++;
                yield return (s[start..i], false);
            }
            else
            {
                while (i < s.Length && IsRegular(s[i])) i++;
                if (i == start) { i++; continue; }

                var word = s[start..i];
                if (IsNumber(word)) { yield return (word, false); continue; }

                // An inline image's data is binary and ends at EI; skipped whole, and never placed.
                if (word == "ID")
                {
                    var end = InlineImageEnd().Match(s, i);
                    i = end.Success ? end.Index + end.Length : s.Length;
                    continue;
                }

                yield return (word, true);
            }
        }
    }

    private static bool IsWhite(char c) => c is ' ' or '\t' or '\n' or '\r' or '\f' or '\0';

    private static bool IsRegular(char c) => !IsWhite(c) && c is not ('(' or ')' or '<' or '>' or '[' or ']' or '{' or '}' or '/' or '%');

    private static bool IsNumber(string s) => s.Length > 0 && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static float Number(string s) => float.Parse(s, NumberStyles.Float, CultureInfo.InvariantCulture);

    private static void SkipString(string s, ref int i)
    {
        var depth = 0;
        for (; i < s.Length; i++)
        {
            if (s[i] == '\\') { i++; continue; }
            if (s[i] == '(') depth++;
            else if (s[i] == ')' && --depth == 0) { i++; return; }
        }
    }

    private static void SkipDict(string s, ref int i)
    {
        var depth = 0;
        while (i < s.Length)
        {
            if (s[i] == '(') { SkipString(s, ref i); continue; }
            if (s[i] == '<' && i + 1 < s.Length && s[i + 1] == '<') { depth++; i += 2; continue; }
            if (s[i] == '>' && i + 1 < s.Length && s[i + 1] == '>') { i += 2; if (--depth == 0) return; continue; }
            i++;
        }
    }

    [GeneratedRegex(@"\sEI(?=\s|$)")]
    private static partial Regex InlineImageEnd();
    #endregion

    #region Types
    /// <summary>A 2-D affine transform in PDF's row-vector form: x' = a x + c y + e, y' = b x + d y + f.</summary>
    private readonly record struct Matrix(float A, float B, float C, float D, float E, float F)
    {
        public static readonly Matrix Identity = new(1, 0, 0, 1, 0, 0);

        /// <summary>This transform, followed by <paramref name="next"/> — what <c>cm</c> does to the CTM.</summary>
        public Matrix Then(Matrix next) => new(
            A * next.A + B * next.C, A * next.B + B * next.D,
            C * next.A + D * next.C, C * next.B + D * next.D,
            E * next.A + F * next.C + next.E, E * next.B + F * next.D + next.F);

        public SKPoint Apply(float x, float y) => new(A * x + C * y + E, B * x + D * y + F);
    }

    /// <summary>Objects, dictionaries and streams from a classic-xref PDF, newest revision first.</summary>
    private sealed partial class Reader
    {
        private readonly byte[] bytes;
        private readonly string text;
        private readonly Dictionary<int, int> offsets = [];
        private readonly Dictionary<int, Dictionary<string, string>?> dicts = [];

        public Reader(byte[] bytes)
        {
            this.bytes = bytes;
            text = Encoding.Latin1.GetString(bytes);

            var startXref = StartXref().Matches(text).LastOrDefault()
                ?? throw new InvalidOperationException("The PDF has no startxref, so its bitmaps could not be measured.");
            var at = int.Parse(startXref.Groups[1].Value, CultureInfo.InvariantCulture);
            var seen = new HashSet<int>();

            // Newest section first; an object an earlier section also lists keeps its newer offset.
            while (at > 0 && seen.Add(at))
            {
                if (!text.AsSpan(at).StartsWith("xref"))
                    throw new InvalidOperationException("The PDF uses an xref stream, so its bitmaps could not be measured.");

                var i = at + 4;
                foreach (Match sub in Subsection().Matches(text[i..TrailerAt(i)]))
                {
                    var first = int.Parse(sub.Groups["first"].Value, CultureInfo.InvariantCulture);
                    var rows = Entry().Matches(sub.Groups["rows"].Value);
                    for (var k = 0; k < rows.Count; k++)
                    {
                        if (rows[k].Groups["kind"].Value == "n")
                            offsets.TryAdd(first + k, int.Parse(rows[k].Groups["offset"].Value, CultureInfo.InvariantCulture));
                    }
                }

                var trailer = Parse(text, TrailerAt(i) + "trailer".Length);
                at = trailer.TryGetValue("Prev", out var prev) && int.TryParse(prev, out var p) ? p : 0;
            }
        }

        public IEnumerable<Dictionary<string, string>?> Pages() =>
            offsets.Keys.Order().Select(ObjectDict).Where(d => Name(d, "Type") == "Page");

        public IEnumerable<int> ImageObjects() =>
            offsets.Keys.Where(n => Name(ObjectDict(n), "Subtype") == "Image");

        /// <summary>Whether this image is another image's alpha channel, which prints with it rather than on its own.</summary>
        public bool IsSoftMask(int number) =>
            offsets.Keys.Any(n => ObjectDict(n) is { } d && d.TryGetValue("SMask", out var s) && RefNumber(s) == number);

        public byte[] Contents(Dictionary<string, string>? page)
        {
            if (page is null || !page.TryGetValue("Contents", out var raw)) return [];
            var refs = RefList().Matches(raw).Select(m => int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture));
            return [.. refs.SelectMany(n => Stream(n).Append((byte)'\n'))];
        }

        public Dictionary<string, string>? ObjectDict(int number)
        {
            if (dicts.TryGetValue(number, out var cached)) return cached;
            var dict = offsets.TryGetValue(number, out var at) && ObjHeader().Match(text, at) is { Success: true } h
                    && text.AsSpan(h.Index + h.Length).TrimStart().StartsWith("<<")
                ? Parse(text, h.Index + h.Length)
                : null;
            return dicts[number] = dict;
        }

        public byte[] Stream(int number)
        {
            if (!offsets.TryGetValue(number, out var at) || ObjHeader().Match(text, at) is not { Success: true } h) return [];

            var i = h.Index + h.Length;
            var dict = ParseAt(text, ref i);
            var keyword = StreamKeyword().Match(text, i);
            if (!keyword.Success || keyword.Index != i) return [];

            var start = keyword.Index + keyword.Length;
            var length = dict.TryGetValue("Length", out var raw) ? (int)(Resolve(raw) is var v && float.TryParse(v, CultureInfo.InvariantCulture, out var l) ? l : 0) : 0;
            var data = bytes.AsSpan(start, Math.Min(length, bytes.Length - start)).ToArray();

            if (!dict.TryGetValue("Filter", out var filter) || !filter.Contains("FlateDecode")) return data;
            using var input = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress);
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }

        /// <summary>A dictionary-valued entry, followed through an indirect reference when it is one.</summary>
        public Dictionary<string, string>? Dict(Dictionary<string, string>? dict, string key)
        {
            if (dict is null || !dict.TryGetValue(key, out var raw)) return null;
            if (RefNumber(raw) is { } n) return ObjectDict(n);
            return raw.StartsWith("<<") ? Parse(raw, 0) : null;
        }

        public string? Name(Dictionary<string, string>? dict, string key) =>
            dict is not null && dict.TryGetValue(key, out var raw) && raw.StartsWith('/') ? raw[1..] : null;

        public float[]? Numbers(Dictionary<string, string>? dict, string key)
        {
            if (dict is null || !dict.TryGetValue(key, out var raw)) return null;
            var values = Resolve(raw).Trim('[', ']', ' ').Split((char[])[' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries);
            return values.All(IsNumber) ? [.. values.Select(Number)] : null;
        }

        /// <summary>An image's stream length in the file, plus its soft mask's: what it costs, compressed.</summary>
        public long StoredBytes(Dictionary<string, string>? image)
        {
            var own = (long)(Numbers(image, "Length")?.FirstOrDefault() ?? 0);
            var mask = image is not null && image.TryGetValue("SMask", out var s) && RefNumber(s) is { } m
                ? (long)(Numbers(ObjectDict(m), "Length")?.FirstOrDefault() ?? 0)
                : 0;
            return own + mask;
        }

        public static int? RefNumber(string raw) =>
            RefOnly().Match(raw) is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;

        /// <summary>A plain value, or the value of the object it refers to.</summary>
        private string Resolve(string raw)
        {
            if (RefNumber(raw) is not { } n || !offsets.TryGetValue(n, out var at) || ObjHeader().Match(text, at) is not { Success: true } h)
                return raw;
            var i = h.Index + h.Length;
            return ReadValue(text, ref i);
        }

        private int TrailerAt(int from) =>
            text.IndexOf("trailer", from, StringComparison.Ordinal) is var t and >= 0
                ? t
                : throw new InvalidOperationException("The PDF's xref has no trailer, so its bitmaps could not be measured.");

        private static Dictionary<string, string> Parse(string s, int at)
        {
            var i = at;
            return ParseAt(s, ref i);
        }

        private static Dictionary<string, string> ParseAt(string s, ref int i)
        {
            var dict = new Dictionary<string, string>(StringComparer.Ordinal);
            SkipWhite(s, ref i);
            if (!s.AsSpan(i).StartsWith("<<")) return dict;
            i += 2;

            while (i < s.Length)
            {
                SkipWhite(s, ref i);
                if (s.AsSpan(i).StartsWith(">>")) { i += 2; break; }
                if (s[i] != '/') { i++; continue; }

                var keyStart = ++i;
                while (i < s.Length && IsRegular(s[i])) i++;
                var key = s[keyStart..i];
                dict[key] = ReadValue(s, ref i);
            }
            SkipWhite(s, ref i);
            return dict;
        }

        /// <summary>One value's source text: a dictionary or array whole, an indirect reference as <c>n g R</c>.</summary>
        private static string ReadValue(string s, ref int i)
        {
            SkipWhite(s, ref i);
            var start = i;
            if (i >= s.Length) return "";

            switch (s[i])
            {
                case '<' when i + 1 < s.Length && s[i + 1] == '<':
                    SkipDict(s, ref i);
                    break;
                case '<':
                    i = s.IndexOf('>', i) + 1;
                    break;
                case '(':
                    SkipString(s, ref i);
                    break;
                case '[':
                    var depth = 0;
                    for (; i < s.Length; i++)
                    {
                        if (s[i] == '(') { SkipString(s, ref i); i--; continue; }
                        if (s[i] == '[') depth++;
                        else if (s[i] == ']' && --depth == 0) { i++; break; }
                    }
                    break;
                case '/':
                    i++;
                    while (i < s.Length && IsRegular(s[i])) i++;
                    break;
                default:
                    while (i < s.Length && IsRegular(s[i])) i++;
                    if (RefTail().Match(s, i) is { Success: true } r && r.Index == i) i += r.Length;
                    break;
            }
            return s[start..i];
        }

        private static void SkipWhite(string s, ref int i)
        {
            while (i < s.Length && IsWhite(s[i])) i++;
        }

        [GeneratedRegex(@"startxref\s+(\d+)")]
        private static partial Regex StartXref();

        [GeneratedRegex(@"(?<first>\d+)\s+(?<count>\d+)\s*\r?\n(?<rows>(?:\d{10}\s\d{5}\s[nf]\s*)*)")]
        private static partial Regex Subsection();

        [GeneratedRegex(@"(?<offset>\d{10})\s(?<gen>\d{5})\s(?<kind>[nf])")]
        private static partial Regex Entry();

        [GeneratedRegex(@"\G\d+\s+\d+\s+obj")]
        private static partial Regex ObjHeader();

        [GeneratedRegex(@"\Gstream\r?\n")]
        private static partial Regex StreamKeyword();

        [GeneratedRegex(@"\G\s+\d+\s+R(?![A-Za-z])")]
        private static partial Regex RefTail();

        [GeneratedRegex(@"^\s*(\d+)\s+\d+\s+R\s*$")]
        private static partial Regex RefOnly();

        [GeneratedRegex(@"(\d+)\s+\d+\s+R")]
        private static partial Regex RefList();
    }
    #endregion
}
