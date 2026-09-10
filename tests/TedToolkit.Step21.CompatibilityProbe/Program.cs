using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

using TedToolkit.Step21;
using ProbeSchemaDescriptor = TedToolkit.Step21.Schemas.CompatibilityProbe.SchemaDescriptor;

internal static class Program
{
    private const string Unsigned =
        "ISO-10303-21;\n"
        + "HEADER;\n"
        + "FILE_DESCRIPTION(('compatibility probe'),'4;3');\n"
        + "FILE_NAME('probe.p21','2026-09-07T12:00:00Z',('Author'),('Org'),'Pre','System','Auth');\n"
        + "FILE_SCHEMA(('compatibility_probe'));\n"
        + "ENDSEC;\n"
        + "END-ISO-10303-21;";

    private static readonly SchemaDescriptor[] Descriptors = { ProbeSchemaDescriptor.Instance };

    private static int Main()
    {
        try
        {
            using var publicCertificate = LoadCertificate("primary-certificate.der.b64");
#if NET472
            using var cngKey = CngKey.Import(
                LoadBytes("primary-private-key.pkcs8.b64"),
                CngKeyBlobFormat.Pkcs8PrivateBlob);
            cngKey.SetProperty(new CngProperty(
                "Export Policy",
                BitConverter.GetBytes(3),
                CngPropertyOptions.None));
            using var key = new RSACng(cngKey);
#else
            using var key = RSA.Create();
            key.ImportPkcs8PrivateKey(LoadBytes("primary-private-key.pkcs8.b64"), out _);
#endif
            using var signingCertificate = publicCertificate.CopyWithPrivateKey(key);

            var structure = ExchangeStructure.Read(new StringReader(Unsigned), Descriptors);
            var output = new StringWriter();
            structure.Write(output, new ExchangeStructureWriteOptions(
                new IPart21SignatureSigner[] { new Part21CmsSigner(signingCertificate) }));
            var signed = output.ToString();
            var verification = new Part21SignatureVerificationOptions(
                new DateTimeOffset(2026, 9, 7, 12, 0, 0, TimeSpan.Zero),
                new[] { new Part21Certificate(publicCertificate.RawData) });
            var verified = ExchangeStructure.Read(
                new StringReader(signed),
                Descriptors,
                ExchangeStructureReadOptions.WithSignatureVerification(verification));
            var signer = verified.Signatures.Single().Signers.Single();
            Require(signer.CryptographicStatus == Part21SignatureCryptographicStatus.Valid, "CMS cryptographic status");
            Require(signer.TrustStatus == Part21SignatureTrustStatus.Trusted, "CMS explicit trust status");

            var invalid = signed.Replace("('Author')", "('Authoz')");
            RequireThrows<ExchangeStructureReadValidationException>(() => ExchangeStructure.Read(
                new StringReader(invalid),
                Descriptors,
                ExchangeStructureReadOptions.WithSignatureVerification(verification)));

            var malformed = signed.Substring(0, signed.IndexOf("SIGNATURE ", StringComparison.Ordinal))
                + "SIGNATURE AQ== ENDSEC;";
            RequireThrows<ExchangeStructureBindingException>(() => ExchangeStructure.Read(
                new StringReader(malformed),
                Descriptors,
                ExchangeStructureReadOptions.WithSignatureVerification(verification)));

            Console.WriteLine("STEP21_COMPATIBILITY_OK " + AppContext.TargetFrameworkName);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static X509Certificate2 LoadCertificate(string fileName)
    {
#if NET472
        return new X509Certificate2(LoadBytes(fileName));
#else
        return X509CertificateLoader.LoadCertificate(LoadBytes(fileName));
#endif
    }

    private static byte[] LoadBytes(string fileName) => Convert.FromBase64String(File.ReadAllText(
        Path.Combine(AppContext.BaseDirectory, "TestData", "Cms", fileName)));

    private static void Require(bool condition, string boundary)
    {
        if (!condition)
            throw new InvalidOperationException("Compatibility boundary failed: " + boundary + ".");
    }

    private static void RequireThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }

        throw new InvalidOperationException("Expected " + typeof(TException).Name + ".");
    }
}
