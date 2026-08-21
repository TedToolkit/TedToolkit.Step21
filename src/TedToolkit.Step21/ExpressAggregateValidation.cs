namespace TedToolkit.Step21;

// Centralizes schema-neutral aggregate evidence so all four public categories use identical codes and ordering.
internal static class ExpressAggregateValidation
{
    internal const string LowerBoundCode = "EXPRESS.AGGREGATE.LOWER_BOUND";
    internal const string UpperBoundCode = "EXPRESS.AGGREGATE.UPPER_BOUND";
    internal const string UniqueCode = "EXPRESS.AGGREGATE.UNIQUE";
    internal const string RequiredSlotCode = "EXPRESS.ARRAY.REQUIRED_SLOT";

    internal static List<ValidationFailure> ValidateBounds(
        int count,
        int lowerBound,
        int? upperBound,
        string path)
    {
        var failures = new List<ValidationFailure>();
        if (count < lowerBound)
        {
            failures.Add(
                new ValidationFailure(
                    LowerBoundCode,
                    path,
                    $"The aggregate contains {count} element(s), fewer than its lower bound of {lowerBound}."));
        }

        if (upperBound is { } maximum && count > maximum)
        {
            failures.Add(
                new ValidationFailure(
                    UpperBoundCode,
                    path,
                    $"The aggregate contains {count} element(s), more than its upper bound of {maximum}."));
        }

        return failures;
    }

    internal static void AddUniquenessFailure<T>(
        IReadOnlyList<T> values,
        IEqualityComparer<T> comparer,
        string path,
        ICollection<ValidationFailure> failures)
    {
        var seen = new HashSet<T>(comparer);
        if (values.Any(value => !seen.Add(value)))
        {
            failures.Add(
                new ValidationFailure(
                    UniqueCode,
                    path,
                    "The aggregate contains duplicate candidates but its EXPRESS declaration requires UNIQUE values."));
        }
    }

    internal static void ValidateVariableBounds(int lowerBound, int? upperBound)
    {
        if (lowerBound < 0)
            throw new ArgumentOutOfRangeException(nameof(lowerBound), lowerBound, "The lower bound must be non-negative.");
        if (upperBound is { } maximum && maximum < lowerBound)
        {
            throw new ArgumentOutOfRangeException(
                nameof(upperBound),
                maximum,
                "The upper bound must not be less than the lower bound.");
        }
    }
}