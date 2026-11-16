namespace Polson.MCPServer;

/// <summary>The engine's words for its tools: it carries the SDK reference and no studio manuals.</summary>
/// <remarks>The studio defines the same constants in <c>DrawingMcpTools.Studio.cs</c>, which this project does not link.</remarks>
public partial class DrawingMcpTools
{
    #region Constants & Static Properties
    private const string SearchLead = "Searches the Polson JS SDK reference — the core and schema documents — for passages " +
        "relevant to a technique, and returns them ranked with the calls that implement them. CALL THIS FIRST when you " +
        "know what you want to draw but not which calls draw it";
    private const string SearchScopes = "'all' (default) or 'sdk'. Both search the SDK reference, the only corpus this server carries.";
    private const string ReadDocLead = "Reads a document IN FULL by its `polson://` URI and returns the text — the SDK method " +
        "reference (polson://sdk/core/{Area}), the schemas (polson://sdk/schema/{Area}) or the symbol index (polson://sdk/symbols).";
    private const string ReadDocBareForms = "'sdk/core/Chart', 'sdk/symbols'";
    private const string ReadDocExample = "'polson://sdk/core/Chart'";
    private const string PrintManual = "";
    #endregion
}
