// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System;
using System.Collections;
using System.Globalization;
using System.Linq;

/// <summary>
/// Numbers as a place writes them: <c>Locale.number(value, { locale, compact, currency, percent, decimals })</c>.
/// </summary>
/// <remarks>
/// Exposed to the JavaScript sandbox. Members follow .NET naming here; Jint resolves the JS camelCase spelling onto them,
/// so a script calling <c>Locale.number(...)</c> reaches <c>Number(...)</c>. The formats are Unicode CLDR's, held to ICU
/// by the tests (<see cref="LocaleNumbers"/>): 13,049.7 crore in Mumbai, 1305億 in Tokyo, 130,5 Mrd. $ in Berlin for the
/// same 130.5 billion dollars. The figure is unchanged; only how it is written follows the place, so a figure set this
/// way is still the document's figure.
/// </remarks>
public sealed class LocaleToolkit
{
    #region Properties
    /// <summary>The locales carried, as language-region tags: <c>en-IN</c>, <c>ja-JP</c>, <c>pt-BR</c>…</summary>
    public string[] Locales => [.. LocaleNumbers.Locales];
    #endregion

    #region Methods
    /// <summary>
    /// <paramref name="value"/> written as <c>locale</c> writes it. <c>compact</c> (<c>'short'</c> or <c>'long'</c>) uses the
    /// place's scale words; <c>currency</c> an ISO code; <c>percent</c> multiplies by 100; <c>decimals</c> is the most fraction
    /// digits shown.
    /// </summary>
    public string Number(double value, object? options = null)
    {
        var o = JsInterop.AsDict(options) ?? throw new ArgumentException("Locale.number needs options with a locale: Locale.number(130.5e9, { locale: 'en-IN', compact: 'long' }).");
        foreach (var key in o.Keys.Cast<object>().Select(k => k.ToString()!))
            if (key is not ("locale" or "compact" or "currency" or "percent" or "decimals"))
                throw new ArgumentException($"Locale.number's options are {{ locale, compact, currency, percent, decimals }}; '{key}' is not one.");

        var compact = o["compact"] switch
        {
            null or false => null,
            true => "short",
            string s => s,
            var other => throw new ArgumentException($"Locale.number's compact is 'short' or 'long', not {other}."),
        };
        int? decimals = o["decimals"] is { } d ? (int)Convert.ToDouble(d, CultureInfo.InvariantCulture) : null;
        return LocaleNumbers.Format(value, o["locale"] as string ?? "", compact, o["currency"] as string, o["percent"] is true, decimals);
    }
    #endregion
}
