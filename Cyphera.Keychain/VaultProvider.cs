using System.Text;
using System.Text.Json;
using VaultSharp;
using VaultSharp.V1.AuthMethods.Token;
using VaultSharp.V1.Commons;
using CypheraKeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain;

/// <summary>
/// Key provider backed by HashiCorp Vault KV v2 secrets engine.
///
/// Key records are stored at {mount}/{ref} with fields:
/// version (string or int), status, algorithm, material (hex or base64), tweak (optional).
/// </summary>
public sealed class VaultProvider : IKeyProvider
{
    private readonly IVaultClient _client;
    private readonly string _mount;

    public VaultProvider(string address, string token, string mount = "secret")
        : this(CreateClient(address, token), mount)
    {
    }

    public VaultProvider(IVaultClient client, string mount = "secret")
    {
        _client = client;
        _mount = mount;
    }

    private static IVaultClient CreateClient(string address, string token)
    {
        var settings = new VaultClientSettings(address, new TokenAuthMethodInfo(token));
        return new VaultClient(settings);
    }

    private async Task<Dictionary<string, object?>> ReadDataAsync(string @ref)
    {
        try
        {
            var secret = await _client.V1.Secrets.KeyValue.V2.ReadSecretAsync(@ref, mountPoint: _mount);
            return secret.Data.Data.ToDictionary(k => k.Key, v => v.Value);
        }
        catch (Exception ex) when (ex.Message.Contains("404") || ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            throw new CypheraKeyNotFoundException(@ref, null, ex);
        }
        catch (Exception ex)
        {
            throw new CypheraKeyNotFoundException(@ref, null, ex);
        }
    }

    private static byte[] DecodeBytes(string value)
    {
        var s = value.Trim();
        if (s.Length % 2 == 0)
        {
            try { return Convert.FromHexString(s); } catch { }
        }
        return Convert.FromBase64String(s);
    }

    private KeyRecord ParseOne(string @ref, JsonElement element)
    {
        var version = element.TryGetProperty("version", out var v) ? v.GetInt32() : 1;
        var statusStr = element.TryGetProperty("status", out var s) ? s.GetString() ?? "active" : "active";
        var status = statusStr.ToLowerInvariant() switch
        {
            "deprecated" => KeyStatus.Deprecated,
            "disabled" => KeyStatus.Disabled,
            _ => KeyStatus.Active,
        };
        var algorithm = element.TryGetProperty("algorithm", out var a) ? a.GetString() ?? "adf1" : "adf1";
        var materialStr = element.TryGetProperty("material", out var m) ? m.GetString() ?? "" : "";
        var material = materialStr.Length > 0 ? DecodeBytes(materialStr) : Array.Empty<byte>();
        byte[]? tweak = null;
        if (element.TryGetProperty("tweak", out var t) && t.ValueKind != JsonValueKind.Null)
            tweak = DecodeBytes(t.GetString() ?? "");
        return new KeyRecord(@ref, version, status, algorithm, material, tweak,
            new Dictionary<string, string>(), null);
    }

    private List<KeyRecord> ParseRecords(string @ref, Dictionary<string, object?> data)
    {
        if (data.TryGetValue("versions", out var versionsObj) && versionsObj is not null)
        {
            var versionsJson = versionsObj.ToString()!;
            var arr = JsonSerializer.Deserialize<JsonElement[]>(versionsJson)!;
            return arr.Select(e => ParseOne(@ref, e)).ToList();
        }
        // Single record: convert the dict to JsonElement
        var json = JsonSerializer.Serialize(data);
        var element = JsonSerializer.Deserialize<JsonElement>(json);
        return new List<KeyRecord> { ParseOne(@ref, element) };
    }

    public async Task<KeyRecord> ResolveAsync(string @ref, CancellationToken ct = default)
    {
        var data = await ReadDataAsync(@ref);
        var records = ParseRecords(@ref, data);
        var active = records.Where(r => r.Status == KeyStatus.Active).ToList();
        if (active.Count == 0)
            throw new NoActiveKeyException(@ref);
        return active.MaxBy(r => r.Version)!;
    }

    public async Task<KeyRecord> ResolveVersionAsync(string @ref, int version, CancellationToken ct = default)
    {
        var data = await ReadDataAsync(@ref);
        var records = ParseRecords(@ref, data);
        var record = records.FirstOrDefault(r => r.Version == version);
        if (record is null)
            throw new CypheraKeyNotFoundException(@ref, version);
        if (record.Status == KeyStatus.Disabled)
            throw new KeyDisabledException(@ref, version);
        return record;
    }
}
