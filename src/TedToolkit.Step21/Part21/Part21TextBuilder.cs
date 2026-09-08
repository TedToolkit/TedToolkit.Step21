using System.Buffers;
using System.Globalization;
using System.Text;

#if ANNEXF_SHARED_TEXT_BUILDER
namespace TedToolkit.Step21.AnnexF;
#else
namespace TedToolkit.Step21;
#endif

// Centralizes output accounting so every formatter rejects before retaining text beyond its budget.
internal sealed class Part21TextBuilder
{
    private readonly StringBuilder _builder;
    private readonly int _maximumLength;
    private readonly Action _throwLimit;

    internal Part21TextBuilder(int maximumLength, Action throwLimit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumLength);
        ArgumentNullException.ThrowIfNull(throwLimit);
        _maximumLength = maximumLength;
        _throwLimit = throwLimit;
        _builder = new StringBuilder(Math.Min(maximumLength, 256));
    }

    internal int Length => _builder.Length;

    internal int Remaining => _maximumLength - _builder.Length;

    internal StringBuilder Builder => _builder;

    internal void EnsureCanAppend(long characterCount)
    {
        if (characterCount < 0 || characterCount > Remaining)
            _throwLimit();
    }

    internal Part21TextBuilder Append(char value)
    {
        EnsureCanAppend(1);
        _ = _builder.Append(value);
        return this;
    }

    internal Part21TextBuilder Append(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        EnsureCanAppend(value.Length);
        _ = _builder.Append(value);
        return this;
    }

    internal Part21TextBuilder Append(ReadOnlySpan<char> value)
    {
        EnsureCanAppend(value.Length);
        _ = _builder.Append(value);
        return this;
    }

    internal void AppendFormattable(ISpanFormattable value, ReadOnlySpan<char> format = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        var maximumProbe = Remaining == int.MaxValue ? int.MaxValue : Remaining + 1;
        Span<char> initial = stackalloc char[Math.Min(128, maximumProbe)];
        if (value.TryFormat(initial, out var written, format, CultureInfo.InvariantCulture))
        {
            Append(initial[..written]);
            return;
        }

        var size = Math.Min(256, maximumProbe);
        while (true)
        {
            if (size == 0)
                _throwLimit();
            var rented = ArrayPool<char>.Shared.Rent(size);
            try
            {
                maximumProbe = Remaining == int.MaxValue ? int.MaxValue : Remaining + 1;
                var available = Math.Min(rented.Length, maximumProbe);
                if (value.TryFormat(
                        rented.AsSpan(0, available),
                        out written,
                        format,
                        CultureInfo.InvariantCulture))
                {
                    Append(rented.AsSpan(0, written));
                    return;
                }
                if (available > Remaining)
                    _throwLimit();
                size = (int)Math.Min(maximumProbe, (long)available * 2);
            }
            finally
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    public override string ToString() => _builder.ToString();
}
