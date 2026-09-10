namespace TedToolkit.Step21;

/// <summary>Represents one standard <c>SECTION_CONTEXT</c> header declaration.</summary>
public sealed class SectionContext
{
    /// <summary>Creates a context declaration for one named section or the default section set.</summary>
    /// <param name="sectionName">The named data section, or <see langword="null"/> for the default declaration.</param>
    /// <param name="contextIdentifiers">One or more application-defined context identifiers.</param>
    /// <exception cref="ArgumentNullException"><paramref name="contextIdentifiers"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="contextIdentifiers"/> is empty or contains a <see langword="null"/> value.
    /// </exception>
    public SectionContext(string? sectionName, IEnumerable<string> contextIdentifiers)
    {
        Guard.NotNull(contextIdentifiers);
        ContextIdentifiers = IsoValueSnapshot.Create(contextIdentifiers, nameof(contextIdentifiers));
        if (ContextIdentifiers.Count == 0)
        {
            throw new ArgumentException(
                "A section-context declaration requires at least one context identifier.",
                nameof(contextIdentifiers));
        }

        SectionName = sectionName;
    }

    /// <summary>Gets the named data section, or <see langword="null"/> for the default declaration.</summary>
    public string? SectionName { get; }

    /// <summary>Gets the snapshotted application-defined context identifiers.</summary>
    public IReadOnlyList<string> ContextIdentifiers { get; }
}
