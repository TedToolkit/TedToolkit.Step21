// -----------------------------------------------------------------------
// <copyright file="ExpressSchemaCompiler.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Express.Binding;

/// <summary>
/// Parses and binds exactly the supplied EXPRESS sources as one deterministic closed universe.
/// </summary>
internal static class ExpressSchemaCompiler
{
    private static readonly StringComparer _nameComparer = StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// Compiles the supplied sources without reading their logical paths or consulting external state.
    /// </summary>
    /// <param name="sources">The complete closed source set.</param>
    /// <returns>The valid independent schemas and complete deterministic diagnostics.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="sources"/> or one of its elements is null.</exception>
    internal static ExpressSchemaCompilation Compile(IEnumerable<ExpressSchemaSource> sources)
    {
        if (sources is null)
        {
            throw new ArgumentNullException(nameof(sources));
        }

        var orderedSources = sources
            .Select(source => source ?? throw new ArgumentNullException(nameof(sources)))
            .OrderBy(source => source.FilePath, StringComparer.Ordinal)
            .ThenBy(source => source.Text, StringComparer.Ordinal)
            .ToArray();
        var syntaxDiagnostics = new List<ExpressSyntaxDiagnostic>();
        var schemas = new List<SchemaDraft>();
        foreach (var source in orderedSources)
        {
            var parseResult = ExpressSyntaxParser.Parse(source.FilePath, source.Text);
            syntaxDiagnostics.AddRange(parseResult.Diagnostics);
            if (parseResult.Root is null)
            {
                continue;
            }

            schemas.AddRange(parseResult.Root.ChildRules("schemaDecl").Select(CreateSchema));
        }

        var bindingDiagnostics = new List<ExpressBindingDiagnostic>();
        MarkDuplicateSchemas(schemas, bindingDiagnostics);
        foreach (var schema in schemas)
        {
            CollectDeclarations(schema, bindingDiagnostics);
            CollectImports(schema);
        }

        var resolver = new Resolver(schemas, bindingDiagnostics);
        resolver.ResolveAll();
        resolver.ValidateCycles();
        var boundSchemas = schemas
            .Where(schema => !schema.IsInvalid)
            .OrderBy(SchemaSortKey, StringComparer.OrdinalIgnoreCase)
            .Select(CreateBoundSchema)
            .ToArray();

        return new(
            boundSchemas,
            OrderSyntaxDiagnostics(syntaxDiagnostics),
            OrderBindingDiagnostics(bindingDiagnostics));
    }

    private static SchemaDraft CreateSchema(ExpressRuleSyntax syntax)
    {
        var nameRule = syntax.RequiredChild("schemaId");
        var nameToken = nameRule.IdentifierToken();
        return new(
            new ExpressBoundSchemaIdentity(nameToken.Text, syntax.Span),
            syntax,
            syntax.RequiredChild("schemaBody"),
            nameToken);
    }

