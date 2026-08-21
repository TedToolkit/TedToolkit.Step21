namespace TedToolkit.Step21;

/// <summary>Identifies the exact numeric alternative retained by an EXPRESS NUMBER value.</summary>
public enum NumberValueKind
{
    /// <summary>The value retains an arbitrary-precision INTEGER.</summary>
    Integer = 0,

    /// <summary>The value retains an exact finite REAL.</summary>
    Real = 1,
}