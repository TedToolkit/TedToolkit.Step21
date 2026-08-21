// -----------------------------------------------------------------------
// <copyright file="ExpressValueEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;

using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Structurally composes one generated EXPRESS nominal value API.
/// </summary>
internal static class ExpressValueEmitter
{
    /// <summary>
    /// Emits one supported defined-type value.
    /// </summary>
    /// <param name="context">The source-production context.</param>
    /// <param name="projection">The supported value projection.</param>
    internal static void Emit(in SourceProductionContext context, ExpressValueProjection projection)
    {
        var generatedNamespace = $"TedToolkit.Step21.Generated.{ExpressEntityProjection.ToPascalCase(projection.Schema.Name)}";
        var generatedMembers = projection.Declaration.UnderlyingType switch
        {
            ExpressBoundEnumerationType enumeration => CreateEnumeration(projection, enumeration),
            ExpressBoundSelectType select => CreateSelect(projection, select),
            _ => new[] { CreateDefinedValue(projection), },
        };
        var nameSpace = SourceComposer.NameSpace(generatedNamespace);
        foreach (var member in generatedMembers)
        {
            nameSpace.AddMember(member);
        }

        SourceComposer.File()
            .AddNameSpace(nameSpace)
            .Generate(
                in context,
                $"ExpressValue_{projection.Schema.Name.ToUpperInvariant()}_{projection.Declaration.Name.ToUpperInvariant()}");
    }

    private static TypeDeclaration CreateDefinedValue(ExpressValueProjection projection)
    {
        var (valueType, isReferenceType) = projection.Resolver.Resolve(
            projection.Schema.Identity,
            projection.Declaration.UnderlyingType);
        var record = SourceComposer<ExpressIncrementalGenerator>.RecordStruct(projection.Name);
        record.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        record.IsReadonly = true;
        var summary = $"Represents the EXPRESS defined type {projection.Declaration.Name}.";
        if (projection.Declaration.UnderlyingType is ExpressBoundAggregateType aggregate)
        {
            summary += $" Its exact aggregate form is {ExpressTypeDocumentation.Format(aggregate)}."
                + " Mutations do not run schema validation; explicit and boundary validation observe the current candidate.";
        }

        AddSummary(record, summary);
        record.AddMember(CreateValueProperty(valueType, isReferenceType));
        record.AddMember(CreateValueConstructor(projection.Name, valueType, isReferenceType, isPublic: true));
        return record;
    }

    private static IMember[] CreateEnumeration(
        ExpressValueProjection projection,
        ExpressBoundEnumerationType enumeration)
    {
        var record = SourceComposer<ExpressIncrementalGenerator>.RecordStruct(projection.Name);
        record.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        record.IsReadonly = true;
        AddSummary(record, $"Represents the EXPRESS enumeration {projection.Declaration.Name}.");
        record.AddMember(CreateValueProperty(DataType.String, isReferenceType: true));
        record.AddMember(CreateEnumerationConstructor(projection, enumeration.IsExtensible));

        foreach (var symbol in projection.Resolver.GetEnumerationValues(enumeration))
        {
            var property = SourceComposer<ExpressIncrementalGenerator>.Property(
                new DataType(projection.Name),
                ExpressEntityProjection.ToPascalCase(symbol));
            property.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
            property.IsStatic = true;
            property.AddAccessor(SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.GET));
            property.AddDefault(CreateObject(projection.Name, symbol.ToUpperInvariant().ToLiteral()));
            AddSummary(property, $"Gets the {symbol} enumeration value.");
            record.AddMember(property);
        }

