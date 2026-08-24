namespace Polson.MCPServer;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Jint;
using Jint.Native;
using Jint.Runtime;
using Polson.Drawing.Svg;

public class JsDrawingEngine
{
    #region Constructors
    public JsDrawingEngine(int timeoutSeconds = 15)
    {
        TimeoutSeconds = timeoutSeconds;
    }
    #endregion

    #region Properties
    public int TimeoutSeconds { get; set; }
    #endregion

    #region Methods
    public DrawingExecutionResult Execute(string jsScript, int defaultWidth = 800, int defaultHeight = 600)
    {
        ArgumentNullException.ThrowIfNull(jsScript);

        var result = new DrawingExecutionResult();
        var sw = Stopwatch.StartNew();
        var papers = new List<SnapPaper>();

        try
        {
            var engine = new Engine(options =>
            {
                options.TimeoutInterval(TimeSpan.FromSeconds(TimeoutSeconds));
                options.LimitRecursion(100);
                options.MaxStatements(500_000);
                options.AllowClr();
            });

            // Console bindings
            engine.SetValue("__raw_log", new Action<object?>(args =>
            {
                var msg = FormatLogArg(args);
                result.Logs.Add("[LOG] " + msg);
                Runtime.Info("[JS LOG] {0}", msg);
            }));
            engine.SetValue("__raw_info", new Action<object?>(args =>
            {
                var msg = FormatLogArg(args);
                result.Logs.Add("[INFO] " + msg);
                Runtime.Info("[JS INFO] {0}", msg);
            }));
            engine.SetValue("__raw_warn", new Action<object?>(args =>
            {
                var msg = FormatLogArg(args);
                result.Logs.Add("[WARN] " + msg);
                Runtime.Warn("[JS WARN] {0}", msg);
            }));
            engine.SetValue("__raw_error", new Action<object?>(args =>
            {
                var msg = FormatLogArg(args);
                result.Logs.Add("[ERROR] " + msg);
                Runtime.Error("[JS ERROR] {0}", msg);
            }));

            engine.Execute(@"
                var console = {
                    log: function() { __raw_log(Array.prototype.slice.call(arguments)); },
                    info: function() { __raw_info(Array.prototype.slice.call(arguments)); },
                    warn: function() { __raw_warn(Array.prototype.slice.call(arguments)); },
                    error: function() { __raw_error(Array.prototype.slice.call(arguments)); }
                };
            ");

            // Snap factory function
            Func<object?, object?, SnapPaper> snapFactory = (w, h) =>
            {
                var paper = Snap.Create(w ?? defaultWidth, h ?? defaultHeight);
                papers.Add(paper);
                return paper;
            };

            engine.SetValue("Snap", snapFactory);

            // Snap static helpers
            engine.Execute(@"
                Snap.version = '0.5.1';
                Snap.matrix = function(a, b, c, d, e, f) { return new Polson.Drawing.Svg.SnapMatrix(a, b, c, d, e, f); };
                Snap.path = {
                    getTotalLength: function(d) { return Polson.Drawing.Svg.SnapPathMeasurement.GetTotalLength(d); },
                    getPointAtLength: function(d, len) { return Polson.Drawing.Svg.SnapPathMeasurement.GetPointAtLength(d, len); },
                    getBBox: function(d) { return Polson.Drawing.Svg.SnapPathMeasurement.GetBBox(d); }
                };
                Snap.rgb = function(r, g, b, a) { return Polson.Drawing.Svg.Snap.Rgb(r, g, b, a == null ? 1 : a); };
                Snap.hsl = function(h, s, l, a) { return Polson.Drawing.Svg.Snap.Hsl(h, s, l, a == null ? 1 : a); };
                Snap.format = function(template, args) { return Polson.Drawing.Svg.Snap.Format(template, args); };
                Snap.parse = function(svg) { return Polson.Drawing.Svg.Snap.Parse(svg); };
                Snap.rad = function(deg) { return Polson.Drawing.Svg.Snap.Rad(deg); };
                Snap.deg = function(rad) { return Polson.Drawing.Svg.Snap.Deg(rad); };
                Snap.angle = function(x1, y1, x2, y2) { return Polson.Drawing.Svg.Snap.Angle(x1, y1, x2, y2); };

                var mina = {
                    linear: function(n) { return n; },
                    easeout: function(n) { return Math.sin(n * Math.PI / 2); },
                    easein: function(n) { return 1 - Math.cos(n * Math.PI / 2); },
                    easeinout: function(n) { return .5 * (1 - Math.cos(Math.PI * n)); },
                    bounce: function(n) {
                        var s = 7.5625, p = 2.75, l;
                        if (n < (1 / p)) { l = s * n * n; }
                        else if (n < (2 / p)) { n -= (1.5 / p); l = s * n * n + .75; }
                        else if (n < (2.5 / p)) { n -= (2.25 / p); l = s * n * n + .9375; }
                        else { n -= (2.625 / p); l = s * n * n + .984375; }
                        return l;
                    },
                    elastic: function(n) {
                        if (n == 0 || n == 1) return n;
                        var p = .3, s = p / 4;
                        return Math.pow(2, -10 * n) * Math.sin((n - s) * (2 * Math.PI) / p) + 1;
                    }
                };
            ");

            var evalResult = engine.Evaluate(jsScript);
            sw.Stop();

            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = true;

            SnapPaper? finalPaper = null;
            if (evalResult.ToObject() is SnapPaper p)
            {
                finalPaper = p;
                result.ReturnValue = p;
            }
            else if (evalResult.ToObject() is SnapElement el && el.Paper != null)
            {
                finalPaper = el.Paper;
                result.ReturnValue = el;
            }
            else if (papers.Count > 0)
            {
                finalPaper = papers.Last();
                result.ReturnValue = evalResult.ToObject();
            }

            if (finalPaper != null)
            {
                result.SvgXml = finalPaper.ToString();
                result.PngBytes = finalPaper.ToPngBytes(defaultWidth, defaultHeight);
            }
            else
            {
                result.ReturnValue = evalResult.ToObject();
            }
        }
        catch (JavaScriptException jsex)
        {
            sw.Stop();
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = false;
            result.Error = $"JavaScript error: {jsex.Message}";
            Runtime.Error("JavaScript execution error: {0}", result.Error);
        }
        catch (Exception ex)
        {
            sw.Stop();
            result.ExecutionTimeMs = sw.ElapsedMilliseconds;
            result.Success = false;
            result.Error = ex.Message;
            Runtime.Error(ex, "Script execution error: {0}", ex.Message);
        }

        return result;
    }

    private static string FormatLogArg(object? arg)
    {
        if (arg is null) return string.Empty;
        if (arg is object[] arr) return string.Join(" ", arr.Select(a => a?.ToString() ?? "null"));
        if (arg is System.Collections.IEnumerable enumerable && arg is not string)
        {
            var items = new List<string>();
            foreach (var item in enumerable)
            {
                items.Add(item?.ToString() ?? "null");
            }
            return string.Join(" ", items);
        }
        return arg.ToString() ?? string.Empty;
    }
    #endregion
}

