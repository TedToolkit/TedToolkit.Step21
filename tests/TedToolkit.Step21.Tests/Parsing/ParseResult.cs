namespace TedToolkit.Step21.Tests.Parsing;

internal sealed record ParseResult(IReadOnlyList<string> Errors, bool ReachedEndOfFile);
