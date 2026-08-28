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

    public long ExecutionTimeMs { get; set; }

    public string? Error { get; set; }

    [JsonIgnore]
    public object? ReturnValue { get; set; }
    #endregion
}
