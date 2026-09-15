using System.Globalization;
using WorkLogManager.Application.Common;

namespace WorkLogManager.Application.Services;

/// <summary>
/// Parses and formats human-readable, space-separated duration strings such as "1h 30m".
/// Unit table is fixed and case-sensitive:
///   s = second, m = minute, h = hour, d = day,
///   S = week (7 days), M = month (30 days, fixed approximation), A = year (365 days, fixed approximation).
/// The <see cref="Format"/> output only ever uses d/h/m/s to avoid display ambiguity,
/// even though larger units are accepted as input.
/// </summary>
public static class WorkLogDurationParser
{
    private const long SecondsPerSecond = 1;
    private const long SecondsPerMinute = 60;
    private const long SecondsPerHour = 60 * SecondsPerMinute;
    private const long SecondsPerDay = 24 * SecondsPerHour;
    private const long SecondsPerWeek = 7 * SecondsPerDay;
    private const long SecondsPerMonth = 30 * SecondsPerDay;
    private const long SecondsPerYear = 365 * SecondsPerDay;

    private static readonly Dictionary<char, long> UnitSecondsMap = new()
    {
        ['s'] = SecondsPerSecond,
        ['m'] = SecondsPerMinute,
        ['h'] = SecondsPerHour,
        ['d'] = SecondsPerDay,
        ['S'] = SecondsPerWeek,
        ['M'] = SecondsPerMonth,
        ['A'] = SecondsPerYear,
    };

    public static long Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            throw new DomainException("Duration text is required.");
        }

        var tokens = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            throw new DomainException("Duration text is required.");
        }

        long totalSeconds = 0;
        foreach (var token in tokens)
        {
            totalSeconds += ParseToken(token);
        }

        return totalSeconds;
    }

    private static long ParseToken(string token)
    {
        var unit = token[^1];
        var numberPart = token[..^1];

        if (numberPart.Length == 0 || !long.TryParse(numberPart, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
        {
            throw new DomainException($"Invalid duration token: '{token}'.");
        }

        if (!UnitSecondsMap.TryGetValue(unit, out var unitSeconds))
        {
            throw new DomainException($"Unknown duration unit: '{unit}'.");
        }

        return value * unitSeconds;
    }

    public static string Format(long totalSeconds)
    {
        if (totalSeconds == 0)
        {
            return "0s";
        }

        var remaining = totalSeconds;
        var days = remaining / SecondsPerDay;
        remaining %= SecondsPerDay;
        var hours = remaining / SecondsPerHour;
        remaining %= SecondsPerHour;
        var minutes = remaining / SecondsPerMinute;
        remaining %= SecondsPerMinute;
        var seconds = remaining;

        var parts = new List<string>();
        if (days != 0)
        {
            parts.Add($"{days}d");
        }

        if (hours != 0)
        {
            parts.Add($"{hours}h");
        }

        if (minutes != 0)
        {
            parts.Add($"{minutes}m");
        }

        if (seconds != 0)
        {
            parts.Add($"{seconds}s");
        }

        return string.Join(' ', parts);
    }
}
