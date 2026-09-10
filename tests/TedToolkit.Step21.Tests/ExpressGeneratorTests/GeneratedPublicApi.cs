// -----------------------------------------------------------------------
// <copyright file="GeneratedPublicApi.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using System.Text;

using Microsoft.CodeAnalysis;

namespace TedToolkit.Step21.Tests.ExpressGeneratorTests;

/// <summary>Renders the complete public contract of one generated schema independently of private layout.</summary>
internal static class GeneratedPublicApi
{
    private static readonly SymbolDisplayFormat PublicApiFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithGenericsOptions(SymbolDisplayGenericsOptions.IncludeTypeParameters
            | SymbolDisplayGenericsOptions.IncludeTypeConstraints | SymbolDisplayGenericsOptions.IncludeVariance)
        .WithMemberOptions(SymbolDisplayMemberOptions.IncludeType | SymbolDisplayMemberOptions.IncludeModifiers
            | SymbolDisplayMemberOptions.IncludeExplicitInterface | SymbolDisplayMemberOptions.IncludeParameters
            | SymbolDisplayMemberOptions.IncludeContainingType | SymbolDisplayMemberOptions.IncludeConstantValue
            | SymbolDisplayMemberOptions.IncludeRef)
        .WithParameterOptions(SymbolDisplayParameterOptions.IncludeType | SymbolDisplayParameterOptions.IncludeName
            | SymbolDisplayParameterOptions.IncludeDefaultValue | SymbolDisplayParameterOptions.IncludeModifiers
            | SymbolDisplayParameterOptions.IncludeExtensionThis)
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    internal static string Render(
        Compilation compilation,
        string schemaNamespace,
        string namespaceRoot = "Schemas")
    {
        var result = new StringBuilder();
        var generatedNamespace = RequiredNamespace(
            compilation.GlobalNamespace,
            "TedToolkit",
            "Step21",
            namespaceRoot,
            schemaNamespace);
        foreach (var type in EnumeratePublicTypes(generatedNamespace)
                     .OrderBy(type => type.ToDisplayString(), StringComparer.Ordinal))
        {
            result.Append("type ")
                .Append(type.DeclaredAccessibility)
                .Append(' ')
                .Append(type.TypeKind)
                .Append(' ')
                .Append(type.ToDisplayString(PublicApiFormat))
                .Append(" static=").Append(type.IsStatic)
                .Append(" abstract=").Append(type.IsAbstract)
                .Append(" sealed=").Append(type.IsSealed)
                .Append(" readonly=").Append(type.IsReadOnly)
                .Append(" ref-like=").Append(type.IsRefLikeType)
                .Append(" enum-underlying=").Append(type.EnumUnderlyingType?.ToDisplayString(PublicApiFormat) ?? "none")
                .Append(" : ")
                .Append(type.BaseType?.ToDisplayString(PublicApiFormat) ?? "none")
                .Append('\n');
            foreach (var contract in type.Interfaces
                         .OrderBy(value => value.ToDisplayString(), StringComparer.Ordinal))
            {
                result.Append("  interface ")
                    .Append(contract.ToDisplayString(PublicApiFormat))
                    .Append('\n');
            }

            AppendDocumentation(result, type, "  ");
            foreach (var member in type.GetMembers()
                         .Where(IsPublicContract)
                         .OrderBy(member => member.Kind)
                         .ThenBy(member => member.ToDisplayString(PublicApiFormat), StringComparer.Ordinal))
            {
                result.Append("  ")
                    .Append(member.Kind)
                    .Append(' ')
                    .Append(member.DeclaredAccessibility)
                    .Append(' ')
                    .Append(member.ToDisplayString(PublicApiFormat));
                if (member is IPropertySymbol property)
                {
                    result.Append(" { get=")
                        .Append(property.GetMethod?.DeclaredAccessibility.ToString() ?? "none")
                        .Append("; set=")
                        .Append(property.SetMethod?.DeclaredAccessibility.ToString() ?? "none")
                        .Append("; init=").Append(property.SetMethod?.IsInitOnly ?? false)
                        .Append("; required=").Append(property.IsRequired)
                        .Append("; }");
                }

                result.Append('\n');
                AppendDocumentation(result, member, "    ");
            }
        }

        return result.ToString();
    }

    /// <summary>Normalizes the approved namespace migration while retaining every other public API spelling.</summary>
    internal static string NormalizeSchemaNamespaceForComparison(string publicApi) => publicApi.Replace(
        "TedToolkit.Step21.Schemas.",
        "TedToolkit.Step21.Generated.",
        StringComparison.Ordinal);

    private static INamespaceSymbol RequiredNamespace(INamespaceSymbol root, params string[] names)
    {
        var current = root;
        foreach (var name in names)
        {
            current = current.GetNamespaceMembers().Single(candidate => candidate.Name == name);
        }

        return current;
    }

    private static IEnumerable<INamedTypeSymbol> EnumeratePublicTypes(INamespaceSymbol generatedNamespace)
    {
        foreach (var type in generatedNamespace.GetTypeMembers().Where(IsPublicContract))
        {
            yield return type;
            foreach (var nested in EnumeratePublicNestedTypes(type))
            {
                yield return nested;
            }
        }
    }

    private static IEnumerable<INamedTypeSymbol> EnumeratePublicNestedTypes(INamedTypeSymbol containingType)
    {
        foreach (var type in containingType.GetTypeMembers().Where(IsPublicContract))
        {
            yield return type;
            foreach (var nested in EnumeratePublicNestedTypes(type))
            {
                yield return nested;
            }
        }
    }

    private static bool IsPublicContract(ISymbol symbol) =>
        symbol.DeclaredAccessibility is Accessibility.Public
            or Accessibility.Protected
            or Accessibility.ProtectedOrInternal;

    private static void AppendDocumentation(StringBuilder result, ISymbol symbol, string indentation)
    {
        var documentation = (symbol.GetDocumentationCommentXml(expandIncludes: true) ?? "")
            .ReplaceLineEndings("\n")
            .Trim();
        if (documentation.Length > 0)
        {
            result.Append(indentation)
                .Append("documentation ")
                .Append(documentation.Replace("\n", " ", StringComparison.Ordinal))
                .Append('\n');
        }
    }
}
