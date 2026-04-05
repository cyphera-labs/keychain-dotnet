namespace Cyphera.Keychain;

/// <summary>
/// Reads keys from environment variables.
/// </summary>
/// <remarks>
/// For a ref of "customer-primary" and prefix "CYPHERA" the provider expects:
/// <list type="bullet">
///   <item><c>CYPHERA_CUSTOMER_PRIMARY_KEY</c> — key material as hex or base64 (required)</item>
///   <item><c>CYPHERA_CUSTOMER_PRIMARY_TWEAK</c> — tweak as hex or base64 (optional)</item>
/// </list>
/// All resolved keys have version 1 and status <see cref="KeyStatus.Active"/>.
/// </remarks>
public sealed class EnvProvider(string prefix) : IKeyProvider
{
    private const string DefaultAlgorithm = "adf1";

    /// <inheritdoc/>
    public Task<KeyRecord> ResolveAsync(string @ref, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(Resolve(@ref, 1));
    }

    /// <inheritdoc/>
    public Task<KeyRecord> ResolveVersionAsync(string @ref, int version, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (version != 1)
            throw new KeyNotFoundException(@ref, version);

        return Task.FromResult(Resolve(@ref, version));
    }

    private KeyRecord Resolve(string @ref, int version)
    {
        var normalized = NormalizeRef(@ref);
        var keyVar = $"{prefix}_{normalized}_KEY";
        var tweakVar = $"{prefix}_{normalized}_TWEAK";

        var keyValue = Environment.GetEnvironmentVariable(keyVar);
        if (string.IsNullOrEmpty(keyValue))
            throw new KeyNotFoundException(@ref, version == 1 ? null : version);

        var material = DecodeBytes(keyValue);

        byte[]? tweak = null;
        var tweakValue = Environment.GetEnvironmentVariable(tweakVar);
        if (!string.IsNullOrEmpty(tweakValue))
            tweak = DecodeBytes(tweakValue);

        return new KeyRecord(
            Ref: @ref,
            Version: 1,
            Status: KeyStatus.Active,
            Algorithm: DefaultAlgorithm,
            Material: material,
            Tweak: tweak,
            Metadata: new Dictionary<string, string>(),
            CreatedAt: null);
    }

    /// <summary>
    /// Normalizes a ref for env-var lookup: uppercase, replace '-' with '_'.
    /// </summary>
    private static string NormalizeRef(string @ref) =>
        @ref.ToUpperInvariant().Replace('-', '_');

    /// <summary>
    /// Tries hex decoding first; falls back to base64.
    /// </summary>
    internal static byte[] DecodeBytes(string value)
    {
        try
        {
            return Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            return Convert.FromBase64String(value);
        }
    }
}
