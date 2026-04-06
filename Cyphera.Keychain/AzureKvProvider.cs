using System.Collections.Concurrent;
using System.Security.Cryptography;
using Azure.Identity;
using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Core;
using CypheraKeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain;

/// <summary>
/// Key provider backed by Azure Key Vault.
///
/// Generates a random AES-256 data key, wraps it with an Azure Key Vault RSA
/// key (RSA-OAEP), and caches the plaintext.
/// </summary>
public sealed class AzureKvProvider : IKeyProvider
{
    private readonly string _keyName;
    private readonly KeyClient _keyClient;
    private readonly TokenCredential _credential;
    private readonly ConcurrentDictionary<string, byte[]> _plaintextCache = new();

    public AzureKvProvider(string vaultUrl, string keyName, TokenCredential? credential = null)
        : this(new KeyClient(new Uri(vaultUrl), credential ?? new DefaultAzureCredential()),
               keyName,
               credential ?? new DefaultAzureCredential())
    {
    }

    public AzureKvProvider(KeyClient keyClient, string keyName, TokenCredential credential)
    {
        _keyClient = keyClient;
        _keyName = keyName;
        _credential = credential;
    }

    private async Task<byte[]> WrapNewKeyAsync(CancellationToken ct)
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        var key = await _keyClient.GetKeyAsync(_keyName, cancellationToken: ct);
        var cryptoClient = new CryptographyClient(new Uri(key.Value.Id), _credential);
        await cryptoClient.WrapKeyAsync(KeyWrapAlgorithm.RsaOaep, plaintext, ct);
        return plaintext;
    }

    public async Task<KeyRecord> ResolveAsync(string @ref, CancellationToken ct = default)
    {
        if (!_plaintextCache.TryGetValue(@ref, out var material))
        {
            try
            {
                material = await WrapNewKeyAsync(ct);
            }
            catch (Exception ex)
            {
                throw new CypheraKeyNotFoundException(@ref, null, ex);
            }
            _plaintextCache.TryAdd(@ref, material);
            material = _plaintextCache[@ref];
        }
        return new KeyRecord(@ref, 1, KeyStatus.Active, "aes256", material, null,
            new Dictionary<string, string>(), null);
    }

    public async Task<KeyRecord> ResolveVersionAsync(string @ref, int version, CancellationToken ct = default)
    {
        if (version != 1)
            throw new CypheraKeyNotFoundException(@ref, version);
        return await ResolveAsync(@ref, ct);
    }
}
