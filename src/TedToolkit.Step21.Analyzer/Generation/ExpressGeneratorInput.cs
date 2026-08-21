// -----------------------------------------------------------------------
// <copyright file="ExpressGeneratorInput.cs" company="TedToolkit">
// Copyright (c) TedToolkit. All rights reserved.
// Licensed under the LGPL-3.0 license. See COPYING, COPYING.LESSER file in the project root for full license information.
// </copyright>
// -----------------------------------------------------------------------

namespace TedToolkit.Step21.Analyzer.Generation;

/// <summary>
/// Retains one AdditionalFile without granting the generator arbitrary file-system access.
/// </summary>
internal readonly struct ExpressGeneratorInput : IEquatable<ExpressGeneratorInput>
{
    /// <summary>
    /// Initializes one AdditionalFile snapshot.
    /// </summary>
    /// <param name="path">The logical diagnostic path.</param>
    /// <param name="text">The supplied text, or <see langword="null"/> when Roslyn could not read it.</param>
    internal ExpressGeneratorInput(string path, string? text)
    {
        Path = path;
        Text = text;
    }

    /// <summary>
    /// Gets the logical diagnostic path.
    /// </summary>
    internal string Path { get; }

    /// <summary>
    /// Gets the supplied text.
    /// </summary>
    internal string? Text { get; }

    /// <inheritdoc />
    public bool Equals(ExpressGeneratorInput other)
    {
        return string.Equals(Path, other.Path, StringComparison.Ordinal)
            && string.Equals(Text, other.Text, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return obj is ExpressGeneratorInput other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return ((Path?.GetHashCode() ?? 0) * 397) ^ (Text?.GetHashCode() ?? 0);
        }
    }
}