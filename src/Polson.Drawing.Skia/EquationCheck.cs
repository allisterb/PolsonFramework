// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;

using AngouriMath;
using CSharpMath.Atom;

using NComplex = System.Numerics.Complex;

/// <summary>Whether two expressions are the same mathematics: the engine behind <c>Equation.equivalent</c>.</summary>
/// <remarks>
/// <para>
/// <b>Two passes, and the definite one wins.</b> AngouriMath simplifies <c>a − b</c>: 0 proves the identity, and a
/// nonzero constant disproves it. Then both sides are evaluated at random points: one point where they differ is a
/// counterexample, which outranks a symbolic "0" (a simplifier can be wrong; a counterexample can be re-run). Agreement
/// at every point without a proof is evidence, not proof, and is reported as such.
/// </para>
/// <para>
/// <b>Bounded, because the input is an agent's or a watcher's.</b> The parser and AngouriMath both recurse, and a stack
/// overflow ends the process with no catch: length, brace and bracket nesting are checked first, and AngouriMath runs on a
/// thread of its own with a large stack, under a cancellation token and a time budget. Only the LaTeX bridge
/// (<see cref="MathEvaluation"/>) builds expressions: AngouriMath's own string parser is never used.
/// </para>
/// </remarks>
internal static partial class EquationCheck
{
    #region Types
    /// <summary>What a check may be asked: the domain of each variable, which are integers, how many points, how long.</summary>
    internal sealed record Options(IReadOnlyDictionary<string, (double Lo, double Hi)> Domain, IReadOnlySet<string> Integers, int Samples, int BudgetMs);

    /// <summary>
    /// The outcome. <c>Kind</c> is <c>'proved'</c>, <c>'agrees'</c>, <c>'differs'</c>, <c>'unsettled'</c> or
    /// <c>'unread'</c> (the LaTeX was refused or did not convert).
    /// </summary>
    internal sealed record Verdict(string Kind, string? A, string? B, string Detail)
    {
        public string? Provided { get; init; }
        public IReadOnlyDictionary<string, double>? At { get; init; }
        public NComplex? ValueA { get; init; }
        public NComplex? ValueB { get; init; }
        public int Points { get; init; }
        public bool Approximate { get; init; }
        public string? Side { get; init; }
    }
    #endregion

    #region Methods
    /// <summary>Whether <paramref name="a"/> and <paramref name="b"/> are equal; with <paramref name="b"/> null, <paramref name="a"/> is an equation, <c>lhs = rhs</c>.</summary>
    public static Verdict Check(string a, string? b, Options options)
    {
        var (listA, linearA, refusedA) = Read(a);
        if (refusedA is not null) return new("unread", null, null, Explain(refusedA)) { Side = "a" };
        MathList? listB = null;
        string? linearB = null;
        if (b is not null)
        {
            (listB, linearB, var refusedB) = Read(b);
            if (refusedB is not null) return new("unread", linearA, null, Explain(refusedB)) { Side = "b" };
        }

        // The bridge, then each pass, on a thread of their own: a deep tree recurses, and a slow one must be stoppable.
        var converted = OnOwnStack(_ => Convert(listA!, listB), ConvertMs, out var convertTimedOut);
        if (convertTimedOut || converted.Error is not null || converted.Left is null || converted.Right is null)
            return new("unread", linearA, linearB, convertTimedOut ? "converting the LaTeX took too long." : Explain(converted.Error) ?? "the LaTeX is not an expression.")
                { Side = converted.Side };
        var (left, right) = (converted.Left, converted.Right);
        if (converted.Equations)
        {
            // An identity holds whatever came before it: ftcexplainer3 wrote ∫₀² x² dx = [x³/3]₀², then [x³/3]₀² = 2³/3 − 0³/3,
            // then 2³/3 − 0³/3 = 8/3 (2026-10-23), and as steps of a solution each lhs − rhs is 0, so their ratio is 0/0 and
            // every pair was unsettled. The second equation is checked as an identity first; one that is false with nothing
            // free in it is a false statement, and any other is an equation with solutions, compared as a step.
            var own = Compare(right, Entity.Number.Integer.Zero, null, null, options with { BudgetMs = Math.Max(200, options.BudgetMs / 3) }, Seed(a, b));
            if (own.Kind is "proved" or "agrees")
                return own with
                {
                    A = linearA, B = linearB,
                    Detail = $"the second is an identity, so it holds whatever came before it: {own.Detail}",
                };
            if (own.Kind == "differs" && right.Vars.Count == 0)
                return own with { A = linearA, B = linearB, Detail = "the second is false: its two sides are different numbers." };
            return EquationStep(left, right, linearA, linearB, options with { BudgetMs = Math.Max(200, options.BudgetMs * 2 / 3) }, Seed(a, b));
        }
        if (b is null) (linearA, linearB) = Halves(linearA!);
        return Compare(left, right, linearA, linearB, options, Seed(a, b));
    }

