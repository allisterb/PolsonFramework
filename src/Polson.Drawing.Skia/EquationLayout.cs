// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;

using CSharpMath.Atom;
using CSharpMath.Atom.Atoms;
using CSharpMath.Display;
using CSharpMath.Rendering.BackEnd;
using CSharpMath.Rendering.FrontEnd;
using SkiaSharp;

using Path = CSharpMath.Rendering.FrontEnd.Path;

#pragma warning disable CS0618   // SKPath's mutators, as in CanvasPath: glyph outlines arrive one segment at a time

/// <summary>
/// One equation, parsed and typeset: its LaTeX, its linear Unicode form, its size, and how to draw it.
/// </summary>
/// <remarks>
/// <para>
/// <b>LaTeX without TeX.</b> CSharpMath parses a LaTeX math subset in managed code and typesets it with the embedded Latin
/// Modern Math, so an agent's equation reaches no TeX engine, no file and no shell. Its own SkiaSharp adapter targets
/// SkiaSharp 3; <see cref="MathCanvas"/> is ours, over the SkiaSharp this engine runs.
/// </para>
/// <para>
/// <b>Guarded before parsing.</b> The parser recurses (<c>ReadArgument</c> → <c>BuildInternal</c>) with no depth limit, and a
/// stack overflow ends a .NET process with no catch, so length and nesting are bounded first (<see cref="Guard"/>).
/// </para>
/// <para>
/// <b>Two readings of one equation.</b> The glyphs reach the canvas as outlines, not text, so what the equation says is read
/// from its atoms instead: <see cref="Linear"/>, the line a reader would transcribe, is what the drawn-text record holds and
/// what a read-back is compared with.
/// </para>
/// </remarks>
internal sealed class EquationLayout
{
    #region Constructors
    private EquationLayout(string latex, MathList list, IDisplay<Fonts, Glyph> display, float size)
    {
        Latex = latex;
        List = list;
        Display = display;
        Size = size;
        Linear = MathLinear.Of(list);
    }
    #endregion

    #region Properties
    public string Latex { get; }
    public MathList List { get; }
    public IDisplay<Fonts, Glyph> Display { get; }

    /// <summary>The em, in canvas units.</summary>
    public float Size { get; }

    /// <summary>The equation as a reader would write it in a line: <c>x = (−b ± √(b² − 4ac))/(2a)</c>.</summary>
    public string Linear { get; }

    public float Width => Display.Width;

    /// <summary>Above the baseline, positive.</summary>
    public float Ascent => Display.Ascent;

    /// <summary>Below the baseline, positive.</summary>
    public float Descent => Display.Descent;
    #endregion

    #region Methods
    /// <summary>The equation typeset at an em of <paramref name="size"/> units, or why it could not be.</summary>
    public static (EquationLayout? Layout, string? Error) Parse(string? latex, float size)
    {
        if (Guard(latex) is { } refusal) return (null, refusal);
        if (!(size > 0) || float.IsInfinity(size) || size > MaxSize) return (null, $"size must be a number above 0 and at most {MaxSize}; it was {size}.");

        lock (Typesetting)
        {
            var (list, error) = LaTeXParser.MathListFromLaTeX(latex!);
            if (error is not null) return (null, error.Trim());
            if (list.Atoms.Count == 0) return (null, "the LaTeX parsed to nothing to draw.");
            // Typography converts points to pixels at 96 dpi, so an em of `size` units is 0.75 × size points.
            var fonts = new Fonts(Array.Empty<Typography.OpenFont.Typeface>(), size * 0.75f);
            var display = Typesetter.CreateLine(list, fonts, TypesettingContext.Instance, LineStyle.Display);
            return (new EquationLayout(latex!, list, display, size), null);
        }
    }

    /// <summary>
    /// The linear form of a LaTeX reading, parsed but not typeset, or null when it does not parse: how a read-back compares
    /// a reader's transcription with the equation that was set, through the same parser and the same linearizer.
    /// </summary>
    public static string? LinearFromLatex(string? latex)
    {
        if (Guard(latex) is not null) return null;
        lock (Typesetting)
        {
            var (list, error) = LaTeXParser.MathListFromLaTeX(latex!);
            return error is null && list.Atoms.Count > 0 ? MathLinear.Of(list) : null;
        }
    }

