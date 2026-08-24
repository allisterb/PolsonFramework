namespace Polson.Drawing.Svg;

public record struct SnapPoint
{
    #region Constructors
    public SnapPoint(float x, float y, float alpha = 0f, float m = 0f, float n = 0f)
    {
        X = x;
        Y = y;
        Alpha = alpha;
        M = m;
        N = n;
    }
    #endregion

    #region Properties
    public float X { get; set; }
    public float Y { get; set; }
    public float Alpha { get; set; }
    public float M { get; set; }
    public float N { get; set; }
    #endregion
}

