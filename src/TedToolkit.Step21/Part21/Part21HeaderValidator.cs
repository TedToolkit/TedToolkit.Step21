using System.Buffers;
using System.Globalization;
using System.Text;

namespace TedToolkit.Step21;

/// <summary>Validates the EXPRESS constraints and normative lexical forms of the standard Part 21 header.</summary>
internal static class Part21HeaderValidator
{
    internal static IReadOnlyList<ValidationFailure> Validate(HeaderSection header)
    {
        Guard.NotNull(header);
        var failures = new List<ValidationFailure>();
        ValidateRequiredList(
            header.FileDescription.Description,
            256,
            "Header.FileDescription.Description",
            "P21.HEADER.FILE_DESCRIPTION.DESCRIPTION",
            failures);
        ValidateBoundedString(
            header.FileDescription.ImplementationLevel,
            256,
            "Header.FileDescription.ImplementationLevel",
            "P21.HEADER.FILE_DESCRIPTION.IMPLEMENTATION_LEVEL.LENGTH",
            failures);

        var fileName = header.FileName;
        ValidateBoundedString(fileName.Name, 256, "Header.FileName.Name", "P21.HEADER.FILE_NAME.NAME.LENGTH", failures);
        ValidateBoundedString(
            fileName.TimeStamp,
            256,
            "Header.FileName.TimeStamp",
            "P21.HEADER.FILE_NAME.TIME_STAMP.LENGTH",
            failures);
        if (!Part21LexicalForms.TryParseTimeStamp(fileName.TimeStamp, out _))
        {
            failures.Add(new ValidationFailure(
                "P21.HEADER.FILE_NAME.TIME_STAMP.FORMAT",
                "Header.FileName.TimeStamp",
                "FILE_NAME time_stamp must use the ISO 8601 complete extended date-and-time form."));
        }
        ValidateRequiredList(
            fileName.Author,
            256,
            "Header.FileName.Author",
            "P21.HEADER.FILE_NAME.AUTHOR",
            failures);
        ValidateRequiredList(
            fileName.Organization,
            256,
            "Header.FileName.Organization",
            "P21.HEADER.FILE_NAME.ORGANIZATION",
            failures);
        ValidateBoundedString(
            fileName.PreprocessorVersion,
            256,
            "Header.FileName.PreprocessorVersion",
            "P21.HEADER.FILE_NAME.PREPROCESSOR_VERSION.LENGTH",
            failures);
        ValidateBoundedString(
            fileName.OriginatingSystem,
            256,
            "Header.FileName.OriginatingSystem",
            "P21.HEADER.FILE_NAME.ORIGINATING_SYSTEM.LENGTH",
            failures);
        ValidateBoundedString(
            fileName.Authorization,
            256,
            "Header.FileName.Authorization",
            "P21.HEADER.FILE_NAME.AUTHORIZATION.LENGTH",
            failures);

        var schemaIdentifiers = header.FileSchema.SchemaIdentifiers;
        if (schemaIdentifiers.Count == 0)
        {
            failures.Add(new ValidationFailure(
                "P21.HEADER.FILE_SCHEMA.CARDINALITY",
                "Header.FileSchema.SchemaIdentifiers",
                "FILE_SCHEMA requires at least one schema identifier."));
        }
        // Writing canonicalizes the complete identifier to upper case, so uniqueness must use the same equivalence.
        var uniqueIdentifiers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < schemaIdentifiers.Count; index++)
        {
            var identifier = schemaIdentifiers[index];
            var path = $"Header.FileSchema.SchemaIdentifiers[{index}]";
            ValidateBoundedString(identifier, 1024, path, "P21.HEADER.FILE_SCHEMA.IDENTIFIER.LENGTH", failures);
            if (!uniqueIdentifiers.Add(identifier))
            {
                failures.Add(new ValidationFailure(
                    "P21.HEADER.FILE_SCHEMA.IDENTIFIER.DUPLICATE",
                    path,
                    $"FILE_SCHEMA repeats schema identifier '{identifier}'."));
            }
            if (!IsCanonicalSchemaIdentifier(identifier))
            {
                failures.Add(new ValidationFailure(
                    "P21.HEADER.FILE_SCHEMA.IDENTIFIER.FORMAT",
                    path,
                    "A FILE_SCHEMA identifier must use an EXPRESS schema name and an optional numeric-arc object identifier."));
            }
        }

        return failures;
    }

    private static void ValidateRequiredList(
        IReadOnlyList<string> values,
        int maximumLength,
        string path,
        string diagnosticPrefix,
        ICollection<ValidationFailure> failures)
    {
        if (values.Count == 0)
        {
            failures.Add(new ValidationFailure(
                diagnosticPrefix + ".CARDINALITY",
                path,
                "The ISO header list requires at least one value."));
        }
        for (var index = 0; index < values.Count; index++)
        {
            ValidateBoundedString(
                values[index],
                maximumLength,
                $"{path}[{index}]",
                diagnosticPrefix + ".LENGTH",
                failures);
        }
    }

    private static void ValidateBoundedString(
        string value,
        int maximumLength,
        string path,
        string code,
        ICollection<ValidationFailure> failures)
    {
        if (CountCharactersThrough(value, maximumLength + 1) <= maximumLength)
            return;

        failures.Add(new ValidationFailure(
            code,
            path,
            $"The ISO header string exceeds its {maximumLength}-character EXPRESS bound."));
    }

    private static int CountCharactersThrough(string value, int limit)
    {
        var count = 0;
        var remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            var status = UnicodeScalar.DecodeFromUtf16(remaining, out _, out var consumed);
            if (status != OperationStatus.Done)
                consumed = 1;
            remaining = remaining[consumed..];
            count++;
            if (count == limit)
                break;
        }
        return count;
    }

    private static bool IsCanonicalSchemaIdentifier(string identifier)
    {
        var openBrace = identifier.IndexOf('{');
        var nominalEnd = openBrace < 0 ? identifier.Length : openBrace;
        while (nominalEnd > 0 && identifier[nominalEnd - 1] == ' ')
            nominalEnd--;
        if (nominalEnd == 0
            || identifier[0] is not (>= 'A' and <= 'Z') and not (>= 'a' and <= 'z'))
            return false;
        for (var index = 1; index < nominalEnd; index++)
        {
            if (identifier[index] is not (>= 'A' and <= 'Z')
                and not (>= 'a' and <= 'z')
                and not (>= '0' and <= '9')
                and not '_')
            {
                return false;
            }
        }

        if (openBrace < 0)
            return true;
        if (nominalEnd == openBrace || identifier[^1] != '}')
            return false;

        var suffix = identifier.AsSpan(openBrace + 1, identifier.Length - openBrace - 2);
        var position = 0;
        while (position < suffix.Length && suffix[position] == ' ')
            position++;
        var arcCount = 0;
        var rootArc = -1;
        while (position < suffix.Length)
        {
            var start = position;
            while (position < suffix.Length && suffix[position] is >= '0' and <= '9')
                position++;
            if (position == start || position - start > 1 && suffix[start] == '0')
                return false;
            if (arcCount == 0)
            {
                if (position - start != 1 || suffix[start] > '2')
                    return false;
                rootArc = suffix[start] - '0';
            }
            else if (arcCount == 1 && rootArc < 2)
            {
                if (!int.TryParse(
                    suffix[start..position].ToString(),
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var secondArc)
                    || secondArc > 39)
                    return false;
            }
            arcCount++;

            var delimiter = position;
            while (position < suffix.Length && suffix[position] == ' ')
                position++;
            if (position < suffix.Length && delimiter == position)
                return false;
        }

        return arcCount >= 2;
    }
}