    /// <summary>Two expressions compared: simplified, then sampled; a counterexample outranks a symbolic 0.</summary>
    private static Verdict Compare(Entity left, Entity right, string? linearA, string? linearB, Options options, int seed)
    {
        var symbolicMs = Math.Max(100, options.BudgetMs * 2 / 5);
        var symbolic = OnOwnStack(_ => Symbolic(left, right), symbolicMs, out var symbolicTimedOut);
        var numeric = OnOwnStack(token => Numeric(left, right, options, seed, token), options.BudgetMs - symbolicMs, out var numericTimedOut);

        var points = numeric?.Agreed ?? 0;
        var approximate = numeric?.Approximate ?? false;
        if (numeric?.Counterexample is { } c)
            return new("differs", linearA, linearB, symbolic?.Kind == "zero"
                    ? "AngouriMath simplified the difference to 0, but the sides differ at a point: the counterexample stands."
                    : "the sides differ at a point.")
                { At = c.At, ValueA = c.A, ValueB = c.B, Points = points, Approximate = approximate };
        if (symbolic?.Kind == "constant")
            return new("differs", linearA, linearB, "the difference simplifies to a constant that is not 0.")
                { ValueA = symbolic.A, ValueB = symbolic.B, Points = points, Approximate = approximate };
        if (symbolic?.Kind == "zero")
            return new("proved", linearA, linearB, symbolic.Provided is null
                    ? "AngouriMath simplified the difference to 0."
                    : $"AngouriMath simplified the difference to 0, provided {symbolic.Provided}.")
                { Provided = symbolic.Provided, Points = points, Approximate = approximate };
        if (numeric is { Constant: true } && points == 1)
            return new("agrees", linearA, linearB, approximate
                    ? "no proof, but the two numbers agree to the precision of a numerical integral."
                    : "no proof, but the two numbers agree to nine significant figures.")
                { Points = points, Approximate = approximate };
        if (points >= MinPoints)
            return new("agrees", linearA, linearB, $"no proof, but the sides agree at all {points} points tried.")
                { Points = points, Approximate = approximate };

        var why = new List<string>();
        if (symbolicTimedOut) why.Add("simplifying took longer than its budget");
        else why.Add("the difference did not simplify to a number");
        if (numericTimedOut) why.Add("evaluating took longer than its budget");
        else if (numeric?.Note is { } note) why.Add(note);
        else why.Add($"only {points} point{(points == 1 ? "" : "s")} could be evaluated, of the {MinPoints} needed");
        return new("unsettled", linearA, linearB, string.Join("; ", why) + ".") { Points = points, Approximate = approximate };
    }

