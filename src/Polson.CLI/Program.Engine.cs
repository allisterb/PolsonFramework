namespace Polson.CLI;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommandLine;
using Polson.Animation;
using Polson.Drawing.Mesh;
using Polson.Drawing.Skia;
using Polson.Drawing.Svg;
using Polson.MCPServer;
using Spectre.Console;

/// <summary>The commands the studio's CLI shares with <c>Polson.Engine.CLI</c>: <c>eval</c>, and the local tools a server finds at startup.</summary>
/// <remarks>
/// Both projects compile this file, so the two command lines run one <c>eval</c> and find potrace, ffmpeg, the speech
/// dictionary, the detectors and Blender the same way. Nothing here names ExtendedMind; what <c>eval</c> turns on beyond
/// the engine is <see cref="ConfigureEvalExtras"/>, which the engine's CLI does not implement.
/// </remarks>
internal partial class Program
{
    #region Methods
    /// <summary>What <c>eval</c> turns on beyond the engine. Implemented in the studio's <c>Program.cs</c>.</summary>
    static partial void ConfigureEvalExtras();

    /// <summary>
    /// A configuration value, treating blank as absent.
    /// </summary>
    /// <remarks>
    /// <c>IConfiguration</c>'s indexer returns null for a missing key but an empty string for a
    /// present-and-empty one, so <c>??</c> does not catch the second. That matters because the
    /// shipped example carries <c>""</c> placeholders for a person to fill in: copied as-is it would
    /// have set the image model to the empty string and resolved the asset cache to nowhere, both
    /// silently.
    /// </remarks>
    static string? Setting(string key) =>
        config?[key] is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>
    /// Fails the process when the command line could not be parsed.
    /// </summary>
    /// <remarks>
    /// The parser has already written what went wrong; what was missing is the exit code. Without
    /// one, a mistyped flag and a successful run are indistinguishable to a script, and
    /// <c>create-project … || exit 1</c> never fires. Asking for help or the version is not a
    /// failure, so those keep exit 0.
    /// </remarks>
    static Task ReportParseFailure(IEnumerable<Error> errors)
    {
        var asked = errors.All(e => e is HelpRequestedError or HelpVerbRequestedError or VersionRequestedError);
        if (!asked) Environment.ExitCode = 1;

        return Task.CompletedTask;
    }

    /// <summary>Points <c>bitmap.trace(...)</c> at a tracer, and says which one it found.</summary>
    /// <remarks>
    /// <para>
    /// <c>Tools:Potrace</c> is an override, not a requirement: the tracer discovers a copy under
    /// <c>bin/</c> and then falls back to the PATH, which is where a Debian package lands. The setting
    /// exists for the case neither is true.
    /// </para>
    /// <para>
    /// Announced either way, because tracing is the one capability here that depends on something
    /// outside the build. A run that quietly could not trace looks identical to one that never tried,
    /// and the difference only surfaces as a deliverable full of base64.
    /// </para>
    /// </remarks>
    static void ConfigureTracing()
    {
        if (Setting("Tools:Potrace") is { Length: > 0 } configured)
            BitmapTracer.ExecutableOverride = configured;

        if (BitmapTracer.Executable is { } exe)
            Info("Tracing enabled ({0}).", exe);
        else
            Warn("Tracing unavailable: no 'potrace' found. bitmap.trace() will refuse, and a "
                 + "requisitioned matte can only reach an SVG as base64. Install it "
                 + "(Debian: apt-get install potrace) or set Tools:Potrace.");
    }

    /// <summary>Points <c>Character.retarget(...)</c> at a pose library, and says what it found.</summary>
    /// <remarks>
    /// Mesh2Motion's CC0 human clips sit next to the assembly in <c>Library/poses/</c>, fetched by <c>tools/bootstrap.py</c>. <c>Poses:Library</c>, or else a
    /// <c>models/poses/</c> folder above the assembly, adds clips ahead of them; a project's own <c>poses/</c> comes first.
    /// </remarks>
    static void ConfigurePoses()
    {
        if (Setting("Poses:Library") is { Length: > 0 } configured)
            PoseRetarget.LibraryOverride = configured;

        var folders = PoseRetarget.Folders(null);
        if (folders.Count > 0)
            Info("Pose library: {0}.", string.Join(", ", folders));
        else
            Warn("No pose library found; Character.retarget reads only a project's poses/ folder. "
                 + "Run tools/bootstrap.py and rebuild, or set Poses:Library.");
    }

