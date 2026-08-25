namespace Polson.MCPServer;

using System;

/// <summary>
/// Thrown by the JS global exit(message) to halt script execution immediately.
/// </summary>
public sealed class ExitException(string message) : Exception(message);

