namespace Polson.Drawing.Skia;

using System;

/// <summary>The <c>Face</c> global — face landmarks from an image, when a backend is installed.</summary>
/// <remarks>
/// <para>
/// <b>Members are PascalCase in C# and Jint resolves the JS camelCase spelling onto them</b>, so
/// this is <c>Face.detect(...)</c> and <c>Face.available</c> to a script. Do not add camelCase
/// aliases.
/// </para>
/// <para>
/// <b>Optional, and it says so before you commit to it.</b> Same shape as <c>Skia.tracer</c>: ask
/// <c>available</c> first, because a script that plans a route needing landmarks and only then finds
/// it has none has spent its passes for nothing.
/// </para>
/// </remarks>
public class FaceApi
{
    #region Properties
    /// <summary>Whether detection will work here.</summary>
    public bool Available => FaceDetector.Available;

    /// <summary>What is missing, when it is not available. Null when everything is present.</summary>
    public string? Missing => FaceDetector.Missing;

    /// <summary>The interpreter that would be used. Worth putting in a <c>Stage.note</c>.</summary>
    public string? Python => FaceDetector.Python;

    /// <summary>The model bundle that would be used.</summary>
    public string? Model => FaceDetector.Model;
    #endregion

    #region Methods
    /// <summary>Finds one face in a bitmap or canvas.</summary>
    /// <param name="image">A <c>SkiaBitmapWrapper</c> or <c>SkiaCanvas</c>.</param>
    /// <param name="timeoutMs">How long to wait for the backend. The first call loads a model.</param>
    /// <remarks>
    /// <b>Not finding a face is a RESULT, not an error</b> — check <c>found</c> before anything
    /// else. Only a broken or absent backend throws, because that is an environment to fix rather
    /// than an outcome to handle, which is the same line <c>bitmap.trace</c> draws.
    /// </remarks>
    public FaceDetection Detect(object image, int timeoutMs = 60000)
    {
        var bitmap = image switch
        {
            SkiaBitmapWrapper b => b,
            SkiaCanvas c => c.Bitmap,
            null => throw new ArgumentNullException(nameof(image)),
            _ => throw new ArgumentException(
                $"Face.detect takes a bitmap or a canvas, and got {image.GetType().Name}.",
                nameof(image))
        };

        return FaceDetector.Detect(bitmap, timeoutMs);
    }
    #endregion
}
