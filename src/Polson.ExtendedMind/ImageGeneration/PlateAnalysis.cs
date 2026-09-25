namespace Polson.ExtendedMind.ImageGeneration;

using System.Collections.Generic;

using SkiaSharp;

/// <summary>
/// Measurement and repair of generated imagery. Pure functions over bitmaps; no network, no state.
/// </summary>
/// <remarks>
/// Everything here exists because a generated asset cannot be taken at its word. The model does not
/// honour "seamless", does not honour a pixel size, and inpaints when asked to fill a frame — and
/// each of those failures is silent and plausible. So every property the requisition API promises is
/// one this class verifies, and where possible repairs, rather than one the prompt requested.
/// </remarks>
public static class PlateAnalysis
{
    #region Methods
    /// <summary>Rec. 709 luminance.</summary>
    public static double Luminance(SKColor c) => 0.2126 * c.Red + 0.7152 * c.Green + 0.0722 * c.Blue;

    /// <summary>Luminance as the 0-255 level it belongs in.</summary>
    /// <remarks>
    /// <b>Rounds rather than truncates, and the difference is not cosmetic.</b> The three coefficients
    /// sum to 1 in decimal and to slightly under 1 in binary, so a neutral grey lands just below its
    /// own value — <c>rgb(20,20,20)</c> computes to 19.999999999999996 and truncates to <b>19</b>.
    /// Harmless where luminance is compared against a coarse cutoff, and not harmless here: the
    /// histogram and the cut must agree exactly, and a reader checking either against a known input
    /// finds it off by one for no reason they can see.
    /// </remarks>
    public static byte LuminanceByte(SKColor c) => (byte)Math.Clamp(Math.Round(Luminance(c)), 0, 255);

    /// <summary>
    /// Measures whether a swatch wraps, by rolling it half a frame and testing the resulting mid-frame
    /// seam as an outlier against the ordinary neighbour-step distribution.
    /// </summary>
    /// <remarks>
    /// Comparing the seam against "unrelated" pixels does not work: on a uniform texture, unrelated
    /// columns differ as much as adjacent ones, so the test has no discriminating power and passes
    /// everything. Comparing against the distribution of real neighbour steps does work — a genuine
    /// discontinuity exceeds the largest legitimate step in the image, often by a factor of two, even
    /// when the motif's own linear features make it look intentional to the eye.
    /// </remarks>
    public static TilingMetrics MeasureTiling(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var rolled = Roll(bitmap, bitmap.Width / 2, bitmap.Height / 2);
        int w = rolled.Width, h = rolled.Height, mx = w / 2, my = h / 2;

        var vSeam = ColumnStep(rolled, mx - 1, mx);
        var hSeam = RowStep(rolled, my - 1, my);

        // The baseline must exclude the seam itself. Sampling straight across would sometimes include
        // the seam column, making NeighbourMax equal the seam step and passing every image — the
        // stride only misses it by luck, which is exactly the sort of accident that makes a validator
        // look like it works.
        var neighbours = new List<double>();
        for (var x = 8; x < w - 8; x += 7)
        {
            if (Math.Abs(x - mx) <= SeamGuard)
            {
                continue;
            }

            neighbours.Add(ColumnStep(rolled, x, x + 1));
        }

        for (var y = 8; y < h - 8; y += 7)
        {
            if (Math.Abs(y - my) <= SeamGuard)
            {
                continue;
            }

            neighbours.Add(RowStep(rolled, y, y + 1));
        }

        neighbours.Sort();

        return new TilingMetrics
        {
            HorizontalSeamStep = hSeam,
            VerticalSeamStep = vSeam,
            NeighbourMedian = neighbours[neighbours.Count / 2],
            NeighbourMax = neighbours[^1],
        };
    }

    /// <summary>
    /// Makes a swatch wrap by cross-fading its leading edge with the content that follows its
    /// trailing edge, cropping by the overlap. Output is smaller than input by <paramref name="overlap"/>.
    /// </summary>
    public static SKBitmap MakeTileable(SKBitmap bitmap, double overlap = 0.125)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var m = Math.Max(2, (int)(Math.Min(bitmap.Width, bitmap.Height) * overlap));

