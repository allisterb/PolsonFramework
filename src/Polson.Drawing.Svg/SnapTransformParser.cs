namespace Polson.Drawing.Svg;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

public static partial class SnapTransformParser
{
    #region Methods
    public static SnapMatrix ParseToMatrix(string? transformStr, SnapBBox? bbox = null)
    {
        var matrix = new SnapMatrix();
        if (string.IsNullOrWhiteSpace(transformStr))
            return matrix;

        var str = transformStr.Trim();
        if (IsShorthand(str))
        {
            ApplyShorthandToMatrix(str, matrix, bbox);
        }
        else
        {
            ApplySvgTransformToMatrix(str, matrix);
        }

        return matrix;
    }

    public static SvgTransformCollection ParseToSvgTransforms(string? transformStr, SnapBBox? bbox = null)
    {
        var collection = new SvgTransformCollection();
        if (string.IsNullOrWhiteSpace(transformStr))
            return collection;

        var str = transformStr.Trim();
        if (IsShorthand(str))
        {
            var matrix = new SnapMatrix();
            ApplyShorthandToMatrix(str, matrix, bbox);
            collection.Add(matrix.ToSvgMatrix());
        }
        else
        {
            ApplySvgTransformToCollection(str, collection);
        }

        return collection;
    }

    /// <summary>
    /// Whether a transform string uses Snap's shorthand grammar rather than standard SVG syntax.
    /// </summary>
    /// <remarks>
    /// Standard SVG's transform functions all begin with a letter the shorthand also uses as a
    /// command — <c>matrix</c>, <c>translate</c>/<c>scale</c>/<c>skew</c>, <c>rotate</c> — so a bare
    /// first-letter test sends every one of them to the shorthand parser, which finds no command it
    /// recognises and yields identity. That silently discarded the whole SVG transform syntax, so
    /// check for a function call first: shorthand never has a name followed by <c>(</c>.
    /// </remarks>
    public static bool IsShorthand(string transformStr)
    {
        var str = transformStr.Trim();
        return !SvgFunctionDetectorRegex().IsMatch(str) && ShorthandDetectorRegex().IsMatch(str);
    }

