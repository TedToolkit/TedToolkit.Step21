namespace TedToolkit.Step21;

/// <summary>
/// Defines the minimal identity shared by generated EXPRESS schema descriptors.
/// </summary>
/// <remarks>
/// Generated mapping hooks are added by a later delivery item. Descriptor identity is ordinal and is snapshotted by
/// <see cref="ExchangeStructure"/> construction.
/// </remarks>
public abstract class SchemaDescriptor
{
    /// <summary>Initializes the base of a generated schema descriptor.</summary>
    protected SchemaDescriptor()
    {
    }

    /// <summary>Gets the descriptor's stable schema name.</summary>
    public abstract SchemaName Name { get; }
}