    private static void MarkDuplicateSchemas(
        IEnumerable<SchemaDraft> schemas,
        ICollection<ExpressBindingDiagnostic> diagnostics)
    {
        foreach (var duplicateGroup in schemas.GroupBy(schema => schema.Identity.Name, _nameComparer)
                     .Where(group => group.Count() > 1))
        {
            foreach (var schema in duplicateGroup)
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    diagnostics,
                    "EXPRESS-BIND-DUPLICATE-SCHEMA",
                    $"Schema '{schema.Identity.Name}' is declared more than once in the closed input set.",
                    schema.NameToken.Span.Start);
            }
        }
    }

    private static void CollectDeclarations(
        SchemaDraft schema,
        ICollection<ExpressBindingDiagnostic> diagnostics)
    {
        foreach (var child in schema.Body.ChildRules())
        {
            if (child.Production == "constantDecl")
            {
                foreach (var constantBody in child.ChildRules("constantBody"))
                {
                    AddDeclaration(schema, constantBody, ExpressDeclarationKind.Constant);
                }

                continue;
            }

            if (child.Production == "ruleDecl")
            {
                AddDeclaration(schema, child, ExpressDeclarationKind.Rule);
                continue;
            }

            if (child.Production != "declaration")
            {
                continue;
            }

            var declaration = child.ChildRules().Single();
            AddDeclaration(schema, declaration, GetDeclarationKind(declaration.Production));
        }

        foreach (var duplicateGroup in schema.Declarations.GroupBy(declaration => declaration.Name, _nameComparer)
                     .Where(group => group.Count() > 1))
        {
            schema.IsInvalid = true;
            foreach (var declaration in duplicateGroup)
            {
                AddDiagnostic(
                    diagnostics,
                    "EXPRESS-BIND-DUPLICATE-DECLARATION",
                    $"Declaration '{declaration.Name}' occurs more than once in schema '{schema.Identity.Name}'.",
                    declaration.NameToken.Span.Start);
            }
        }

        foreach (var declaration in schema.Declarations
                     .GroupBy(item => item.Name, _nameComparer)
                     .Where(group => group.Count() == 1)
                     .Select(group => group.Single()))
        {
            schema.LocalSymbols.Add(declaration.Name, declaration);
        }
    }

    private static void AddDeclaration(
        SchemaDraft schema,
        ExpressRuleSyntax syntax,
        ExpressDeclarationKind kind)
    {
        var nameRule = FindDeclarationNameRule(syntax, kind);
        var nameToken = nameRule.IdentifierToken();
        var symbol = new ExpressBoundSymbol(nameToken.Text, kind, schema.Identity, syntax.Span);
        schema.Declarations.Add(new SymbolDraft(symbol, syntax, nameToken));
    }

    private static ExpressRuleSyntax FindDeclarationNameRule(
        ExpressRuleSyntax syntax,
        ExpressDeclarationKind kind)
    {
        return kind switch
        {
            ExpressDeclarationKind.Entity => syntax.RequiredChild("entityHead").RequiredChild("entityId"),
            ExpressDeclarationKind.Type => syntax.RequiredChild("typeId"),
            ExpressDeclarationKind.Constant => syntax.RequiredChild("constantId"),
            ExpressDeclarationKind.Function => syntax.RequiredChild("functionHead").RequiredChild("functionId"),
            ExpressDeclarationKind.Procedure => syntax.RequiredChild("procedureHead").RequiredChild("procedureId"),
            ExpressDeclarationKind.Rule => syntax.RequiredChild("ruleHead").RequiredChild("ruleId"),
            ExpressDeclarationKind.SubtypeConstraint => syntax.RequiredChild("subtypeConstraintHead")
                .RequiredChild("subtypeConstraintId"),
            _ => throw new InvalidOperationException($"Unsupported declaration kind '{kind.ToString()}'."),
        };
    }

    private static ExpressDeclarationKind GetDeclarationKind(string production)
    {
        return production switch
        {
            "entityDecl" => ExpressDeclarationKind.Entity,
            "typeDecl" => ExpressDeclarationKind.Type,
            "functionDecl" => ExpressDeclarationKind.Function,
            "procedureDecl" => ExpressDeclarationKind.Procedure,
            "subtypeConstraintDecl" => ExpressDeclarationKind.SubtypeConstraint,
            _ => throw new InvalidOperationException($"Unsupported declaration production '{production}'."),
        };
    }

    private static void CollectImports(SchemaDraft schema)
    {
        foreach (var specification in schema.Body.ChildRules("interfaceSpecification"))
        {
            var clause = specification.ChildRules().Single();
            var kind = clause.Production == "useClause"
                ? ExpressImportKind.Use
                : ExpressImportKind.Reference;
            var schemaNameToken = clause.RequiredChild("schemaRef").IdentifierToken();
            var itemProduction = kind == ExpressImportKind.Use
                ? "namedTypeOrRename"
                : "resourceOrRename";
            var items = clause.ChildRules(itemProduction)
                .Select(CreateImportItem)
                .ToArray();
            schema.Imports.Add(new ImportDraft(kind, schemaNameToken, items, clause.Span));
        }
    }

    private static ImportItemDraft CreateImportItem(ExpressRuleSyntax syntax)
    {
        var identifiers = syntax.DescendantTokens()
            .Where(token => token.TokenName == "SimpleId")
            .ToArray();
        var source = identifiers[0];
        var local = syntax.HasToken("AS") ? identifiers[identifiers.Length - 1] : source;
        return new(source, local, syntax.Span);
    }

    private static ExpressBoundSchema CreateBoundSchema(SchemaDraft schema)
    {
        var declarations = schema.Declarations.Select(CreateBoundDeclaration).ToArray();
        var nestedDeclarations = schema.NestedDeclarations
            .OrderBy(declaration => declaration.Syntax.Span.Start.Line)
            .ThenBy(declaration => declaration.Syntax.Span.Start.Column)
            .Select(CreateBoundDeclaration)
            .ToArray();
        var expressions = ExpressExpressionBinder.Bind(
            declarations,
            schema.NameReferences);
        return new(
            schema.Identity,
            schema.ResolvedImports,
            declarations,
            nestedDeclarations,
            schema.NameReferences,
            expressions);
    }

    private static ExpressBoundDeclaration CreateBoundDeclaration(SymbolDraft declaration)
    {
        if (declaration.Symbol.Kind == ExpressDeclarationKind.Entity)
        {
            return new ExpressBoundEntity(
                declaration.Symbol,
                declaration.Syntax,
                declaration.IsAbstract,
                declaration.Supertypes,
                declaration.Attributes);
        }

        if (declaration.Symbol.Kind == ExpressDeclarationKind.Type)
        {
            return new ExpressBoundDefinedType(
                declaration.Symbol,
                declaration.Syntax,
                declaration.BoundType
                ?? throw new InvalidOperationException("A valid type declaration must have a bound type."));
        }

        return new ExpressBoundOpaqueDeclaration(
            declaration.Symbol,
            declaration.Syntax,
            declaration.BoundType);
    }

    private static IEnumerable<ExpressSyntaxDiagnostic> OrderSyntaxDiagnostics(
        IEnumerable<ExpressSyntaxDiagnostic> diagnostics)
    {
        return diagnostics
            .OrderBy(diagnostic => diagnostic.SourceLocation.FilePath, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.SourceLocation.Line)
            .ThenBy(diagnostic => diagnostic.SourceLocation.Column)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal);
    }

    private static IEnumerable<ExpressBindingDiagnostic> OrderBindingDiagnostics(
        IEnumerable<ExpressBindingDiagnostic> diagnostics)
    {
        return diagnostics
            .OrderBy(diagnostic => diagnostic.SourceLocation.FilePath, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.SourceLocation.Line)
            .ThenBy(diagnostic => diagnostic.SourceLocation.Column)
            .ThenBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
            .ThenBy(diagnostic => diagnostic.Message, StringComparer.Ordinal);
    }

    private static string SchemaSortKey(SchemaDraft schema)
    {
        return $"{schema.Identity.Name}\0{schema.NameToken.Span.Start.FilePath}\0"
            + $"{schema.NameToken.Span.Start.Line:D10}\0{schema.NameToken.Span.Start.Column:D10}";
    }

    private static void AddDiagnostic(
        ICollection<ExpressBindingDiagnostic> diagnostics,
        string code,
        string message,
        ExpressSourceLocation location)
    {
        diagnostics.Add(new ExpressBindingDiagnostic(code, message, location));
    }

    private sealed class Resolver
    {
        private readonly IReadOnlyDictionary<string, SchemaDraft> _uniqueSchemas;

        private readonly Dictionary<string, IReadOnlyList<SchemaDraft>> _schemaGroups;

        private readonly ICollection<ExpressBindingDiagnostic> _diagnostics;

        internal Resolver(
            IEnumerable<SchemaDraft> schemas,
            ICollection<ExpressBindingDiagnostic> diagnostics)
        {
            var groups = schemas
                .GroupBy(schema => schema.Identity.Name, _nameComparer)
                .ToDictionary(
                    group => group.Key,
                    group => (IReadOnlyList<SchemaDraft>)group.ToArray(),
                    _nameComparer);
            _schemaGroups = groups;
            _uniqueSchemas = groups
                .Where(pair => pair.Value.Count == 1)
                .ToDictionary(pair => pair.Key, pair => pair.Value[0], _nameComparer);
            _diagnostics = diagnostics;
        }

        internal void ResolveAll()
        {
            InitializeExports();
            ExpandUseExports();
            foreach (var schema in _uniqueSchemas.Values.OrderBy(SchemaSortKey, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var import in schema.Imports)
                {
                    ResolveImport(schema, import);
                }
            }

            foreach (var schema in _uniqueSchemas.Values.OrderBy(SchemaSortKey, StringComparer.OrdinalIgnoreCase))
            {
                BindDeclarations(schema);
            }

            foreach (var schema in _uniqueSchemas.Values
                         .Where(candidate => !candidate.IsInvalid)
                         .OrderBy(SchemaSortKey, StringComparer.OrdinalIgnoreCase))
            {
                BindNameReferences(schema);
            }
        }

        private void InitializeExports()
        {
            foreach (var schema in _uniqueSchemas.Values)
            {
                foreach (var local in schema.LocalSymbols)
                {
                    schema.LocalAndUseSymbols[local.Key] = local.Value.Symbol;
                    if (IsNamedType(local.Value.Symbol.Kind))
                    {
                        schema.ExportedNamedTypes[local.Key] = local.Value.Symbol;
                    }

                    if (IsResource(local.Value.Symbol.Kind))
                    {
                        schema.ExportedResources[local.Key] = local.Value.Symbol;
                    }
                }
            }
        }

        private void ExpandUseExports()
        {
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var schema in _uniqueSchemas.Values.OrderBy(SchemaSortKey, StringComparer.OrdinalIgnoreCase))
                {
                    foreach (var import in schema.Imports.Where(item => item.Kind == ExpressImportKind.Use))
                    {
                        if (!_uniqueSchemas.TryGetValue(import.SchemaNameToken.Text, out var target))
                        {
                            continue;
                        }

                        if (import.Items.Count == 0)
                        {
                            foreach (var pair in target.ExportedNamedTypes)
                            {
                                changed |= TryAddExport(schema, pair.Key, pair.Value);
                            }

                            continue;
                        }

                        foreach (var item in import.Items)
                        {
                            if (target.ExportedNamedTypes.TryGetValue(item.SourceToken.Text, out var declaration))
                            {
                                changed |= TryAddExport(schema, item.LocalToken.Text, declaration);
                            }
                        }
                    }
                }
            }
        }

        private static bool TryAddExport(
            SchemaDraft schema,
            string localName,
            ExpressBoundSymbol declaration)
        {
            if (schema.ExportedNamedTypes.ContainsKey(localName))
            {
                return false;
            }

            schema.ExportedNamedTypes[localName] = declaration;
            schema.ExportedResources[localName] = declaration;
            return true;
        }

        internal void ValidateCycles()
        {
            ValidateConstructedTypeBases();
            ValidateTypeCycles();
            ValidateInheritanceCycles();
            PropagateNewInvalidity();
        }

        private void ResolveImport(SchemaDraft schema, ImportDraft import)
        {
            var targetName = import.SchemaNameToken.Text;
            if (!_schemaGroups.TryGetValue(targetName, out var targetGroup))
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-MISSING-SCHEMA",
                    $"Imported schema '{targetName}' is not present in the closed input set.",
                    import.SchemaNameToken.Span.Start);
                foreach (var item in import.Items)
                {
                    AddMissingImport(schema, import, item);
                }

                return;
            }

            if (targetGroup.Count != 1 || !_uniqueSchemas.TryGetValue(targetName, out var target))
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-AMBIGUOUS-SCHEMA",
                    $"Imported schema name '{targetName}' has multiple declarations in the closed input set.",
                    import.SchemaNameToken.Span.Start);
                return;
            }

            var available = import.Kind == ExpressImportKind.Use
                ? target.ExportedNamedTypes
                : target.ExportedResources;
            if (import.Items.Count == 0)
            {
                foreach (var pair in available.OrderBy(pair => pair.Key, _nameComparer))
                {
                    AddResolvedImport(schema, import, pair.Key, pair.Value, import.Span);
                }

                return;
            }

            foreach (var item in import.Items)
            {
                if (!available.TryGetValue(item.SourceToken.Text, out var declaration))
                {
                    schema.IsInvalid = true;
                    AddMissingImport(schema, import, item);
                    continue;
                }

                AddResolvedImport(schema, import, item.LocalToken.Text, declaration, item.Span);
            }
        }

        private void AddMissingImport(SchemaDraft schema, ImportDraft import, ImportItemDraft item)
        {
            schema.IsInvalid = true;
            AddDiagnostic(
                _diagnostics,
                "EXPRESS-BIND-MISSING-IMPORT",
                $"Schema '{import.SchemaNameToken.Text}' does not supply resource '{item.SourceToken.Text}' for {import.Kind.ToString().ToUpperInvariant()} FROM.",
                item.SourceToken.Span.Start);
        }

        private void AddResolvedImport(
            SchemaDraft schema,
            ImportDraft import,
            string localName,
            ExpressBoundSymbol declaration,
            ExpressSourceSpan span)
        {
            var destination = import.Kind == ExpressImportKind.Use
                ? schema.LocalAndUseSymbols
                : schema.ReferenceSymbols;
            if (destination.TryGetValue(localName, out var existing)
                && !ReferenceEquals(existing, declaration))
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-CONFLICTING-NAME",
                    $"Imported name '{localName}' resolves to conflicting declarations in schema '{schema.Identity.Name}'.",
                    span.Start);
                return;
            }

            if (import.Kind == ExpressImportKind.Use
                && schema.LocalSymbols.TryGetValue(localName, out var local)
                && !ReferenceEquals(local.Symbol, declaration))
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-CONFLICTING-NAME",
                    $"USE name '{localName}' conflicts with a local declaration in schema '{schema.Identity.Name}'.",
                    span.Start);
                return;
            }

            destination[localName] = declaration;
            schema.ResolvedImports.Add(new ExpressBoundImport(import.Kind, localName, declaration, span));
        }

        private void BindDeclarations(SchemaDraft schema)
        {
            foreach (var declaration in schema.Declarations)
            {
                switch (declaration.Symbol.Kind)
                {
                    case ExpressDeclarationKind.Entity:
                        BindEntity(schema, declaration);
                        break;

                    case ExpressDeclarationKind.Type:
                        declaration.BoundType = BindType(
                            schema,
                            declaration.Syntax.RequiredChild("underlyingType"));
                        break;

                    case ExpressDeclarationKind.Constant:
                        declaration.BoundType = BindType(
                            schema,
                            declaration.Syntax.RequiredChild("instantiableType"));
                        break;

                    case ExpressDeclarationKind.Function:
                        declaration.BoundType = BindType(
                            schema,
                            declaration.Syntax.RequiredChild("functionHead").RequiredChild("parameterType"));
                        BindAlgorithmStaticTypes(schema, declaration.Syntax, parentScope: null);
                        break;

                    case ExpressDeclarationKind.Procedure:
                        BindAlgorithmStaticTypes(schema, declaration.Syntax, parentScope: null);
                        break;

                    case ExpressDeclarationKind.Rule:
                        BindRequiredEntityReferences(
                            schema,
                            declaration.Syntax.RequiredChild("ruleHead"));
                        BindAlgorithmStaticTypes(schema, declaration.Syntax, parentScope: null);
                        break;

                    case ExpressDeclarationKind.SubtypeConstraint:
                        BindRequiredEntityReferences(schema, declaration.Syntax);
                        break;

                    default:
                        BindContainedTypes(schema, declaration.Syntax);
                        break;
                }
            }
        }

        private void BindAlgorithmStaticTypes(
            SchemaDraft schema,
            ExpressRuleSyntax declaration,
            NameScope? parentScope)
        {
            var scope = new NameScope(parentScope ?? CreateTypeScope(schema));
            var algorithmHead = declaration.RequiredChild("algorithmHead");
            var nestedDeclarations = algorithmHead.ChildRules("declaration")
                .Select(container => container.ChildRules().Single())
                .Select(nested =>
                {
                    var kind = GetDeclarationKind(nested.Production);
                    var token = FindDeclarationNameRule(nested, kind).IdentifierToken();
                    return new SymbolDraft(
                        new ExpressBoundSymbol(token.Text, kind, schema.Identity, nested.Span),
                        nested,
                        token);
                })
                .ToArray();
            schema.NestedDeclarations.AddRange(nestedDeclarations);
            foreach (var nested in nestedDeclarations.Where(item => IsNamedType(item.Symbol.Kind)))
            {
                scope.TryAdd(CreateSchemaName(nested.Symbol));
            }

            foreach (var type in declaration.ChildRules().First().ChildRules("formalParameter")
                         .Select(formal => formal.RequiredChild("parameterType")))
            {
                BindType(schema, type, scope);
            }

            foreach (var child in algorithmHead.ChildRules())
            {
                if (child.Production == "declaration")
                {
                    var nested = schema.NestedDeclarations.Single(item =>
                        ReferenceEquals(item.Syntax, child.ChildRules().Single()));
                    BindNestedDeclarationTypes(schema, nested, scope);
                }
                else
                {
                    BindContainedTypes(schema, child, scope);
                }
            }
        }

        private NameScope CreateTypeScope(SchemaDraft schema)
        {
            var scope = new NameScope(parent: null);
            foreach (var pair in schema.LocalAndUseSymbols.Where(pair => IsNamedType(pair.Value.Kind)))
            {
                scope.TryAdd(CreateSchemaName(pair.Value, pair.Key));
            }

            foreach (var pair in schema.ReferenceSymbols.Where(pair => IsNamedType(pair.Value.Kind)))
            {
                scope.TryAdd(CreateSchemaName(pair.Value, pair.Key));
            }

            return scope;
        }

        private void BindNestedDeclarationTypes(
            SchemaDraft schema,
            SymbolDraft declaration,
            NameScope scope)
        {
            switch (declaration.Syntax.Production)
            {
                case "functionDecl":
                    declaration.BoundType = BindType(
                        schema,
                        declaration.Syntax.RequiredChild("functionHead").RequiredChild("parameterType"),
                        scope);
                    BindAlgorithmStaticTypes(schema, declaration.Syntax, scope);
                    break;

                case "procedureDecl":
                    BindAlgorithmStaticTypes(schema, declaration.Syntax, scope);
                    break;

                case "ruleDecl":
                    BindRequiredEntityReferences(
                        schema,
                        declaration.Syntax.RequiredChild("ruleHead"),
                        scope);
                    BindAlgorithmStaticTypes(schema, declaration.Syntax, scope);
                    break;

                case "typeDecl":
                    declaration.BoundType = BindType(
                        schema,
                        declaration.Syntax.RequiredChild("underlyingType"),
                        scope);
                    break;

                case "entityDecl":
                    BindEntity(schema, declaration, scope);
                    break;

                case "subtypeConstraintDecl":
                    BindRequiredEntityReferences(schema, declaration.Syntax, scope);
                    break;
            }
        }

        private void BindNameReferences(SchemaDraft schema)
        {
            var schemaScope = new NameScope(parent: null);
            foreach (var symbol in schema.LocalAndUseSymbols.Where(pair => IsResource(pair.Value.Kind)))
            {
                schemaScope.TryAdd(CreateSchemaName(symbol.Value, symbol.Key));
            }

            foreach (var symbol in schema.ReferenceSymbols.Where(pair => IsResource(pair.Value.Kind)))
            {
                schemaScope.TryAdd(CreateSchemaName(symbol.Value, symbol.Key));
            }

            foreach (var declaration in schema.Declarations)
            {
                BindDeclarationNames(schema, declaration.Syntax, schemaScope);
            }

            schema.NameReferences.Sort(static (left, right) =>
            {
                var line = left.Span.Start.Line.CompareTo(right.Span.Start.Line);
                return line != 0
                    ? line
                    : left.Span.Start.Column.CompareTo(right.Span.Start.Column);
            });
        }

        private ExpressBoundName CreateSchemaName(ExpressBoundSymbol symbol, string? visibleName = null)
        {
            var draft = FindSymbol(symbol);
            var type = symbol.Kind switch
            {
                ExpressDeclarationKind.Entity or ExpressDeclarationKind.Type =>
                    new ExpressBoundNamedType(symbol, symbol.Span),
                ExpressDeclarationKind.Constant or ExpressDeclarationKind.Function => draft?.BoundType,
                _ => null,
            };
            return new(
                visibleName ?? symbol.Name,
                symbol.Kind switch
                {
                    ExpressDeclarationKind.Entity => ExpressBoundNameKind.Entity,
                    ExpressDeclarationKind.Type => ExpressBoundNameKind.Type,
                    ExpressDeclarationKind.Constant => ExpressBoundNameKind.Constant,
                    ExpressDeclarationKind.Function => ExpressBoundNameKind.Function,
                    ExpressDeclarationKind.Procedure => ExpressBoundNameKind.Procedure,
                    _ => throw new InvalidOperationException(
                        $"Declaration kind '{symbol.Kind.ToString()}' is not an expression-visible resource."),
                },
                type,
                symbol,
                symbol.Span);
        }

        private void BindDeclarationNames(
            SchemaDraft schema,
            ExpressRuleSyntax declaration,
            NameScope parentScope)
        {
            switch (declaration.Production)
            {
                case "entityDecl":
                    BindEntityNames(schema, declaration, parentScope);
                    break;

                case "functionDecl":
                case "procedureDecl":
                case "ruleDecl":
                    BindAlgorithmNames(schema, declaration, parentScope);
                    break;

                case "typeDecl":
                    BindTypeDeclarationNames(schema, declaration, parentScope);
                    break;

                default:
                    VisitNames(schema, declaration, parentScope);
                    break;
            }
        }

        private void BindEntityNames(
            SchemaDraft schema,
            ExpressRuleSyntax declaration,
            NameScope parentScope)
        {
            var scope = new NameScope(parentScope);
            var draft = schema.Declarations.Concat(schema.NestedDeclarations)
                .Single(candidate => ReferenceEquals(candidate.Syntax, declaration));
            foreach (var attribute in EnumerateAttributes(draft, new HashSet<ExpressBoundSymbol>()))
            {
                scope.TryAdd(
                    new ExpressBoundName(
                        attribute.Name,
                        ExpressBoundNameKind.Attribute,
                        attribute.Type,
                        schemaDeclaration: null,
                        attribute.Span,
                        attribute.IsOptional,
                        attribute));
            }

            VisitNames(schema, declaration.RequiredChild("entityBody"), scope);
        }

        private void BindTypeDeclarationNames(
            SchemaDraft schema,
            ExpressRuleSyntax declaration,
            NameScope parentScope)
        {
            var scope = new NameScope(parentScope);
            var typeToken = declaration.RequiredChild("typeId").IdentifierToken();
            var typeSymbol = ResolveVisible(schema, typeToken.Text);
            var enumeration = declaration.RequiredChild("underlyingType").DescendantsAndSelf()
                .FirstOrDefault(node => node.Production == "enumerationType");
            if (enumeration is not null)
            {
                foreach (var item in enumeration.DescendantsAndSelf()
                             .Where(node => node.Production == "enumerationId"))
                {
                    var token = item.IdentifierToken();
                    AddLexicalName(
                        schema,
                        scope,
                        new ExpressBoundName(
                            token.Text,
                            ExpressBoundNameKind.Enumeration,
                            typeSymbol is null ? null : new ExpressBoundNamedType(typeSymbol, item.Span),
                            schemaDeclaration: null,
                            item.Span));
                }
            }

            VisitNames(schema, declaration.RequiredChild("underlyingType"), scope);

            foreach (var whereClause in declaration.ChildRules("whereClause"))
            {
                VisitNames(schema, whereClause, scope);
            }
        }

        private void BindAlgorithmNames(
            SchemaDraft schema,
            ExpressRuleSyntax declaration,
            NameScope parentScope)
        {
            var scope = new NameScope(parentScope);
            var head = declaration.ChildRules().First();
            if (declaration.Production == "ruleDecl")
            {
                AddRulePopulationNames(schema, head, scope);
            }

            var algorithmHead = declaration.RequiredChild("algorithmHead");
            AddAlgorithmDeclarations(schema, algorithmHead, scope);
            foreach (var formal in head.ChildRules("formalParameter"))
            {
                var type = BindType(schema, formal.RequiredChild("parameterType"), scope);
                foreach (var parameter in formal.ChildRules("parameterId"))
                {
                    var token = parameter.IdentifierToken();
                    AddLexicalName(
                        schema,
                        scope,
                        new ExpressBoundName(
                            token.Text,
                            ExpressBoundNameKind.Parameter,
                            type,
                            schemaDeclaration: null,
                            parameter.Span));
                }
            }

            foreach (var child in algorithmHead.ChildRules())
            {
                if (child.Production == "declaration")
                {
                    BindDeclarationNames(schema, child.ChildRules().Single(), scope);
                }
                else
                {
                    VisitNames(schema, child, scope);
                }
            }

            foreach (var child in declaration.ChildRules()
                         .Where(child => !ReferenceEquals(child, head) && !ReferenceEquals(child, algorithmHead)))
            {
                VisitNames(schema, child, scope);
            }
        }

        private void AddRulePopulationNames(
            SchemaDraft schema,
            ExpressRuleSyntax ruleHead,
            NameScope scope)
        {
            foreach (var entityReference in ruleHead.ChildRules("entityRef"))
            {
                var token = entityReference.IdentifierToken();
                var symbol = ResolveVisible(schema, token.Text);
                if (symbol?.Kind != ExpressDeclarationKind.Entity)
                {
                    continue;
                }

                var entityType = new ExpressBoundNamedType(symbol, entityReference.Span);
                AddLexicalName(
                    schema,
                    scope,
                    new ExpressBoundName(
                        token.Text,
                        ExpressBoundNameKind.Population,
                        new ExpressBoundAggregateType(
                            ExpressAggregateKind.Set,
                            entityType,
                            lowerBoundText: "0",
                            upperBoundText: "?",
                            isOptional: false,
                            isUnique: true,
                            typeLabel: null,
                            entityReference.Span),
                        symbol,
                        entityReference.Span));
            }
        }

        private void AddAlgorithmDeclarations(
            SchemaDraft schema,
            ExpressRuleSyntax algorithmHead,
            NameScope scope)
        {
            var declarations = algorithmHead.ChildRules("declaration")
                .Select(container => container.ChildRules().Single())
                .Select(declaration => schema.NestedDeclarations.Single(item =>
                    ReferenceEquals(item.Syntax, declaration)))
                .Where(item => IsResource(item.Symbol.Kind))
                .ToArray();
            foreach (var declaration in declarations.Where(item => IsNamedType(item.Symbol.Kind)))
            {
                AddLexicalName(schema, scope, CreateSchemaName(declaration.Symbol));
            }

            foreach (var declaration in declarations.Where(item => !IsNamedType(item.Symbol.Kind)))
            {
                AddLexicalName(
                    schema,
                    scope,
                    new ExpressBoundName(
                        declaration.NameToken.Text,
                        declaration.Symbol.Kind == ExpressDeclarationKind.Function
                            ? ExpressBoundNameKind.Function
                            : ExpressBoundNameKind.Procedure,
                        declaration.BoundType,
                        declaration.Symbol,
                        declaration.Syntax.Span));
            }

            var constants = algorithmHead.ChildRules("constantDecl").SingleOrDefault();
            if (constants is not null)
            {
                foreach (var constant in constants.ChildRules("constantBody"))
                {
                    var token = constant.RequiredChild("constantId").IdentifierToken();
                    var type = BindType(schema, constant.RequiredChild("instantiableType"), scope);
                    AddLexicalName(
                        schema,
                        scope,
                        new ExpressBoundName(
                            token.Text,
                            ExpressBoundNameKind.Constant,
                            type,
                            schemaDeclaration: null,
                            constant.Span));
                }
            }

            var locals = algorithmHead.ChildRules("localDecl").SingleOrDefault();
            if (locals is null)
            {
                return;
            }

            foreach (var local in locals.ChildRules("localVariable"))
            {
                var type = BindType(schema, local.RequiredChild("parameterType"), scope);
                foreach (var variable in local.ChildRules("variableId"))
                {
                    var token = variable.IdentifierToken();
                    AddLexicalName(
                        schema,
                        scope,
                        new ExpressBoundName(
                            token.Text,
                            ExpressBoundNameKind.Variable,
                            type,
                            schemaDeclaration: null,
                            variable.Span));
                }
            }
        }

        private void VisitNames(SchemaDraft schema, ExpressRuleSyntax syntax, NameScope scope)
        {
            switch (syntax.Production)
            {
                case "primary":
                    BindPrimaryNames(schema, syntax, scope);
                    return;

                case "namedApplication":
                    BindNeutralName(schema, syntax, scope, isApplication: true);
                    break;

                case "namedReference":
                    BindNeutralName(schema, syntax, scope, isApplication: false);
                    break;

                case "generalRef":
                    BindRestrictedName(schema, syntax, scope, expectedKind: null);
                    return;

                case "procedureCallStmt":
                    var procedure = syntax.ChildRules("procedureRef").SingleOrDefault();
                    if (procedure is not null)
                    {
                        BindRestrictedName(schema, procedure, scope, ExpressBoundNameKind.Procedure);
                    }

                    foreach (var parameters in syntax.ChildRules("actualParameterList"))
                    {
                        VisitNames(schema, parameters, scope);
                    }

                    return;

                case "groupQualifier":
                    var entity = syntax.RequiredChild("entityRef");
                    BindRestrictedName(schema, entity, scope, ExpressBoundNameKind.Entity);
                    return;

                case "qualifiedAttribute":
                    BindQualifiedAttributeName(schema, syntax, scope);
                    return;

                case "inverseAttr":
                    BindInverseAttributeName(schema, syntax, scope);
                    return;

                case "referencedAttribute":
                    var attribute = syntax.ChildRules("attributeRef").SingleOrDefault();
                    if (attribute is not null)
                    {
                        BindRestrictedName(schema, attribute, scope, ExpressBoundNameKind.Attribute);
                    }
                    else
                    {
                        VisitNames(schema, syntax.ChildRules().Single(), scope);
                    }

                    return;

                case "queryExpression":
                    BindQueryNames(schema, syntax, scope);
                    return;

                case "aliasStmt":
                    BindAliasNames(schema, syntax, scope);
                    return;

                case "repeatStmt":
                    BindRepeatNames(schema, syntax, scope);
                    return;
            }

            foreach (var child in syntax.ChildRules())
            {
                VisitNames(schema, child, scope);
            }
        }

        private void BindPrimaryNames(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope scope)
        {
            var named = syntax.ChildRules()
                .FirstOrDefault(child => child.Production is "namedApplication" or "namedReference");
            if (named is null)
            {
                foreach (var child in syntax.ChildRules())
                {
                    VisitNames(schema, child, scope);
                }

                return;
            }

            var target = named.ChildRules("builtInFunction").Any()
                ? null
                : BindNeutralName(
                    schema,
                    named,
                    scope,
                    named.Production == "namedApplication");
            foreach (var parameterList in named.ChildRules("actualParameterList"))
            {
                VisitNames(schema, parameterList, scope);
            }

            foreach (var qualifier in syntax.ChildRules("qualifier"))
            {
                var child = qualifier.ChildRules().Single();
                if (child.Production == "attributeQualifier")
                {
                    target = BindMemberName(schema, child, target);
                }
                else if (child.Production == "groupQualifier")
                {
                    target = BindRestrictedName(
                        schema,
                        child.RequiredChild("entityRef"),
                        scope,
                        ExpressBoundNameKind.Entity);
                }
                else
                {
                    VisitNames(schema, child, scope);
                }
            }
        }

        private ExpressBoundName? BindMemberName(
            SchemaDraft schema,
            ExpressRuleSyntax qualifier,
            ExpressBoundName? source)
        {
            if (source is null)
            {
                return null;
            }

            var token = qualifier.RequiredChild("attributeRef").IdentifierToken();
            return BindMemberName(schema, token, qualifier.Span, source);
        }

        private ExpressBoundName? BindMemberName(
            SchemaDraft schema,
            ExpressTokenSyntax token,
            ExpressSourceSpan span,
            ExpressBoundName? source)
        {
            if (source is null)
            {
                return null;
            }

            var declaration = source.SchemaDeclaration
                ?? (source.Type as ExpressBoundNamedType)?.Declaration;
            if (declaration is null)
            {
                return null;
            }

            var draft = FindSymbol(declaration);
            ExpressBoundName? target = null;
            if (draft?.Symbol.Kind == ExpressDeclarationKind.Entity)
            {
                var attributes = draft.Attributes
                    .Where(candidate => _nameComparer.Equals(candidate.Name, token.Text))
                    .ToArray();
                if (attributes.Length == 0)
                {
                    attributes = draft.Supertypes
                        .Select(FindSymbol)
                        .Where(candidate => candidate is not null)
                        .SelectMany(candidate => EnumerateAttributes(
                            candidate!,
                            new HashSet<ExpressBoundSymbol>()))
                        .Where(candidate => _nameComparer.Equals(candidate.Name, token.Text))
                        .Distinct()
                        .ToArray();
                }

                if (attributes.Length == 1)
                {
                    var attribute = attributes[0];
                    target = new(
                        attribute.Name,
                        ExpressBoundNameKind.Attribute,
                        attribute.Type,
                        schemaDeclaration: null,
                        attribute.Span,
                        attribute.IsOptional,
                        attribute);
                }
            }
            else if (draft?.BoundType is ExpressBoundEnumerationType)
            {
                var item = FindEnumerationItem(
                    draft,
                    token.Text,
                    new HashSet<ExpressBoundSymbol>());
                if (item is not null)
                {
                    target = new(
                        item.IdentifierToken().Text,
                        ExpressBoundNameKind.Enumeration,
                        new ExpressBoundNamedType(draft.Symbol, item.Span),
                        schemaDeclaration: null,
                        item.Span);
                }
            }

            if (target is null)
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-UNRESOLVED-MEMBER",
                    $"Member '{token.Text}' cannot be resolved from '{source.Name}'.",
                    token.Span.Start);
                return null;
            }

            schema.NameReferences.Add(new ExpressBoundNameReference(target, isApplication: false, span));
            return target;
        }

        private ExpressRuleSyntax? FindEnumerationItem(
            SymbolDraft type,
            string name,
            ISet<ExpressBoundSymbol> visited)
        {
            if (!visited.Add(type.Symbol))
            {
                return null;
            }

            var local = type.Syntax.DescendantsAndSelf()
                .FirstOrDefault(node => node.Production == "enumerationId"
                    && _nameComparer.Equals(node.IdentifierToken().Text, name));
            if (local is not null)
            {
                return local;
            }

            if (type.BoundType is not ExpressBoundEnumerationType { BaseType: not null, } enumeration)
            {
                return null;
            }

            var baseType = FindSymbol(enumeration.BaseType);
            return baseType is null
                ? null
                : FindEnumerationItem(baseType, name, visited);
        }

        private void BindQualifiedAttributeName(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope scope)
        {
            var entity = BindRestrictedName(
                schema,
                syntax.RequiredChild("groupQualifier").RequiredChild("entityRef"),
                scope,
                ExpressBoundNameKind.Entity);
            var qualifier = syntax.RequiredChild("attributeQualifier");
            BindMemberName(
                schema,
                qualifier.RequiredChild("attributeRef").IdentifierToken(),
                qualifier.Span,
                entity);
        }

        private void BindInverseAttributeName(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope scope)
        {
            var entities = syntax.ChildRules("entityRef").ToArray();
            var entity = BindRestrictedName(
                schema,
                entities[entities.Length - 1],
                scope,
                ExpressBoundNameKind.Entity);
            var attribute = syntax.RequiredChild("attributeRef");
            BindMemberName(schema, attribute.IdentifierToken(), attribute.Span, entity);
        }

        private IEnumerable<ExpressBoundAttribute> EnumerateAttributes(
            SymbolDraft entity,
            ISet<ExpressBoundSymbol> visited)
        {
            if (!visited.Add(entity.Symbol))
            {
                yield break;
            }

            foreach (var attribute in entity.Attributes)
            {
                yield return attribute;
            }

            foreach (var supertype in entity.Supertypes)
            {
                var draft = FindSymbol(supertype);
                if (draft is null)
                {
                    continue;
                }

                foreach (var attribute in EnumerateAttributes(draft, visited))
                {
                    yield return attribute;
                }
            }
        }

        private ExpressBoundName? BindNeutralName(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope scope,
            bool isApplication)
        {
            var token = syntax.DescendantTokens().FirstOrDefault(item => item.TokenName == "SimpleId");
            if (token is null)
            {
                return null;
            }

            if (!scope.TryResolve(token.Text, out var target))
            {
                ReportUnresolvedName(schema, token);
                return null;
            }

            if (isApplication
                && target.Kind is not ExpressBoundNameKind.Entity and not ExpressBoundNameKind.Function)
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-EXPECTED-APPLICATION",
                    $"Name '{token.Text}' does not identify a function or entity constructor.",
                    token.Span.Start);
                return null;
            }

            schema.NameReferences.Add(new ExpressBoundNameReference(target, isApplication, syntax.Span));
            return target;
        }

        private ExpressBoundName? BindRestrictedName(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope scope,
            ExpressBoundNameKind? expectedKind)
        {
            var token = syntax.IdentifierToken();
            if (!scope.TryResolve(token.Text, out var target))
            {
                ReportUnresolvedName(schema, token);
                return null;
            }

            if (expectedKind is not null && target.Kind != expectedKind)
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-WRONG-NAME-KIND",
                    $"Name '{token.Text}' does not identify the required {expectedKind.Value.ToString()} declaration kind.",
                    token.Span.Start);
                return null;
            }

            schema.NameReferences.Add(new ExpressBoundNameReference(target, isApplication: false, syntax.Span));
            return target;
        }

        private SymbolDraft? FindSymbol(ExpressBoundSymbol symbol)
        {
            var schema = FindSchema(symbol.DeclaringSchema);
            return schema.Declarations.Concat(schema.NestedDeclarations)
                .SingleOrDefault(candidate => ReferenceEquals(candidate.Symbol, symbol));
        }

        private void BindQueryNames(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope parentScope)
        {
            VisitNames(schema, syntax.RequiredChild("aggregateSource"), parentScope);
            var scope = new NameScope(parentScope);
            var variable = syntax.RequiredChild("variableId");
            var token = variable.IdentifierToken();
            AddLexicalName(
                schema,
                scope,
                new ExpressBoundName(
                    token.Text,
                    ExpressBoundNameKind.QueryVariable,
                    type: null,
                    schemaDeclaration: null,
                    variable.Span));
            VisitNames(schema, syntax.RequiredChild("logicalExpression"), scope);
        }

        private void BindAliasNames(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope parentScope)
        {
            var source = syntax.RequiredChild("generalRef");
            BindRestrictedName(schema, source, parentScope, expectedKind: null);
            foreach (var qualifier in syntax.ChildRules("qualifier"))
            {
                VisitNames(schema, qualifier, parentScope);
            }

            var scope = new NameScope(parentScope);
            var variable = syntax.RequiredChild("variableId");
            var token = variable.IdentifierToken();
            AddLexicalName(
                schema,
                scope,
                new ExpressBoundName(
                    token.Text,
                    ExpressBoundNameKind.Alias,
                    type: null,
                    schemaDeclaration: null,
                    variable.Span));
            foreach (var statement in syntax.ChildRules("stmt"))
            {
                VisitNames(schema, statement, scope);
            }
        }

        private void BindRepeatNames(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope parentScope)
        {
            var scope = new NameScope(parentScope);
            var control = syntax.RequiredChild("repeatControl");
            var incrementControl = control.ChildRules("incrementControl").SingleOrDefault();
            if (incrementControl is not null)
            {
                var variable = incrementControl.RequiredChild("variableId");
                var token = variable.IdentifierToken();
                AddLexicalName(
                    schema,
                    scope,
                    new ExpressBoundName(
                        token.Text,
                        ExpressBoundNameKind.RepeatVariable,
                        new ExpressBoundScalarType(
                            ExpressScalarKind.Number,
                            constraintText: null,
                            isFixed: false,
                            variable.Span),
                        schemaDeclaration: null,
                        variable.Span));
            }

            VisitNames(schema, control, scope);
            foreach (var statement in syntax.ChildRules("stmt"))
            {
                VisitNames(schema, statement, scope);
            }
        }

        private void AddLexicalName(SchemaDraft schema, NameScope scope, ExpressBoundName name)
        {
            if (scope.TryAdd(name))
            {
                return;
            }

            schema.IsInvalid = true;
            AddDiagnostic(
                _diagnostics,
                "EXPRESS-BIND-DUPLICATE-LOCAL-NAME",
                $"Name '{name.Name}' occurs more than once in the same lexical scope.",
                name.Span.Start);
        }

        private void ReportUnresolvedName(SchemaDraft schema, ExpressTokenSyntax token)
        {
            schema.IsInvalid = true;
            AddDiagnostic(
                _diagnostics,
                "EXPRESS-BIND-UNRESOLVED-NAME",
                $"Name '{token.Text}' cannot be resolved in schema '{schema.Identity.Name}'.",
                token.Span.Start);
        }

        private void BindEntity(
            SchemaDraft schema,
            SymbolDraft declaration,
            NameScope? scope = null)
        {
            var entityHead = declaration.Syntax.RequiredChild("entityHead");
            declaration.IsAbstract = entityHead.HasToken("ABSTRACT");
            foreach (var reference in entityHead.DescendantsAndSelf()
                         .Where(node => node.Production == "supertypeConstraint")
                         .SelectMany(node => node.DescendantsAndSelf())
                         .Where(node => node.Production == "entityRef"))
            {
                ResolveEntity(schema, reference, scope);
            }

            var subtype = entityHead.RequiredChild("subsuper")
                .ChildRules("subtypeDeclaration")
                .SingleOrDefault();
            if (subtype is not null)
            {
                foreach (var entityReference in subtype.ChildRules("entityRef"))
                {
                    var symbol = ResolveEntity(schema, entityReference, scope);
                    if (symbol is not null)
                    {
                        declaration.Supertypes.Add(symbol);
                    }
                }
            }

            var body = declaration.Syntax.RequiredChild("entityBody");
            foreach (var explicitAttribute in body.ChildRules("explicitAttr"))
            {
                var type = BindType(schema, explicitAttribute.RequiredChild("parameterType"), scope);
                if (type is null)
                {
                    continue;
                }

                foreach (var attributeDeclaration in explicitAttribute.ChildRules("attributeDecl"))
                {
                    AddAttribute(
                        schema,
                        declaration,
                        attributeDeclaration,
                        ExpressAttributeKind.Explicit,
                        type,
                        explicitAttribute.HasDirectToken("OPTIONAL"));
                }
            }

            var deriveClause = body.ChildRules("deriveClause").SingleOrDefault();
            if (deriveClause is not null)
            {
                foreach (var derivedAttribute in deriveClause.ChildRules("derivedAttr"))
                {
                    var type = BindType(schema, derivedAttribute.RequiredChild("parameterType"), scope);
                    if (type is not null)
                    {
                        AddAttribute(
                            schema,
                            declaration,
                            derivedAttribute.RequiredChild("attributeDecl"),
                            ExpressAttributeKind.Derived,
                            type,
                            isOptional: false);
                    }
                }
            }

            var inverseClause = body.ChildRules("inverseClause").SingleOrDefault();
            if (inverseClause is null)
            {
                return;
            }

            foreach (var inverseAttribute in inverseClause.ChildRules("inverseAttr"))
            {
                BindInverseAttribute(schema, declaration, inverseAttribute, scope);
            }
        }

        private void BindInverseAttribute(
            SchemaDraft schema,
            SymbolDraft declaration,
            ExpressRuleSyntax syntax,
            NameScope? scope)
        {
            var entityReference = syntax.ChildRules("entityRef").First();
            var entity = ResolveEntity(schema, entityReference, scope);
            if (entity is null)
            {
                return;
            }

            ExpressBoundType type = new ExpressBoundNamedType(entity, entityReference.Span);
            if (syntax.HasToken("SET") || syntax.HasToken("BAG"))
            {
                var bound = syntax.ChildRules("boundSpec").SingleOrDefault();
                type = new ExpressBoundAggregateType(
                    syntax.HasToken("SET") ? ExpressAggregateKind.Set : ExpressAggregateKind.Bag,
                    type,
                    bound?.RequiredChild("bound1").TokenText(),
                    bound?.RequiredChild("bound2").TokenText(),
                    isOptional: false,
                    isUnique: false,
                    typeLabel: null,
                    syntax.Span,
                    NormalizeBoundText(
                        schema,
                        bound?.RequiredChild("bound1").TokenText(),
                        scope),
                    NormalizeBoundText(
                        schema,
                        bound?.RequiredChild("bound2").TokenText(),
                        scope));
            }

            AddAttribute(
                schema,
                declaration,
                syntax.RequiredChild("attributeDecl"),
                ExpressAttributeKind.Inverse,
                type,
                isOptional: false);
        }

        private void AddAttribute(
            SchemaDraft schema,
            SymbolDraft declaration,
            ExpressRuleSyntax attributeDeclaration,
            ExpressAttributeKind kind,
            ExpressBoundType type,
            bool isOptional)
        {
            var nameToken = attributeDeclaration.DescendantTokens()
                .Last(token => token.TokenName == "SimpleId");
            if (declaration.Attributes.Any(attribute => _nameComparer.Equals(attribute.Name, nameToken.Text)))
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-DUPLICATE-ATTRIBUTE",
                    $"Attribute '{nameToken.Text}' occurs more than once in entity '{declaration.Symbol.Name}'.",
                    nameToken.Span.Start);
                return;
            }

            declaration.Attributes.Add(new ExpressBoundAttribute(
                nameToken.Text,
                kind,
                type,
                isOptional,
                attributeDeclaration.Span));
        }

        private void BindContainedTypes(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope? scope = null)
        {
            foreach (var child in syntax.ChildRules())
            {
                if (child.Production is "parameterType" or "instantiableType" or "underlyingType")
                {
                    BindType(schema, child, scope);
                }
                else
                {
                    BindContainedTypes(schema, child, scope);
                }
            }
        }

        private void BindRequiredEntityReferences(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope? scope = null)
        {
            foreach (var reference in syntax.DescendantsAndSelf()
                         .Where(node => node.Production == "entityRef"))
            {
                ResolveEntity(schema, reference, scope);
            }

            BindContainedTypes(schema, syntax, scope);
        }

        private ExpressBoundType? BindType(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope? scope = null)
        {
            switch (syntax.Production)
            {
                case "underlyingType":
                case "constructedTypes":
                case "concreteTypes":
                case "aggregationTypes":
                case "simpleTypes":
                case "parameterType":
                case "generalizedTypes":
                case "generalAggregationTypes":
                case "instantiableType":
                    return BindType(schema, syntax.ChildRules().Single(), scope);

                case "namedTypes":
                case "typeRef":
                case "entityRef":
                    return BindNamedType(schema, syntax, scope);

                case "binaryType":
                    return BindScalar(syntax, ExpressScalarKind.Binary);

                case "booleanType":
                    return BindScalar(syntax, ExpressScalarKind.Boolean);

                case "integerType":
                    return BindScalar(syntax, ExpressScalarKind.Integer);

                case "logicalType":
                    return BindScalar(syntax, ExpressScalarKind.Logical);

                case "numberType":
                    return BindScalar(syntax, ExpressScalarKind.Number);

                case "realType":
                    return BindScalar(syntax, ExpressScalarKind.Real);

                case "stringType":
                    return BindScalar(syntax, ExpressScalarKind.String);

                case "arrayType":
                case "generalArrayType":
                    return BindAggregate(schema, syntax, ExpressAggregateKind.Array, scope);

                case "bagType":
                case "generalBagType":
                    return BindAggregate(schema, syntax, ExpressAggregateKind.Bag, scope);

                case "listType":
                case "generalListType":
                    return BindAggregate(schema, syntax, ExpressAggregateKind.List, scope);

                case "setType":
                case "generalSetType":
                    return BindAggregate(schema, syntax, ExpressAggregateKind.Set, scope);

                case "aggregateType":
                    return BindAggregate(schema, syntax, ExpressAggregateKind.Aggregate, scope);

                case "genericType":
                case "genericEntityType":
                    return new ExpressBoundGenericType(
                        syntax.Production == "genericEntityType",
                        syntax.ChildRules("typeLabel").SingleOrDefault()?.IdentifierToken().Text,
                        syntax.Span);

                case "enumerationType":
                    return BindEnumeration(schema, syntax, scope);

                case "selectType":
                    return BindSelect(schema, syntax, scope);

                default:
                    schema.IsInvalid = true;
                    AddDiagnostic(
                        _diagnostics,
                        "EXPRESS-BIND-UNSUPPORTED-TYPE",
                        $"Type production '{syntax.Production}' is not represented by the bound IR.",
                        syntax.Span.Start);
                    return null;
            }
        }

        private ExpressBoundNamedType? BindNamedType(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope? scope = null)
        {
            var nameToken = syntax.IdentifierToken();
            var symbol = ResolveVisible(schema, nameToken.Text, scope);
            if (symbol is null || !IsNamedType(symbol.Kind))
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-UNRESOLVED-TYPE",
                    $"Named type '{nameToken.Text}' cannot be resolved in schema '{schema.Identity.Name}'.",
                    nameToken.Span.Start);
                return null;
            }

            return new(symbol, syntax.Span);
        }

        private ExpressBoundSymbol? ResolveEntity(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope? scope = null)
        {
            var nameToken = syntax.IdentifierToken();
            var symbol = ResolveVisible(schema, nameToken.Text, scope);
            if (symbol is null)
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-UNRESOLVED-ENTITY",
                    $"Entity '{nameToken.Text}' cannot be resolved in schema '{schema.Identity.Name}'.",
                    nameToken.Span.Start);
                return null;
            }

            if (symbol.Kind != ExpressDeclarationKind.Entity)
            {
                schema.IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-EXPECTED-ENTITY",
                    $"Name '{nameToken.Text}' does not identify an entity declaration.",
                    nameToken.Span.Start);
                return null;
            }

            return symbol;
        }

        private static ExpressBoundScalarType BindScalar(
            ExpressRuleSyntax syntax,
            ExpressScalarKind kind)
        {
            var constraint = syntax.ChildRules("widthSpec").SingleOrDefault()
                ?? syntax.ChildRules("precisionSpec").SingleOrDefault();
            return new(
                kind,
                constraint?.TokenText(),
                syntax.HasToken("FIXED"),
                syntax.Span);
        }

        private ExpressBoundAggregateType? BindAggregate(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            ExpressAggregateKind kind,
            NameScope? scope = null)
        {
            var elementSyntax = syntax.ChildRules().Last(child =>
                child.Production is "instantiableType" or "parameterType");
            var elementType = BindType(schema, elementSyntax, scope);
            if (elementType is null)
            {
                return null;
            }

            var bound = syntax.ChildRules("boundSpec").SingleOrDefault();
            return new(
                kind,
                elementType,
                bound?.RequiredChild("bound1").TokenText(),
                bound?.RequiredChild("bound2").TokenText(),
                syntax.HasDirectToken("OPTIONAL"),
                syntax.HasDirectToken("UNIQUE"),
                syntax.ChildRules("typeLabel").SingleOrDefault()?.IdentifierToken().Text,
                syntax.Span,
                NormalizeBoundText(
                    schema,
                    bound?.RequiredChild("bound1").TokenText(),
                    scope),
                NormalizeBoundText(
                    schema,
                    bound?.RequiredChild("bound2").TokenText(),
                    scope));
        }

        private string? NormalizeBoundText(
            SchemaDraft schema,
            string? text,
            NameScope? scope,
            ISet<ExpressBoundSymbol>? visited = null)
        {
            if (text is null || text == "?")
            {
                return text;
            }

            visited ??= new HashSet<ExpressBoundSymbol>();
            if (!TryEvaluateIntegerBound(schema, text, scope, visited, out var value)
                || value < int.MinValue
                || value > int.MaxValue)
            {
                return text;
            }

            return ((int)value).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private bool TryEvaluateIntegerBound(
            SchemaDraft schema,
            string text,
            NameScope? scope,
            ISet<ExpressBoundSymbol> visited,
            out System.Numerics.BigInteger value)
        {
            text = TrimOuterParentheses(text);
            if (System.Numerics.BigInteger.TryParse(
                    text,
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out value))
            {
                return true;
            }

            if (TrySplitIntegerBound(
                    text,
                    ["+", "-",],
                    out var left,
                    out var operation,
                    out var right)
                && TryEvaluateIntegerBound(
                    schema,
                    left,
                    scope,
                    visited,
                    out var leftValue)
                && TryEvaluateIntegerBound(
                    schema,
                    right,
                    scope,
                    visited,
                    out var rightValue))
            {
                value = operation == "+"
                    ? leftValue + rightValue
                    : leftValue - rightValue;
                return true;
            }

            if (TrySplitIntegerBound(
                    text,
                    ["*",],
                    out left,
                    out operation,
                    out right)
                && TryEvaluateIntegerBound(
                    schema,
                    left,
                    scope,
                    visited,
                    out leftValue)
                && TryEvaluateIntegerBound(
                    schema,
                    right,
                    scope,
                    visited,
                    out rightValue))
            {
                value = leftValue * rightValue;
                return true;
            }

            if (TrySplitIntegerBound(
                    text,
                    ["**",],
                    out left,
                    out operation,
                    out right,
                    scanFromRight: false)
                && TryEvaluateIntegerBound(
                    schema,
                    left,
                    scope,
                    visited,
                    out leftValue)
                && TryEvaluateIntegerBound(
                    schema,
                    right,
                    scope,
                    visited,
                    out rightValue)
                && rightValue >= System.Numerics.BigInteger.Zero
                && rightValue <= int.MaxValue)
            {
                value = System.Numerics.BigInteger.Pow(leftValue, (int)rightValue);
                return true;
            }

            if (text.Length > 0 && text[0] == '+')
            {
                return TryEvaluateIntegerBound(schema, text.Substring(1), scope, visited, out value);
            }

            if (text.Length > 0
                && text[0] == '-'
                && TryEvaluateIntegerBound(schema, text.Substring(1), scope, visited, out var operand))
            {
                value = -operand;
                return true;
            }

            ExpressBoundSymbol? symbol = null;
            if (scope is not null
                && scope.TryResolve(text, out var scoped)
                && scoped.Kind == ExpressBoundNameKind.Constant)
            {
                symbol = scoped.SchemaDeclaration;
            }

            symbol ??= ResolveVisible(schema, text);
            if (symbol?.Kind != ExpressDeclarationKind.Constant)
            {
                value = default;
                return false;
            }

            if (!visited.Add(symbol))
            {
                value = default;
                return false;
            }

            var declaration = FindSymbol(symbol);
            var initializer = declaration?.Syntax.ChildRules("expression").SingleOrDefault();
            var evaluated = initializer is not null
                && TryEvaluateIntegerBound(schema, initializer.TokenText(), scope, visited, out value);
            visited.Remove(symbol);
            return evaluated;
        }

        private static string TrimOuterParentheses(string text)
        {
            while (text.Length >= 2 && text[0] == '(' && text[text.Length - 1] == ')')
            {
                var depth = 0;
                var enclosesAll = true;
                for (var index = 0; index < text.Length - 1; index++)
                {
                    if (text[index] == '(')
                    {
                        depth++;
                    }
                    else if (text[index] == ')')
                    {
                        depth--;
                    }

                    if (depth == 0)
                    {
                        enclosesAll = false;
                        break;
                    }
                }

                if (!enclosesAll)
                {
                    break;
                }

                text = text.Substring(1, text.Length - 2);
            }

            return text;
        }

        private static bool TrySplitIntegerBound(
            string text,
            IReadOnlyList<string> operators,
            out string left,
            out string operation,
            out string right,
            bool scanFromRight = true)
        {
            var depth = 0;
            var index = scanFromRight ? text.Length - 1 : 0;
            while (index >= 0 && index < text.Length)
            {
                if (text[index] == ')')
                {
                    depth++;
                }
                else if (text[index] == '(')
                {
                    depth--;
                }

                if (depth == 0)
                {
                    foreach (var candidate in operators)
                    {
                        var isPowerCharacter = candidate == "*"
                            && ((index > 0 && text[index - 1] == '*')
                                || (index + 1 < text.Length && text[index + 1] == '*'));
                        var isUnarySign = candidate is "+" or "-"
                            && (index == 0
                                || text[index - 1] is '+' or '-' or '*' or '/' or '(');
                        var isIdentifierFragment = char.IsLetter(candidate[0])
                            && ((index > 0 && IsIdentifierCharacter(text[index - 1]))
                                || (index + candidate.Length < text.Length
                                    && IsIdentifierCharacter(text[index + candidate.Length])));
                        if (!isPowerCharacter
                            && !isUnarySign
                            && !isIdentifierFragment
                            && index + candidate.Length <= text.Length
                            && string.Equals(
                                text.Substring(index, candidate.Length),
                                candidate,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            left = text.Substring(0, index);
                            operation = candidate;
                            right = text.Substring(index + candidate.Length);
                            return left.Length > 0 && right.Length > 0;
                        }
                    }
                }

                index += scanFromRight ? -1 : 1;
            }

            left = "";
            operation = "";
            right = "";
            return false;
        }

        private static bool IsIdentifierCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private ExpressBoundEnumerationType BindEnumeration(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope? scope = null)
        {
            var extension = syntax.ChildRules("enumerationExtension").SingleOrDefault();
            var baseType = extension is null
                ? null
                : ResolveDefinedType(schema, extension.RequiredChild("typeRef"), scope);
            var values = syntax.DescendantsAndSelf()
                .Where(node => node.Production == "enumerationId")
                .Select(node => node.IdentifierToken().Text);
            return new(
                syntax.HasToken("EXTENSIBLE"),
                baseType,
                values,
                syntax.Span);
        }

        private ExpressBoundSelectType BindSelect(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope? scope = null)
        {
            var extension = syntax.ChildRules("selectExtension").SingleOrDefault();
            var baseType = extension is null
                ? null
                : ResolveDefinedType(schema, extension.RequiredChild("typeRef"), scope);
            var alternatives = new List<ExpressBoundSymbol>();
            foreach (var namedType in syntax.DescendantsAndSelf()
                         .Where(node => node.Production == "namedTypes"))
            {
                var bound = BindNamedType(schema, namedType, scope);
                if (bound is ExpressBoundNamedType named)
                {
                    alternatives.Add(named.Declaration);
                }
            }

            return new(
                syntax.HasToken("EXTENSIBLE"),
                syntax.HasToken("GENERIC_ENTITY"),
                baseType,
                alternatives,
                syntax.Span);
        }

        private ExpressBoundSymbol? ResolveDefinedType(
            SchemaDraft schema,
            ExpressRuleSyntax syntax,
            NameScope? scope = null)
        {
            var nameToken = syntax.IdentifierToken();
            var symbol = ResolveVisible(schema, nameToken.Text, scope);
            if (symbol?.Kind == ExpressDeclarationKind.Type)
            {
                return symbol;
            }

            schema.IsInvalid = true;
            AddDiagnostic(
                _diagnostics,
                "EXPRESS-BIND-UNRESOLVED-TYPE",
                $"Defined type '{nameToken.Text}' cannot be resolved in schema '{schema.Identity.Name}'.",
                nameToken.Span.Start);
            return null;
        }

        private static ExpressBoundSymbol? ResolveVisible(
            SchemaDraft schema,
            string name,
            NameScope? scope = null)
        {
            if (scope is not null
                && scope.TryResolve(name, out var scoped)
                && scoped.SchemaDeclaration is not null)
            {
                return scoped.SchemaDeclaration;
            }

            if (schema.LocalAndUseSymbols.TryGetValue(name, out var local))
            {
                return local;
            }

            return schema.ReferenceSymbols.TryGetValue(name, out var referenced)
                ? referenced
                : null;
        }

        private void ValidateInheritanceCycles()
        {
            var entities = _uniqueSchemas.Values
                .SelectMany(schema => schema.Declarations.Concat(schema.NestedDeclarations))
                .Where(declaration => declaration.Symbol.Kind == ExpressDeclarationKind.Entity)
                .ToDictionary(declaration => declaration.Symbol, declaration => declaration);
            var states = new Dictionary<ExpressBoundSymbol, VisitState>();
            var reported = new HashSet<ExpressBoundSymbol>();
            foreach (var entity in entities.Values)
            {
                VisitEntity(entity, entities, states, reported, new List<SymbolDraft>());
            }
        }

        private void ValidateConstructedTypeBases()
        {
            var types = _uniqueSchemas.Values
                .SelectMany(schema => schema.Declarations.Concat(schema.NestedDeclarations))
                .Where(declaration => declaration.Symbol.Kind == ExpressDeclarationKind.Type)
                .ToDictionary(declaration => declaration.Symbol, declaration => declaration);
            foreach (var type in types.Values)
            {
                if (type.BoundType is ExpressBoundEnumerationType enumeration
                    && enumeration.BaseType is not null
                    && (!types.TryGetValue(enumeration.BaseType, out var enumerationBase)
                        || enumerationBase.BoundType is not ExpressBoundEnumerationType))
                {
                    ReportWrongConstructedBase(
                        type,
                        "enumerationExtension",
                        "EXPRESS-BIND-EXPECTED-ENUMERATION",
                        "enumeration");
                }

                if (type.BoundType is ExpressBoundSelectType select
                    && select.BaseType is not null
                    && (!types.TryGetValue(select.BaseType, out var selectBase)
                        || selectBase.BoundType is not ExpressBoundSelectType))
                {
                    ReportWrongConstructedBase(
                        type,
                        "selectExtension",
                        "EXPRESS-BIND-EXPECTED-SELECT",
                        "select");
                }
            }
        }

        private void ReportWrongConstructedBase(
            SymbolDraft type,
            string extensionProduction,
            string code,
            string expectedKind)
        {
            var reference = type.Syntax.DescendantsAndSelf()
                .Single(node => node.Production == extensionProduction)
                .RequiredChild("typeRef");
            FindSchema(type.Symbol.DeclaringSchema).IsInvalid = true;
            AddDiagnostic(
                _diagnostics,
                code,
                $"Type '{reference.IdentifierToken().Text}' is not an {expectedKind} type.",
                reference.Span.Start);
        }

        private void ValidateTypeCycles()
        {
            var types = _uniqueSchemas.Values
                .SelectMany(schema => schema.Declarations.Concat(schema.NestedDeclarations))
                .Where(declaration => declaration.Symbol.Kind == ExpressDeclarationKind.Type)
                .ToDictionary(declaration => declaration.Symbol, declaration => declaration);
            var states = new Dictionary<ExpressBoundSymbol, VisitState>();
            var reported = new HashSet<ExpressBoundSymbol>();
            foreach (var type in types.Values)
            {
                VisitType(type, types, states, reported, new List<SymbolDraft>());
            }
        }

        private void VisitType(
            SymbolDraft type,
            IReadOnlyDictionary<ExpressBoundSymbol, SymbolDraft> types,
            IDictionary<ExpressBoundSymbol, VisitState> states,
            ISet<ExpressBoundSymbol> reported,
            IList<SymbolDraft> path)
        {
            if (states.TryGetValue(type.Symbol, out var state))
            {
                if (state == VisitState.Visiting)
                {
                    ReportTypeCycle(type.Symbol, path, reported);
                }

                return;
            }

            states[type.Symbol] = VisitState.Visiting;
            path.Add(type);
            foreach (var dependency in EnumerateTypeDependencies(type.BoundType))
            {
                if (types.TryGetValue(dependency, out var target))
                {
                    VisitType(target, types, states, reported, path);
                }
            }

            path.RemoveAt(path.Count - 1);
            states[type.Symbol] = VisitState.Visited;
        }

        private void ReportTypeCycle(
            ExpressBoundSymbol repeated,
            IEnumerable<SymbolDraft> path,
            ISet<ExpressBoundSymbol> reported)
        {
            foreach (var member in path.SkipWhile(item => !ReferenceEquals(item.Symbol, repeated)))
            {
                if (!reported.Add(member.Symbol))
                {
                    continue;
                }

                FindSchema(member.Symbol.DeclaringSchema).IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-TYPE-CYCLE",
                    $"Defined type '{member.Symbol.Name}' participates in a type cycle.",
                    member.NameToken.Span.Start);
            }
        }

        private static IEnumerable<ExpressBoundSymbol> EnumerateTypeDependencies(ExpressBoundType? type)
        {
            switch (type)
            {
                case ExpressBoundNamedType named:
                    yield return named.Declaration;
                    break;

                case ExpressBoundAggregateType aggregate:
                    foreach (var dependency in EnumerateTypeDependencies(aggregate.ElementType))
                    {
                        yield return dependency;
                    }

                    break;

                case ExpressBoundEnumerationType enumeration when enumeration.BaseType is not null:
                    yield return enumeration.BaseType;
                    break;

                case ExpressBoundSelectType select:
                    if (select.BaseType is not null)
                    {
                        yield return select.BaseType;
                    }

                    foreach (var alternative in select.Alternatives)
                    {
                        yield return alternative;
                    }

                    break;
            }
        }

        private void VisitEntity(
            SymbolDraft entity,
            IReadOnlyDictionary<ExpressBoundSymbol, SymbolDraft> entities,
            IDictionary<ExpressBoundSymbol, VisitState> states,
            ISet<ExpressBoundSymbol> reported,
            IList<SymbolDraft> path)
        {
            if (states.TryGetValue(entity.Symbol, out var state))
            {
                if (state == VisitState.Visiting)
                {
                    ReportInheritanceCycle(entity.Symbol, path, reported);
                }

                return;
            }

            states[entity.Symbol] = VisitState.Visiting;
            path.Add(entity);
            foreach (var supertype in entity.Supertypes)
            {
                if (entities.TryGetValue(supertype, out var target))
                {
                    VisitEntity(target, entities, states, reported, path);
                }
            }

            path.RemoveAt(path.Count - 1);
            states[entity.Symbol] = VisitState.Visited;
        }

        private void ReportInheritanceCycle(
            ExpressBoundSymbol repeated,
            IEnumerable<SymbolDraft> path,
            ISet<ExpressBoundSymbol> reported)
        {
            foreach (var member in path.SkipWhile(item => !ReferenceEquals(item.Symbol, repeated)))
            {
                if (!reported.Add(member.Symbol))
                {
                    continue;
                }

                FindSchema(member.Symbol.DeclaringSchema).IsInvalid = true;
                AddDiagnostic(
                    _diagnostics,
                    "EXPRESS-BIND-INHERITANCE-CYCLE",
                    $"Entity '{member.Symbol.Name}' participates in an inheritance cycle.",
                    member.NameToken.Span.Start);
            }
        }

        private SchemaDraft FindSchema(ExpressBoundSchemaIdentity identity)
        {
            return _uniqueSchemas.Values.Single(schema => ReferenceEquals(schema.Identity, identity));
        }

        private void PropagateNewInvalidity()
        {
            var changed = true;
            while (changed)
            {
                changed = false;
                foreach (var schema in _uniqueSchemas.Values.Where(candidate => !candidate.IsInvalid))
                {
                    foreach (var import in schema.Imports)
                    {
                        if (!_uniqueSchemas.TryGetValue(import.SchemaNameToken.Text, out var target)
                            || !target.IsInvalid)
                        {
                            continue;
                        }

                        schema.IsInvalid = true;
                        changed = true;
                        AddDiagnostic(
                            _diagnostics,
                            "EXPRESS-BIND-INVALID-DEPENDENCY",
                            $"Schema '{schema.Identity.Name}' depends on invalid schema '{target.Identity.Name}'.",
                            import.SchemaNameToken.Span.Start);
                        break;
                    }
                }
            }
        }

        private static bool IsNamedType(ExpressDeclarationKind kind)
        {
            return kind is ExpressDeclarationKind.Entity or ExpressDeclarationKind.Type;
        }

        private static bool IsResource(ExpressDeclarationKind kind)
        {
            return kind is ExpressDeclarationKind.Constant
                or ExpressDeclarationKind.Entity
                or ExpressDeclarationKind.Function
                or ExpressDeclarationKind.Procedure
                or ExpressDeclarationKind.Type;
        }
    }

    private sealed class NameScope
    {
        private readonly Dictionary<string, ExpressBoundName> _names = new(_nameComparer);

        private readonly NameScope? _parent;

        internal NameScope(NameScope? parent)
        {
            _parent = parent;
        }

        internal bool TryAdd(ExpressBoundName name)
        {
            if (_names.ContainsKey(name.Name))
            {
                return false;
            }

            _names.Add(name.Name, name);
            return true;
        }

        internal bool TryResolve(string name, out ExpressBoundName result)
        {
            if (_names.TryGetValue(name, out var local))
            {
                result = local;
                return true;
            }

            if (_parent is not null)
            {
                return _parent.TryResolve(name, out result);
            }

            result = null!;
            return false;
        }
    }

    private sealed class SchemaDraft
    {
        internal SchemaDraft(
            ExpressBoundSchemaIdentity identity,
            ExpressRuleSyntax syntax,
            ExpressRuleSyntax body,
            ExpressTokenSyntax nameToken)
        {
            Identity = identity;
            Syntax = syntax;
            Body = body;
            NameToken = nameToken;
        }

        internal ExpressBoundSchemaIdentity Identity { get; }

        internal ExpressRuleSyntax Syntax { get; }

        internal ExpressRuleSyntax Body { get; }

        internal ExpressTokenSyntax NameToken { get; }

        internal List<SymbolDraft> Declarations { get; } = [];

        internal List<SymbolDraft> NestedDeclarations { get; } = [];

        internal List<ImportDraft> Imports { get; } = [];

        internal Dictionary<string, SymbolDraft> LocalSymbols { get; } = new(_nameComparer);

        internal Dictionary<string, ExpressBoundSymbol> LocalAndUseSymbols { get; } = new(_nameComparer);

        internal Dictionary<string, ExpressBoundSymbol> ReferenceSymbols { get; } = new(_nameComparer);

        internal Dictionary<string, ExpressBoundSymbol> ExportedNamedTypes { get; } = new(_nameComparer);

        internal Dictionary<string, ExpressBoundSymbol> ExportedResources { get; } = new(_nameComparer);

        internal List<ExpressBoundImport> ResolvedImports { get; } = [];

        internal List<ExpressBoundNameReference> NameReferences { get; } = [];

        internal bool IsInvalid { get; set; }
    }

    private sealed class SymbolDraft
    {
        internal SymbolDraft(
            ExpressBoundSymbol symbol,
            ExpressRuleSyntax syntax,
            ExpressTokenSyntax nameToken)
        {
            Symbol = symbol;
            Syntax = syntax;
            NameToken = nameToken;
        }

        internal string Name
        {
            get
            {
                return Symbol.Name;
            }
        }

        internal ExpressBoundSymbol Symbol { get; }

        internal ExpressRuleSyntax Syntax { get; }

        internal ExpressTokenSyntax NameToken { get; }

        internal ExpressBoundType? BoundType { get; set; }

        internal bool IsAbstract { get; set; }

        internal List<ExpressBoundSymbol> Supertypes { get; } = [];

        internal List<ExpressBoundAttribute> Attributes { get; } = [];
    }

    private sealed class ImportDraft
    {
        internal ImportDraft(
            ExpressImportKind kind,
            ExpressTokenSyntax schemaNameToken,
            IReadOnlyList<ImportItemDraft> items,
            ExpressSourceSpan span)
        {
            Kind = kind;
            SchemaNameToken = schemaNameToken;
            Items = items;
            Span = span;
        }

        internal ExpressImportKind Kind { get; }

        internal ExpressTokenSyntax SchemaNameToken { get; }

        internal IReadOnlyList<ImportItemDraft> Items { get; }

        internal ExpressSourceSpan Span { get; }
    }

    private sealed class ImportItemDraft
    {
        internal ImportItemDraft(
            ExpressTokenSyntax sourceToken,
            ExpressTokenSyntax localToken,
            ExpressSourceSpan span)
        {
            SourceToken = sourceToken;
            LocalToken = localToken;
            Span = span;
        }

        internal ExpressTokenSyntax SourceToken { get; }

        internal ExpressTokenSyntax LocalToken { get; }

        internal ExpressSourceSpan Span { get; }
    }

    private enum VisitState
    {
        Visiting = 0,

        Visited = 1,
    }
}