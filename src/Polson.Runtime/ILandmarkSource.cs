namespace Polson;

/// <summary>Named points found in an image, in its pixels: a body detection, or anything else that names joints.</summary>
/// <remarks>
/// The seam that lets <c>composition.rigFromDrawing(...)</c> take <c>Character.detect(...)</c> without the animation
/// project depending on the 3D one, the same way <see cref="IDataUriSource"/> lets the vector layer take a bitmap.
/// Implemented explicitly, so it stays off the script surface.
/// </remarks>
public interface ILandmarkSource
{
    /// <summary>The width of the image the points are in.</summary>
    int SourceWidth { get; }

    /// <summary>The height of the image the points are in.</summary>
    int SourceHeight { get; }

    /// <summary>A named point and how sure the finder was of it, 0 to 1; false for a name there is not.</summary>
    bool TryGetLandmark(string name, out double x, out double y, out double visibility);
}
