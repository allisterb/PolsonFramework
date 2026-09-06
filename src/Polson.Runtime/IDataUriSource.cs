namespace Polson;

/// <summary>
/// Something that can hand over its pixels as a self-contained <c>data:</c> URI.
/// </summary>
/// <remarks>
/// <para>
/// Exists so <c>paper.image(...)</c> can take a bitmap, a canvas, a requisitioned material or a
/// reference photograph directly, rather than making every caller remember to write
/// <c>.toDataUri()</c>. It lives in <c>Polson.Runtime</c> because that is the only assembly
/// <c>Polson.Drawing.Svg</c> can see — the vector layer must not depend on the raster layer or on
/// the cloud surfaces, and this interface is the seam that keeps that true while still letting all
/// three be passed to it.
/// </para>
/// <para>
/// <b>Why it is worth an interface at all.</b> An <c>&lt;image&gt;</c> in an SVG resolves an external
/// href only when the SVG is treated as a <i>document</i>. Loaded through <c>&lt;img&gt;</c> or a CSS
/// <c>background-image</c> — which is most of how a deliverable is embedded — external references are
/// not fetched at all, and our own renderer never fetches them either. So a data URI is the only form
/// that works everywhere, and the API should make it the easy thing to reach for. See
/// <c>polson://manual/14</c>.
/// </para>
/// </remarks>
public interface IDataUriSource
{
    /// <summary>The content as <c>data:&lt;mime&gt;;base64,…</c>, or an empty string if there is none.</summary>
    string ToDataUri();
}