        return new IMember[] { record, };
    }

    private static IMember[] CreateSelect(
        ExpressValueProjection projection,
        ExpressBoundSelectType select)
    {
        var alternatives = projection.Resolver.GetSelectAlternatives(select)
            .Select(symbol => new SelectAlternative(
                symbol,
                ExpressEntityProjection.ToPascalCase(symbol.Name),
                projection.Resolver.Resolve(projection.Schema.Identity, symbol)))
            .ToArray();
        var kindName = $"{projection.Name}Kind";
        var kind = SourceComposer<ExpressIncrementalGenerator>.Enum(kindName, DataType.Int);
        kind.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        AddSummary(kind, $"Identifies the selected alternative of {projection.Declaration.Name}.");
        for (var index = 0; index < alternatives.Length; index++)
        {
            var member = SourceComposer<ExpressIncrementalGenerator>.EnumMember(
                alternatives[index].Name,
                index.ToLiteral());
            AddSummary(member, $"The {alternatives[index].Symbol.Name} alternative.");
            kind.AddEnumMember(member);
        }

        var record = SourceComposer<ExpressIncrementalGenerator>.Record(projection.Name);
        record.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        record.Polymorphism = Polymorphism.SEALED;
        AddSummary(record, $"Represents the EXPRESS select {projection.Declaration.Name}.");
        record.AddMember(CreateKindProperty(kindName));
        foreach (var alternative in alternatives)
        {
            record.AddMember(CreateSelectField(alternative));
        }

        foreach (var alternative in alternatives)
        {
            record.AddMember(CreateSelectConstructor(kindName, alternatives, alternative));
            record.AddMember(CreateSelectFactory(projection.Name, alternative));
            record.AddMember(CreateSelectTryGet(kindName, alternative));
        }

        record.AddMember(CreateSelectMatch(projection.Name, kindName, alternatives));
        return new IMember[] { kind, record, };
    }

    private static Property CreateValueProperty(DataType valueType, bool isReferenceType)
    {
        var property = SourceComposer<ExpressIncrementalGenerator>.Property(valueType, "Value");
        property.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        property.AddAccessor(SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.GET));
        if (isReferenceType)
        {
            property.AddAttribute(SourceComposer.Attribute(new DataType(
                "global::System.Diagnostics.CodeAnalysis.NotNullAttribute")));
        }

        AddSummary(property, "Gets the retained strongly typed value.");
        return property;
    }

    private static Constructor CreateValueConstructor(
        string ownerName,
        DataType valueType,
        bool isReferenceType,
        bool isPublic)
    {
        var constructor = SourceComposer<ExpressIncrementalGenerator>.Constructor();
        constructor.Accessibility = isPublic
            ? TedToolkit.RoslynHelper.Accessibility.PUBLIC
            : TedToolkit.RoslynHelper.Accessibility.PRIVATE;
        var parameter = SourceComposer.Parameter(valueType, "value");
        if (isReferenceType)
        {
            parameter.AddAttribute(SourceComposer.Attribute(new DataType(
                "global::System.Diagnostics.CodeAnalysis.DisallowNullAttribute")));
        }

        constructor.AddParameter(parameter);
        constructor.AddStatement("Value".ToSimpleName().Assign("value".ToSimpleName()));
        AddSummary(constructor, $"Initializes a new {ownerName} value.");
        constructor.AddRootDescription(new DescriptionParam(
            "value",
            new IDescriptionItem[] { new DescriptionText("The retained strongly typed value."), }));
        return constructor;
    }

    private static Constructor CreateEnumerationConstructor(
        ExpressValueProjection projection,
        bool isExtensible)
    {
        var constructor = CreateValueConstructor(
            projection.Name,
            DataType.String,
            isReferenceType: true,
            isPublic: isExtensible);
        if (isExtensible)
        {
            var validate = new DataType("global::TedToolkit.Step21.ParameterValue")
                .Type
                .Sub("FromEnumeration")
                .Invoke()
                .AddArgument(SourceComposer.Argument("value".ToSimpleName()));
            constructor.Statements.Insert(0, new Statement(validate));
        }

        return constructor;
    }

    private static Property CreateKindProperty(string kindName)
    {
        var property = SourceComposer<ExpressIncrementalGenerator>.Property(new DataType(kindName), "Kind");
        property.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        property.AddAccessor(SourceComposer<ExpressIncrementalGenerator>.Accessor(AccessorType.GET));
        AddSummary(property, "Gets the selected alternative.");
        return property;
    }

    private static Field CreateSelectField(SelectAlternative alternative)
    {
        var fieldType = alternative.Type.IsReferenceType
            ? new DataType(alternative.Type.DataType.Type).Null
            : alternative.Type.DataType;
        var field = SourceComposer<ExpressIncrementalGenerator>.Field(fieldType, FieldName(alternative));
        field.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
        field.IsReadonly = true;
        return field;
    }

    private static Constructor CreateSelectConstructor(
        string kindName,
        IReadOnlyList<SelectAlternative> alternatives,
        SelectAlternative selected)
    {
        var constructor = SourceComposer<ExpressIncrementalGenerator>.Constructor();
        constructor.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
        var parameterName = ParameterName(selected);
        constructor.AddParameter(SourceComposer.Parameter(selected.Type.DataType, parameterName));
        constructor.AddStatement("Kind".ToSimpleName().Assign($"{kindName}.{selected.Name}".ToSimpleName()));
        foreach (var alternative in alternatives)
        {
            constructor.AddStatement(FieldName(alternative).ToSimpleName().Assign(
                ReferenceEquals(alternative, selected)
                    ? parameterName.ToSimpleName()
                    : "default".ToSimpleName()));
        }

        AddSummary(constructor, $"Initializes the {selected.Symbol.Name} select alternative.");
        constructor.AddRootDescription(new DescriptionParam(
            ParameterDocumentationName(selected),
            new IDescriptionItem[] { new DescriptionText($"The {selected.Symbol.Name} value."), }));
        return constructor;
    }

    private static Method CreateSelectFactory(string ownerName, SelectAlternative alternative)
    {
        var method = SourceComposer<ExpressIncrementalGenerator>.Method(
            $"From{alternative.Name}",
            SourceComposer.ReturnType(new DataType(ownerName)));
        method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        method.IsStatic = true;
        var parameterName = ParameterName(alternative);
        method.AddParameter(SourceComposer.Parameter(alternative.Type.DataType, parameterName));
        if (alternative.Type.IsReferenceType)
        {
            method.AddStatement(CreateThrowIfNull(parameterName));
        }

        method.AddStatement(CreateObject(ownerName, parameterName.ToSimpleName()).Return);
        AddSummary(method, $"Creates the {alternative.Symbol.Name} select alternative.");
        method.AddRootDescription(new DescriptionParam(
            ParameterDocumentationName(alternative),
            new IDescriptionItem[] { new DescriptionText($"The {alternative.Symbol.Name} value."), }));
        method.AddRootDescription(new DescriptionReturns(
            new IDescriptionItem[] { new DescriptionText("The selected value."), }));
        return method;
    }

    private static Method CreateSelectTryGet(string kindName, SelectAlternative alternative)
    {
        var method = SourceComposer<ExpressIncrementalGenerator>.Method(
            $"TryGet{alternative.Name}",
            SourceComposer.ReturnType(DataType.Bool));
        method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        var outputType = alternative.Type.IsReferenceType
            ? new DataType(alternative.Type.DataType.Type).Null
            : alternative.Type.DataType;
        var parameter = SourceComposer.Parameter(new DataType(outputType.Type).Out, "value");
        if (alternative.Type.IsReferenceType)
        {
            parameter.AddAttribute(SourceComposer.Attribute(new DataType(
                "global::System.Diagnostics.CodeAnalysis.NotNullWhenAttribute"))
                .AddArgument(SourceComposer.Argument(true.ToLiteral())));
        }

        method.AddParameter(parameter);
        var whenSelected = new IfStatement(
            "Kind".ToSimpleName().EqualTo($"{kindName}.{alternative.Name}".ToSimpleName()))
            .AddStatement("value".ToSimpleName().Assign(FieldName(alternative).ToSimpleName()))
            .AddStatement(true.ToLiteral().Return);
        method.AddStatement(whenSelected);
        method.AddStatement("value".ToSimpleName().Assign("default".ToSimpleName()));
        method.AddStatement(false.ToLiteral().Return);
        AddSummary(method, $"Attempts to obtain the {alternative.Symbol.Name} alternative.");
        method.AddRootDescription(new DescriptionParam(
            "value",
            new IDescriptionItem[] { new DescriptionText("The selected value, or default for another alternative."), }));
        method.AddRootDescription(new DescriptionReturns(
            new IDescriptionItem[] { new DescriptionText("True when the requested alternative is selected."), }));
        return method;
    }

    private static Method CreateSelectMatch(
        string ownerName,
        string kindName,
        IReadOnlyList<SelectAlternative> alternatives)
    {
        var method = SourceComposer<ExpressIncrementalGenerator>.Method(
            "Match",
            SourceComposer.ReturnType(new DataType("TResult")));
        method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PUBLIC;
        method.AddTypeParameter(SourceComposer.TypeParameter("TResult"));
        foreach (var alternative in alternatives)
        {
            var parameterName = ParameterName(alternative);
            var functionType = new DataType("global::System.Func")
                .Generic(alternative.Type.DataType, new DataType("TResult"));
            method.AddParameter(SourceComposer.Parameter(functionType, parameterName));
            method.AddStatement(CreateThrowIfNull(parameterName));
            method.AddRootDescription(new DescriptionParam(
                ParameterDocumentationName(alternative),
                new IDescriptionItem[] { new DescriptionText($"Handles the {alternative.Symbol.Name} alternative."), }));
        }

        foreach (var alternative in alternatives)
        {
            var invoke = ParameterName(alternative).ToSimpleName()
                .Invoke()
                .AddArgument(SourceComposer.Argument(FieldName(alternative).ToSimpleName()));
            method.AddStatement(new IfStatement(
                    "Kind".ToSimpleName().EqualTo($"{kindName}.{alternative.Name}".ToSimpleName()))
                .AddStatement(invoke.Return));
        }

        var exception = new DataType("global::System.InvalidOperationException").New;
        method.AddStatement(exception.Throw);
        AddSummary(method, $"Exhaustively matches the selected {ownerName} alternative.");
        method.AddRootDescription(new DescriptionReturns(
            new IDescriptionItem[] { new DescriptionText("The result returned by the selected handler."), }));
        return method;
    }

    private static Statement CreateThrowIfNull(string parameterName)
    {
        var invoke = new DataType("global::System.ArgumentNullException")
            .Type
            .Sub("ThrowIfNull")
            .Invoke()
            .AddArgument(SourceComposer.Argument(parameterName.ToSimpleName()));
        return new(invoke);
    }

    private static ObjectCreationExpression CreateObject(string typeName, IExpression argument)
    {
        return new DataType(typeName).New
            .AddArgument(SourceComposer.Argument(argument));
    }

    private static string FieldName(SelectAlternative alternative)
    {
        return $"_{LowerFirst(alternative.Name)}";
    }

    private static string ParameterName(SelectAlternative alternative)
    {
        return EscapeIdentifier(ParameterDocumentationName(alternative));
    }

    private static string ParameterDocumentationName(SelectAlternative alternative)
    {
        return LowerFirst(alternative.Name);
    }

    private static string LowerFirst(string value)
    {
        return char.ToLowerInvariant(value[0]) + value.Substring(1);
    }

    private static string EscapeIdentifier(string identifier)
    {
        return SyntaxFacts.GetKeywordKind(identifier) == SyntaxKind.None
            ? identifier
            : $"@{identifier}";
    }

    private static void AddSummary(IRootDescription target, string text)
    {
        target.AddRootDescription(new DescriptionSummary(
            new IDescriptionItem[] { new DescriptionText(text), }));
    }

    private sealed class SelectAlternative
    {
        /// <summary>
        /// Initializes one resolved generated SELECT alternative.
        /// </summary>
        /// <param name="symbol">The bound alternative declaration.</param>
        /// <param name="name">The generated alternative suffix.</param>
        /// <param name="type">The generated value type and its reference-type classification.</param>
        public SelectAlternative(
            ExpressBoundSymbol symbol,
            string name,
            (DataType DataType, bool IsReferenceType) type)
        {
            Symbol = symbol;
            Name = name;
            Type = type;
        }

        /// <summary>
        /// Gets the bound alternative declaration.
        /// </summary>
        public ExpressBoundSymbol Symbol { get; }

        /// <summary>
        /// Gets the generated alternative suffix.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the generated value type and its reference-type classification.
        /// </summary>
        public (DataType DataType, bool IsReferenceType) Type { get; }
    }
}