    /// <summary>Reads checker options: <c>{ domain?, integers?, samples?, budget? }</c>.</summary>
    public static Options ReadOptions(object? options, string method)
    {
        var o = JsInterop.AsDict(options);
        var domain = new Dictionary<string, (double, double)>(StringComparer.Ordinal);
        var integers = new HashSet<string>(StringComparer.Ordinal);
        int samples = DefaultSamples, budget = DefaultBudgetMs;
        if (o is null) return new(domain, integers, samples, budget);

        foreach (var key in o.Keys.Cast<object>().Select(k => k.ToString()!))
            if (key is not ("domain" or "integers" or "samples" or "budget"))
                throw new ArgumentException($"{method}'s options are {{ domain, integers, samples, budget }}; '{key}' is not one.");
        if (JsInterop.AsDict(o["domain"]) is { } d)
            foreach (var name in d.Keys.Cast<object>().Select(k => k.ToString()!))
            {
                var range = Numbers(d[name]);
                if (range is not [var lo, var hi] || !(lo < hi) || !double.IsFinite(lo) || !double.IsFinite(hi))
                    throw new ArgumentException($"{method}'s domain.{name} is [low, high], with low below high, such as {{ x: [0, 5] }}.");
                domain[name] = (lo, hi);
            }
        if (o["integers"] is IEnumerable names and not string)
            foreach (var n in names) integers.Add(n?.ToString() ?? "");
        if (o["samples"] is { } s) samples = (int)Math.Clamp(System.Convert.ToDouble(s, CultureInfo.InvariantCulture), 4, MaxSamples);
        if (o["budget"] is { } bu) budget = (int)Math.Clamp(System.Convert.ToDouble(bu, CultureInfo.InvariantCulture), 200, MaxBudgetMs);
        return new(domain, integers, samples, budget);

        static double[]? Numbers(object? v) => v is IEnumerable e and not string
            ? [.. e.Cast<object?>().Select(x => x is null ? double.NaN : System.Convert.ToDouble(x, CultureInfo.InvariantCulture))]
            : null;
    }

    /// <summary>
    /// LaTeX as its writer meant it: math delimiters taken off (<c>$…$</c>, <c>$$…$$</c>, <c>\(…\)</c>, <c>\[…\]</c>), and
    /// commands a JSON answer turned into control characters put back. A model writes <c>\begin</c> into a JSON string
    /// without doubling the backslash, and the parser reads <c>\b</c> as a backspace: <c>\begin</c> arrived as backspace +
    /// <c>egin</c> (2026-10-23), and <c>\times</c>, <c>\theta</c>, <c>\frac</c>, <c>\neq</c>, <c>\rho</c> go the same way. Shared
    /// by the read-back, for a watcher's reading, and by <see cref="Check"/>, for LaTeX a model wrote.
    /// </summary>
    internal static string Unwrap(string latex)
    {
        var r = latex;
        if (r.IndexOfAny(['\b', '\f', '\t', '\r', '\n']) >= 0)
        {
            var repaired = new System.Text.StringBuilder(r.Length + 8);
            for (var i = 0; i < r.Length; i++)
            {
                var c = r[i];
                var letterNext = i + 1 < r.Length && char.IsAsciiLetter(r[i + 1]);
                var escape = c switch
                {
                    '\b' => "\\b", '\f' => "\\f", '\t' => "\\t", '\r' when letterNext => "\\r",
                    '\n' when letterNext && MathMark().IsMatch(r) => "\\n",
                    _ => null,
                };
                if (escape is null) repaired.Append(c); else repaired.Append(escape);
            }
            r = repaired.ToString();
        }
        var t = r.Trim();
        foreach (var (open, close) in Delimiters)
            if (t.Length > open.Length + close.Length && t.StartsWith(open, StringComparison.Ordinal) && t.EndsWith(close, StringComparison.Ordinal))
                return t[open.Length..^close.Length].Trim();
        return r;
    }

    /// <summary>A command, a superscript, or a braced subscript: <c>\frac</c>, <c>b^2</c>, <c>x_{i}</c>.</summary>
    internal static bool LooksLikeMath(string text) => MathMark().IsMatch(text);

