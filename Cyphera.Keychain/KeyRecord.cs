namespace Cyphera.Keychain;

/// <summary>
/// Immutable record describing a single versioned key.
/// </summary>
/// <param name="Ref">Logical key reference (e.g. "customer-primary").</param>
/// <param name="Version">Monotonically increasing version number.</param>
/// <param name="Status">Lifecycle status of this key.</param>
/// <param name="Algorithm">Algorithm identifier (e.g. "adf1").</param>
/// <param name="Material">Raw key bytes.</param>
/// <param name="Tweak">Optional tweak bytes.</param>
/// <param name="Metadata">Arbitrary string metadata.</param>
/// <param name="CreatedAt">Optional creation timestamp.</param>
public sealed record KeyRecord(
    string Ref,
    int Version,
    KeyStatus Status,
    string Algorithm,
    byte[] Material,
    byte[]? Tweak,
    IReadOnlyDictionary<string, string> Metadata,
    DateTimeOffset? CreatedAt);