    private static void ApplyShorthandToMatrix(string str, SnapMatrix matrix, SnapBBox? bbox)
    {
        var matches = ShorthandCommandRegex().Matches(str);
        foreach (Match match in matches)
        {
            var command = match.Groups[1].Value.ToLowerInvariant();
            var numbers = ExtractNumbers(match.Groups[2].Value);
            if (numbers.Count == 0) continue;

            switch (command)
            {
                case "t":
                    var tx = numbers[0];
                    var ty = numbers.Count > 1 ? numbers[1] : 0f;
                    matrix.Translate(tx, ty);
                    break;

                case "r":
                    var deg = numbers[0];
                    var rcx = numbers.Count > 2 ? numbers[1] : (bbox?.Cx ?? 0f);
                    var rcy = numbers.Count > 2 ? numbers[2] : (bbox?.Cy ?? 0f);
                    matrix.Rotate(deg, rcx, rcy);
                    break;

                case "s":
                    if (numbers.Count == 1)
                    {
                        var s = numbers[0];
                        var scx = bbox?.Cx ?? 0f;
                        var scy = bbox?.Cy ?? 0f;
                        matrix.Scale(s, s, scx, scy);
                    }
                    else if (numbers.Count == 2)
                    {
                        var sx = numbers[0];
                        var sy = numbers[1];
                        var scx = bbox?.Cx ?? 0f;
                        var scy = bbox?.Cy ?? 0f;
                        matrix.Scale(sx, sy, scx, scy);
                    }
                    else if (numbers.Count == 3)
                    {
                        var s = numbers[0];
                        var scx = numbers[1];
                        var scy = numbers[2];
                        matrix.Scale(s, s, scx, scy);
                    }
                    else if (numbers.Count >= 4)
                    {
                        var sx = numbers[0];
                        var sy = numbers[1];
                        var scx = numbers[2];
                        var scy = numbers[3];
                        matrix.Scale(sx, sy, scx, scy);
                    }
                    break;

                case "m":
                    if (numbers.Count >= 6)
                    {
                        matrix.Add(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5]);
                    }
                    break;
            }
        }
    }

    private static void ApplySvgTransformToMatrix(string str, SnapMatrix matrix)
    {
        var matches = SvgFunctionRegex().Matches(str);
        foreach (Match match in matches)
        {
            var func = match.Groups[1].Value.ToLowerInvariant();
            var numbers = ExtractNumbers(match.Groups[2].Value);
            if (numbers.Count == 0) continue;

            switch (func)
            {
                case "translate":
                    var tx = numbers[0];
                    var ty = numbers.Count > 1 ? numbers[1] : 0f;
                    matrix.Translate(tx, ty);
                    break;

                case "rotate":
                    var deg = numbers[0];
                    var rcx = numbers.Count > 2 ? numbers[1] : 0f;
                    var rcy = numbers.Count > 2 ? numbers[2] : 0f;
                    matrix.Rotate(deg, rcx, rcy);
                    break;

                case "scale":
                    var sx = numbers[0];
                    var sy = numbers.Count > 1 ? numbers[1] : sx;
                    matrix.Scale(sx, sy);
                    break;

                case "matrix":
                    if (numbers.Count >= 6)
                    {
                        matrix.Add(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5]);
                    }
                    break;

                case "skewx":
                    matrix.SkewX(numbers[0]);
                    break;

                case "skewy":
                    matrix.SkewY(numbers[0]);
                    break;
            }
        }
    }

    private static void ApplySvgTransformToCollection(string str, SvgTransformCollection collection)
    {
        var matches = SvgFunctionRegex().Matches(str);
        foreach (Match match in matches)
        {
            var func = match.Groups[1].Value.ToLowerInvariant();
            var numbers = ExtractNumbers(match.Groups[2].Value);
            if (numbers.Count == 0) continue;

            switch (func)
            {
                case "translate":
                    var tx = numbers[0];
                    var ty = numbers.Count > 1 ? numbers[1] : 0f;
                    collection.Add(new SvgTranslate(tx, ty));
                    break;

                case "rotate":
                    var deg = numbers[0];
                    if (numbers.Count >= 3)
                    {
                        collection.Add(new SvgRotate(deg, numbers[1], numbers[2]));
                    }
                    else
                    {
                        collection.Add(new SvgRotate(deg));
                    }
                    break;

                case "scale":
                    var sx = numbers[0];
                    var sy = numbers.Count > 1 ? numbers[1] : sx;
                    collection.Add(new SvgScale(sx, sy));
                    break;

                case "matrix":
                    if (numbers.Count >= 6)
                    {
                        collection.Add(new SvgMatrix(new List<float> { numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5] }));
                    }
                    break;

                case "skewx":
                    collection.Add(new SvgSkew(numbers[0], 0f));
                    break;

                case "skewy":
                    collection.Add(new SvgSkew(0f, numbers[0]));
                    break;
            }
        }
    }

    private static List<float> ExtractNumbers(string val)
    {
        var list = new List<float>();
        var matches = NumberRegex().Matches(val);
        foreach (Match m in matches)
        {
            if (float.TryParse(m.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var f))
            {
                list.Add(f);
            }
        }
        return list;
    }

    [GeneratedRegex(@"^[rstmRSTM]", RegexOptions.Compiled)]
    private static partial Regex ShorthandDetectorRegex();

    [GeneratedRegex(@"^\s*(matrix|translate|scale|rotate|skewX|skewY)\s*\(", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SvgFunctionDetectorRegex();

    [GeneratedRegex(@"([rstmRSTM])\s*([-\d.,eE\s]+)", RegexOptions.Compiled)]
    private static partial Regex ShorthandCommandRegex();

    [GeneratedRegex(@"([a-zA-Z]+)\s*\(([^)]*)\)", RegexOptions.Compiled)]
    private static partial Regex SvgFunctionRegex();

    [GeneratedRegex(@"[-+]?[0-9]*\.?[0-9]+(?:[eE][-+]?[0-9]+)?", RegexOptions.Compiled)]
    private static partial Regex NumberRegex();
    #endregion
}

