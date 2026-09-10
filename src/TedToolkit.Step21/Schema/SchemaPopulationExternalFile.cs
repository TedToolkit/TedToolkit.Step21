namespace TedToolkit.Step21;

/// <summary>Describes whether an external schema-population resource resolved during the current read.</summary>
public enum SchemaPopulationResourceStatus
{
    /// <summary>The declaration has not been resolved by a read operation.</summary>
    Unresolved = 0,

    /// <summary>The resource did not resolve to a valid exchange structure.</summary>
    Missing = 1,

    /// <summary>The resource resolved to the associated exchange structure.</summary>
    Resolved = 2,

    /// <summary>The resource was supplied and digestable but was not an exchange structure.</summary>
    ContentOnly = 3,
}

/// <summary>Describes the ISO timestamp observation for an external schema-population resource.</summary>
public enum SchemaPopulationTimestampStatus
{
    /// <summary>No last-visited timestamp was supplied.</summary>
    NotProvided = 0,

    /// <summary>The supplied timestamp does not establish that the referenced structure is unchanged.</summary>
    NotVerified = 1,

    /// <summary>The supplied timestamp is after the referenced structure creation timestamp.</summary>
    Verified = 2,
}

/// <summary>Describes message-digest verification for an external schema-population resource.</summary>
public enum SchemaPopulationDigestStatus
{
    /// <summary>No message digest was supplied.</summary>
    NotProvided = 0,

    /// <summary>A digest was supplied but has not been verified by a signature capability.</summary>
    NotVerified = 1,

    /// <summary>The supplied digest was verified.</summary>
    Verified = 2,

    /// <summary>The supplied digest did not verify.</summary>
    Mismatch = 3,
}

/// <summary>Represents one external-file identification in a standard <c>SCHEMA_POPULATION</c> header.</summary>
public sealed class SchemaPopulationExternalFile
{
    /// <summary>Creates an external-file identification from its ISO triple.</summary>
    public SchemaPopulationExternalFile(Uri location, string? timeStamp = null, string? messageDigest = null)
    {
        Guard.NotNull(location);
        if (location.OriginalString.Length == 0)
            throw new ArgumentException("An external schema-population URI cannot be empty.", nameof(location));
        try
        {
            Part21NameValidation.ValidateResource(location.OriginalString, nameof(location));
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                "The external schema-population location must satisfy the Part 21 URI-reference syntax.",
                nameof(location),
                exception);
        }
        if (timeStamp is not null && !Part21LexicalForms.TryParseTimeStamp(timeStamp, out _))
            throw new ArgumentException("The timestamp must use the ISO extended date-and-time form.", nameof(timeStamp));
        if (messageDigest is not null && !Part21LexicalForms.IsCanonicalBase64(messageDigest))
            throw new ArgumentException("The message digest must use canonical Base64.", nameof(messageDigest));

        Location = location;
        TimeStamp = timeStamp;
        MessageDigest = messageDigest;
        TimestampStatus = timeStamp is null
            ? SchemaPopulationTimestampStatus.NotProvided
            : SchemaPopulationTimestampStatus.NotVerified;
        DigestStatus = messageDigest is null
            ? SchemaPopulationDigestStatus.NotProvided
            : SchemaPopulationDigestStatus.NotVerified;
    }

    /// <summary>Gets the absolute or relative resource location retained from the header.</summary>
    public Uri Location { get; }

    /// <summary>Gets the optional last-visited timestamp spelling.</summary>
    public string? TimeStamp { get; }

    /// <summary>Gets the optional Base64 message-digest spelling.</summary>
    public string? MessageDigest { get; }

    /// <summary>Gets the resource-resolution result for the current read.</summary>
    public SchemaPopulationResourceStatus ResourceStatus { get; internal set; }

    /// <summary>Gets the timestamp observation for the current read.</summary>
    public SchemaPopulationTimestampStatus TimestampStatus { get; internal set; }

    /// <summary>Gets the message-digest verification result for the current read.</summary>
    public SchemaPopulationDigestStatus DigestStatus { get; internal set; }

    /// <summary>Gets the resolved external exchange structure, or <see langword="null"/>.</summary>
    public ExchangeStructure? Structure { get; internal set; }
}
