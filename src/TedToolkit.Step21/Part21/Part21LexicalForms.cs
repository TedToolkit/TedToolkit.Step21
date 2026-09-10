using System.Globalization;

namespace TedToolkit.Step21;

internal static class Part21LexicalForms
{
    internal static bool IsCanonicalBase64(string value)
    {
        if (value.Length == 0 || value.Length % 4 != 0)
            return false;

        try
        {
            var bytes = Convert.FromBase64String(value);
            return Convert.ToBase64String(bytes).Equals(value, StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    internal static bool TryParseTimeStamp(string value, out ParsedTimeStamp result)
    {
        result = default;
        if (value.Length < 19
            || value[4] != '-'
            || value[7] != '-'
            || value[10] != 'T'
            || value[13] != ':'
            || value[16] != ':'
            || !DateTime.TryParseExact(
                value.Substring(0, 10),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date)
            || !TryParseTwoDigits(value, 11, out var hour)
            || !TryParseTwoDigits(value, 14, out var minute)
            || !TryParseTwoDigits(value, 17, out var second))
        {
            return false;
        }

        var position = 19;
        var fraction = ReadOnlySpan<char>.Empty;
        if (position < value.Length && value[position] is '.' or ',')
        {
            var fractionStart = ++position;
            while (position < value.Length && IsDigit(value[position]))
                position++;
            if (position == fractionStart)
                return false;
            fraction = value.AsSpan(fractionStart, position - fractionStart);
        }

        var hasOffset = false;
        var offsetMinutes = 0;
        if (position < value.Length)
        {
            if (value[position] == 'Z')
            {
                hasOffset = true;
                position++;
            }
            else if (value[position] is '+' or '-')
            {
                if (position + 6 != value.Length
                    || value[position + 3] != ':'
                    || !TryParseTwoDigits(value, position + 1, out var offsetHour)
                    || !TryParseTwoDigits(value, position + 4, out var offsetMinute)
                    || offsetHour > 23
                    || offsetMinute > 59)
                {
                    return false;
                }

                hasOffset = true;
                offsetMinutes = (offsetHour * 60) + offsetMinute;
                if (value[position] == '-')
                    offsetMinutes = -offsetMinutes;
                position += 6;
            }
            else
            {
                return false;
            }
        }

        if (position != value.Length || minute > 59 || second > 60)
            return false;

        var fractionIsZero = IsZero(fraction);
        if (hour == 24)
        {
            if (minute != 0 || second != 0 || !fractionIsZero)
                return false;
        }
        else if (hour > 23 || (second == 60 && minute != 59))
        {
            return false;
        }

        var dayNumber = date.Ticks / TimeSpan.TicksPerDay;
        var seconds = (dayNumber * 86_400) + (hour * 3_600L) + (minute * 60L) + second;
        result = new ParsedTimeStamp(seconds, fraction.ToString(), hasOffset, offsetMinutes);
        return true;
    }

    private static bool TryParseTwoDigits(string value, int position, out int result)
    {
        result = 0;
        if (position + 1 >= value.Length || !IsDigit(value[position]) || !IsDigit(value[position + 1]))
            return false;

        result = ((value[position] - '0') * 10) + value[position + 1] - '0';
        return true;
    }

    private static bool IsDigit(char value) => value is >= '0' and <= '9';

    private static bool IsZero(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (character != '0')
                return false;
        }
        return true;
    }
}

internal readonly record struct ParsedTimeStamp(
    long LocalSeconds,
    string Fraction,
    bool HasOffset,
    int OffsetMinutes)
{
    internal bool IsAfter(ParsedTimeStamp other)
    {
        if (HasOffset != other.HasOffset)
            return false;

        var seconds = HasOffset ? LocalSeconds - (OffsetMinutes * 60L) : LocalSeconds;
        var otherSeconds = other.HasOffset ? other.LocalSeconds - (other.OffsetMinutes * 60L) : other.LocalSeconds;
        if (seconds != otherSeconds)
            return seconds > otherSeconds;

        var width = Math.Max(Fraction.Length, other.Fraction.Length);
        for (var index = 0; index < width; index++)
        {
            var digit = index < Fraction.Length ? Fraction[index] : '0';
            var otherDigit = index < other.Fraction.Length ? other.Fraction[index] : '0';
            if (digit != otherDigit)
                return digit > otherDigit;
        }
        return false;
    }
}