    /// <summary>
    /// Length and nesting, checked on the text before the parser sees it: braces (escaped ones not counted),
    /// <c>\left…\right</c> and <c>\begin…\end</c>.
    /// </summary>
    internal static string? Guard(string? latex)
    {
        if (string.IsNullOrWhiteSpace(latex)) return "the LaTeX is empty.";
        if (latex.Length > MaxLength) return $"the LaTeX is {latex.Length} characters; at most {MaxLength} are accepted.";
        int depth = 0, deepest = 0;
        for (var i = 0; i < latex.Length; i++)
        {
            var c = latex[i];
            if (c == '\\' && i + 1 < latex.Length)
            {
                var rest = latex.AsSpan(i + 1);
                if (rest[0] is '{' or '}') { i++; continue; }
                if (rest.StartsWith("left") || rest.StartsWith("begin")) depth++;
                else if (rest.StartsWith("right") || rest.StartsWith("end")) depth--;
            }
            else if (c == '{') depth++;
            else if (c == '}') depth--;
            deepest = Math.Max(deepest, depth);
        }
        return deepest > MaxNesting ? $"the LaTeX nests {deepest} levels deep; at most {MaxNesting} are accepted." : null;
    }

    /// <summary>
    /// Draws the equation with its left baseline at (<paramref name="x"/>, <paramref name="y"/>) in the canvas's current
    /// transform, filled with <paramref name="paint"/> where the LaTeX sets no colour of its own.
    /// </summary>
    public void Draw(SKCanvas canvas, float x, float y, SKPaint paint)
    {
        lock (Typesetting)
        {
            var target = new MathCanvas(canvas, paint);
            canvas.Save();
            try
            {
                canvas.Translate(x, y);
                canvas.Scale(1, -1);                       // displays are laid out with y up
                Display.Position = new PointF(0, 0);
                Display.Draw(new GraphicsContext(target, null));
            }
            finally
            {
                canvas.Restore();
            }
        }
    }

    /// <summary>
    /// The outlines the equation draws, its left baseline at (<paramref name="x"/>, <paramref name="y"/>), with their
    /// colours: in y-down units as <see cref="Draw"/> uses, or with <paramref name="yDown"/> false in y-up units, as a
    /// Synfig-style view is.
    /// </summary>
    public List<(SKPath Path, SKColor Color)> Outlines(float x, float y, SKColor color, bool yDown = true)
    {
        using var bitmap = new SKBitmap(1, 1);
        using var canvas = new SKCanvas(bitmap);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        var collected = new List<(SKPath, SKColor)>();
        lock (Typesetting)
        {
            var target = new MathCanvas(canvas, paint, collected);
            canvas.Save();
            canvas.Translate(x, y);
            if (yDown) canvas.Scale(1, -1);
            Display.Position = new PointF(0, 0);
            Display.Draw(new GraphicsContext(target, null));
            canvas.Restore();
        }
        return collected;
    }
    #endregion

    #region Fields
    public const int MaxLength = 2000;
    public const int MaxNesting = 32;
    public const float MaxSize = 4000;

    /// <summary>CSharpMath's typesetting context and Typography's glyph caches are shared statics, not made for concurrent scripts.</summary>
    /// <summary>CSharpMath's parser and typesetter are not safe to run concurrently: everything that parses holds this.</summary>
    internal static readonly object Typesetting = new();
    #endregion

    #region Types
    /// <summary>
    /// CSharpMath's canvas over SkiaSharp: paths, lines and rectangles, with the transform kept by the <see cref="SKCanvas"/>.
    /// With <c>collected</c>, outlines are gathered in canvas coordinates instead of drawn.
    /// </summary>
    private sealed class MathCanvas(SKCanvas canvas, SKPaint fill, List<(SKPath, SKColor)>? collected = null) : ICanvas
    {
        public SKCanvas Canvas { get; } = canvas;
        public float Width => Canvas.LocalClipBounds.Width;
        public float Height => Canvas.LocalClipBounds.Height;
        public System.Drawing.Color DefaultColor { get; set; }
        public System.Drawing.Color? CurrentColor { get; set; }
        public PaintStyle CurrentStyle { get; set; }

        public Path StartNewPath() => new MathPath(this);

        public void DrawLine(float x1, float y1, float x2, float y2, float lineThickness) => this.StrokeLineOutline(x1, y1, x2, y2, lineThickness);

        public void StrokeRect(float left, float top, float width, float height) { }      // glyph boxes: a debugging aid, never drawn

        public void FillRect(float left, float top, float width, float height)
        {
            if (CurrentColor is not { A: > 0 } c) return;                                      // the painter's highlight, transparent by default
            using var path = new SKPath();
            path.AddRect(SKRect.Create(left, top, width, height));
            Emit(path, ToSk(c));
        }