    /// <summary>
    /// The LaTeX as the bridge needs it: calculus written with an italic d, as agents and readers write it
    /// (<c>\frac{d}{dx}</c>, <c>\int … dx</c>), given the upright d the bridge requires.
    /// </summary>
    internal static string Normalise(string latex)
    {
        var s = DerivativeFraction().Replace(latex, m => $@"\frac{{\mathrm{{d}}{m.Groups[1].Value}{m.Groups[2].Value}}}{{\mathrm{{d}}{m.Groups[3].Value}{m.Groups[4].Value}}}");
        s = AbsoluteBars(s.Replace(@"\lvert", @"\left|").Replace(@"\rvert", @"\right|"));
        // Written as code is written: `sin^2 x` without its backslash (s·i·n to the parser, and i the imaginary unit), and
        // `*` for a product, which CSharpMath does not read. Nemotron Ultra wrote both in a check (2026-10-23).
        s = BareFunction().Replace(s, m => @"\" + m.Value);
        s = s.Replace("*", @" \cdot ");
        // A sum over i: the bridge reads i as the imaginary unit, so the index is given a letter the LaTeX does not use.
        // Every lone i is renamed, which misreads only a sum over i beside a complex i, as in no explainer seen yet.
        if (SumOverI().IsMatch(s) && new[] { "k", "j", "m", "r", "q" }.FirstOrDefault(l => !Regex.IsMatch(s, $@"(?<![\\A-Za-z]){l}(?![A-Za-z])")) is { } free)
            s = LoneI().Replace(s, free);
        // An integral's own d is the last one before its end: one per \int, taken from the right.
        var integrals = Regex.Count(s, @"\\int(?![a-zA-Z])");
        if (integrals == 0) return s;
        var ds = IntegralD().Matches(s).Cast<Match>().TakeLast(integrals).Select(m => m.Index).ToHashSet();
        return IntegralD().Replace(s, m => ds.Contains(m.Index) ? $@"\mathrm{{d}}{m.Groups[1].Value}" : m.Value);
    }

    /// <summary>
    /// Bare absolute-value bars as <c>\left| … \right|</c>, which the bridge reads as <c>abs</c>: CSharpMath parses a bare
    /// <c>|</c> as a plain symbol. Bars pair within their brace group, alternately opening and closing, so <c>|a||b|</c> is
    /// |a|·|b|; nested bars, <c>|x - |y||</c>, are ambiguous in LaTeX itself and pair left to right. <c>\|</c> and
    /// <c>\left|</c>, <c>\right|</c> are left alone.
    /// </summary>
    internal static string AbsoluteBars(string latex)
    {
        if (!latex.Contains('|')) return latex;
        var b = new System.Text.StringBuilder(latex.Length + 16);
        var open = new Stack<bool>([false]);
        for (var i = 0; i < latex.Length; i++)
        {
            var c = latex[i];
            if (c == '{') open.Push(false);
            else if (c == '}' && open.Count > 1) open.Pop();
            var written = i > 0 && latex[i - 1] == '\\'
                || b.ToString().EndsWith(@"\left", StringComparison.Ordinal) || b.ToString().EndsWith(@"\right", StringComparison.Ordinal);
            if (c != '|' || written) { b.Append(c); continue; }
            var opening = !open.Pop();
            open.Push(opening);
            b.Append(opening ? @"\left|" : @"\right|");
        }
        return b.ToString();
    }

    /// <summary>
    /// The bridge's refusal, with what to do about it where the bridge's words name only a symbol. ftcexplainer1's plan was
    /// told "Unsupported Relation ≈" and "Unsupported Ordinary ′" (2026-10-23), neither of which says what would be read.
    /// </summary>
    private static string? Explain(string? error) => error switch
    {
        null => null,
        _ when error.StartsWith("Unsupported", StringComparison.Ordinal) && (error.Contains('≈') || error.Contains('∼') || error.Contains('≃')) =>
            $"{error}: an approximation is not an identity, so no step check can prove it. Check what it approximates with Verify.claim, "
            + "or write the exact statement, such as a limit.",
        _ when error.StartsWith("Unsupported", StringComparison.Ordinal) && (error.Contains('′') || error.Contains('″') || error.EndsWith('\''))
            || error.Contains(@"\prime", StringComparison.Ordinal) =>
            $"{error}: f′(x) names the derivative of a function the check does not know. Write the function out, as "
            + @"\frac{d}{dx}(x^3/3), or check the statement in words with Verify.claim.",
        _ => error,
    };

    /// <summary>The parsed MathList and its linear form, or why the LaTeX was refused.</summary>
    private static (MathList? List, string? Linear, string? Refused) Read(string latex)
    {
        latex = Unwrap(latex);      // a model's LaTeX, as from a JSON answer: $ … $, and \frac arriving as a form feed
        if (EquationLayout.Guard(latex) is { } refusal) return (null, null, refusal);
        if (latex.Length > MaxLength) return (null, null, $"the LaTeX is {latex.Length} characters; at most {MaxLength} are checked.");
        int depth = 0, deepest = 0;
        foreach (var ch in latex)
        {
            if (ch is '(' or '[') deepest = Math.Max(deepest, ++depth);
            else if (ch is ')' or ']') depth--;
        }
        if (deepest > EquationLayout.MaxNesting) return (null, null, $"the brackets nest {deepest} deep; at most {EquationLayout.MaxNesting} are checked.");

        var normal = Normalise(latex);
        lock (EquationLayout.Typesetting)
        {
            // Parsed twice: the bridge rewrites the list it reads, and the linear form is of the LaTeX as written.
            var (shown, error) = LaTeXParser.MathListFromLaTeX(latex);
            if (error is not null) return (null, null, error.Trim());
            var (list, normalError) = LaTeXParser.MathListFromLaTeX(normal);
            return normalError is null ? (list, MathLinear.Of(shown), null) : (null, null, normalError.Trim());
        }
    }

    /// <summary>
    /// The two things compared: two expressions, the two sides of one equation, or — <c>Equations</c> — two equations,
    /// each as its <c>lhs − rhs</c>.
    /// </summary>
    private static (Entity? Left, Entity? Right, bool Equations, string? Error, string? Side) Convert(MathList a, MathList? b)
    {
        var (itemA, errorA) = MathEvaluation.Evaluate(a);
        if (errorA is not null) return (null, null, false, errorA, "a");
        if (itemA is not MathEvaluation.MathItem.Entity { Content: var left }) return (null, null, false, "the LaTeX is not a single expression.", "a");
        if (b is null)
            return left is Entity.Equalsf eq ? (eq.Left, eq.Right, false, null, null) : (null, null, false, "with one argument, the LaTeX is an equation: lhs = rhs.", "a");
        var (itemB, errorB) = MathEvaluation.Evaluate(b);
        if (errorB is not null) return (null, null, false, errorB, "b");
        if (itemB is not MathEvaluation.MathItem.Entity { Content: var right }) return (null, null, false, "the LaTeX is not a single expression.", "b");
        return (left, right) switch
        {
            (Entity.Equalsf p, Entity.Equalsf q) => (p.Left - p.Right, q.Left - q.Right, true, null, null),
            (Entity.Equalsf, _) or (_, Entity.Equalsf) => (null, null, false,
                "one is an equation and the other an expression: compare two equations, or two expressions. In a worked chain, "
                + "Verify.derivation checks an equation beside an expression as its sides.", left is Entity.Equalsf ? "b" : "a"),
            _ => (left, right, false, null, null),
        };
    }

    /// <summary>
    /// Two equations as a step of a solution: equivalent when the first's <c>lhs − rhs</c> is a nonzero constant times the
    /// second's — rearranging, adding to both sides, scaling. Dividing by a variable is not, as it can lose a solution.
    /// </summary>
    private static Verdict EquationStep(Entity first, Entity second, string? linearA, string? linearB, Options options, int seed)
    {
        var symbolicMs = Math.Max(100, options.BudgetMs * 2 / 5);
        var ratio = OnOwnStack(_ => Ratio(first, second), symbolicMs, out var symbolicTimedOut);

        // The ratio at the first informative point, held to at every other; a point on either solution set says nothing.
        NComplex? r0 = null;
        bool? Proportional(NComplex x, NComplex y, double tolerance)
        {
            var scale = Math.Max(1, Math.Max(x.Magnitude, y.Magnitude));
            if (x.Magnitude <= tolerance * scale || y.Magnitude <= tolerance * scale) return null;
            var r = x / y;
            r0 ??= r;
            return (r - r0.Value).Magnitude <= tolerance * Math.Max(1, r0.Value.Magnitude) * 10;
        }
        var numeric = OnOwnStack(token => Numeric(first, second, options, seed, token, Proportional), options.BudgetMs - symbolicMs, out var numericTimedOut);
        var points = numeric?.Agreed ?? 0;
        var approximate = numeric?.Approximate ?? false;

        if (numeric?.Counterexample is { } c && r0 is { } k0)
            return new("differs", linearA, linearB,
                $"the equations are not equivalent: lhs − rhs of the first is {Show(k0)} times the second's at one point and {Show(c.A / c.B)} times at this one.")
                { At = c.At, ValueA = c.A, ValueB = c.B, Points = points, Approximate = approximate };
        if (ratio is { } k)
            return new("proved", linearA, linearB, k == NComplex.One
                    ? "the same equation, rearranged: lhs − rhs is unchanged."
                    : $"equivalent: lhs − rhs of the first is {Show(k)} times the second's.")
                { Points = points, Approximate = approximate };
        if (points >= MinPoints)
            return new("agrees", linearA, linearB, $"no proof, but lhs − rhs keeps one ratio between them at all {points} points tried.")
                { Points = points, Approximate = approximate };
        return new("unsettled", linearA, linearB,
            (symbolicTimedOut ? "simplifying took longer than its budget" : "the ratio did not simplify to a number")
            + (numericTimedOut ? "; evaluating took longer than its budget." : $"; only {points} informative points of the {MinPoints} needed."))
            { Points = points, Approximate = approximate };

        static string Show(NComplex v) => Math.Abs(v.Imaginary) < 1e-12 ? Math.Round(v.Real, 9).ToString(CultureInfo.InvariantCulture) : v.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>The constant the first expression is of the second, when their ratio simplifies to a nonzero number.</summary>
    private static NComplex? Ratio(Entity first, Entity second)
    {
        var r = (first / second).Simplify();
        if (r is Entity.Providedf { Expression: var e }) r = e;
        return r.Vars.Count == 0 && Value(r) is { } k && k != NComplex.Zero ? k : null;
    }

    /// <summary>
    /// The two sides of an equation's linear form, split at its <c>=</c> outside every bracket: a sum's
    /// <c>∑_(k = 1)ⁿ</c> has one of its own.
    /// </summary>
    private static (string, string?) Halves(string linear)
    {
        var depth = 0;
        for (var i = 0; i + 2 < linear.Length; i++)
        {
            if (linear[i] is '(' or '[' or '{') depth++;
            else if (linear[i] is ')' or ']' or '}') depth--;
            else if (depth == 0 && string.CompareOrdinal(linear, i, " = ", 0, 3) == 0) return (linear[..i], linear[(i + 3)..]);
        }
        return (linear, null);
    }

    private sealed record SymbolicResult(string Kind, string? Provided = null, NComplex? A = null, NComplex? B = null);

    private static SymbolicResult Symbolic(Entity a, Entity b)
    {
        var difference = (a - b).Simplify();
        if (IsZero(difference)) return new("zero");
        if (difference is Entity.Providedf { Expression: var e, Predicate: var p } && IsZero(e)) return new("zero", p.ToString());
        if (difference.Vars.Count == 0 && difference.EvaluableNumerical && Value(a) is { } va && Value(b) is { } vb)
            return new("constant", A: va, B: vb);
        return new("open");

        static bool IsZero(Entity e) => e is Entity.Number.Complex c && c.IsZero;
    }

    private sealed record Counter(IReadOnlyDictionary<string, double> At, NComplex A, NComplex B);

    private sealed record NumericResult(int Agreed, Counter? Counterexample, bool Approximate, string? Note, bool Constant = false);

    /// <summary>
    /// Both sides at random points, drawn reproducibly from the inputs: free variables from <c>domain</c>, else either sign
    /// of 0.25 to 2.5; a sum's bounds and <c>integers</c> from 1 to 12.
    /// </summary>
    private static NumericResult Numeric(Entity a, Entity b, Options options, int seed, CancellationToken token,
        Func<NComplex, NComplex, double, bool?>? judge = null)
    {
        // Equal, by default; a judge may also skip a point (null) that says nothing.
        judge ??= (x, y, tolerance) => (x - y).Magnitude <= tolerance * Math.Max(1, Math.Max(x.Magnitude, y.Magnitude));
        // Calculus first: substituting into an unevaluated derivative rewrites its own variable.
        var (ea, eb) = (a.Evaled, b.Evaled);
        if (new[] { ea, eb }.Any(e => e.Nodes.OfType<Entity.Derivativef>().Any() || e.Nodes.OfType<Entity.Limitf>().Any()))
            return new(0, null, false, "a derivative or limit that AngouriMath could not work out cannot be evaluated at a point");

        var bound = new[] { ea, eb }.SelectMany(e => e.Nodes.OfType<Entity.CalculusOperator>()).Select(c => c.Var).OfType<Entity.Variable>().ToHashSet();
        var free = ea.Vars.Concat(eb.Vars).Where(v => !bound.Contains(v)).Distinct().OrderBy(v => v.Name, StringComparer.Ordinal).ToArray();
        // A sum's bounds are whole numbers, read before evaluation works the sum out (to n(n + 1)/2 for n ≥ 0, and 0 below).
        var integer = new[] { a, b, ea, eb }.SelectMany(e => e.Nodes.OfType<Entity.Summationf>())
            .SelectMany(s => s.From.Vars.Concat(s.To.Vars)).Select(v => v.Name).Concat(options.Integers).ToHashSet(StringComparer.Ordinal);

        var random = new Random(seed);
        int agreed = 0, tried = 0;
        var approximate = false;
        // A constant needs one evaluation; anything else, points until enough agree or three times as many were drawn.
        for (var k = 0; k < (free.Length == 0 ? 1 : options.Samples * 3) && agreed < options.Samples; k++)
        {
            token.ThrowIfCancellationRequested();
            var at = free.ToDictionary(v => v, v => Draw(v.Name));
            var (va, approxA) = At(ea, at);
            var (vb, approxB) = At(eb, at);
            if (va is not { } x || vb is not { } y) continue;
            tried++;
            approximate |= approxA || approxB;
            var verdict = judge(x, y, approxA || approxB ? LooseTolerance : Tolerance);
            if (verdict is null) continue;
            if (verdict.Value) agreed++;
            else return new(agreed, new(at.ToDictionary(p => p.Key.Name, p => p.Value), x, y), approximate, null);
        }
        return new(agreed, null, approximate, tried == 0 ? "neither side could be evaluated as a number at any point" : null, free.Length == 0);

        double Draw(string name)
        {
            if (options.Domain.TryGetValue(name, out var d))
                return integer.Contains(name) ? random.Next((int)Math.Ceiling(d.Lo), (int)Math.Floor(d.Hi) + 1) : Math.Round(d.Lo + random.NextDouble() * (d.Hi - d.Lo), 6);
            if (integer.Contains(name)) return random.Next(1, 13);
            return Math.Round((random.Next(2) == 0 ? -1 : 1) * (0.25 + random.NextDouble() * 2.25), 6);
        }
    }

    /// <summary>An expression's value at a point: exact where AngouriMath evaluates it, else with integrals and sums done numerically.</summary>
    private static (NComplex? Value, bool Approximate) At(Entity e, IReadOnlyDictionary<Entity.Variable, double> at)
    {
        try
        {
            var s = e;
            foreach (var (variable, value) in at) s = s.Substitute(variable, value);
            if (Value(s) is { } exact) return (exact, false);
            var approximate = false;
            var n = s.Replace(node => node switch
            {
                Entity.Integralf { Range: ({ } from, { } to) } i when Real(from) is { } lo && Real(to) is { } hi
                    && Quadrature(i.Expression, i.Var, lo, hi) is { } area => Mark(area),
                Entity.Summationf { From: var from, To: var to } sum when Real(from) is { } lo && Real(to) is { } hi
                    && lo == Math.Floor(lo) && hi == Math.Floor(hi) && hi - lo < MaxSumTerms && Sum(sum.Expression, sum.Var, (long)lo, (long)hi) is { } total => total,
                _ => node,
            });
            return (Value(n), approximate);

            Entity Mark(double area) { approximate = true; return area; }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return (null, false);
        }
    }

    private static double? Quadrature(Entity body, Entity variable, double lo, double hi)
    {
        double F(double t) => Value(body.Substitute(variable, t)) is { } v && Math.Abs(v.Imaginary) < 1e-12 ? v.Real : double.NaN;
        var area = MathNet.Numerics.Integrate.OnClosedInterval(F, lo, hi, 1e-10);
        return double.IsFinite(area) ? area : null;
    }

    private static Entity? Sum(Entity body, Entity variable, long lo, long hi)
    {
        var total = NComplex.Zero;
        for (var k = lo; k <= hi; k++)
            if (Value(body.Substitute(variable, k)) is { } v) total += v;
            else return null;
        return total.Imaginary == 0 ? total.Real : null;
    }

    private static NComplex? Value(Entity e)
    {
        if (!e.EvaluableNumerical) return null;
        var v = e.EvalNumerical().ToNumerics();
        return double.IsFinite(v.Real) && double.IsFinite(v.Imaginary) ? v : null;
    }

    private static double? Real(Entity e) => Value(e) is { } v && Math.Abs(v.Imaginary) < 1e-12 ? v.Real : null;

    /// <summary>
    /// Runs <paramref name="work"/> on a thread with a large stack and a cancellation token, waiting at most
    /// <paramref name="budgetMs"/>; on overrun it cancels, which AngouriMath honours at its next check, and returns default.
    /// </summary>
    private static T? OnOwnStack<T>(Func<CancellationToken, T> work, int budgetMs, out bool timedOut)
    {
        using var cancel = new CancellationTokenSource();
        T? result = default;
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                MathS.Multithreading.SetLocalCancellationToken(cancel.Token);
                result = work(cancel.Token);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { failure = ex; }
        }, StackBytes) { IsBackground = true, Name = "Equation.equivalent" };
        thread.Start();
        timedOut = !thread.Join(budgetMs);
        if (timedOut)
        {
            cancel.Cancel();
            thread.Join(CancelGraceMs);
            return default;
        }
        return failure is null ? result : default;
    }