    /// <summary>Stock bodies for <c>Character.stock</c>: the fetched <c>Library/stock/</c>, plus <c>Characters:Stock</c> or <c>models/stock/</c>.</summary>
    static void ConfigureStock()
    {
        if (Setting("Characters:Stock") is { Length: > 0 } configured)
            CharacterStock.LibraryOverride = configured;
        var bodies = CharacterStock.Names(null);
        if (bodies.Length > 0) Info("Stock bodies: {0}.", string.Join(", ", bodies));
        else Warn("No stock bodies found; Character.stock reads only a project's stock/ folder. Run tools/bootstrap.py and rebuild.");
    }

    /// <summary>Points <c>Motion.speech(...)</c> at its dictionary and its optional backend, and says what it found.</summary>
    /// <remarks>
    /// The dictionary is required and the backend is not: without the backend, a line with a word the dictionary lacks
    /// or a number in it is refused, naming them, and every other line works. Both are found beside the assembly or by
    /// walking up to the checkout; <c>Tools:Cmudict</c>, <c>Tools:SpeechPython</c>, <c>Tools:SpeechScript</c> and
    /// <c>Tools:SpeechData</c> override.
    /// </remarks>
    static void ConfigureSpeech()
    {
        if (Setting("Tools:Cmudict") is { Length: > 0 } dictionary)
            CmuDictionary.PathOverride = dictionary;
        if (Setting("Tools:SpeechPython") is { Length: > 0 } python)
            SpeechBackend.PythonOverride = python;
        if (Setting("Tools:SpeechScript") is { Length: > 0 } script)
            SpeechBackend.ScriptOverride = script;
        if (Setting("Tools:SpeechData") is { Length: > 0 } data)
            SpeechBackend.DataOverride = data;
        if (Setting("Tools:AlignScript") is { Length: > 0 } align)
            SpeechBackend.AlignScriptOverride = align;
        if (Setting("Tools:SpeechModels") is { Length: > 0 } models)
            SpeechBackend.ModelsOverride = models;

        if (!CmuDictionary.Available)
            Warn("Speech dictionary missing at {0}; Motion.speech will refuse. Run tools/bootstrap.py --only speech and rebuild.", CmuDictionary.Path);
        else if (SpeechBackend.Available)
            Info("Speech: dictionary and backend (unknown words guessed, numbers spelled out).");
        else
            Info("Speech: dictionary only; unknown words and numbers will be refused (no backend: {0}).", SpeechBackend.Missing);
        if (CmuDictionary.Available)
            Info(SpeechBackend.AlignAvailable ? "Speech: recordings can be aligned (Motion.speech audio)."
                : "Speech: no aligner, so Motion.speech will refuse audio (missing {0}).", SpeechBackend.AlignMissing ?? "");
    }

    /// <summary>Points <c>Motion.saveVideo</c> and <c>comp.saveVideo</c> at ffmpeg, and says which one it found.</summary>
    /// <remarks>
    /// Found in <c>bin/ffmpeg/</c> (<c>tools/bootstrap.py --only ffmpeg</c> on Windows), then on the PATH, where
    /// Debian's package lands; <c>Tools:FFmpeg</c> overrides. Announced either way, as tracing is: a run that could not
    /// write video should say so before an agent spends a scene finding out.
    /// </remarks>
    static void ConfigureVideo()
    {
        if (Setting("Tools:FFmpeg") is { Length: > 0 } configured)
            FFmpeg.ExecutableOverride = configured;

        if (FFmpeg.Executable is { } exe)
            Info("Video enabled ({0}).", exe);
        else
            Warn("Video unavailable: no 'ffmpeg' found, so Motion.saveVideo and comp.saveVideo will refuse. "
                 + "Run py tools/bootstrap.py --only ffmpeg (Windows), apt-get install ffmpeg (Debian), or set Tools:FFmpeg.");
    }

