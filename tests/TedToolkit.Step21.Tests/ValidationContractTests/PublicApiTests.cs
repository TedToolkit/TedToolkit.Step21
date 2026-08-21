using System.Reflection;
using System.Text;

using ReflectionAssembly = System.Reflection.Assembly;

namespace TedToolkit.Step21.Tests.ValidationContractTests;

internal sealed class PublicApiTests
{
    /// <summary>
    /// Verifies that the runtime exposes exactly the approved SBRT-004 evidence contract.
    /// </summary>
    [Test]
    public async Task Should_match_approved_surface_when_validation_contract_is_inspected()
    {
        var assembly = ReflectionAssembly.Load("TedToolkit.Step21");
        var expectedPath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "PublicApi",
            "SBRT-004.approved.txt");
        var expected = NormalizeLineEndings(await File.ReadAllTextAsync(expectedPath));
        var actual = NormalizeLineEndings(RenderPublicApi(assembly));

        await Assert.That(actual).IsEqualTo(expected);
    }

    private static string RenderPublicApi(ReflectionAssembly assembly)
    {
        var builder = new StringBuilder();
        foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            RenderType(builder, type);
        }

        return builder.ToString();
    }

    private static void RenderType(StringBuilder builder, Type type)
    {
        if (type.IsEnum)
        {
            builder.Append("enum ").AppendLine(type.FullName);
            foreach (var name in Enum.GetNames(type))
            {
                var value = Convert.ToInt64(Enum.Parse(type, name));
                builder.Append("  value ").Append(name).Append(" = ").AppendLine(value.ToString());
            }

            return;
        }

        builder.Append(type.IsSealed ? "sealed class " : "class ")
            .Append(type.FullName)
            .Append(" : ")
            .AppendLine(FormatType(type.BaseType!));

        foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                     .OrderBy(FormatConstructor, StringComparer.Ordinal))
        {
            builder.Append("  constructor ").AppendLine(FormatConstructor(constructor));
        }

        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .OrderBy(property => property.Name, StringComparer.Ordinal))
        {
            builder.Append("  property ")
                .Append(FormatType(property.PropertyType, new NullabilityInfoContext().Create(property).ReadState))
                .Append(' ')
                .Append(property.Name)
                .AppendLine(property.SetMethod is null ? " { get; }" : " { get; set; }");
        }
    }

    private static string FormatConstructor(ConstructorInfo constructor) =>
        $"({string.Join(", ", constructor.GetParameters().Select(FormatParameter))})";

    private static string FormatParameter(ParameterInfo parameter)
    {
        var suffix = parameter.HasDefaultValue
            ? $" = {FormatDefaultValue(parameter.DefaultValue)}"
            : string.Empty;
        var nullability = new NullabilityInfoContext().Create(parameter).ReadState;
        return $"{FormatType(parameter.ParameterType, nullability)} {parameter.Name}{suffix}";
    }

    private static string FormatDefaultValue(object? value) => value switch
    {
        null => "null",
        string text => $"\"{text}\"",
        _ => value.ToString()!,
    };

    private static string FormatType(Type type, NullabilityState nullability = NullabilityState.Unknown)
    {
        var nullableSuffix = nullability == NullabilityState.Nullable ? "?" : string.Empty;
        if (!type.IsGenericType)
            return $"{type.FullName}{nullableSuffix}";

        var genericName = type.GetGenericTypeDefinition().FullName!;
        genericName = genericName[..genericName.IndexOf('`')];
        return $"{genericName}<{string.Join(", ", type.GetGenericArguments().Select(type => FormatType(type)))}>{nullableSuffix}";
    }

    private static string NormalizeLineEndings(string value) => value.Replace("\r\n", "\n");
}