    /// <summary>A seed from the inputs, so the same check draws the same points every run.</summary>
    private static int Seed(string a, string? b)
    {
        var h = 2166136261u;
        foreach (var ch in a + "\u0001" + b) h = (h ^ ch) * 16777619u;
        return (int)h;
    }

    [GeneratedRegex(@"\\frac\{\s*d(\^\{?\d+\}?)?\s*([A-Za-z]?)\s*\}\{\s*d\s*([A-Za-z])\s*(\^\{?\d+\}?)?\s*\}")]
    private static partial Regex DerivativeFraction();

    [GeneratedRegex(@"(?<![\\A-Za-z])d\s*([A-Za-z])(?![A-Za-z])")]
    private static partial Regex IntegralD();

    [GeneratedRegex(@"\\[A-Za-z]+|\^|_\{")]
    private static partial Regex MathMark();

    /// <summary>A function name written without its backslash, as a word on its own: <c>sin x</c>, <c>ln(2)</c>.</summary>
    [GeneratedRegex(@"(?<![\\A-Za-z])(arcsin|arccos|arctan|sinh|cosh|tanh|sin|cos|tan|cot|sec|csc|ln|log)(?![A-Za-z])")]
    private static partial Regex BareFunction();

    [GeneratedRegex(@"\\(sum|prod)\s*_\s*\{?\s*i\s*=")]
    private static partial Regex SumOverI();

    [GeneratedRegex(@"(?<![\\A-Za-z])i(?![A-Za-z])")]
    private static partial Regex LoneI();

    private static readonly (string Open, string Close)[] Delimiters = [("$$", "$$"), ("$", "$"), (@"\(", @"\)"), (@"\[", @"\]")];
    #endregion

    #region Fields
    internal const int DefaultSamples = 24, MaxSamples = 200, DefaultBudgetMs = 4000, MaxBudgetMs = 15000, MinPoints = 8, MaxLength = 600;
    private const int ConvertMs = 2000, CancelGraceMs = 1000, StackBytes = 64 << 20, MaxSumTerms = 2000;
    private const double Tolerance = 1e-9, LooseTolerance = 1e-6;
    #endregion
}
