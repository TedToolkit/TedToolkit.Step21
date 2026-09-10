using System.Security.Cryptography.X509Certificates;

namespace TedToolkit.Step21;

internal static class CertificateCompat
{
    internal static X509Certificate2 Load(ReadOnlySpan<byte> encoded)
    {
#pragma warning disable SYSLIB0057 // Avoid a net8 asset reference to Microsoft.Bcl.Cryptography when selected by net10.
        return new X509Certificate2(encoded.ToArray());
#pragma warning restore SYSLIB0057
    }
}
