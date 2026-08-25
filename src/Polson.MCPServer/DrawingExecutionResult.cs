namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

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

    public byte[]? PngBytes { get; set; }

    public string? PngDataUrl =>
        PngBytes != null && PngBytes.Length > 0
            ? "data:image/png;base64," + Convert.ToBase64String(PngBytes)
            : null;

    public List<string> Logs { get; set; }

    public long ExecutionTimeMs { get; set; }

    public string? Error { get; set; }

    [JsonIgnore]
    public object? ReturnValue { get; set; }
    #endregion
}

