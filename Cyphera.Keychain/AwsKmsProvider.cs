using System.Collections.Concurrent;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using CypheraKeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain;

/// <summary>
/// Key provider backed by AWS KMS data-key generation.
///
/// Each resolved ref is backed by an AES-256 data key generated via the
/// configured KMS master key. Plaintext keys are cached in memory.
/// </summary>
public sealed class AwsKmsProvider : IKeyProvider
{
    private readonly string _keyId;
    private readonly IAmazonKeyManagementService _kms;
    private readonly ConcurrentDictionary<string, KeyRecord> _cache = new();

    public AwsKmsProvider(string keyId, string region = "us-east-1", string? endpointUrl = null)
        : this(keyId, CreateClient(region, endpointUrl))
    {
    }

    public AwsKmsProvider(string keyId, IAmazonKeyManagementService kmsClient)
    {
        _keyId = keyId;
        _kms = kmsClient;
    }

    private static IAmazonKeyManagementService CreateClient(string region, string? endpointUrl)
    {
        var config = new AmazonKeyManagementServiceConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(region),
        };
        if (endpointUrl is not null)
            config.ServiceURL = endpointUrl;
        return new AmazonKeyManagementServiceClient(config);
    }

    private async Task<KeyRecord> GenerateAsync(string @ref, CancellationToken ct)
    {
        var request = new GenerateDataKeyRequest
        {
            KeyId = _keyId,
            KeySpec = DataKeySpec.AES_256,
            EncryptionContext = new Dictionary<string, string> { ["cyphera:ref"] = @ref },
        };
        GenerateDataKeyResponse resp;
        try
        {
            resp = await _kms.GenerateDataKeyAsync(request, ct);
        }
        catch (Exception ex)
        {
            throw new CypheraKeyNotFoundException(@ref, null, ex);
        }
        return new KeyRecord(
            @ref,
            1,
            KeyStatus.Active,
            "aes256",
            resp.Plaintext.ToArray(),
            null,
            new Dictionary<string, string>(),
            null);
    }

    public async Task<KeyRecord> ResolveAsync(string @ref, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(@ref, out var cached))
            return cached;
        var record = await GenerateAsync(@ref, ct);
        _cache.TryAdd(@ref, record);
        return _cache[@ref];
    }

    public async Task<KeyRecord> ResolveVersionAsync(string @ref, int version, CancellationToken ct = default)
    {
        if (version != 1)
            throw new CypheraKeyNotFoundException(@ref, version);
        return await ResolveAsync(@ref, ct);
    }
}
