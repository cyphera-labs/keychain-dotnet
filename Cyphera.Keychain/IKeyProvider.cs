namespace Cyphera.Keychain;

/// <summary>
/// Abstraction for resolving key records by reference and version.
/// </summary>
public interface IKeyProvider
{
    /// <summary>
    /// Returns the highest-version <see cref="KeyStatus.Active"/> record for the given reference.
    /// </summary>
    /// <param name="ref">Logical key reference.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">No record exists for <paramref name="ref"/>.</exception>
    /// <exception cref="NoActiveKeyException">Records exist but none are active.</exception>
    Task<KeyRecord> ResolveAsync(string @ref, CancellationToken ct = default);

    /// <summary>
    /// Returns the record for the given reference at the specified version.
    /// </summary>
    /// <param name="ref">Logical key reference.</param>
    /// <param name="version">Exact version number.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="KeyNotFoundException">No record exists for <paramref name="ref"/> at <paramref name="version"/>.</exception>
    /// <exception cref="KeyDisabledException">The record at <paramref name="version"/> is disabled.</exception>
    Task<KeyRecord> ResolveVersionAsync(string @ref, int version, CancellationToken ct = default);
}