    /// <summary>Points <c>Face.detect(...)</c> at a backend, and says whether it found one.</summary>
    /// <remarks>
    /// <para>
    /// Three settings rather than one, because unlike potrace this is not a single binary: an
    /// interpreter, a script and a model bundle, each of which can be missing on its own. All three
    /// are overrides rather than requirements — the detector walks up from the assembly looking for
    /// <c>python-mediapipe/</c>, <c>src/vision/</c> and <c>models/</c>, which is the developer case.
    /// </para>
    /// <para>
    /// Announced either way, and the absence names what is missing. This capability is the difference
    /// between reading three landmarks off a photograph by eye and measuring them, so a run that
    /// quietly could not detect is worth distinguishing from one that never tried.
    /// </para>
    /// </remarks>
    static void ConfigureFaceDetection()
    {
        if (Setting("Tools:FacePython") is { Length: > 0 } python)
            FaceDetector.PythonOverride = python;
        if (Setting("Tools:FaceScript") is { Length: > 0 } script)
            FaceDetector.ScriptOverride = script;
        if (Setting("Tools:FaceModel") is { Length: > 0 } model)
            FaceDetector.ModelOverride = model;

        if (FaceDetector.Available)
            Info("Face detection enabled ({0}).", FaceDetector.Model);
        else
            Warn("Face detection unavailable: missing {0}. Face.detect() will refuse, and a mesh "
                 + "textured from a photograph needs its three landmarks supplied by hand. "
                 + "See src/vision/requirements.in.", FaceDetector.Missing);
    }

    /// <summary>Body detection for <c>Character.detect</c>: the face venv's interpreter, with the pose script and model.</summary>
    static void ConfigureBodyDetection()
    {
        if (Setting("Tools:PoseScript") is { Length: > 0 } script)
            BodyDetector.ScriptOverride = script;
        if (Setting("Tools:PoseModel") is { Length: > 0 } model)
            BodyDetector.ModelOverride = model;

        if (BodyDetector.Available)
            Info("Body detection enabled ({0}).", BodyDetector.Model);
        else
            Warn("Body detection unavailable: missing {0}. Character.detect() will refuse.", BodyDetector.Missing);
    }

    /// <summary>Blender, for props and for <c>Character.drape</c>: discovered under <c>bin/</c> unless the settings say otherwise.</summary>
    static void ConfigureBlender()
    {
        if (Setting("Tools:Blender") is { Length: > 0 } blender)
            BlenderDriver.BlenderOverride = blender;
        if (Setting("Tools:BlenderScript") is { Length: > 0 } script)
            BlenderDriver.ScriptOverride = script;
        if (Setting("Tools:BlenderClothScript") is { Length: > 0 } cloth)
            BlenderDriver.ClothScriptOverride = cloth;

        if (BlenderDriver.CanDrape)
            Info("Cloth enabled ({0}).", BlenderDriver.Blender);
        else
            Warn("Cloth unavailable: no {0}. Character.drape() will refuse; Character.canDrape says so.",
                 BlenderDriver.Blender is null ? "Blender under bin/ (or Tools:Blender)" : "src/blender/cloth_drape.py");
    }

