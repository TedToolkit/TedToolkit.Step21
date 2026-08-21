using System.Reflection;
using System.Text;

using ReflectionAssembly = System.Reflection.Assembly;

namespace TedToolkit.Step21.Tests.ValidationContractTests;

internal sealed class PublicApiTests
{
    /// <summary>
    /// Verifies that the runtime exposes exactly the public contract approved through SBRT-005.
    /// </summary>
    [Test]
    public async Task Should_match_approved_surface_when_validation_contract_is_inspected()
    {
        var assembly = ReflectionAssembly.Load("TedToolkit.Step21");
        var expectedPath = Path.Combine(
            AppContext.BaseDirectory,
            "TestData",
            "PublicApi",
            "SBRT-005.approved.txt");
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

        var inheritedInterfaces = type.BaseType?.GetInterfaces() ?? [];
        foreach (var contract in type.GetInterfaces()
                     .Except(inheritedInterfaces)
                     .OrderBy(contract => FormatType(contract), StringComparer.Ordinal))
        {
            builder.Append("  interface ").AppendLine(FormatType(contract));
        }

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
                .Append(FormatIndexer(property))
                .AppendLine(property.SetMethod is null ? " { get; }" : " { get; set; }");
        }

        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                     .Where(method => !method.IsSpecialName)
                     .OrderBy(method => method.Name, StringComparer.Ordinal)
                     .ThenBy(FormatMethod, StringComparer.Ordinal))
        {
            builder.Append("  method ").AppendLine(FormatMethod(method));
        }
    }

    private static string FormatIndexer(PropertyInfo property)
    {
        var indexes = property.GetIndexParameters();
        return indexes.Length == 0
            ? string.Empty
            : $"[{string.Join(", ", indexes.Select(FormatParameter))}]";
    }

    private static string FormatMethod(MethodInfo method)
    {
        var nullability = new NullabilityInfoContext().Create(method.ReturnParameter).ReadState;
        return $"{FormatType(method.ReturnType, nullability)} {method.Name}({string.Join(", ", method.GetParameters().Select(FormatParameter))})";
    }

    private static string FormatConstructor(ConstructorInfo constructor) =>
        $"({string.Join(", ", constructor.GetParameters().Select(FormatParameter))})";

    private static string FormatParameter(ParameterInfo parameter)
    {
        var suffix = parameter.HasDefaultValue
            ? $" = {FormatDefaultValue(parameter.DefaultValue)}"
            : string.Empty;
        var nullability = new NullabilityInfoContext().Create(parameter).ReadState;
        var modifier = parameter.IsOut ? "out " : parameter.ParameterType.IsByRef ? "ref " : string.Empty;
        var parameterType = parameter.ParameterType.IsByRef
            ? parameter.ParameterType.GetElementType()!
            : parameter.ParameterType;
        return $"{modifier}{FormatType(parameterType, nullability)} {parameter.Name}{suffix}";
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
        if (type.IsGenericParameter)
            return $"{type.Name}{nullableSuffix}";
        if (type.IsArray)
            return $"{FormatType(type.GetElementType()!)}[]{nullableSuffix}";
        if (!type.IsGenericType)
            return $"{type.FullName}{nullableSuffix}";

        if (type.GetGenericTypeDefinition() == typeof(Nullable<>))
            return $"{FormatType(type.GetGenericArguments()[0])}?";

        var genericName = type.GetGenericTypeDefinition().FullName!;
        genericName = genericName[..genericName.IndexOf('`')];
        return $"{genericName}<{string.Join(", ", type.GetGenericArguments().Select(type => FormatType(type)))}>{nullableSuffix}";
    }

    private static string NormalizeLineEndings(string value) => value.Replace("\r\n", "\n");
}