namespace Polson.Drawing.Svg;

public class SnapPathApi
{
    #region Methods
    public float getTotalLength(string pathData) =>
        SnapPathMeasurement.GetTotalLength(pathData);

    public SnapPoint getPointAtLength(string pathData, float length) =>
        SnapPathMeasurement.GetPointAtLength(pathData, length);

    public SnapPoint getPointAtLength(string pathData, double length) =>
        SnapPathMeasurement.GetPointAtLength(pathData, (float)length);

    public SnapBBox getBBox(string pathData) =>
        SnapPathMeasurement.GetBBox(pathData);

    public string squircle(float x, float y, float width, float height, float exponent = 4.5f) =>
        VectorLogoToolkit.CreateSquirclePath(x, y, width, height, exponent);

    public string goldenSpiral(float startX, float startY, float initialRadius, float turns = 3f, int segmentsPerTurn = 36) =>
        VectorLogoToolkit.CreateGoldenSpiralPath(startX, startY, initialRadius, turns, segmentsPerTurn);

    public string emblemBadge(float cx, float cy, float width, float height, string style = "shield") =>
        VectorLogoToolkit.CreateEmblemBadgePath(cx, cy, width, height, style);

    public string tangentFillet(float x1, float y1, float cornerX, float cornerY, float x2, float y2, float radius) =>
        VectorLogoToolkit.CreateTangentFilletPath(x1, y1, cornerX, cornerY, x2, y2, radius);

    public string boneEffect(float startX, float startY, float endX, float endY, float maxBulge = 4f, float controlT = 0.5f) =>
        VectorLogoToolkit.CreateBoneEffectPath(startX, startY, endX, endY, maxBulge, controlT);
    #endregion
}

