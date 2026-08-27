namespace Polson.Drawing.Svg;

/// <summary>The <c>Snap.path</c> shorthand helpers, returning SVG path data strings.</summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS
/// camelCase spelling onto them, so a script calling <c>x.doThing()</c> reaches <c>DoThing()</c>.
/// The camelCase form is the one documented in <c>docs/Polson.core.md</c> and the studio manuals.
/// </remarks>
public class SnapPathApi
{
    #region Methods
    public float GetTotalLength(string pathData) =>
        SnapPathMeasurement.GetTotalLength(pathData);

    public SnapPoint GetPointAtLength(string pathData, float length) =>
        SnapPathMeasurement.GetPointAtLength(pathData, length);

    public SnapPoint GetPointAtLength(string pathData, double length) =>
        SnapPathMeasurement.GetPointAtLength(pathData, (float)length);

    public SnapBBox GetBBox(string pathData) =>
        SnapPathMeasurement.GetBBox(pathData);

    public string Squircle(float x, float y, float width, float height, float exponent = 4.5f) =>
        VectorLogoToolkit.CreateSquirclePath(x, y, width, height, exponent);

    public string GoldenSpiral(float startX, float startY, float initialRadius, float turns = 3f, int segmentsPerTurn = 36) =>
        VectorLogoToolkit.CreateGoldenSpiralPath(startX, startY, initialRadius, turns, segmentsPerTurn);

    public string EmblemBadge(float cx, float cy, float width, float height, string style = "shield") =>
        VectorLogoToolkit.CreateEmblemBadgePath(cx, cy, width, height, style);

    public string TangentFillet(float x1, float y1, float cornerX, float cornerY, float x2, float y2, float radius) =>
        VectorLogoToolkit.CreateTangentFilletPath(x1, y1, cornerX, cornerY, x2, y2, radius);

    public string BoneEffect(float startX, float startY, float endX, float endY, float maxBulge = 4f, float controlT = 0.5f) =>
        VectorLogoToolkit.CreateBoneEffectPath(startX, startY, endX, endY, maxBulge, controlT);
    #endregion
}

