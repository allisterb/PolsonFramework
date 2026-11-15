namespace Polson.MCPServer;

using Jint;

using Polson.ExtendedMind.DocumentProcessing;
using Polson.ExtendedMind.ImageGeneration;
using Polson.ExtendedMind.ParallelSearch;
using Polson.ExtendedMind.Photos;

/// <summary>The studio's half of <see cref="JsDrawingEngine"/>: the cloud surfaces and the run's own globals.</summary>
/// <remarks>
/// Kept apart so <c>Polson.Engine</c> can link <c>JsDrawingEngine.cs</c> without ExtendedMind.
/// A build without this file registers none of these globals: <see cref="RegisterStudio"/> has no
/// body there, and its call compiles away.
/// </remarks>
public partial class JsDrawingEngine
{
    #region Properties
    /// <summary>
    /// Asset requisition surface, configured once at startup. Null means the global is not registered
    /// at all; prefer a disabled toolkit, so a script asking for material gets a clear "not
    /// configured" refusal instead of a ReferenceError it cannot interpret.
    /// </summary>
    public static AssetRequisitionToolkit? Assets { get; set; }

    /// <summary>
    /// Document reading, configured once at startup. Registered on the same terms as
    /// <see cref="Assets"/>, and for the same reason a photograph surface is: a script that cannot
    /// read the document it was pointed at must be *told* so, because the failure it would otherwise
    /// reach for is answering from recall and presenting it as sourced.
    /// </summary>
    public static DocumentProcessor? Documents { get; set; }

    /// <summary>
    /// Watching a clip, configured once at startup: Gemini, or Nemotron when <c>Verify:Reader</c> names it. Registered on
    /// the same terms as <see cref="Documents"/>: unconfigured, it refuses with a remedy rather than being absent.
    /// </summary>
    public static VideoReader? Video { get; set; }

    /// <summary>
    /// Checks the facts a read-back's <c>sources</c> option states, on the web, configured with Parallel at startup. Null and
    /// they are recorded as unchecked.
    /// </summary>
    public static FactChecker? Facts { get; set; }

    /// <summary>
    /// Reference-photograph surface, configured once at startup. Null means the same thing it means
    /// for <see cref="Assets"/> — a disabled toolkit is registered instead, so a script asking for a
    /// likeness is refused in words rather than by a ReferenceError.
    /// </summary>
    public static PhotoToolkit? Photos { get; set; }

    /// <summary>
    /// Where <c>Taste.images</c> fetches Qloo's pictures, configured with Qloo at startup. Null and every picture is
    /// refused as not configured; the answers themselves are unaffected.
    /// </summary>
    public static Polson.ExtendedMind.Taste.QlooImageSource? TasteImages { get; set; }
    #endregion

    #region Methods
    partial void RegisterStudio(Engine engine, SessionContext? session, string? executionId, DrawingExecutionResult result)
    {
        // Cloud asset requisition. Registered even when disabled so scripts can branch on the
        // returned failure rather than on the global being absent.
        var assets = Assets ?? new AssetRequisitionToolkit(null, new RequisitionCache(), new AssetBudget(0), "agent");
        engine.SetValue("Assets", assets);

        // Reference photography. Registered on the same terms as Assets, and for a stronger
        // reason: a script that cannot fetch a likeness must be told so, because the failure it
        // would otherwise reach for is inventing an image URL.
        engine.SetValue("Photo", Photos ?? new PhotoToolkit(null, new PhotoBudget(0), "agent"));

        // Reading a supplied document. Same terms again: an unconfigured surface refuses with a
        // remedy rather than being absent.
        engine.SetValue("Documents", Documents ?? new DocumentProcessor(null, new DocumentBudget(0)));
        engine.SetValue("Video", Video ?? new VideoReader([], new DocumentBudget(0)));

        // A finished film against what it set and where that came from; its verdicts are recorded for the studio.
        var verifyEvents = Events;
        engine.SetValue("Verify", new VerifyToolkit(Video, ProjectRoot, session,
            (type, fields) => verifyEvents?.Append(type, session?.Stage, executionId, fields), Facts));

        // Sourced research, read-only. Commissioning is the Research MCP tool's job because a run
        // takes far longer than ScriptTimeoutSeconds allows; by the time a script sees a task the
        // waiting is done, so every member here is a plain read.
        engine.SetValue("Research", new ResearchToolkit(session?.Research ?? new ResearchRegistry()));

        // Qloo answers, read-only, for the same reason: the agent fetches them with the Taste tool so it
        // can reason with them, and a script reads them back to cite what a mark rests on.
        // checkReferences reads direction.md inside the project and records its verdicts as `taste.check`.
        // images is the one fetch here: Qloo's own pictures of entities its answers named, for visual research.
        var tasteEvents = Events;
        engine.SetValue("Taste", new Polson.ExtendedMind.Taste.TasteToolkit(
            session?.Taste ?? new Polson.ExtendedMind.Taste.TasteRegistry(), ProjectRoot,
            (type, fields) => tasteEvents?.Append(type, session?.Stage, executionId, fields), TasteImages));
    }
    #endregion
}
