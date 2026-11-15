namespace Polson.MCPServer;

using Polson.ExtendedMind.DocumentProcessing;
using Polson.ExtendedMind.ImageGeneration;
using Polson.ExtendedMind.ParallelSearch;
using Polson.ExtendedMind.Photos;
using Polson.ExtendedMind.SpeechGeneration;

/// <summary>The studio's receivers: the run record's <c>Stage</c>, and the cloud surfaces.</summary>
/// <remarks>
/// Spread into <see cref="Receivers"/> where they always stood, so the studio's index reads as it did. Properties
/// rather than fields: <see cref="Receivers"/> is initialised from them, and field order across partial files is
/// not defined. <c>Polson.Engine</c> declares both empty.
/// </remarks>
public static partial class JsSurface
{
    #region Properties
    private static JsReceiver[] StudioGlobals =>
    [
        new("Stage", typeof(StageApi), "Globals", false),
    ];

    private static JsReceiver[] StudioCloud =>
    [
        new("Assets", typeof(AssetRequisitionToolkit), "Assets", false),
        new("material", typeof(MaterialAsset), "Assets", true),
        new("plate", typeof(BackdropPlate), "Assets", true),
        new("matte", typeof(MatteAsset), "Assets", true),
        new("cutout", typeof(CutoutAsset), "Assets", true),
        new("cell", typeof(CutoutCell), "Assets", true),
        new("budget", typeof(AssetBudget), "Assets", true),
        new("spoken", typeof(SpeechAsset), "Assets", true),

        new("Documents", typeof(DocumentProcessor), "Documents", false),
        new("answer", typeof(DocumentAnswer), "Documents", true),
        new("entry", typeof(DocumentEntry), "Documents", true),
        new("evidence", typeof(DocumentEvidence), "Documents", true),
        new("documentBudget", typeof(DocumentBudget), "Documents", true),

        new("Video", typeof(VideoReader), "Video", false),

        new("Verify", typeof(VerifyToolkit), "Verify", false),

        new("Photo", typeof(PhotoToolkit), "Photo", false),
        new("photo", typeof(PhotoAsset), "Photo", true),
        new("subject", typeof(SubjectMatch), "Photo", true),
        new("licence", typeof(PhotoLicence), "Photo", true),
        new("photoBudget", typeof(PhotoBudget), "Photo", true),

        // Read-only. Research is commissioned by the Research MCP tool, never from a script.
        new("Research", typeof(ResearchToolkit), "Research", false),
        new("task", typeof(ResearchTask), "Research", true),
        new("basis", typeof(FieldBasis), "Research", true),
        new("citation", typeof(TaskCitation), "Research", true),

        // Read-only as well. Qloo is asked by the Taste MCP tool, so the agent reads the answer; scripts cite it.
        new("Taste", typeof(Polson.ExtendedMind.Taste.TasteToolkit), "Taste", false),
        new("tasteResult", typeof(Polson.ExtendedMind.Taste.TasteResult), "Taste", true),
        new("tasteBudget", typeof(Polson.ExtendedMind.Taste.QlooBudget), "Taste", true),
        new("tasteImage", typeof(Polson.ExtendedMind.Taste.TasteImage), "Taste", true),
    ];
    #endregion
}
