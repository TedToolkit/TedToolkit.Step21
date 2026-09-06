using TedToolkit.Step21.PackedConsumer;

namespace TedToolkit.Step21.Tests.PackageFixtureTests;

internal sealed class ReferenceSignatureTests
{
    /// <summary>Accepts formatting changes without erasing physical values or quoted text.</summary>
    [Test]
    public async Task Should_normalize_only_equivalent_fixture_spelling()
    {
        const string before = "DATA; #1=POINT('a b', (1.,2.E0,-3.00)); ENDSEC;";
        const string after = "DATA;\n#1 = POINT('a b',(1, 2.000, -3)); /* comment */ ENDSEC;";
        await Assert.That(Part21FixtureSignature.Create(before)).IsEqualTo(Part21FixtureSignature.Create(after));
    }

    /// <summary>Rejects degree-preserving rewiring and changes to the values or kinds of identified instances.</summary>
    [Test]
    [Arguments("#1=EDGE('',#3,#4,.T.);#2=EDGE('',#4,#3,.T.);", "#1=EDGE('',#4,#3,.T.);#2=EDGE('',#3,#4,.T.);")]
    [Arguments("#1=POINT('',(1.,2.,3.));", "#1=POINT('',(2.,1.,3.));")]
    [Arguments("#1=POINT('',(1.,2.,3.));", "#1=DIRECTION('',(1.,2.,3.));")]
    [Arguments("#1=POINT('a b',(1.,2.,3.));", "#1=POINT('ab',(1.,2.,3.));")]
    public async Task Should_detect_graph_changes(string before, string after)
    {
        await Assert.That(Part21FixtureSignature.Create("DATA;" + before + "ENDSEC;"))
            .IsNotEqualTo(Part21FixtureSignature.Create("DATA;" + after + "ENDSEC;"));
    }
}
