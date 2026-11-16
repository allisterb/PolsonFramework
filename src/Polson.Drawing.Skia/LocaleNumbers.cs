// SPDX-License-Identifier: MIT
namespace Polson.Drawing.Skia;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

/// <summary>
/// Numbers written as a place writes them: grouping, separators, compact scale words (lakh and crore, 万 and 億,
/// Mrd., Md), currency and percent placement, from Unicode CLDR data.
/// </summary>
/// <remarks>
/// <para>
/// <b>The data is CLDR's, not ours.</b> .NET has no compact notation and Jint no <c>Intl</c>, so <c>tools/cldr_gen.js</c>
/// reads Node's ICU and writes <c>Data/cldr-numbers.json</c>: per locale, the pattern each power of ten takes for the
/// plural categories <i>one</i> and <i>other</i>, the separators, grouping and minus sign, and which plural rule the
/// locale follows. The tests format 2,187 cases ICU formatted and require the same text.
/// </para>
/// <para>
/// <b>Plural forms are why a pattern alone is not enough.</b> French writes 1,2 million and 130,5 milliards, Italian
/// mille and 5 mila, German 1 Million and 1,2 Millionen: the word follows the displayed number's category. Four rule
/// classes cover the locales carried; Russian and Polish, with four categories, are not carried rather than approximated.
/// </para>
/// <para>
/// Rounding is ICU's default, half away from zero, on decimals rather than doubles; <c>decimals</c> is a maximum, so
/// trailing zeros are dropped as ICU drops them, except in a plain currency amount, which shows its currency's digits.
/// </para>
/// </remarks>
public static class LocaleNumbers
{
    #region Methods
    /// <summary>The locales carried, in the order the data lists them.</summary>
    public static IReadOnlyList<string> Locales => Data.Value.Keys.ToArray();

    /// <summary>
    /// <paramref name="value"/> as <paramref name="locale"/> writes it. <paramref name="compact"/> is <c>null</c>, <c>short</c>
    /// or <c>long</c>; <paramref name="currency"/> an ISO code the data carries; <paramref name="percent"/> multiplies by 100.
    /// <paramref name="decimals"/> is the most fraction digits shown: 1 compact, 0 percent, the currency's own for a plain
    /// amount, 3 otherwise.
    /// </summary>
    public static string Format(double value, string locale, string? compact = null, string? currency = null, bool percent = false, int? decimals = null)
    {
        if (!double.IsFinite(value)) throw new ArgumentException($"Locale.number formats a finite number, not {value}.");
        var entry = Lookup(locale);
        if (compact is not (null or "short" or "long"))
            throw new ArgumentException($"Locale.number's compact is 'short' or 'long', not '{compact}'.");
        if (percent && (compact is not null || currency is not null))
            throw new ArgumentException("Locale.number's percent stands alone: not with compact or currency.");
        Currency? money = null;
        if (currency is not null && !entry.Currencies.TryGetValue(currency.ToUpperInvariant(), out money))
            throw new ArgumentException($"Locale.number knows these currencies: {string.Join(", ", entry.Currencies.Keys)}; not '{currency}'.");
        if (decimals is < 0 or > 10) throw new ArgumentException("Locale.number's decimals is 0 to 10.");

        var sign = value < 0 ? entry.Minus : "";
        var magnitude = (decimal)Math.Abs(value);

        string body;
        if (percent)
            body = entry.Percent.Replace("{n}", Digits(entry, Round(magnitude * 100m, decimals ?? 0), decimals ?? 0, false, entry.MinGroup));
        else if (compact is not null && magnitude >= 1000m)
            // A currency has one compact form, its own, whichever length is asked: ICU writes $130.5B for long as for short.
            body = Compact(entry, magnitude, money?.Compact ?? (compact == "long" ? entry.Long : entry.Short), decimals ?? 1);
        else if (money is not null && compact is null)
        {
            // A negative plain amount places its minus as the locale does (Dutch "US$ -4,00"), so it is returned whole.
            var digits = decimals ?? money.Digits;
            var amount = Digits(entry, Round(magnitude, digits), digits, true, entry.MinGroup);
            if (value < 0 && Round(magnitude, digits) != 0m) return money.Negative.Replace("{n}", amount);
            body = money.Plain.Replace("{n}", amount);
        }
        else
        {
            // Below a thousand a compact number is a plain one, rounded as compact rounds; a currency keeps its sign.
            var digits = decimals ?? (compact is not null ? 1 : 3);
            var text = Digits(entry, Round(magnitude, digits), digits, false, entry.MinGroup);
            body = money is null ? text : money.Small.Replace("{n}", text);
        }
        return Round(magnitude, 12) == 0m ? body : sign + body;
    }
    #endregion

    #region Methods (private)
    /// <summary>A compact number: the pattern for its power of ten, re-chosen when rounding carries into the next.</summary>
    private static string Compact(Entry entry, decimal magnitude, Step[] steps, int decimals)
    {
        var power = Math.Clamp((int)Math.Floor(Math.Log10((double)magnitude)), 3, 3 + steps.Length - 1);
        var step = steps[power - 3];
        var shown = Round(magnitude / step.Divisor, decimals);
        if (power - 3 + 1 < steps.Length && shown * step.Divisor >= Pow10(power + 1))
        {
            step = steps[power - 3 + 1];
            shown = Round(magnitude / step.Divisor, decimals);
        }
        var text = Digits(entry, shown, decimals, false, 2);
        // A displayed 1 takes the exact form (French and Italian "mille"); otherwise the plural category decides.
        var template = text == "1" ? step.Exact : Category(entry.Plural, shown, text, entry.Decimal) == "one" ? step.One : step.Other;
        return template.Replace("{n}", text);
    }