        public void Save() => Canvas.Save();
        public void Translate(float dx, float dy) => Canvas.Translate(dx, dy);
        public void Scale(float sx, float sy) => Canvas.Scale(sx, sy);
        public void Restore() => Canvas.Restore();

        /// <summary>A path in the current transform: drawn with the fill (or its own colour), or collected in canvas coordinates.</summary>
        internal void Emit(SKPath path, SKColor? own)
        {
            if (collected is not null)
            {
                var placed = new SKPath(path);
                placed.Transform(Canvas.TotalMatrix);
                collected.Add((placed, own ?? fill.Color));
                return;
            }
            if (own is { } color)
            {
                using var paint = fill.Clone();
                paint.Shader = null;
                paint.Color = color;
                Canvas.DrawPath(path, paint);
            }
            else Canvas.DrawPath(path, fill);
        }

        /// <summary>A colour the LaTeX set (<c>\color</c>, passed as the current colour), or null for the fill's own.</summary>
        internal SKColor? Own(System.Drawing.Color? foreground) =>
            foreground is { } f ? ToSk(f) : CurrentColor is { } current ? ToSk(current) : null;

        private static SKColor ToSk(System.Drawing.Color c) => new(c.R, c.G, c.B, c.A);
    }

    private sealed class MathPath(MathCanvas owner) : Path
    {
        private readonly SKPath path = new();
        public override System.Drawing.Color? Foreground { get; set; }
        public override void MoveTo(float x0, float y0) { path.Close(); path.MoveTo(x0, y0); }
        public override void LineTo(float x1, float y1) => path.LineTo(x1, y1);
        public override void Curve3(float x1, float y1, float x2, float y2) => path.QuadTo(x1, y1, x2, y2);
        public override void Curve4(float x1, float y1, float x2, float y2, float x3, float y3) => path.CubicTo(x1, y1, x2, y2, x3, y3);
        public override void CloseContour() => path.Close();
        public override void Dispose()
        {
            owner.Emit(path, owner.Own(Foreground));
            path.Dispose();
        }
    }
    #endregion
}

/// <summary>
/// A math list as one line of Unicode, the way a reader would write what they see: <c>x = (−b ± √(b² − 4ac))/(2a)</c>.
/// </summary>
/// <remarks>
/// Fractions become <c>(numerator)/(denominator)</c>, brackets dropped round a single token; radicals <c>√</c>; scripts
/// Unicode superscript and subscript characters where every character has one, else <c>^(…)</c> and <c>_(…)</c>; matrices
/// <c>[a, b; c, d]</c>. Variables keep their plain letters: the math italic the typesetter draws is not what anyone types.
/// </remarks>
internal static class MathLinear
{
    #region Methods
    public static string Of(MathList list) => Collapse(Write(list));

    private static string Write(MathList? list)
    {
        if (list is null) return "";
        var sb = new StringBuilder();
        MathAtom? previous = null;
        foreach (var atom in list.Atoms)
        {
            // A minus at the start, or after an operator, a relation or an opening bracket, is a sign: −b, not − b.
            var unary = atom is BinaryOperator && previous is null or BinaryOperator or Relation or Open or Punctuation or LargeOperator;
            sb.Append(unary ? atom.Nucleus + Scripts(atom) : Write(atom));
            if (atom is not (Space or Style or Comment)) previous = atom;
        }
        return sb.ToString();
    }

    private static string Write(MathAtom atom)
    {
        var body = atom switch
        {
            Fraction f when f.HasRule => Group(Write(f.Numerator)) + "/" + Group(Write(f.Denominator)),
            Fraction f => (f.LeftDelimiter.Nucleus ?? "(") + Write(f.Numerator) + " " + Write(f.Denominator) + (f.RightDelimiter.Nucleus ?? ")"),
            Radical r => Degree(Write(r.Degree)) + "√" + Group(Write(r.Radicand)),
            Inner { InnerList.Atoms: [Table t] } i => (i.LeftBoundary.Nucleus ?? "") + Cells(t) + (i.RightBoundary.Nucleus ?? ""),
            Inner i => (i.LeftBoundary.Nucleus ?? "") + Write(i.InnerList) + (i.RightBoundary.Nucleus ?? ""),
            Accent a => Write(a.InnerList) + a.Nucleus,
            Overline o => Write(o.InnerList),
            Underline u => Write(u.InnerList),
            Colored c => Write(c.InnerList),
            ColorBox c => Write(c.InnerList),
            RaiseBox r => Write(r.InnerList),
            UnderAnnotation u => Write(u.InnerList),
            Table t => t.Environment == "matrix" ? "[" + Cells(t) + "]" : Cells(t),
            LargeOperator op => op.Nucleus,
            BinaryOperator or Relation => " " + atom.Nucleus + " ",
            Punctuation => atom.Nucleus + " ",
            Space s => s.Length > 0 ? " " : "",
            Style or Comment or Placeholder => "",
            _ => atom.Nucleus,
        };
        // A large operator's limits belong to it; what follows is its argument: ∑_(i = 1)ⁿ i, sin x.
        return body + Scripts(atom) + (atom is LargeOperator ? " " : "");
    }

