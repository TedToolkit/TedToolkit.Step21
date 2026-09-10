#if NETSTANDARD2_0
using System.IO.Compression;
using System.Reflection;

namespace TedToolkit.Step21;

internal static class ZipArchiveCompat
{
    private static readonly PropertyInfo? Crc32Property = typeof(ZipArchiveEntry).GetProperty(
        "Crc32",
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
    private static readonly FieldInfo? Crc32Field = typeof(ZipArchiveEntry).GetField(
        "_crc32",
        BindingFlags.Instance | BindingFlags.NonPublic);

    internal static bool TryGetCrc32(ZipArchiveEntry entry, out uint crc32)
    {
        var value = Crc32Property?.GetValue(entry) ?? Crc32Field?.GetValue(entry);
        if (value is uint result)
        {
            crc32 = result;
            return true;
        }

        crc32 = 0;
        return false;
    }
}
#endif
