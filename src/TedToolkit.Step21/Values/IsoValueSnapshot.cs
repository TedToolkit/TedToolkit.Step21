using System.Collections.ObjectModel;

namespace TedToolkit.Step21;

internal static class IsoValueSnapshot
{
    internal static ReadOnlyCollection<string> Create(IEnumerable<string> values, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        var snapshot = values.ToArray();
        if (snapshot.Any(value => value is null))
            throw new ArgumentException("ISO string collections cannot contain null values.", parameterName);

        return Array.AsReadOnly(snapshot);
    }
}