    static Task HandleEvalArgs(EvalOptions opts)
    {
        // Whatever else this build turns on for eval: the studio's reference photography. Nothing in Polson.Engine.CLI.
        ConfigureEvalExtras();

        // The print page is read before anything runs, as ExecuteScript reads it: a size nobody can print
        // should cost nothing, and a missing one is a decision rather than a default to guess.
        (PrintSize Size, float BleedPt, float SafePt)? print = null;
        if (!string.IsNullOrWhiteSpace(opts.OutPdf))
        {
            try
            {
                print = PrintPdfRequest.Parse(opts.OutPdf, opts.PdfSize, opts.PdfBleed, opts.PdfSafeMargin, PrintPdfRequest.Cli);
            }
            catch (ArgumentException ex)
            {
                AnsiConsole.MarkupLine($"[bold red]error:[/] {Markup.Escape(ex.Message)}");
                Environment.ExitCode = 1;
                return Task.CompletedTask;
            }
        }

        var engine = new JsDrawingEngine();
        DrawingExecutionResult result;

        if (!string.IsNullOrWhiteSpace(opts.SvgIn))
        {
            // Rendering a file the run already produced, rather than executing anything. Without
            // this the only way to look at an artifact was to embed its markup into a script, which
            // is both awkward and a good way to corrupt the very thing being inspected.
            if (!File.Exists(opts.SvgIn))
            {
                AnsiConsole.MarkupLine($"[bold red]error:[/] {Markup.Escape($"No such SVG file: {opts.SvgIn}")}");
                return Task.CompletedTask;
            }

            var svgXml = File.ReadAllText(opts.SvgIn);
            result = new DrawingExecutionResult
            {
                Success = true,
                SvgXml = svgXml,
                ImageFormat = SkiaImageEncoder.NormalizeFormatName(opts.Format),
                ImageBytes = SvgRenderPipeline.RenderToImage(svgXml, opts.Width, opts.Height,
                    opts.Format, opts.Quality),
            };
        }
        else if (string.IsNullOrWhiteSpace(opts.ScriptFile))
        {
            AnsiConsole.MarkupLine("[bold red]error:[/] give a script file, or --svg-in to render an SVG.");
            return Task.CompletedTask;
        }
        else
        {
            var script = File.Exists(opts.ScriptFile)
                ? File.ReadAllText(opts.ScriptFile)
                : opts.ScriptFile;

            result = engine.Execute(script, opts.Width, opts.Height, null, opts.Format, opts.Quality);
        }

        AnsiConsole.MarkupLine($"[bold]Script Execution:[/] {(result.Success ? "[green]Success[/]" : "[red]Failed[/]")} ({result.ExecutionTimeMs}ms)");

        foreach (var log in result.Logs)
        {
            AnsiConsole.MarkupLine($"[grey]{Markup.Escape(log)}[/]");
        }

        if (!result.Success && !string.IsNullOrEmpty(result.Error))
        {
            AnsiConsole.MarkupLine($"[bold red]Error:[/] {Markup.Escape(result.Error)}");
        }

        if (result.ImageBytes != null && result.ImageBytes.Length > 0)
        {
            var ext = result.ImageFormat == "png" ? ".png" : (result.ImageFormat == "jpeg" ? ".jpg" : ".webp");
            var outPath = !string.IsNullOrWhiteSpace(opts.OutImg)
                ? Path.GetFullPath(opts.OutImg)
                : Path.GetFullPath($"output{ext}");

            File.WriteAllBytes(outPath, result.ImageBytes);
            AnsiConsole.MarkupLine($"[bold green]Rendered {result.ImageFormat.ToUpperInvariant()} saved:[/] {outPath} ({result.ImageBytes.Length:N0} bytes)");
        }

        // Rendering an SVG file already has the markup on disk, so echoing it back out under the
        // default name would drop an `output.svg` wherever the command was run for no gain. An
        // explicit --svg still writes, since that is a copy the caller asked for.
        var echoingInput = !string.IsNullOrWhiteSpace(opts.SvgIn) && IsDefaultOutSvg(opts.OutSvg);

        if (!echoingInput && !string.IsNullOrWhiteSpace(result.SvgXml) && !string.IsNullOrWhiteSpace(opts.OutSvg))
        {
            var svgPath = Path.GetFullPath(opts.OutSvg);
            File.WriteAllText(svgPath, result.SvgXml);
            AnsiConsole.MarkupLine($"[bold green]Rendered SVG saved:[/] {svgPath} ({result.SvgXml.Length:N0} chars)");
        }

        // What ExecuteScript warns on every paper, so `eval --svg-in` can audit a finished run's artifacts too.
        if (result.Success && (result.RenderedFromPaper || !string.IsNullOrWhiteSpace(opts.SvgIn)))
        {
            foreach (var warning in FontAudit.Check(result.SvgXml ?? ""))
                AnsiConsole.MarkupLine($"[yellow][[WARN]][/] {Markup.Escape(warning)}");
        }

        if (print is { } page && result.Success)
        {
            WritePrintPdf(opts.OutPdf!, page.Size, page.BleedPt, page.SafePt, result,
                fromFile: !string.IsNullOrWhiteSpace(opts.SvgIn));
        }

        if (!string.IsNullOrWhiteSpace(opts.OutSif)) WriteSif(opts.OutSif, result);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes <c>--pdf</c> and prints what <c>ExecuteScript</c>'s <c>outPdf</c> would have said, word for word.
    /// </summary>
    /// <remarks>
    /// Unlike the tool, a refusal here exits non-zero: <c>eval --pdf</c> is asked for one file, and a script calling
    /// it needs to know the file is not there. The warnings — bleed, resolution, margins, lines — do not: the PDF
    /// was written, and whether a warning matters is the reader's call.
    /// </remarks>
    /// <summary>
    /// Writes <c>--sif</c>: the last <c>Motion.composition</c> the script built, as Synfig's file. A run need not write it;
    /// this takes it from the script when someone wants the film in Synfig Studio. A refusal exits non-zero, as
    /// <c>--pdf</c>'s does: one file was asked for, and a caller needs to know it is not there.
    /// </summary>
    static void WriteSif(string outSif, DrawingExecutionResult result)
    {
        void Refuse(string message)
        {
            AnsiConsole.MarkupLine($"[bold red]--sif wrote nothing:[/] {Markup.Escape(message)}");
            Environment.ExitCode = 1;
        }

        // A script that failed may have built only half its film; writing that would hand someone a .sif that looks
        // finished. Said rather than skipped: the file was asked for, and silence would read as success.
        if (!result.Success)
        {
            Refuse("the script failed, so its film may be incomplete. Fix the error above and run it again. Outside a "
                + "run Stage.elapsedMinutes is null, so a script that reads it must guard the read.");
            return;
        }

        if (result.Composition is not { } comp)
        {
            Refuse("the script built no Motion.composition, so there is no film to write. --sif exports the last "
                + "composition a script makes; a canvas or a paper has no .sif.");
            return;
        }

        try
        {
            var path = Path.GetFullPath(outSif);
            var (bytes, pictures) = MotionSifExport.Write(comp, path);
            AnsiConsole.MarkupLine($"[bold green]Synfig file saved:[/] {Markup.Escape(path)} ({bytes:N0} bytes"
                + (pictures.Length > 0 ? $", {pictures.Length} picture(s) beside it" : "") + ")");
        }
        catch (InvalidOperationException ex)
        {
            Refuse(ex.Message);
        }
    }

    static void WritePrintPdf(string outPdf, PrintSize size, float bleedPt, float safePt, DrawingExecutionResult result, bool fromFile)
    {
        void Refuse(string message)
        {
            AnsiConsole.MarkupLine($"[bold red]error:[/] {Markup.Escape($"--pdf '{outPdf}' wrote nothing: {message}")}");
            Environment.ExitCode = 1;
        }

        // An SVG read from disk is a vector page by definition; a script must have returned its paper, or the
        // paper is only part of the picture — the same rule, and the same reason, as the tool's.
        if (string.IsNullOrWhiteSpace(result.SvgXml) || !(fromFile || result.RenderedFromPaper))
        {
            Refuse("the script did not return a Snap paper, so there is no vector page to print. Build the poster on "
                + "Snap(width, height), place raster material with paper.image(bitmap, x, y, w, h), and return the paper.");
            return;
        }

        PdfPrintReport report;
        try
        {
            report = SvgPdfWriter.Write(result.SvgXml, Path.GetFullPath(outPdf), size, bleedPt, safeMarginPt: safePt);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            Refuse(ex.Message);
            return;
        }

        AnsiConsole.MarkupLine($"[bold green]Print PDF saved:[/] {Markup.Escape(report.Path)}");
        AnsiConsole.MarkupLine($"[grey]{Markup.Escape(PrintPdfRequest.Summary(outPdf, report, safePt))}[/]");
        foreach (var warning in report.Warnings)
            AnsiConsole.MarkupLine($"[yellow][[WARN]][/] {Markup.Escape(warning)}");
    }

    /// <summary>Whether <c>--svg</c> is still at its default rather than having been asked for.</summary>
    static bool IsDefaultOutSvg(string? outSvg) =>
        string.Equals(outSvg, "output.svg", StringComparison.OrdinalIgnoreCase);
    #endregion
}