    /// <summary>
    /// The number's text: rounded already, its fraction trimmed of zeros unless <paramref name="fixedDigits"/>, the integer
    /// part grouped when it has at least <paramref name="minGroup"/> + 3 digits — ICU's minimum-grouping rule.
    /// </summary>
    private static string Digits(Entry entry, decimal number, int digits, bool fixedDigits, int minGroup)
    {
        var raw = number.ToString(fixedDigits ? "F" + digits : "0." + new string('#', Math.Max(1, digits)), CultureInfo.InvariantCulture);
        if (digits == 0 && !fixedDigits) raw = number.ToString("0", CultureInfo.InvariantCulture);
        var dot = raw.IndexOf('.');
        var integer = dot < 0 ? raw : raw[..dot];
        var fraction = dot < 0 ? "" : raw[(dot + 1)..];

        if (entry.Group.Length > 0 && integer.Length >= minGroup + 3)
        {
            var parts = new List<string> { integer[^entry.Groups[0]..] };
            var rest = integer[..^entry.Groups[0]];
            while (rest.Length > entry.Groups[1])
            {
                parts.Insert(0, rest[^entry.Groups[1]..]);
                rest = rest[..^entry.Groups[1]];
            }
            if (rest.Length > 0) parts.Insert(0, rest);
            integer = string.Join(entry.Group, parts);
        }
        return fraction.Length == 0 ? integer : integer + entry.Decimal + fraction;
    }

    /// <summary>The CLDR plural category of a displayed number, for the four rule classes the data carries.</summary>
    private static string Category(string rule, decimal shown, string text, string decimalSeparator)
    {
        var at = text.IndexOf(decimalSeparator, StringComparison.Ordinal);
        var visible = at < 0 ? 0 : text.Length - at - decimalSeparator.Length;
        var integer = decimal.Truncate(shown);
        return rule switch
        {
            "i1v0" => integer == 1m && visible == 0 ? "one" : "other",
            "n1" => shown == 1m ? "one" : "other",
            "i01" => integer is 0m or 1m ? "one" : "other",
            "i0n1" => integer == 0m || shown == 1m ? "one" : "other",
            _ => "other",
        };
    }

    private static decimal Round(decimal value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    private static decimal Pow10(int power) => Enumerable.Repeat(10m, power).Aggregate(1m, (a, b) => a * b);

    private static Entry Lookup(string locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
            throw new ArgumentException("Locale.number needs a locale, such as 'en-IN' or 'ja-JP'.");
        var data = Data.Value;
        if (data.TryGetValue(locale.Trim(), out var exact)) return exact;
        var match = data.FirstOrDefault(kv => string.Equals(kv.Key, locale.Trim(), StringComparison.OrdinalIgnoreCase)).Value;
        if (match is not null) return match;
        throw new ArgumentException($"Locale.number carries these locales: {string.Join(", ", data.Keys)}; not '{locale}'. "
            + "Name a language and a region, as 'en-IN' or 'pt-BR'.");
    }
    #endregion

    #region Data
    private sealed record Step(string Exact, string One, string Other, decimal Divisor);

    private sealed record Currency(string Plain, string Negative, int Digits, Step[] Compact, string Small);

    private sealed record Entry(string Plural, Step[] Short, Step[] Long, Dictionary<string, Currency> Currencies, string Percent,
        string Group, string Decimal, string Minus, int[] Groups, int MinGroup);

    private static readonly Lazy<Dictionary<string, Entry>> Data = new(Load);

    private static Dictionary<string, Entry> Load()
    {
        using var stream = typeof(LocaleNumbers).Assembly.GetManifestResourceStream("Polson.Drawing.Skia.cldr-numbers.json")
            ?? throw new InvalidOperationException("The CLDR number data is not embedded in Polson.Drawing.Skia.");
        using var doc = JsonDocument.Parse(stream);
        static Step[] Steps(JsonElement e) =>
            [.. e.EnumerateArray().Select(s => new Step(s.GetProperty("exact").GetString()!, s.GetProperty("one").GetString()!,
                s.GetProperty("other").GetString()!, s.GetProperty("d").GetDecimal()))];

        var result = new Dictionary<string, Entry>(StringComparer.Ordinal);
        foreach (var locale in doc.RootElement.GetProperty("locales").EnumerateObject())
        {
            var e = locale.Value;
            var currencies = e.GetProperty("currency").EnumerateObject().ToDictionary(c => c.Name,
                c => new Currency(c.Value.GetProperty("plain").GetString()!, c.Value.GetProperty("negative").GetString()!,
                    c.Value.GetProperty("digits").GetInt32(), Steps(c.Value.GetProperty("compact")),
                    c.Value.GetProperty("small").GetString()!));
            result[locale.Name] = new Entry(e.GetProperty("plural").GetString()!, Steps(e.GetProperty("short")), Steps(e.GetProperty("long")),
                currencies, e.GetProperty("percent").GetString()!, e.GetProperty("group").GetString()!, e.GetProperty("decimal").GetString()!,
                e.GetProperty("minus").GetString()!, [.. e.GetProperty("groups").EnumerateArray().Select(g => g.GetInt32())],
                e.GetProperty("minGroup").GetInt32());
        }
        return result;
    }
    #endregion
}
