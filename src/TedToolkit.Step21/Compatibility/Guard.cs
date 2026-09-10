using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace TedToolkit.Step21;

internal static class Guard
{
    internal static void NotNull(
        [NotNull] object? argument,
        [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
    {
        if (argument is null)
            throw new ArgumentNullException(parameterName);
    }

    internal static void NullOrWhiteSpace(
        [NotNull] string? argument,
        [CallerArgumentExpression(nameof(argument))] string? parameterName = null)
    {
        if (argument is null || argument.Trim().Length == 0)
            throw new ArgumentException("The value cannot be an empty string or composed entirely of whitespace.", parameterName);
    }

    internal static void Negative(
        int value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be non-negative.");
    }

    internal static void NegativeOrZero(
        int value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be positive.");
    }

    internal static void NegativeOrZero(
        long value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(parameterName, value, "The value must be positive.");
    }

    internal static void LessThan(
        int value,
        int other,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        if (value < other)
            throw new ArgumentOutOfRangeException(parameterName, value, $"The value must be greater than or equal to {other}.");
    }
}
