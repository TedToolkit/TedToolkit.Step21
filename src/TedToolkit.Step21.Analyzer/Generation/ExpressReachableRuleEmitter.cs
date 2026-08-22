// -----------------------------------------------------------------------
// <copyright file="ExpressReachableRuleEmitter.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

using TedToolkit.RoslynHelper;
using TedToolkit.RoslynHelper.Syntaxes;

using TedToolkit.Step21.Analyzer.Express;
using TedToolkit.Step21.Analyzer.Express.Binding;

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Emits the validated private constant, function, and derived-attribute rule closure.
/// </summary>
internal static class ExpressReachableRuleEmitter
{
    /// <summary>
    /// Creates private static helpers for every reachable declaration dependency.
    /// </summary>
    /// <param name="plan">The validated reachability plan.</param>
    /// <param name="resolver">The generated value-type resolver.</param>
    /// <returns>The helper methods in deterministic declaration order.</returns>
    /// <exception cref="InvalidOperationException">A reachable plan contains a non-executable declaration kind.</exception>
    internal static IReadOnlyList<Method> CreateDependencyMethods(
        ExpressReachableRulePlan plan,
        ExpressGeneratedTypeResolver resolver)
    {
        var result = new List<Method>();
        foreach (var declaration in plan.ReachableDeclarations)
        {
            if (declaration is not ExpressBoundOpaqueDeclaration opaque
                || opaque.DeclaredType is null)
            {
                continue;
            }

            result.Add(declaration.Kind switch
            {
                ExpressDeclarationKind.Constant => CreateConstantMethod(plan, opaque, resolver),
                ExpressDeclarationKind.Function => CreateFunctionMethod(plan, opaque, resolver),
                _ => throw new InvalidOperationException(
                    $"Reachable declaration '{declaration.Name}' is not a constant or function."),
            });
        }

        result.AddRange(plan.ReachableDerivedAttributes.Select(attribute =>
            CreateDerivedMethod(plan, attribute, resolver)));
        return result;
    }

    /// <summary>
    /// Creates an expression-emission context for a generated validation or helper method.
    /// </summary>
    /// <param name="plan">The validated reachability plan.</param>
    /// <param name="selfExpression">The enclosing EXPRESS SELF expression.</param>
    /// <param name="lexicalNames">Generated spellings for formal or local names.</param>
    /// <returns>The immutable emission context.</returns>
    internal static ExpressExpressionEmissionContext CreateContext(
        ExpressReachableRulePlan plan,
        string? selfExpression,
        IReadOnlyDictionary<string, string>? lexicalNames = null)
    {
        return new(
            reference => ResolveReference(plan, reference, selfExpression, lexicalNames),
            selfExpression,
            resolveAttribute: (reference, source) => ResolveAttribute(plan, reference, source));
    }

