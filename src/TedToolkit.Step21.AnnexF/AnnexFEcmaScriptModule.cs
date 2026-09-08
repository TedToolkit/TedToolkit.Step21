using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace TedToolkit.Step21.AnnexF;

/// <summary>Provides the deterministic, engine-neutral ISO 10303-21 Annex F ECMAScript source.</summary>
public static class AnnexFEcmaScriptModule
{
    /// <summary>The version of the JSON bridge contract consumed by the module.</summary>
    public const int BridgeFormatVersion = 1;

    /// <summary>The stable packaged module file name.</summary>
    public const string FileName = "AnnexF.js";

    private static readonly string _source = LoadSource();
    private static readonly string _sourceSha256 = Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(_source)));

    /// <summary>Gets the exact embedded ECMAScript source.</summary>
    public static string Source => _source;

    /// <summary>Gets the upper-case SHA-256 identity of the UTF-8 source bytes.</summary>
    public static string SourceSha256 => _sourceSha256;

    private static string LoadSource()
    {
        using var stream = typeof(AnnexFEcmaScriptModule).Assembly.GetManifestResourceStream(
            "TedToolkit.Step21.AnnexF.AnnexF.js")
            ?? throw new InvalidOperationException("The Annex F ECMAScript resource is missing.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }
}