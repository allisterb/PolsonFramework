namespace Polson.MCPServer;

/// <summary>The engine's half of <see cref="JsSurface"/>: it registers no cloud surfaces, so it lists none.</summary>
/// <remarks>The studio's list is in <c>JsSymbols.Studio.cs</c>, which this project does not compile.</remarks>
public static partial class JsSurface
{
    #region Properties
    private static JsReceiver[] StudioCloud => [];
    #endregion
}
