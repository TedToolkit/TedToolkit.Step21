using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace TedToolkit.Step21;

internal static class EncodingCompat
{
    internal static int GetBytes(Encoding encoding, ReadOnlySpan<char> source, Span<byte> destination)
    {
#if NETSTANDARD2_0
        var encoded = encoding.GetBytes(source.ToArray());
        encoded.AsSpan().CopyTo(destination);
        return encoded.Length;
#else
        return encoding.GetBytes(source, destination);
#endif
    }

    internal static string GetString(Encoding encoding, ReadOnlySpan<byte> source)
    {
#if NETSTANDARD2_0
        return encoding.GetString(source.ToArray());
#else
        return encoding.GetString(source);
#endif
    }

    internal static string GetSha256Fingerprint(X509Certificate2 certificate)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(certificate.RawData)).Replace("-", string.Empty);
    }
}
