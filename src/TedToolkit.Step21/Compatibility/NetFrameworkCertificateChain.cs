#if NETSTANDARD2_0
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace TedToolkit.Step21;

internal static class NetFrameworkCertificateChain
{
    private const uint CertificateEncoding = 0x00010001;
    private const uint StoreCreateNew = 0x00002000;
    private const uint StoreAddAlways = 4;
    private const uint CacheOnlyUrlRetrieval = 0x00000004;
    private const uint DisableAia = 0x00002000;
    private const uint UntrustedRoot = 0x00000020;

    internal static bool IsTrusted(
        X509Certificate2 certificate,
        DateTime verificationTime,
        IReadOnlyCollection<X509Certificate2> trustedRoots,
        IReadOnlyCollection<X509Certificate2> explicitCertificates)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return false;

        var store = CertOpenStore(new IntPtr(2), CertificateEncoding, IntPtr.Zero, StoreCreateNew, IntPtr.Zero);
        if (store == IntPtr.Zero)
            return false;

        IntPtr chainContext = IntPtr.Zero;
        try
        {
            foreach (var candidate in explicitCertificates)
            {
                var encoded = candidate.RawData;
                if (!CertAddEncodedCertificateToStore(
                        store,
                        CertificateEncoding,
                        encoded,
                        encoded.Length,
                        StoreAddAlways,
                        IntPtr.Zero))
                {
                    return false;
                }
            }

            var parameters = new CertChainPara
            {
                Size = (uint)Marshal.SizeOf<CertChainPara>(),
            };
            var fileTimeValue = verificationTime.ToUniversalTime().ToFileTimeUtc();
            var fileTime = new FileTime
            {
                Low = unchecked((uint)fileTimeValue),
                High = unchecked((uint)(fileTimeValue >> 32)),
            };
            if (!CertGetCertificateChain(
                    IntPtr.Zero,
                    certificate.Handle,
                    ref fileTime,
                    store,
                    ref parameters,
                    CacheOnlyUrlRetrieval | DisableAia,
                    IntPtr.Zero,
                    out chainContext)
                || chainContext == IntPtr.Zero)
            {
                return false;
            }

            var context = Marshal.PtrToStructure<CertChainContext>(chainContext);
            if ((context.TrustStatus.ErrorStatus & ~UntrustedRoot) != 0 || context.ChainCount != 1)
                return false;

            var simpleChainPointer = Marshal.ReadIntPtr(context.Chains);
            var simpleChain = Marshal.PtrToStructure<CertSimpleChain>(simpleChainPointer);
            if (simpleChain.ElementCount == 0)
                return false;

            var explicitFingerprints = new HashSet<string>(
                explicitCertificates.Select(EncodingCompat.GetSha256Fingerprint),
                StringComparer.Ordinal);
            var trustedFingerprints = new HashSet<string>(
                trustedRoots.Select(EncodingCompat.GetSha256Fingerprint),
                StringComparer.Ordinal);
            string? rootFingerprint = null;
            for (var index = 0; index < simpleChain.ElementCount; index++)
            {
                var elementPointer = Marshal.ReadIntPtr(simpleChain.Elements, checked(index * IntPtr.Size));
                var element = Marshal.PtrToStructure<CertChainElement>(elementPointer);
                var encoded = ReadEncodedCertificate(element.CertificateContext);
                using var elementCertificate = new X509Certificate2(encoded);
                var fingerprint = EncodingCompat.GetSha256Fingerprint(elementCertificate);
                if (!explicitFingerprints.Contains(fingerprint))
                    return false;
                rootFingerprint = fingerprint;
            }

            return rootFingerprint is not null && trustedFingerprints.Contains(rootFingerprint);
        }
        catch (Exception exception) when (exception is Win32Exception
            or CryptographicException
            or ExternalException
            or ArgumentException
            or InvalidOperationException
            or DllNotFoundException
            or EntryPointNotFoundException)
        {
            return false;
        }
        finally
        {
            if (chainContext != IntPtr.Zero)
                CertFreeCertificateChain(chainContext);
            _ = CertCloseStore(store, 0);
        }
    }

    private static byte[] ReadEncodedCertificate(IntPtr certificateContext)
    {
        var context = Marshal.PtrToStructure<CertContext>(certificateContext);
        var encoded = new byte[context.EncodedLength];
        Marshal.Copy(context.Encoded, encoded, 0, encoded.Length);
        return encoded;
    }

    [DllImport("crypt32.dll", SetLastError = true)]
    private static extern IntPtr CertOpenStore(
        IntPtr storeProvider,
        uint encodingType,
        IntPtr cryptographicProvider,
        uint flags,
        IntPtr parameters);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CertAddEncodedCertificateToStore(
        IntPtr certificateStore,
        uint encodingType,
        byte[] encodedCertificate,
        int encodedCertificateLength,
        uint disposition,
        IntPtr certificateContext);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CertGetCertificateChain(
        IntPtr chainEngine,
        IntPtr certificateContext,
        ref FileTime verificationTime,
        IntPtr additionalStore,
        ref CertChainPara chainParameters,
        uint flags,
        IntPtr reserved,
        out IntPtr chainContext);

    [DllImport("crypt32.dll")]
    private static extern void CertFreeCertificateChain(IntPtr chainContext);

    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CertCloseStore(IntPtr certificateStore, uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        internal uint Low;
        internal uint High;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertEnhKeyUsage
    {
        internal uint UsageIdentifierCount;
        internal IntPtr UsageIdentifiers;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertUsageMatch
    {
        internal uint Type;
        internal CertEnhKeyUsage Usage;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertChainPara
    {
        internal uint Size;
        internal CertUsageMatch RequestedUsage;
        internal CertUsageMatch RequestedIssuancePolicy;
        internal uint UrlRetrievalTimeout;
        [MarshalAs(UnmanagedType.Bool)] internal bool CheckRevocationFreshnessTime;
        internal uint RevocationFreshnessTime;
        internal IntPtr CacheResyncTime;
        internal IntPtr StrongSignParameters;
        internal uint StrongSignFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertTrustStatus
    {
        internal uint ErrorStatus;
        internal uint InformationStatus;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertChainContext
    {
        internal uint Size;
        internal CertTrustStatus TrustStatus;
        internal uint ChainCount;
        internal IntPtr Chains;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertSimpleChain
    {
        internal uint Size;
        internal CertTrustStatus TrustStatus;
        internal uint ElementCount;
        internal IntPtr Elements;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertChainElement
    {
        internal uint Size;
        internal IntPtr CertificateContext;
        internal CertTrustStatus TrustStatus;
        internal IntPtr RevocationInfo;
        internal IntPtr IssuanceUsage;
        internal IntPtr ApplicationUsage;
        internal IntPtr ExtendedErrorInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CertContext
    {
        internal uint EncodingType;
        internal IntPtr Encoded;
        internal int EncodedLength;
        internal IntPtr CertificateInfo;
        internal IntPtr CertificateStore;
    }
}
#endif
