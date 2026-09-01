namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

using Acornima;
using Acornima.Ast;

/// <summary>
/// Answers structural questions about a script without reading the whole thing into context.
/// </summary>
/// <remarks>
/// <para>
/// A downstream stage is told to read the previous agent's script, and does — a live
/// <c>comic_studio</c> run pulled between 0.9 and 1.9 MB of tool results into each subagent's window,
/// and its Critic read twenty-four <c>.js</c> files whole, three of them twice, to answer questions
/// like <i>is <c>SHAFT</c> actually drawn?</i> It answered correctly. It just had to carry the entire
/// program to do it.
/// </para>
/// <para>
/// <b>This is a context tool, not a speed tool</b>, and the distinction is measured rather than
/// assumed. In that run wall-clock tracked bytes *emitted*, not bytes read: the Critic ingested the
/// most and finished fastest, in eleven minutes against the Colorist's sixty-seven. Reading arrives
/// as cached input and is cheap. So the gain here is window headroom and precision — fewer
/// compactions, and an exact answer instead of a re-read — while <c>scriptFile</c> remains the lever
/// that moves the clock.
/// </para>
/// <para>
/// Parsing uses <b>Acornima</b>, which is already in the tree as Jint's own parser. That is the whole
/// reason to prefer it: what parses here parses in the engine, so this can never report a structure
/// the sandbox would reject.
/// </para>
/// <para>
/// Every answer carries character offsets as well as line numbers, because the next thing to build on
/// this is a surgical edit, and splicing the original text by node range preserves the comments and
/// formatting outside the edited node — which re-printing an AST does not. Scripts here are required
/// to carry inline comments, so that is not a nicety.
/// </para>
/// </remarks>
internal static class ScriptInspector
{
    #region Methods
    /// <summary>
    /// What a script declares at the top level: functions, constants, and where each one lives.
    /// </summary>
    /// <remarks>
    /// Top level only, deliberately. The workflows ask for one function per layer and a palette
    /// object at the top, so the top level *is* the structure a reader wants; descending into every
    /// nested arrow function would bury it in the same volume this exists to avoid.
    /// </remarks>
    internal static JsonObject Outline(string source, string file)
    {
        var script = Parse(source, file);
        var declarations = new JsonArray();

        foreach (var node in script.Body)
        {
            foreach (var declared in Declared(node))
            {
                declarations.Add(Describe(declared.Name, declared.Kind, declared.Node, source));
            }
        }

        return new JsonObject
        {
            ["file"] = file,
            ["characters"] = source.Length,
            ["lines"] = source.Count(c => c == '\n') + 1,
            ["declarations"] = declarations,
        };
    }

    /// <summary>
    /// One declaration, and everywhere its name is used.
    /// </summary>
    /// <remarks>
    /// The references are the half a search cannot give honestly: a text match finds the name in a
    /// comment and in a longer identifier that merely contains it, and misses nothing only because it
    /// over-reports. These are resolved identifier nodes, so <c>SHAFT</c> does not match
    /// <c>SHAFT_TOP</c> and does not match the word in a note.
    /// </remarks>
    internal static JsonObject Find(string source, string file, string name, bool includeSource)
    {
        var script = Parse(source, file);

        var match = script.Body
            .SelectMany(Declared)
            .FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));

        var result = new JsonObject { ["file"] = file, ["name"] = name };

        if (match.Node is null)
        {
            // Said as a fact rather than returned as an empty list: "no such declaration" is a
            // definitive answer, and an agent that reads it as "the search failed" will go and read
            // the whole file to check — which is the cost this exists to remove.
            result["found"] = false;
            result["message"] = $"No top-level declaration named '{name}' in {file}.";
            result["declared"] = new JsonArray([.. script.Body.SelectMany(Declared)
                .Select(d => (JsonNode)d.Name)]);
            return result;
        }

        result["found"] = true;
        result["declaration"] = Describe(match.Name, match.Kind, match.Node, source);

        var references = new JsonArray();
        foreach (var identifier in Walk(script).OfType<Identifier>())
        {
            if (!string.Equals(identifier.Name, name, StringComparison.Ordinal)) continue;

            // The declaration names itself; counting that as a reference would report every unused
            // constant as used once.
            if (identifier.Range.Start >= match.Node.Range.Start
                && identifier.Range.End <= match.Node.Range.End
                && Inside(identifier, match.Node)) continue;

            references.Add(new JsonObject
            {
                ["line"] = identifier.Location.Start.Line,
                ["column"] = identifier.Location.Start.Column,
                ["start"] = identifier.Range.Start,
            });
        }

        result["references"] = references;
        result["referenceCount"] = references.Count;

        if (includeSource)
        {
            result["source"] = source[match.Node.Range.Start..match.Node.Range.End];
        }

        return result;
    }
    #endregion

    #region Methods (private)
    private static Script Parse(string source, string file)
    {
        try
        {
            return new Parser().ParseScript(source);
        }
        catch (ParseErrorException ex)
        {
            // The same parser the engine uses, so a failure here is a failure there. Saying where
            // turns "the file is broken" into a line to look at.
            throw new ArgumentException(
                $"{file} is not parseable JavaScript: {ex.Error.Description} "
              + $"at line {ex.Error.Position.Line}, column {ex.Error.Position.Column}.", ex);
        }
    }

    /// <summary>Top-level things that introduce a name, with the kind a reader would call them.</summary>
    private static IEnumerable<(string Name, string Kind, Node Node)> Declared(Node node)
    {
        switch (node)
        {
            case FunctionDeclaration { Id.Name: { } fn }:
                yield return (fn, "function", node);
                break;

            case ClassDeclaration { Id.Name: { } cls }:
                yield return (cls, "class", node);
                break;

            case VariableDeclaration declaration:
                foreach (var declarator in declaration.Declarations)
                {
                    // Only plain names. A destructuring pattern declares several at once and none of
                    // them is the thing a caller asked about by name, so reporting the pattern under
                    // one of its names would be a small lie in the outline.
                    if (declarator.Id is Identifier { Name: { } id })
                    {
                        yield return (id, declaration.Kind.ToString().ToLowerInvariant(), declarator);
                    }
                }

                break;
        }
    }

    private static JsonObject Describe(string name, string kind, Node node, string source)
    {
        var text = source[node.Range.Start..node.Range.End];

        return new JsonObject
        {
            ["name"] = name,
            ["kind"] = kind,
            ["line"] = node.Location.Start.Line,
            ["endLine"] = node.Location.End.Line,

            // Offsets as well as lines: a later surgical edit splices by range, and a range read off
            // the same parse is the one that cannot drift from what a line number means.
            ["start"] = node.Range.Start,
            ["end"] = node.Range.End,
            ["characters"] = text.Length,
        };
    }

    /// <summary>Whether <paramref name="inner"/> is the declared name itself rather than a use of it.</summary>
    private static bool Inside(Identifier inner, Node declaration) => declaration switch
    {
        FunctionDeclaration f => ReferenceEquals(f.Id, inner),
        ClassDeclaration c => ReferenceEquals(c.Id, inner),
        VariableDeclarator v => ReferenceEquals(v.Id, inner),
        _ => false,
    };

    private static IEnumerable<Node> Walk(Node root)
    {
        var stack = new Stack<Node>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            yield return node;

            foreach (var child in node.ChildNodes)
            {
                if (child is not null) stack.Push(child);
            }
        }
    }
    #endregion
}