    private static string Scripts(MathAtom atom) =>
        Script(Write(atom.Subscript), Subscripts, "_") + Script(Write(atom.Superscript), Superscripts, "^");

    /// <summary>A table's cells, <c>a, b; c, d</c>. A bracketed matrix is brackets round a plain one, so the brackets come from those.</summary>
    private static string Cells(Table t) =>
        string.Join("; ", t.Cells.Select(row => string.Join(", ", row.Select(cell => Collapse(Write(cell))))));

    /// <summary>Brackets round anything but one token: <c>a/b</c>, <c>(a + b)/2</c>, <c>√2</c>, <c>√(x + 1)</c>.</summary>
    private static string Group(string s)
    {
        var t = Collapse(s);
        return t.Length == 0 ? "()" : IsToken(t) ? t : "(" + t + ")";
    }

    /// <summary>A number, or one letter, either with scripts written as script characters: <c>2.5</c>, <c>x²</c>, <c>aₙ</c>.</summary>
    private static bool IsToken(string s)
    {
        var core = s.TrimEnd([.. Superscripts.Values, .. Subscripts.Values]);
        return core.Length > 0 && (core.All(c => char.IsDigit(c) || c == '.') || (core.Length == 1 && char.IsLetter(core[0])));
    }

    private static string Degree(string degree)
    {
        var d = Collapse(degree);
        if (d.Length == 0) return "";
        return d.All(c => Superscripts.ContainsKey(c)) ? new string([.. d.Select(c => Superscripts[c])]) : "root(" + d + ")";
    }

    private static string Script(string script, Dictionary<char, char> map, string mark)
    {
        var s = Collapse(script);
        if (s.Length == 0) return "";
        return s.All(map.ContainsKey) ? new string([.. s.Select(c => map[c])]) : mark + (s.Length == 1 ? s : "(" + s + ")");
    }

    private static string Collapse(string s) => string.Join(" ", s.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Replace("( ", "(").Replace(" )", ")");
    #endregion

    #region Fields
    private static readonly Dictionary<char, char> Superscripts = new()
    {
        ['0'] = '⁰', ['1'] = '¹', ['2'] = '²', ['3'] = '³', ['4'] = '⁴', ['5'] = '⁵', ['6'] = '⁶', ['7'] = '⁷', ['8'] = '⁸', ['9'] = '⁹',
        ['+'] = '⁺', ['−'] = '⁻', ['-'] = '⁻', ['='] = '⁼', ['('] = '⁽', [')'] = '⁾', ['n'] = 'ⁿ', ['i'] = 'ⁱ',
    };

    private static readonly Dictionary<char, char> Subscripts = new()
    {
        ['0'] = '₀', ['1'] = '₁', ['2'] = '₂', ['3'] = '₃', ['4'] = '₄', ['5'] = '₅', ['6'] = '₆', ['7'] = '₇', ['8'] = '₈', ['9'] = '₉',
        ['+'] = '₊', ['−'] = '₋', ['-'] = '₋', ['='] = '₌', ['('] = '₍', [')'] = '₎',
        ['a'] = 'ₐ', ['e'] = 'ₑ', ['h'] = 'ₕ', ['i'] = 'ᵢ', ['j'] = 'ⱼ', ['k'] = 'ₖ', ['l'] = 'ₗ', ['m'] = 'ₘ', ['n'] = 'ₙ',
        ['o'] = 'ₒ', ['p'] = 'ₚ', ['r'] = 'ᵣ', ['s'] = 'ₛ', ['t'] = 'ₜ', ['u'] = 'ᵤ', ['v'] = 'ᵥ', ['x'] = 'ₓ',
    };
    #endregion
}
