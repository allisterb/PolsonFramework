namespace Polson;

/// <summary>A recorded motion flattened onto the page: per frame, where each body part points and where the hips are.</summary>
/// <remarks>
/// The seam that lets <c>composition.rigFromDrawing(..., { follow })</c> take <c>Character.track(...)</c> without the
/// animation project depending on the 3D one, as <see cref="ILandmarkSource"/> does for a detection. Parts carry
/// <c>Character.jointMap</c>'s names. Implemented explicitly, so it stays off the script surface.
/// </remarks>
public interface IPlanarMotion
{
    /// <summary>The sample times, in seconds from the first.</summary>
    double[] FrameTimes { get; }

    /// <summary>
    /// Where a part points at a frame, in page degrees (0 right, 90 down), and the share of its length in the page
    /// plane, 0 to 1; false for a part there is not. <c>hips</c> is the line from the right hip to the left.
    /// </summary>
    bool TryGetDirection(string part, int frame, out double angleDeg, out double inPlane);

    /// <summary>How far the middle of the hips has moved from standing at a frame, in page axes, in leg lengths.</summary>
    (double X, double Y) RootOffset(int frame);
}
