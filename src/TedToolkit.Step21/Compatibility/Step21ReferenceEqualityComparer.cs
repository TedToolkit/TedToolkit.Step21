using System.Runtime.CompilerServices;

namespace TedToolkit.Step21;

internal sealed class Step21ReferenceEqualityComparer : IEqualityComparer<object>
{
    internal static Step21ReferenceEqualityComparer Instance { get; } = new();

    private Step21ReferenceEqualityComparer()
    {
    }

    bool IEqualityComparer<object>.Equals(object? x, object? y) => ReferenceEquals(x, y);

    int IEqualityComparer<object>.GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
}
