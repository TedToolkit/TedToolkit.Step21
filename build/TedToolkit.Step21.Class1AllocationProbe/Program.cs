using System.Globalization;
using System.Numerics;
using System.Text.Json;

using TedToolkit.Step21;

const string Source = """
    ISO-10303-21;
    HEADER;
    FILE_DESCRIPTION(('class-1 allocation probe'),'4;1');
    FILE_NAME('allocation.step','2026-09-10T00:00:00+08:00',(''),(''),'TedToolkit','TedToolkit','');
    FILE_SCHEMA(('ALLOCATION_SCHEMA'));
    ENDSEC;
    DATA;
    #1=SAMPLE(1);
    ENDSEC;
    END-ISO-10303-21;
    """;

var iterations = ReadIterations(args);
var descriptor = new AllocationSchemaDescriptor();
SchemaDescriptor[] descriptors = [descriptor];

for (var index = 0; index < 10; index++)
    RunJourney(descriptors);

GC.Collect();
GC.WaitForPendingFinalizers();
GC.Collect();

var samples = new long[iterations];
for (var index = 0; index < samples.Length; index++)
{
    var before = GC.GetAllocatedBytesForCurrentThread();
    RunJourney(descriptors);
    samples[index] = GC.GetAllocatedBytesForCurrentThread() - before;
}

Array.Sort(samples);
var median = samples.Length % 2 == 0
    ? checked((samples[(samples.Length / 2) - 1] + samples[samples.Length / 2]) / 2)
    : samples[samples.Length / 2];

Console.WriteLine(JsonSerializer.Serialize(new
{
    scenario = "class1-read-write-reread",
    iterations,
    medianAllocatedBytes = median,
    minimumAllocatedBytes = samples[0],
    maximumAllocatedBytes = samples[^1],
}));

static int ReadIterations(string[] arguments)
{
    if (arguments.Length == 0)
        return 20;
    if (arguments.Length == 2
        && arguments[0] == "--iterations"
        && int.TryParse(arguments[1], NumberStyles.None, CultureInfo.InvariantCulture, out var iterations)
        && iterations > 0)
    {
        return iterations;
    }

    throw new ArgumentException("Use --iterations followed by a positive integer.", nameof(arguments));
}

static void RunJourney(IReadOnlyCollection<SchemaDescriptor> descriptors)
{
    var structure = ExchangeStructure.Read(new StringReader(Source), descriptors);
    var destination = new StringWriter(CultureInfo.InvariantCulture);
    structure.Write(destination);
    var output = destination.ToString();
    var reread = ExchangeStructure.Read(new StringReader(output), descriptors);
    if (reread.Entities.Single() is not SampleEntity sample || sample.Value != BigInteger.One)
        throw new InvalidOperationException("The fixed class-1 journey did not preserve its entity value.");
}

file sealed class SampleEntity : Entity
{
    public BigInteger Value { get; set; }

    public override IEnumerable<Entity> DirectReferences => [];
}

file sealed class AllocationSchemaDescriptor : SchemaDescriptor
{
    public override SchemaName Name { get; } = new("ALLOCATION_SCHEMA");

    protected override Entity? AllocateEntityCore(IReadOnlyList<string> entityNames) =>
        entityNames.Count == 1 && string.Equals(entityNames[0], "SAMPLE", StringComparison.OrdinalIgnoreCase)
            ? new SampleEntity()
            : null;

    protected override IReadOnlyList<Step21Diagnostic> HydrateEntityCore(
        ExchangeStructure structure,
        Entity value,
        IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> components)
    {
        if (value is SampleEntity sample
            && components.Count == 1
            && string.Equals(components[0].Key, "SAMPLE", StringComparison.OrdinalIgnoreCase)
            && components[0].Value.Count == 1
            && components[0].Value[0].TryGetInteger(out var integer))
        {
            sample.Value = integer;
            return [];
        }

        return [new Step21Diagnostic(
            "P21-ALLOCATION-PROBE",
            Step21DiagnosticSeverity.Error,
            "The fixed allocation probe entity shape is invalid.")];
    }

    protected override ValidationResult ValidateCore(
        ExchangeStructure structure,
        IReadOnlyList<KeyValuePair<string, Entity>> entities) => new([]);

    protected override IReadOnlyList<Step21Diagnostic> GetCapabilityDiagnosticsCore(
        ExchangeStructure structure) => [];

    protected override IReadOnlyList<KeyValuePair<string, IReadOnlyList<ParameterValue>>> ProjectEntityCore(
        Entity value) => value is SampleEntity sample
            ? [new("SAMPLE", [ParameterValue.FromInteger(sample.Value)])]
            : [];
}
