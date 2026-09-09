namespace TedToolkit.Step21;

/// <summary>Represents one ISO 10303-21 user-defined header entity and its schema-neutral parameters.</summary>
/// <remarks>
/// Part 21 defines the physical mapping while the application-defined EXPRESS declaration defines parameter types.
/// </remarks>
public sealed class UserDefinedHeaderEntity
{
    /// <summary>Creates a user-defined header entity from a stable parameter snapshot.</summary>
    /// <param name="keyword">The canonical upper-case Part 21 keyword, including its leading exclamation mark.</param>
    /// <param name="parameters">The mapped physical parameters in declaration order.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="keyword"/> is not a user-defined keyword or <paramref name="parameters"/> contains a null value.
    /// </exception>
    public UserDefinedHeaderEntity(string keyword, IEnumerable<ParameterValue> parameters)
    {
        ValidateKeyword(keyword);
        ArgumentNullException.ThrowIfNull(parameters);
        var snapshot = parameters.ToArray();
        if (snapshot.Any(value => value is null))
            throw new ArgumentException("User-defined header parameters cannot contain null values.", nameof(parameters));

        Keyword = keyword;
        Parameters = Array.AsReadOnly(snapshot);
    }

    /// <summary>Gets the canonical user-defined keyword, including its leading exclamation mark.</summary>
    public string Keyword { get; }

    /// <summary>Gets the stable schema-neutral parameter snapshot.</summary>
    public IReadOnlyList<ParameterValue> Parameters { get; }

    private static void ValidateKeyword(string keyword)
    {
        ArgumentNullException.ThrowIfNull(keyword);
        if (keyword.Length < 2
            || keyword[0] != '!'
            || keyword[1] is not (>= 'A' and <= 'Z') && keyword[1] != '_'
            || keyword.AsSpan(2).ContainsAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZ_0123456789"))
        {
            throw new ArgumentException(
                "A user-defined header keyword must start with '!' and a Part 21 UPPER character, followed only by UPPER or DIGIT characters.",
                nameof(keyword));
        }
    }
}
