namespace Cyphera.Keychain;

/// <summary>
/// Thrown when no key record exists for the requested reference (and optional version).
/// </summary>
public sealed class KeyNotFoundException : Exception
{
    /// <summary>The logical key reference that was not found.</summary>
    public string Ref { get; }

    /// <summary>The specific version that was not found, or <c>null</c> if no version was requested.</summary>
    public int? Version { get; }

    public KeyNotFoundException(string @ref, int? version = null)
        : base(version.HasValue
            ? $"Key not found: ref=\"{@ref}\" version={version.Value}"
            : $"Key not found: ref=\"{@ref}\"")
    {
        Ref = @ref;
        Version = version;
    }

    public KeyNotFoundException(string @ref, int? version, Exception innerException)
        : base(version.HasValue
            ? $"Key not found: ref=\"{@ref}\" version={version.Value}"
            : $"Key not found: ref=\"{@ref}\"",
            innerException)
    {
        Ref = @ref;
        Version = version;
    }
}

/// <summary>
/// Thrown when the requested key record exists but is <see cref="KeyStatus.Disabled"/>.
/// </summary>
public sealed class KeyDisabledException : Exception
{
    /// <summary>The logical key reference.</summary>
    public string Ref { get; }

    /// <summary>The version that is disabled.</summary>
    public int Version { get; }

    public KeyDisabledException(string @ref, int version)
        : base($"Key is disabled: ref=\"{@ref}\" version={version}")
    {
        Ref = @ref;
        Version = version;
    }
}

/// <summary>
/// Thrown when records exist for a reference but none have <see cref="KeyStatus.Active"/> status.
/// </summary>
public sealed class NoActiveKeyException : Exception
{
    /// <summary>The logical key reference.</summary>
    public string Ref { get; }

    public NoActiveKeyException(string @ref)
        : base($"No active key found for ref=\"{@ref}\"")
    {
        Ref = @ref;
    }
}