        // Two independent passes. Blending both axes in one pass has to sample a source pixel that
        // is itself mid-blend, which leaves the horizontal seam unrepaired.
        var horizontal = BlendAxis(bitmap, m, horizontally: true);
        var output = BlendAxis(horizontal, m, horizontally: false);
        horizontal.Dispose();
        return output;
    }

    /// <summary>
    /// Measures a backdrop so the foreground can read it rather than guess at it.
    /// </summary>
    /// <param name="blocking">
    /// The blocking sent as <c>conditionOn</c>, if any. Baked-mask detection needs it: a plate can be
    /// legitimately dark over most of its area — an unconditioned "keep the lower third black" plate
    /// measured 18% pure black — so neither the black fraction nor the sharpness of the black
    /// boundary separates a mask from a night sky. What does separate them is agreement with the
    /// silhouette actually sent: 99% overlap for an inpainted plate against 61% for an unrelated one.
    /// </param>
    public static PlateMetrics MeasurePlate(SKBitmap plate, QuietRegion requested, SKBitmap? blocking = null)
    {
        ArgumentNullException.ThrowIfNull(plate);

        int w = plate.Width, h = plate.Height;
        var bands = new double[3];

        for (var band = 0; band < 3; band++)
        {
            double sum = 0;
            var n = 0;
            for (var y = band * h / 3; y < (band + 1) * h / 3; y += 2)
            {
                for (var x = 0; x < w; x += 2)
                {
                    sum += Luminance(plate.GetPixel(x, y));
                    n++;
                }
            }

            bands[band] = n == 0 ? 0 : sum / n;
        }

        double wx = 0, wy = 0, ws = 0;
        for (var y = 0; y < h; y += 2)
        {
            for (var x = 0; x < w; x += 2)
            {
                var l = Luminance(plate.GetPixel(x, y));
                if (l <= BrightThreshold)
                {
                    continue;
                }

                wx += x * l;
                wy += y * l;
                ws += l;
            }
        }

        var darkest = Array.IndexOf(bands, bands.Min());

        return new PlateMetrics
        {
            KeyLightX = ws > 0 ? wx / ws / w : 0.5,
            KeyLightY = ws > 0 ? wy / ws / h : 0.5,
            BandLuminance = bands,
            QuietX = 0,
            QuietY = darkest / 3.0,
            QuietWidth = 1,
            QuietHeight = 1.0 / 3,
            QuietRegionHonoured = IsQuietHonoured(requested, bands),
            HasBakedMask = blocking is not null && MaskAgreement(plate, blocking) >= BakedMaskIoU,
        };
    }

    /// <summary>
    /// Why an image is not a blocking, or null when it is one: black silhouettes on a single flat grey.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A blocking is the one image the studio sends a model that it did not generate</b>, so it is
    /// also the one route by which a photograph or a drawn face could reach the model as something to
    /// build around. A silhouette carries no likeness, which is the line a matte already draws, so the
    /// test is on tone: every pixel near black, or near the one neutral tone the ground is painted in.
    /// A photograph fails by colour and by tonal spread; a greyscale one by the spread alone.
    /// </para>
    /// <para>
    /// The allowance for other tones is for antialiased edges, which fall between the two. Sampled at
    /// 280 across, a skyline puts about 2% of its samples on an edge; 8% leaves room for a lacier
    /// silhouette such as a tree line without letting a picture through.
    /// </para>
    /// </remarks>
    public static string? BlockingProblem(SKBitmap blocking, int sample = 280)
    {
        ArgumentNullException.ThrowIfNull(blocking);

        const double MaxOther = 0.08;
        var h = Math.Max(1, sample * blocking.Height / Math.Max(1, blocking.Width));
        var samples = new List<(double Luminance, int Chroma)>(sample * h);

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < sample; x++)
            {
                var c = blocking.GetPixel(x * blocking.Width / sample, y * blocking.Height / h);

                // Transparency is ground: nothing is drawn there, so nothing is asked of the model.
                samples.Add(c.Alpha < 128
                    ? (-1, 0)
                    : (Luminance(c), Math.Max(c.Red, Math.Max(c.Green, c.Blue)) - Math.Min(c.Red, Math.Min(c.Green, c.Blue))));
            }
        }

        static bool Black((double Luminance, int Chroma) s) => s.Luminance is >= 0 and < 30 && s.Chroma < 40;

        // The ground is whatever neutral tone most of the non-silhouette is painted in.
        var neutral = samples.Where(s => s.Luminance >= 0 && !Black(s) && s.Chroma <= 24).ToList();
        var ground = neutral.Count == 0 ? -1
            : neutral.GroupBy(s => (int)Math.Round(s.Luminance / 4)).MaxBy(g => g.Count())!.Key * 4;

        var other = samples.Count(s => s.Luminance >= 0 && !Black(s)
                                       && !(s.Chroma <= 24 && Math.Abs(s.Luminance - ground) <= 12));
        var share = (double)other / samples.Count;

        return share <= MaxOther ? null
            : $"{share:P0} of it is neither silhouette black nor the flat ground tone"
              + (samples.Any(s => s.Chroma > 40) ? ", and it is in colour" : string.Empty);
    }

    /// <summary>
    /// Intersection-over-union of the plate's near-black region with the blocking's silhouette.
    /// </summary>
    public static double MaskAgreement(SKBitmap plate, SKBitmap blocking, int sample = 280)
    {
        ArgumentNullException.ThrowIfNull(plate);
        ArgumentNullException.ThrowIfNull(blocking);

        var h = Math.Max(1, sample * plate.Height / Math.Max(1, plate.Width));
        long intersection = 0, union = 0;

        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < sample; x++)
            {
                var inPlate = Luminance(plate.GetPixel(x * plate.Width / sample, y * plate.Height / h)) < 6;
                var inBlocking = Luminance(blocking.GetPixel(x * blocking.Width / sample, y * blocking.Height / h)) < 30;

                if (inPlate && inBlocking)
                {
                    intersection++;
                }

                if (inPlate || inBlocking)
                {
                    union++;
                }
            }
        }

        return union == 0 ? 0 : (double)intersection / union;
    }

    /// <summary>
    /// Grows the surrounding plate inward over a baked mask, so small silhouette edits do not expose
    /// black fringes. The filled area sits under the foreground; it only has to survive being clipped.
    /// </summary>
    public static SKBitmap ExtendIntoMask(SKBitmap plate, int margin = 24)
    {
        ArgumentNullException.ThrowIfNull(plate);

        var output = plate.Copy();
        for (var pass = 0; pass < margin; pass++)
        {
            var changed = false;
            for (var y = 0; y < output.Height; y++)
            {
                for (var x = 0; x < output.Width; x++)
                {
                    if (Luminance(output.GetPixel(x, y)) >= 4)
                    {
                        continue;
                    }

                    foreach (var (dx, dy) in Neighbours)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= output.Width || ny >= output.Height)
                        {
                            continue;
                        }

                        var c = output.GetPixel(nx, ny);
                        if (Luminance(c) < 4)
                        {
                            continue;
                        }

                        output.SetPixel(x, y, c);
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
            {
                break;
            }
        }

        return output;
    }

    /// <summary>Resamples to a square edge length.</summary>
    public static SKBitmap Resize(SKBitmap bitmap, int size) => Resize(bitmap, size, size);

    public static SKBitmap Resize(SKBitmap bitmap, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        return bitmap.Resize(new SKImageInfo(width, height), Sampling) ?? bitmap.Copy();
    }

    /// <summary>
    /// Fits to a target by cover-cropping, never by stretching.
    /// </summary>
    /// <remarks>
    /// Aspect is approximate upstream: a 16:9 request came back 1344x768, a ratio of 1.75 rather than
    /// 1.778. Stretching that to a canvas would skew every feature in the plate.
    /// </remarks>
    public static SKBitmap FitTo(SKBitmap bitmap, int width, int height)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var scale = Math.Max((double)width / bitmap.Width, (double)height / bitmap.Height);
        int sw = (int)Math.Ceiling(bitmap.Width * scale), sh = (int)Math.Ceiling(bitmap.Height * scale);

        using var scaled = Resize(bitmap, sw, sh);
        var output = new SKBitmap(width, height);
        using var canvas = new SKCanvas(output);
        canvas.DrawBitmap(scaled, (width - sw) / 2f, (height - sh) / 2f, Sampling, null);
        return output;
    }

    /// <summary>Converts to a single-channel-looking grey bitmap for use as a matte.</summary>
    /// <param name="threshold">
    /// Cut level in 0-255. Null keeps the luminance ramp; a value cuts every pixel to pure black or
    /// pure white, which is what a stencil needs and what a ramp cannot supply.
    /// </param>
    /// <remarks>
    /// <b>Inversion is applied after the cut, not before.</b> Doing it the other way round means the
    /// cut is tested against a level measured on the un-inverted image, so an inverted stencil comes
    /// back cut at <c>255 - threshold</c> — a plausible-looking picture with the wrong coverage.
    /// </remarks>
    public static SKBitmap ToMatte(SKBitmap bitmap, bool invert = false, int? threshold = null)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var cut = threshold is null ? -1 : Math.Clamp(threshold.Value, 0, 255);
        var output = new SKBitmap(bitmap.Width, bitmap.Height);
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                var v = LuminanceByte(bitmap.GetPixel(x, y));
                if (cut >= 0)
                {
                    v = v > cut ? (byte)255 : (byte)0;
                }

                if (invert)
                {
                    v = (byte)(255 - v);
                }

                output.SetPixel(x, y, new SKColor(v, v, v));
            }
        }

        return output;
    }

    /// <summary>
    /// Picks a cut level from the image's own luminance histogram (Otsu's method).
    /// </summary>
    /// <remarks>
    /// <b>A fixed 128 is the wrong default for a generated stencil, which is why this exists.</b> The
    /// model is asked for pure white on pure black and does not deliver it: its blacks land anywhere
    /// from 20 to 90 and its whites from 170 to 250, varying per generation and per subject. Cutting
    /// a plate whose range is 35-160 at 128 keeps a sliver of the subject and reports a stencil that
    /// is 4% white — a silent, plausible failure, and exactly the class this project treats as worst.
    /// Choosing the level that best separates the two populations tracks whatever the model returned.
    /// </remarks>
    public static int OtsuThreshold(SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var histogram = new long[256];
        for (var y = 0; y < bitmap.Height; y++)
        {
            for (var x = 0; x < bitmap.Width; x++)
            {
                histogram[LuminanceByte(bitmap.GetPixel(x, y))]++;
            }
        }

        long total = (long)bitmap.Width * bitmap.Height;
        double sumAll = 0;
        for (var i = 0; i < 256; i++)
        {
            sumAll += (double)i * histogram[i];
        }

        double sumBelow = 0, best = -1;
        long countBelow = 0;
        var level = 127;

        for (var i = 0; i < 256; i++)
        {
            countBelow += histogram[i];
            if (countBelow == 0)
            {
                continue;
            }

            var countAbove = total - countBelow;
            if (countAbove == 0)
            {
                break;
            }

            sumBelow += (double)i * histogram[i];
            var meanBelow = sumBelow / countBelow;
            var meanAbove = (sumAll - sumBelow) / countAbove;
            var between = (double)countBelow * countAbove * (meanBelow - meanAbove) * (meanBelow - meanAbove);

            if (between > best)
            {
                best = between;
                level = i;
            }
        }

        return level;
    }

    /// <summary>Share of the matte that is "on", as a fraction of the frame.</summary>
    /// <remarks>
    /// The readback that makes a threshold checkable. A stencil that came back 2% or 98% white is a
    /// failed generation that still decodes, still encodes, and still draws — so without a measured
    /// coverage the caller learns nothing until a human looks at the artifact.
    /// </remarks>
    public static double Coverage(SKBitmap matte)
    {
        ArgumentNullException.ThrowIfNull(matte);

        double on = 0;
        for (var y = 0; y < matte.Height; y++)
        {
            for (var x = 0; x < matte.Width; x++)
            {
                on += matte.GetPixel(x, y).Red / 255.0;
            }
        }

        return on / ((double)matte.Width * matte.Height);
    }

    /// <summary>Encodes to the delivery format. WebP q85 is ~10x smaller than the PNG the service returns.</summary>
    #region Cutout

    /// <summary>
    /// The plate's background colour, measured from its own corners rather than assumed.
    /// </summary>
    /// <remarks>
    /// The same discipline <see cref="OtsuThreshold"/> applies to a stencil, and for the same reason:
    /// a model asked for pure magenta does not deliver pure magenta, and keying on the literal value
    /// leaves a fringe of near-misses standing all round the subject. Per-channel median over four
    /// corner patches, so a stray dark pixel in one corner cannot move the answer.
    /// </remarks>
    public static SKColor SampleBackground(SKBitmap plate, int patch = 12)
    {
        var size = Math.Max(1, Math.Min(patch, Math.Min(plate.Width, plate.Height) / 4));
        List<byte> r = [], g = [], b = [];

        foreach (var (ox, oy) in new[]
                 {
                     (0, 0), (plate.Width - size, 0),
                     (0, plate.Height - size), (plate.Width - size, plate.Height - size),
                 })
        {
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    var c = plate.GetPixel(ox + x, oy + y);
                    r.Add(c.Red);
                    g.Add(c.Green);
                    b.Add(c.Blue);
                }
            }
        }

        return new SKColor(Median(r), Median(g), Median(b));

        static byte Median(List<byte> values)
        {
            values.Sort();
            return values.Count == 0 ? (byte)0 : values[values.Count / 2];
        }
    }

    /// <summary>
    /// Replaces a flat background with transparency, keeping the subject's interior intact.
    /// </summary>
    /// <remarks>
    /// <b>This is what separates a cutout from a matte.</b> A matte thresholds on <i>luminance</i>, so
    /// everything inside the subject collapses to one value — which is the point there and fatal here,
    /// because a face is nothing but interior. Keying on distance from a background <i>colour</i>
    /// leaves every interior value untouched and removes only what matches the ground.
    /// <para>
    /// The band between <paramref name="tolerance"/> and twice it is ramped rather than cut, so the
    /// antialiased rim of the subject keeps a partial alpha instead of a staircase. A hard cut here
    /// is visible the moment the cutout is composited over anything that is not the colour it was
    /// generated on.
    /// </para>
    /// </remarks>
    public static SKBitmap ChromaKey(SKBitmap source, SKColor background, double tolerance = 0.18)
    {
        var keyed = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        var near = Math.Clamp(tolerance, 0.01, 0.9) * 441.673;      // sqrt(3) * 255
        var far = near * 2.0;

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var c = source.GetPixel(x, y);
                double dr = c.Red - background.Red, dg = c.Green - background.Green, db = c.Blue - background.Blue;
                var d = Math.Sqrt((dr * dr) + (dg * dg) + (db * db));

                var alpha = d <= near ? 0.0 : d >= far ? 1.0 : (d - near) / (far - near);
                keyed.SetPixel(x, y, new SKColor(c.Red, c.Green, c.Blue, (byte)Math.Round(alpha * 255)));
            }
        }

        return keyed;
    }

    /// <summary>
    /// Keys out a ground by how far each pixel leans toward the ground's hue, not by how close it is to
    /// the ground's colour. Null when the ground does not lean that way enough to key on.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why not <see cref="ChromaKey"/>.</b> The model draws its ground approximately and always toward
    /// the middle: asked for <c>#FF00FF</c> it drew <c>#D34090</c>, asked for <c>#00FF00</c> about
    /// <c>#74B060</c>. A distance key around a desaturated ground reaches whatever sits in the middle too,
    /// and measured on live head sheets at tolerance 0.10 that was ruddy cheeks on magenta and a grey
    /// beard on green. A lean key cannot do that: grey leans toward no hue at all, and skin leans red,
    /// which is away from both green and the blue half of magenta.
    /// </para>
    /// <para>
    /// The lean is <c>g − max(r, b)</c> for green, <c>min(r, b) − g</c> for magenta and
    /// <c>b − max(r, g)</c> for blue, taken as a share of the measured ground's own. A pixel at 60% of it
    /// or more is ground; at 30% or less it is subject; between the two it ramps, which keeps an
    /// antialiased edge soft. <paramref name="tolerance"/> moves both, 0.10 giving exactly those.
    /// </para>
    /// </remarks>
    public static SKBitmap? DifferenceKey(SKBitmap source, SKColor ground, string hue, double tolerance = 0.10)
    {
        Func<int, int, int, int> lean = hue.ToLowerInvariant() switch
        {
            "green" => (r, g, b) => g - Math.Max(r, b),
            "magenta" => (r, g, b) => Math.Min(r, b) - g,
            "blue" => (r, g, b) => b - Math.Max(r, g),
            _ => throw new ArgumentException($"No lean is defined for a '{hue}' ground.", nameof(hue)),
        };

        // A ground that barely leans is not the hue it was asked for, and keying on it would take the subject.
        double full = lean(ground.Red, ground.Green, ground.Blue);
        if (full < 16)
        {
            return null;
        }

        var groundAt = Math.Clamp(0.7 - tolerance, 0.3, 0.9);
        var subjectAt = groundAt / 2;
        var keyed = new SKBitmap(source.Width, source.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        for (var y = 0; y < source.Height; y++)
        {
            for (var x = 0; x < source.Width; x++)
            {
                var c = source.GetPixel(x, y);
                var t = lean(c.Red, c.Green, c.Blue) / full;
                var alpha = t >= groundAt ? 0.0 : t <= subjectAt ? 1.0 : (groundAt - t) / (groundAt - subjectAt);
                keyed.SetPixel(x, y, new SKColor(c.Red, c.Green, c.Blue, (byte)Math.Round(alpha * 255)));
            }
        }

        return keyed;
    }

    /// <summary>Share of the frame carrying any opacity at all, 0 to 1.</summary>
    public static double AlphaCoverage(SKBitmap keyed)
    {
        if (keyed.Width == 0 || keyed.Height == 0) return 0;

        var on = 0L;
        for (var y = 0; y < keyed.Height; y++)
        {
            for (var x = 0; x < keyed.Width; x++)
            {
                if (keyed.GetPixel(x, y).Alpha > 16) on++;
            }
        }

        return on / (double)(keyed.Width * (long)keyed.Height);
    }

    /// <summary>
    /// Share of the frame that is transparent but <i>enclosed</i> by the subject, 0 to 1.
    /// </summary>
    /// <remarks>
    /// <b>The hole detector, and the measurement <see cref="AlphaCoverage"/> cannot make.</b> Coverage
    /// is a whole-cell statistic, so a subject whose face has been keyed away still measures healthy —
    /// a face is around 2% of a standing figure, comfortably inside the noise. Measured on a live run:
    /// a cell at 68% coverage carried a face averaging 33/255 alpha, and four passes were spent
    /// lighting a hole.
    /// <para>
    /// Enclosure is what separates the two, and it needs no threshold to be chosen: the ground is
    /// whatever transparency is reachable from the border, so anything transparent that is *not*
    /// reachable is surrounded by subject and was therefore taken out of it. A clean cutout measures
    /// near zero. A pixel counts as transparent below half alpha, because the key ramps rather than
    /// cuts and a half-removed face is already lost.
    /// </para>
    /// </remarks>
    public static double EnclosedTransparency(SKBitmap keyed)
    {
        int w = keyed.Width, h = keyed.Height;
        if (w == 0 || h == 0) return 0;

        var open = new bool[w * h];
        var stack = new Stack<int>();

        void Seed(int x, int y)
        {
            var i = (y * w) + x;
            if (open[i] || keyed.GetPixel(x, y).Alpha >= 128) return;
            open[i] = true;
            stack.Push(i);
        }

        for (var x = 0; x < w; x++) { Seed(x, 0); Seed(x, h - 1); }
        for (var y = 0; y < h; y++) { Seed(0, y); Seed(w - 1, y); }

        while (stack.Count > 0)
        {
            var i = stack.Pop();
            int cx = i % w, cy = i / w;
            if (cx > 0) Seed(cx - 1, cy);
            if (cx < w - 1) Seed(cx + 1, cy);
            if (cy > 0) Seed(cx, cy - 1);
            if (cy < h - 1) Seed(cx, cy + 1);
        }

        var enclosed = 0L;
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                if (keyed.GetPixel(x, y).Alpha < 128 && !open[(y * w) + x]) enclosed++;
            }
        }

        return enclosed / (double)(w * (long)h);
    }

    /// <summary>Per-column share of opaque pixels: the profile a row of figures is split on.</summary>
    public static double[] AlphaColumnProfile(SKBitmap keyed)
    {
        var profile = new double[keyed.Width];
        for (var x = 0; x < keyed.Width; x++)
        {
            var on = 0;
            for (var y = 0; y < keyed.Height; y++)
            {
                if (keyed.GetPixel(x, y).Alpha > 16) on++;
            }

            profile[x] = keyed.Height == 0 ? 0 : on / (double)keyed.Height;
        }

        return profile;
    }

    /// <summary>
    /// Runs of occupied columns, separated by empty ones. Empty when nothing is occupied.
    /// </summary>
    /// <remarks>
    /// <b>Why gaps rather than an even division into n.</b> A model asked for four figures in a row
    /// does not place them on a grid, so slicing the sheet into quarters cuts through shoulders. The
    /// gaps are where the background actually is, and finding them costs one pass. The caller checks
    /// the count it got against the count it asked for and falls back rather than trusting this.
    /// </remarks>
    public static (int Start, int End)[] SegmentsByGaps(double[] profile, double floor = 0.004, int minWidth = 8)
    {
        List<(int, int)> runs = [];
        int? start = null;

        for (var x = 0; x < profile.Length; x++)
        {
            if (profile[x] > floor)
            {
                start ??= x;
            }
            else if (start is { } from)
            {
                if (x - from >= minWidth) runs.Add((from, x - 1));
                start = null;
            }
        }

        if (start is { } last && profile.Length - last >= minWidth) runs.Add((last, profile.Length - 1));
        return [.. runs];
    }

    /// <summary>The bitmap cropped to its own opaque extent, or one transparent pixel if it has none.</summary>
    public static SKBitmap TrimToAlpha(SKBitmap keyed, int margin = 0)
    {
        int minX = keyed.Width, minY = keyed.Height, maxX = -1, maxY = -1;

        for (var y = 0; y < keyed.Height; y++)
        {
            for (var x = 0; x < keyed.Width; x++)
            {
                if (keyed.GetPixel(x, y).Alpha <= 16) continue;
                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        if (maxX < 0) return new SKBitmap(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);

        minX = Math.Max(0, minX - margin);
        minY = Math.Max(0, minY - margin);
        maxX = Math.Min(keyed.Width - 1, maxX + margin);
        maxY = Math.Min(keyed.Height - 1, maxY + margin);

        var cropped = new SKBitmap(maxX - minX + 1, maxY - minY + 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
        using var canvas = new SKCanvas(cropped);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(keyed, new SKRect(minX, minY, maxX + 1, maxY + 1),
            new SKRect(0, 0, cropped.Width, cropped.Height), Sampling, null);
        return cropped;
    }

    #endregion

    public static byte[] Encode(SKBitmap bitmap, string format = "webp", int quality = 85)
    {
        ArgumentNullException.ThrowIfNull(bitmap);

        var encoded = format.ToLowerInvariant() switch
        {
            "png" => SKEncodedImageFormat.Png,
            "jpeg" or "jpg" => SKEncodedImageFormat.Jpeg,
            _ => SKEncodedImageFormat.Webp,
        };

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(encoded, Math.Clamp(quality, 1, 100));
        return data.ToArray();
    }

    public static string MimeFor(string format) => format.ToLowerInvariant() switch
    {
        "png" => "image/png",
        "jpeg" or "jpg" => "image/jpeg",
        _ => "image/webp",
    };

    static bool IsQuietHonoured(QuietRegion requested, double[] bands) => requested switch
    {
        QuietRegion.None => true,
        QuietRegion.LowerThird => bands[2] < QuietCeiling && bands[2] < bands[0],
        QuietRegion.UpperThird => bands[0] < QuietCeiling && bands[0] < bands[2],
        _ => true,
    };

    /// <summary>
    /// Crops one axis by <paramref name="m"/> and cross-fades the leading strip with the content
    /// that follows the new trailing edge, so the axis wraps continuously.
    /// </summary>
    static SKBitmap BlendAxis(SKBitmap bitmap, int m, bool horizontally)
    {
        int ow = horizontally ? bitmap.Width - m : bitmap.Width;
        int oh = horizontally ? bitmap.Height : bitmap.Height - m;
        var output = new SKBitmap(ow, oh);

        for (var y = 0; y < oh; y++)
        {
            for (var x = 0; x < ow; x++)
            {
                var c = bitmap.GetPixel(x, y);
                var along = horizontally ? x : y;

                if (along < m)
                {
                    var beyond = horizontally
                        ? bitmap.GetPixel(x + ow, y)
                        : bitmap.GetPixel(x, y + oh);
                    c = Lerp(beyond, c, (double)along / m);
                }

                output.SetPixel(x, y, c);
            }
        }

        return output;
    }

    static SKBitmap Roll(SKBitmap bitmap, int dx, int dy)
    {
        int w = bitmap.Width, h = bitmap.Height;
        var output = new SKBitmap(w, h);
        for (var y = 0; y < h; y++)
        {
            for (var x = 0; x < w; x++)
            {
                output.SetPixel(x, y, bitmap.GetPixel((x + dx) % w, (y + dy) % h));
            }
        }

        return output;
    }

    static double ColumnStep(SKBitmap b, int xa, int xb)
    {
        double sum = 0;
        for (var y = 0; y < b.Height; y++)
        {
            sum += Delta(b.GetPixel(xa, y), b.GetPixel(xb, y));
        }

        return sum / b.Height;
    }

    static double RowStep(SKBitmap b, int ya, int yb)
    {
        double sum = 0;
        for (var x = 0; x < b.Width; x++)
        {
            sum += Delta(b.GetPixel(x, ya), b.GetPixel(x, yb));
        }

        return sum / b.Width;
    }

    static double Delta(SKColor p, SKColor q) =>
        (Math.Abs(p.Red - q.Red) + Math.Abs(p.Green - q.Green) + Math.Abs(p.Blue - q.Blue)) / 3.0;

    static SKColor Lerp(SKColor a, SKColor b, double t) => new(
        (byte)(a.Red + (b.Red - a.Red) * t),
        (byte)(a.Green + (b.Green - a.Green) * t),
        (byte)(a.Blue + (b.Blue - a.Blue) * t),
        (byte)(a.Alpha + (b.Alpha - a.Alpha) * t));
    #endregion

    #region Fields
    /// <summary>Overlap at or above which a plate's dark region is judged to be an inpaint mask.</summary>
    public const double BakedMaskIoU = 0.85;

    const double BrightThreshold = 120;
    const double QuietCeiling = 8;

    /// <summary>Columns/rows this close to the seam are excluded from the neighbour baseline.</summary>
    const int SeamGuard = 3;

    internal static readonly SKSamplingOptions Sampling = new(SKFilterMode.Linear, SKMipmapMode.Linear);

    static readonly (int Dx, int Dy)[] Neighbours = [(1, 0), (-1, 0), (0, 1), (0, -1)];
    #endregion
}