    private static Method CreateConstantMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundOpaqueDeclaration declaration,
        ExpressGeneratedTypeResolver resolver)
    {
        var method = CreateMethod(
            ConstantMethodName(declaration.Symbol),
            resolver.Resolve(plan.Schema.Identity, declaration.DeclaredType!).DataType);
        var expression = plan.GetExpression(declaration.Syntax.RequiredChild("expression"));
        var generated = ExpressExpressionEmitter.Emit(expression, CreateContext(plan, selfExpression: null));
        method.AddStatement(new CustomExpression(generated.Code).Return);
        AddSummary(method, $"Evaluates reachable EXPRESS constant {declaration.Name}.");
        return method;
    }

    private static Method CreateFunctionMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundOpaqueDeclaration declaration,
        ExpressGeneratedTypeResolver resolver)
    {
        var method = CreateMethod(
            FunctionMethodName(declaration.Symbol),
            resolver.Resolve(plan.Schema.Identity, declaration.DeclaredType!).DataType);
        var lexicalNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var head = declaration.Syntax.RequiredChild("functionHead");
        foreach (var formal in head.ChildRules("formalParameter"))
        {
            foreach (var parameter in formal.ChildRules("parameterId"))
            {
                var name = parameter.IdentifierToken().Text;
                var boundName = plan.Schema.NameReferences
                    .Select(reference => reference.Target)
                    .Where(candidate => candidate.Kind == ExpressBoundNameKind.Parameter
                        && SameStart(candidate.Span, parameter.Span))
                    .Distinct()
                    .Single();
                var generatedName = ParameterName(name);
                lexicalNames.Add(name, generatedName);
                method.AddParameter(SourceComposer.Parameter(
                    resolver.Resolve(plan.Schema.Identity, boundName.Type!).DataType,
                    generatedName));
            }
        }

        var returnExpression = declaration.Syntax.ChildRules("stmt").Single()
            .RequiredChild("returnStmt")
            .RequiredChild("expression");
        var expression = plan.GetExpression(returnExpression);
        var generated = ExpressExpressionEmitter.Emit(
            expression,
            CreateContext(plan, selfExpression: null, lexicalNames));
        method.AddStatement(new CustomExpression(generated.Code).Return);
        AddSummary(method, $"Evaluates reachable EXPRESS function {declaration.Name}.");
        return method;
    }

    private static Method CreateDerivedMethod(
        ExpressReachableRulePlan plan,
        ExpressBoundAttribute attribute,
        ExpressGeneratedTypeResolver resolver)
    {
        var owner = plan.GetAttributeOwner(attribute);
        var ownerName = ExpressEntityProjection.ToPascalCase(owner.Name);
        var method = CreateMethod(
            DerivedMethodName(owner, attribute),
            resolver.Resolve(plan.Schema.Identity, attribute.Type).DataType);
        method.AddParameter(SourceComposer.Parameter(
            new DataType($"I{ownerName}"),
            "value"));
        var expression = plan.GetDerivedExpression(attribute);
        var generated = ExpressExpressionEmitter.Emit(expression, CreateContext(plan, "value"));
        method.AddStatement(new CustomExpression(generated.Code).Return);
        AddSummary(method, $"Evaluates reachable derived attribute {owner.Name}.{attribute.Name}.");
        return method;
    }

    private static Method CreateMethod(string name, DataType returnType)
    {
        var method = SourceComposer<ExpressIncrementalGenerator>.Method(
            name,
            SourceComposer.ReturnType(returnType));
        method.Accessibility = TedToolkit.RoslynHelper.Accessibility.PRIVATE;
        method.IsStatic = true;
        return method;
    }

    private static string ResolveReference(
        ExpressReachableRulePlan plan,
        ExpressBoundName reference,
        string? selfExpression,
        IReadOnlyDictionary<string, string>? lexicalNames)
    {
        if (lexicalNames is not null
            && lexicalNames.TryGetValue(reference.Name, out var lexicalName))
        {
            return lexicalName;
        }

        if (reference.Attribute is { } attribute)
        {
            if (selfExpression is null)
            {
                throw new InvalidOperationException(
                    $"Unqualified attribute '{reference.Name}' has no enclosing entity value.");
            }

            return ResolveAttribute(plan, reference, selfExpression);
        }

        if (reference.SchemaDeclaration is { } symbol)
        {
            return symbol.Kind switch
            {
                ExpressDeclarationKind.Constant => $"{ConstantMethodName(symbol)}()",
                ExpressDeclarationKind.Function => FunctionMethodName(symbol),
                _ => throw new InvalidOperationException(
                    $"Reachable reference '{reference.Name}' has no private generated evaluator."),
            };
        }

        throw new InvalidOperationException(
            $"Lexical reference '{reference.Name}' has no generated spelling in this operation.");
    }

    private static string ResolveAttribute(
        ExpressReachableRulePlan plan,
        ExpressBoundName reference,
        string source)
    {
        var attribute = reference.Attribute;
        if (attribute?.Kind == ExpressAttributeKind.Derived)
        {
            var owner = plan.GetAttributeOwner(attribute);
            return $"{DerivedMethodName(owner, attribute)}({source})";
        }

        return $"({source}).{ExpressEntityProjection.ToPascalCase(reference.Name)}";
    }

    private static string ConstantMethodName(ExpressBoundSymbol symbol)
    {
        return $"__ExpressConstant_{ExpressEntityProjection.ToPascalCase(symbol.Name)}";
    }

    private static string FunctionMethodName(ExpressBoundSymbol symbol)
    {
        return $"__ExpressFunction_{ExpressEntityProjection.ToPascalCase(symbol.Name)}";
    }

    private static string DerivedMethodName(
        ExpressBoundEntity owner,
        ExpressBoundAttribute attribute)
    {
        return "__ExpressDerived_"
            + ExpressEntityProjection.ToPascalCase(owner.Name)
            + "_"
            + ExpressEntityProjection.ToPascalCase(attribute.Name);
    }

    private static string ParameterName(string name)
    {
        return $"__parameter_{ExpressEntityProjection.ToPascalCase(name)}";
    }

    private static bool SameStart(ExpressSourceSpan left, ExpressSourceSpan right)
    {
        return string.Equals(left.Start.FilePath, right.Start.FilePath, StringComparison.Ordinal)
            && left.Start.Line == right.Start.Line
            && left.Start.Column == right.Start.Column;
    }

    private static void AddSummary(IRootDescription target, string text)
    {
        target.AddRootDescription(new DescriptionSummary(
            new IDescriptionItem[] { new DescriptionText(text), }));
    }
}