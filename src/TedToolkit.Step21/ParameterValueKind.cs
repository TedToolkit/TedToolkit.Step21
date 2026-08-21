namespace TedToolkit.Step21;

/// <summary>Identifies one schema-neutral ISO 10303-21 parameter alternative.</summary>
public enum ParameterValueKind
{
    /// <summary>The explicit OPTIONAL absence marker <c>$</c>.</summary>
    Omitted = 0,

    /// <summary>The derived attribute marker <c>*</c>.</summary>
    Derived = 1,

    /// <summary>An arbitrary-precision INTEGER.</summary>
    Integer = 2,

    /// <summary>An exact finite REAL.</summary>
    Real = 3,

    /// <summary>A decoded STRING.</summary>
    String = 4,

    /// <summary>A bit-accurate BINARY.</summary>
    Binary = 5,

    /// <summary>A two-state BOOLEAN.</summary>
    Boolean = 6,

    /// <summary>A three-state LOGICAL.</summary>
    Logical = 7,

    /// <summary>An untyped enumeration symbol.</summary>
    Enumeration = 8,

    /// <summary>A resolved runtime entity occurrence.</summary>
    Entity = 9,

    /// <summary>A recursively nested aggregate parameter.</summary>
    Aggregate = 10,

    /// <summary>A keyword-qualified typed parameter.</summary>
    Typed = 11,
}