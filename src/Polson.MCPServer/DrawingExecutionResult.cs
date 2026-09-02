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
