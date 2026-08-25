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
    #endregion
}

