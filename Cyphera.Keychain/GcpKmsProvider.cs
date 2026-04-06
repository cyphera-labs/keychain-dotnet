using System.Collections.Concurrent;
using System.Security.Cryptography;
using Google.Cloud.Kms.V1;
using Google.Protobuf;
using CypheraKeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain;

/// <summary>
/// Key provider backed by GCP Cloud KMS.
///
/// Generates a random AES-256 data key, wraps it via GCP KMS encrypt, and
/// caches the plaintext for the lifetime of the provider.
/// </summary>
public sealed class GcpKmsProvider : IKeyProvider
{
    private readonly string _keyName;
    private readonly KeyManagementServiceClient _client;
    private readonly ConcurrentDictionary<string, byte[]> _plaintextCache = new();

    public GcpKmsProvider(string keyName)
        : this(keyName, KeyManagementServiceClient.Create())
    {
    }

    public GcpKmsProvider(string keyName, KeyManagementServiceClient client)
    {
        _keyName = keyName;
        _client = client;
    }

    private async Task<byte[]> WrapNewKeyAsync(string @ref, CancellationToken ct)
    {
        var plaintext = RandomNumberGenerator.GetBytes(32);
        try
        {
            await _client.EncryptAsync(new EncryptRequest
            {
                Name = _keyName,
                Plaintext = ByteString.CopyFrom(plaintext),
                AdditionalAuthenticatedData = ByteString.CopyFromUtf8(@ref),
            }, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            throw new CypheraKeyNotFoundException(@ref, null, ex);
        }
        return plaintext;
    }

    public async Task<KeyRecord> ResolveAsync(string @ref, CancellationToken ct = default)
    {
        if (!_plaintextCache.TryGetValue(@ref, out var material))
        {
            material = await WrapNewKeyAsync(@ref, ct);
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
