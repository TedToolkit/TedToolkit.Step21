using System.Reflection;
using System.Xml.Linq;

using ReflectionAssembly = System.Reflection.Assembly;

namespace TedToolkit.Step21.Tests.ValidationContractTests;

internal sealed class XmlDocumentationTests
{
    /// <summary>
    /// Verifies that generated XML documentation contains every caller- or derivation-visible runtime member.
    /// </summary>
    [Test]
    public async Task Should_document_every_member_when_runtime_xml_is_generated()
    {
        var assembly = ReflectionAssembly.Load("TedToolkit.Step21");
        var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");
        var documentedMembers = XDocument.Load(xmlPath)
            .Descendants("member")
            .Select(member => (string?)member.Attribute("name"))
            .Where(name => name is not null)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var member in GetApprovedMembers(assembly))
        {
            await Assert.That(documentedMembers.Contains(member))
                .IsTrue()
                .Because($"{member} must have generated XML documentation");
        }
    }

    private static IEnumerable<string> GetApprovedMembers(ReflectionAssembly assembly)
    {
        foreach (var type in assembly.GetExportedTypes().OrderBy(type => type.FullName, StringComparer.Ordinal))
        {
            yield return $"T:{type.FullName}";

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (!field.IsSpecialName)
                    yield return $"F:{type.FullName}.{field.Name}";
            }

            foreach (var property in type.GetProperties(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(property => property.GetAccessors(nonPublic: true).Any(IsApprovedVisibility)))
            {
                var indexes = property.GetIndexParameters();
                var suffix = indexes.Length == 0
                    ? string.Empty
                    : $"({string.Join(",", indexes.Select(parameter => FormatXmlType(parameter.ParameterType)))})";
                yield return $"P:{type.FullName}.{property.Name}{suffix}";
            }

            foreach (var constructor in type.GetConstructors(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                         .Where(IsApprovedVisibility))
            {
                var parameters = string.Join(",", constructor.GetParameters().Select(parameter => FormatXmlType(parameter.ParameterType)));
                var suffix = parameters.Length == 0 ? string.Empty : $"({parameters})";
                yield return $"M:{type.FullName}.#ctor{suffix}";
            }

            foreach (var method in type.GetMethods(
                             BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                         .Where(method => !method.IsSpecialName && IsApprovedVisibility(method)))
            {
                var parameters = string.Join(",", method.GetParameters().Select(parameter => FormatXmlType(parameter.ParameterType)));
                var suffix = parameters.Length == 0 ? string.Empty : $"({parameters})";
                yield return $"M:{type.FullName}.{method.Name}{suffix}";
            }
        }
    }

    private static bool IsApprovedVisibility(MethodBase method) =>
        method.IsPublic || method.IsFamily || method.IsFamilyOrAssembly;

    private static string FormatXmlType(Type type)
    {
        if (type.IsGenericParameter)
            return $"`{type.GenericParameterPosition}";
        if (type.IsByRef)
            return $"{FormatXmlType(type.GetElementType()!)}@";
        if (type.IsArray)
            return $"{FormatXmlType(type.GetElementType()!)}[]";
        if (!type.IsGenericType)
            return type.FullName!;

        var name = type.GetGenericTypeDefinition().FullName!;
        name = name[..name.IndexOf('`')];
        return $"{name}{{{string.Join(",", type.GetGenericArguments().Select(FormatXmlType))}}}";
    }
}