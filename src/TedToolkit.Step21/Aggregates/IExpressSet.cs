namespace TedToolkit.Step21;

/// <summary>
/// Represents a covariant, read-only view of an EXPRESS <c>SET</c> and its declared metadata.
/// </summary>
/// <typeparam name="T">The element type exposed by the view.</typeparam>
/// <remarks>The view does not copy storage; a compatible <see cref="ExpressSet{T}"/> implements it directly.</remarks>
public interface IExpressSet<out T> : IReadOnlyCollection<T>
{
    /// <summary>Gets the inclusive minimum permitted cardinality.</summary>
    int LowerBound { get; }

    /// <summary>Gets the inclusive maximum permitted cardinality, or <see langword="null"/> when unbounded.</summary>
    int? UpperBound { get; }

    /// <summary>Validates the visible candidate state without mutation.</summary>
    /// <param name="path">The deterministic caller path assigned to produced failures.</param>
    /// <returns>Every detected bound or uniqueness failure in deterministic order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <example><code>ValidationResult result = view.Validate("ENTITY.Items");</code></example>
    ValidationResult Validate(string path = "$");
}