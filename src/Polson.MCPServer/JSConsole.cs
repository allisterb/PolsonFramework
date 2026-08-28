namespace Polson.MCPServer;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class JsConsole
{
    #region Constructors
    public JsConsole(List<string>? logs = null)
    {
        _logs = logs ?? [];
    }
    #endregion

    #region Properties
    public IReadOnlyList<string> Logs => _logs;
    #endregion

    #region Methods
    public void log(params object?[] args)
    {
        var msg = FormatArgs(args);
        _logs.Add("[LOG] " + msg);
        Runtime.Info("[JS LOG] {0}", msg);
    }

    public void info(params object?[] args)
    {
        var msg = FormatArgs(args);
        _logs.Add("[INFO] " + msg);
        Runtime.Info("[JS INFO] {0}", msg);
    }

    public void warn(params object?[] args)
    {
        var msg = FormatArgs(args);
        _logs.Add("[WARN] " + msg);
        Runtime.Warn("[JS WARN] {0}", msg);
    }

    public void error(params object?[] args)
    {
        var msg = FormatArgs(args);
        _logs.Add("[ERROR] " + msg);
        Runtime.Error("[JS ERROR] {0}", msg);
    }

    public void debug(params object?[] args)
    {
        var msg = FormatArgs(args);
        _logs.Add("[DEBUG] " + msg);
        Runtime.Debug("[JS DEBUG] {0}", msg);
    }

    public void trace(params object?[] args)
    {
        var msg = FormatArgs(args);
        _logs.Add("[TRACE] " + msg);
        Runtime.Debug("[JS TRACE] {0}", msg);
    }

    public void clear() => _logs.Clear();

    public static string FormatArgs(params object?[] args)
    {
        if (args == null || args.Length == 0) return string.Empty;
        return string.Join(" ", args.Select(FormatArg));
    }

    private static string FormatArg(object? arg)
    {
        if (arg is null) return "null";
        if (arg is string s) return s;
        if (arg is IDictionary<string, object?> dict)
        {
            var pairs = dict.Select(kv => $"{kv.Key}: {FormatArg(kv.Value)}");
            return $"{{ {string.Join(", ", pairs)} }}";
        }
        if (arg is IEnumerable enumerable and not string)
        {
            var items = enumerable.Cast<object?>().Select(FormatArg);
            return $"[{string.Join(", ", items)}]";
        }
        return arg.ToString() ?? string.Empty;
    }
    #endregion

    #region Fields
    private readonly List<string> _logs;
    #endregion
}

