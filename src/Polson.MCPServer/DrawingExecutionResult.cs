namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Polson.Drawing.Skia;

public class DrawingExecutionResult
{
    #region Constructors
    public DrawingExecutionResult()
    {
        Logs = new List<string>();
        SvgXml = string.Empty;
    }
    #endregion

    #region Properties
    public bool Success { get; set; }

    /// <summary>
    /// The serialized vector document. <b>Server-side only — never travels in the tool response.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// It used to. Nothing ever consumed it from the response: every consumer is on this side of the
    /// boundary — the CLI writes it to a file, <see cref="DrawingMcpTools"/> writes it and scans it
    /// for unresolvable hrefs. An agent does not need it to <i>see</i> the work, which is the raster
    /// peek, nor to <i>understand</i> it, which is the script it wrote.
    /// </para>
    /// <para>
    /// <b>What it cost.</b> An agent run flagged it in 2026 as "returned in full even when outSvg
    /// wrote it to disk — a few KB of duplicated payload per call", and it was filed as a nit
    /// because a few KB is what it was. Once a bitmap could be inlined as a data URI it stopped
    /// being a nit: measured over a live MCP session, a vector page carrying one 400px portrait
    /// returned a <b>117,786-character</b> tool result of which <b>109,045 characters were this
    /// field</b> — roughly 29,000 tokens, on a call that had already asked for both
    /// <c>outFile</c> and <c>outSvg</c>. Four portraits would exceed 100,000 tokens, on every call,
    /// carried forward for the rest of the conversation.
    /// </para>
    /// <para>
    /// <see cref="ImageBytes"/> has had this discipline all along — suppressed by <c>outFile</c>,
    /// opt-in through <c>includeBytes</c>, and accounted for by a <c>bytesInlined</c> event. This
    /// field had none of it, which is the whole reason it grew dangerous unnoticed.
    /// </para>
    /// <para>
    /// The round trip is now file-based, matching the raster side exactly:
    /// <c>outFile</c> → <c>Skia.Image.load</c>, and <c>outSvg</c> → <c>Snap.load</c>. Within one
    /// session <c>Session.svg = paper.toString()</c> also works and costs nothing.
    /// </para>
    /// </remarks>
    [JsonIgnore]
    public string SvgXml { get; set; }

    public byte[]? ImageBytes { get; set; }

    public string? ImageFilePath { get; set; }

    public string? SvgFilePath { get; set; }

    /// <summary>
    /// Identifies this tool call in the run's record. Cite it when referring to a specific render.
    /// </summary>
    public string? ExecutionId { get; set; }

    public int ImageSize { get; set; }

    public string ImageFormat { get; set; } = "webp";

    [JsonIgnore]
    public string? ImageDataUri =>
        ImageBytes != null && ImageBytes.Length > 0
            ? SkiaImageEncoder.ToDataUri(ImageBytes, ImageFormat)
            : null;

    public List<string> Logs { get; set; }

    /// <summary>How long the script itself took to evaluate. Rendering is not included.</summary>
    public long ExecutionTimeMs { get; set; }

    /// <summary>
    /// How long it took to turn the returned drawing into <see cref="ImageBytes"/> — rasterising a
    /// paper where one was returned, then encoding.
    /// </summary>
    /// <remarks>
    /// Reported separately because it is usually the larger half and used to be invisible: on a
    /// 1200x760 scene, 24 ms of script against 93 ms of render and encode. A run record showing only
    /// <see cref="ExecutionTimeMs"/> therefore under-reported the true cost of every render, which
    /// makes anything measured beside it look dominant by comparison.
    /// </remarks>
    public long EncodeTimeMs { get; set; }

    public string? Error { get; set; }

    [JsonIgnore]
    public object? ReturnValue { get; set; }
    #endregion
}
