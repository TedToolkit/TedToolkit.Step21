using System.Xml.Linq;

namespace TedToolkit.Step21.Tests.ExchangeStructureSyntaxTests;

internal sealed class InternalBoundaryTests
{
    /// <summary>
    /// Verifies that the Part 21 syntax graph and parser exist only as internal runtime infrastructure.
    /// </summary>
    [Test]
    public async Task Should_remain_internal_when_syntax_handoff_is_inspected()
    {
        var assembly = typeof(SourceLocation).Assembly;
        var graphType = assembly.GetType("TedToolkit.Step21.Syntax.ExchangeStructureSyntax");
        var parserType = assembly.GetType("TedToolkit.Step21.Syntax.ExchangeStructureSyntaxParser");

        using (Assert.Multiple())
        {
            await Assert.That(graphType is not null).IsTrue();
            await Assert.That(graphType?.IsNotPublic == true).IsTrue();
            await Assert.That(parserType is not null).IsTrue();
            await Assert.That(parserType?.IsNotPublic == true).IsTrue();
            await Assert.That(
                    assembly.GetExportedTypes().Any(type => type.Namespace == "TedToolkit.Step21.Syntax"))
                .IsFalse();
        }
    }

    /// <summary>
    /// Verifies that internal syntax machinery is not published in the runtime XML documentation artifact.
    /// </summary>
    [Test]
    public async Task Should_remain_absent_when_runtime_xml_documentation_is_generated()
    {
        var assembly = typeof(SourceLocation).Assembly;
        var xmlPath = Path.ChangeExtension(assembly.Location, ".xml");
        var syntaxMembers = XDocument.Load(xmlPath)
            .Descendants("member")
            .Select(value => (string?)value.Attribute("name"))
            .Where(value => value?.Contains("TedToolkit.Step21.Syntax", StringComparison.Ordinal) == true)
            .ToArray();

        await Assert.That(syntaxMembers).IsEmpty();
    }
}
