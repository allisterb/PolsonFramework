namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;

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

    /// <summary>
    /// A face that turns to profile in the artist's own drawing, from a turnaround's views:
    /// <c>Face.fromViews({ front, left, right })</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Each view is a crop of one head</b>, as a bitmap or canvas. <c>front</c> is required;
    /// <c>left</c> and <c>right</c> are optional, and <b>which is which is read from the drawing</b>
    /// — each side view's turn is detected — so the names are only labels, and a sheet's own "LEFT"
    /// may be either. Crops need not be the same size: each side view's scale and offset are fitted
    /// from its features, and a view that will not line up is refused with the measurement.
    /// </para>
    /// <para>
    /// The result is a mesh on MediaPipe's canonical topology and texture layout, so
    /// <c>Mesh.draw</c>'s expression units and <c>mesh.withFace(...)</c> work on it. Its
    /// <c>source</c> says how many triangles came from each view and at what scale each side was read.
    /// Options: <c>{ resolution }</c>, the atlas size, 2048 by default.
    /// </para>
    /// </remarks>
    public FaceMesh FromViews(object views, object? options = null)
    {
        var v = JsInterop.AsDict(views)
            ?? throw new ArgumentException("Face.fromViews takes { front, left?, right? }, each a bitmap or canvas.", nameof(views));
        MeshToolkit.RefuseUnknown(v, ViewNames, "Face.fromViews view");
        var opt = JsInterop.AsDict(options);
        MeshToolkit.RefuseUnknown(opt, ["resolution"], "Face.fromViews option");

        if (!v.Contains("front") || v["front"] is null)
            throw new ArgumentException(
                "Face.fromViews needs a front view: { front, left?, right? }. The front gives every vertex its place; the sides correct its depth.",
                nameof(views));

        FaceViews.View Read(string name)
        {
            var bitmap = v[name] switch
            {
                SkiaBitmapWrapper b => b,
                SkiaCanvas c => c.Bitmap,
                var other => throw new ArgumentException(
                    $"The '{name}' view must be a bitmap or a canvas, and got {other?.GetType().Name ?? "null"}.", nameof(views))
            };
            return new FaceViews.View(name, FaceDetector.Detect(bitmap), bitmap.Bitmap);
        }

        var front = Read("front");
        List<FaceViews.View> sides = [];
        foreach (var name in new[] { "left", "right" })
            if (v.Contains(name) && v[name] is not null) sides.Add(Read(name));

        return FaceViews.Build(front, sides, FaceDetector.Triangles(), FaceDetector.CanonicalUvs(),
                               (int)MeshToolkit.Num(opt, "resolution", 0f));
    }
    #endregion

    #region Fields
    static readonly string[] ViewNames = ["front", "left", "right"];
    #endregion
}
