namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;

/// <summary>
/// Append-only record of what the server did, written to <c>events/server.jsonl</c> inside a
/// project directory.
/// </summary>
/// <remarks>
/// The filesystem is the record, not the transport: this is what survives a page refresh, a second
/// viewer, an orchestrator restart, and the end of the session. See <c>docs/project-layout.md</c>.
/// <para>
/// One writer per file is the whole concurrency model — readers merge on read, ordering by
/// timestamp and tie-breaking on <c>(src, seq)</c>. This class owns <c>server.jsonl</c> and writes
/// nothing else, so a crash here truncates its own file and no one else's.
/// </para>
/// <para>
/// Every failure is swallowed. A run that dies because its diary could not be written would be a
/// worse outcome than a run with no diary, so the first write failure disables the log, warns once,
/// and lets the drawing continue.
/// </para>
/// </remarks>
public sealed class RunEventLog : Runtime
{
    #region Constants
    /// <summary>Millisecond precision, sortable, unambiguous. Matches the layout document.</summary>
    private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
    #endregion

    #region Fields
    private readonly Lock gate = new();
    private readonly string? eventsFile;
    private readonly string? scriptsDir;

    private long seq = -1;
    private int scriptCount = -1;
    private bool disabled;
    #endregion

    #region Constructors
    public RunEventLog(string? projectRoot)
    {
        if (string.IsNullOrWhiteSpace(projectRoot)) return;

        var root = Path.GetFullPath(projectRoot);
        eventsFile = Path.Combine(root, "events", "server.jsonl");
        scriptsDir = Path.Combine(root, "scripts");
    }
    #endregion

    #region Properties
    /// <summary>Whether events are being recorded. False for an ad-hoc server with no project directory.</summary>
    public bool Enabled => eventsFile is not null && !disabled;

    /// <summary>Absolute path of the log, or null when there is no project to write into.</summary>
    public string? FilePath => eventsFile;
    #endregion

    #region Methods
    /// <summary>Appends one event. Never throws.</summary>
    /// <remarks>
    /// <paramref name="stage"/> is the agent's own account of what it was doing and may be absent;
    /// <paramref name="execution"/> identifies the tool call, so a render can be tied to the
    /// <c>script.start</c> that produced it without matching on script paths.
    /// </remarks>
    public void Append(string type, string? stage = null, string? execution = null, IReadOnlyDictionary<string, object?>? fields = null)
    {
        if (!Enabled) return;

        try
        {
            lock (gate)
            {
                if (disabled) return;

                Directory.CreateDirectory(Path.GetDirectoryName(eventsFile!)!);

                // Seed from what is already on disk so a restart continues the sequence rather than
                // repeating it, which would make (src, seq) useless as a tie-break.
                if (seq < 0) seq = CountLines(eventsFile!);

                var line = Serialize(type, ++seq, stage, execution, fields);
                File.AppendAllText(eventsFile!, line + "\n", new UTF8Encoding(false));
            }
        }
        catch (Exception ex)
        {
            Disable(ex);
        }
    }

    /// <summary>
    /// Persists an executed script and returns its project-relative path, or null if not recorded.
    /// </summary>
    /// <remarks>
    /// The server has the script already, so saving it here makes the trace complete without asking
    /// the agent to duplicate its own work — and a run stays reconstructible even when the agent
    /// never got round to writing anything down.
    /// </remarks>
    public string? SaveScript(string script)
    {
        if (!Enabled || string.IsNullOrEmpty(script)) return null;

        try
        {
            lock (gate)
            {
                if (disabled) return null;

                Directory.CreateDirectory(scriptsDir!);

                if (scriptCount < 0) scriptCount = Directory.EnumerateFiles(scriptsDir!, "*.js").Count();

                var name = $"{++scriptCount:0000}.js";
                File.WriteAllText(Path.Combine(scriptsDir!, name), script, new UTF8Encoding(false));
                return "scripts/" + name;
            }
        }
        catch (Exception ex)
        {
            Disable(ex);
            return null;
        }
    }

    /// <summary>Rewrites an absolute path as a forward-slashed path relative to the project.</summary>
    /// <remarks>
    /// Relative because the log has to stay valid when the directory is moved or served; forward
    /// slashes because the demo site turns these straight into URLs, and a Windows separator would
    /// arrive at the browser as an escape character.
    /// </remarks>
    public string? Relativize(string? absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath) || scriptsDir is null) return absolutePath;

        var root = Path.GetDirectoryName(scriptsDir)!;
        try
        {
            var relative = Path.GetRelativePath(root, absolutePath);
            return relative.Replace('\\', '/');
        }
        catch
        {
            return absolutePath;
        }
    }

    /// <summary>
    /// Relaxed escaping, because this file is meant to be read by people as well as parsed.
    /// </summary>
    /// <remarks>
    /// The strict default turns every apostrophe and angle bracket into a <c>\uXXXX</c> escape,
    /// which makes a JavaScript error message nearly unreadable. Relaxing it costs no safety here:
    /// the escapes only protect JSON pasted verbatim into an HTML <c>&lt;script&gt;</c> block, and
    /// any consumer that parses the line gets the same characters either way. A viewer rendering
    /// these values into a page must escape them itself, exactly as it would for any other data.
    /// </remarks>
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static string Serialize(string type, long sequence, string? stage, string? execution, IReadOnlyDictionary<string, object?>? fields)
    {
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString("ts", DateTime.UtcNow.ToString(TimestampFormat, CultureInfo.InvariantCulture));
            writer.WriteNumber("seq", sequence);
            writer.WriteString("src", "server");
            writer.WriteString("type", type);

            // Ahead of the payload so a reader can filter on them without parsing the whole line.
            if (!string.IsNullOrEmpty(stage)) writer.WriteString("stage", stage);
            if (!string.IsNullOrEmpty(execution)) writer.WriteString("execution", execution);

            foreach (var (key, value) in fields ?? new Dictionary<string, object?>())
            {
                // Absent rather than null: every successful script would otherwise carry an
                // "error": null, and a reader checks for the key's presence either way.
                if (value is null) continue;

                writer.WritePropertyName(key);
                switch (value)
                {
                    case string s: writer.WriteStringValue(s); break;
                    case bool b: writer.WriteBooleanValue(b); break;
                    case int i: writer.WriteNumberValue(i); break;
                    case long l: writer.WriteNumberValue(l); break;
                    case double d: writer.WriteNumberValue(d); break;

                    // A probe tally is the one field with structure. Written as a nested object so
                    // `"probes":{"measure":12}` stays greppable and parseable; the default case would
                    // stringify the dictionary's type name.
                    case IReadOnlyDictionary<string, int> tally:
                        writer.WriteStartObject();
                        foreach (var (k, n) in tally) writer.WriteNumber(k, n);
                        writer.WriteEndObject();
                        break;

                    default: writer.WriteStringValue(value.ToString()); break;
                }
            }

            writer.WriteEndObject();
        }

        // One event is one line, so a reader can tail the file and parse each line independently.
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static long CountLines(string file)
    {
        if (!File.Exists(file)) return 0;

        var n = 0L;
        foreach (var _ in File.ReadLines(file)) n++;
        return n;
    }

    private void Disable(Exception ex)
    {
        if (disabled) return;
        disabled = true;
        Warn("Run event log disabled after a write failure ({0}): {1}", eventsFile ?? "(no path)", ex.Message);
    }
    #endregion
}
