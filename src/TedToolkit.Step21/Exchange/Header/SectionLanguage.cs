namespace TedToolkit.Step21;

/// <summary>Represents one standard <c>SECTION_LANGUAGE</c> header declaration.</summary>
public sealed class SectionLanguage
{
    /// <summary>Creates a language declaration for one named section or the default section set.</summary>
    /// <param name="sectionName">The named data section, or <see langword="null"/> for the default declaration.</param>
    /// <param name="languageCode">An ISO 639-2 Alpha-3 bibliographic language code.</param>
    /// <exception cref="ArgumentNullException"><paramref name="languageCode"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="languageCode"/> is not an ISO 639-2 Alpha-3 bibliographic code.
    /// </exception>
    public SectionLanguage(string? sectionName, string languageCode)
    {
        ArgumentNullException.ThrowIfNull(languageCode);
        if (!Iso639Part2BibliographicCodes.Contains(languageCode))
        {
            throw new ArgumentException(
                "The language code must be an ISO 639-2 Alpha-3 bibliographic code.",
                nameof(languageCode));
        }

        SectionName = sectionName;
        LanguageCode = languageCode;
    }

    /// <summary>Gets the named data section, or <see langword="null"/> for the default declaration.</summary>
    public string? SectionName { get; }

    /// <summary>Gets the ISO 639-2 Alpha-3 bibliographic language code.</summary>
    public string LanguageCode { get; }
}
