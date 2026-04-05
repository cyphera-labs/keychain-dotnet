using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cyphera.Keychain;

/// <summary>
/// Loads keys from a JSON file at construction time.
/// </summary>
/// <remarks>
/// Expected file format:
/// <code>
/// {
///   "keys": [
///     {
///       "ref": "customer-primary",
///       "version": 1,
///       "status": "active",
///       "algorithm": "adf1",
///       "material": "&lt;hex or base64&gt;",
///       "tweak": "&lt;hex or base64&gt;",
///       "metadata": { "env": "prod" },
///       "created_at": "2024-01-01T00:00:00Z"
///     }
///   ]
/// }
/// </code>
/// </remarks>
public sealed class FileProvider : IKeyProvider
{
    private readonly Dictionary<string, List<KeyRecord>> _store;

    public FileProvider(string path)
    {
        var json = File.ReadAllText(path);
        var root = JsonSerializer.Deserialize<KeyFile>(json, JsonOptions)
                   ?? throw new InvalidOperationException($"Failed to parse key file: {path}");

        _store = new Dictionary<string, List<KeyRecord>>(StringComparer.Ordinal);

        foreach (var entry in root.Keys ?? [])
        {
            var record = ToKeyRecord(entry);
            if (!_store.TryGetValue(record.Ref, out var list))
            {
                list = new List<KeyRecord>();
                _store[record.Ref] = list;
            }
            list.Add(record);
        }

        // Sort each list descending by version.
        foreach (var list in _store.Values)
            list.Sort(static (a, b) => b.Version.CompareTo(a.Version));
    }

    /// <inheritdoc/>
    public Task<KeyRecord> ResolveAsync(string @ref, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_store.TryGetValue(@ref, out var list) || list.Count == 0)
            throw new KeyNotFoundException(@ref);

        foreach (var record in list)
        {
            if (record.Status == KeyStatus.Active)
                return Task.FromResult(record);
        }

        throw new NoActiveKeyException(@ref);
    }

    /// <inheritdoc/>
    public Task<KeyRecord> ResolveVersionAsync(string @ref, int version, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (!_store.TryGetValue(@ref, out var list) || list.Count == 0)
            throw new KeyNotFoundException(@ref, version);

        foreach (var record in list)
        {
            if (record.Version == version)
            {
                if (record.Status == KeyStatus.Disabled)
                    throw new KeyDisabledException(@ref, version);
                return Task.FromResult(record);
            }
        }

        throw new KeyNotFoundException(@ref, version);
    }

    // ---------------------------------------------------------------------------
    // JSON plumbing
    // ---------------------------------------------------------------------------

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static KeyRecord ToKeyRecord(KeyFileEntry entry)
    {
        var status = (entry.Status ?? "active").ToLowerInvariant() switch
        {
            "active"     => KeyStatus.Active,
            "deprecated" => KeyStatus.Deprecated,
            "disabled"   => KeyStatus.Disabled,
            var s        => throw new InvalidOperationException($"Unknown key status: \"{s}\""),
        };

        var material = EnvProvider.DecodeBytes(
            entry.Material ?? throw new InvalidOperationException("Key entry missing 'material'."));

        byte[]? tweak = string.IsNullOrEmpty(entry.Tweak) ? null : EnvProvider.DecodeBytes(entry.Tweak);

        IReadOnlyDictionary<string, string> metadata =
            entry.Metadata is { Count: > 0 }
                ? entry.Metadata
                : new Dictionary<string, string>();

        DateTimeOffset? createdAt = string.IsNullOrEmpty(entry.CreatedAt)
            ? null
            : DateTimeOffset.Parse(entry.CreatedAt);

        return new KeyRecord(
            Ref: entry.Ref ?? throw new InvalidOperationException("Key entry missing 'ref'."),
            Version: entry.Version,
            Status: status,
            Algorithm: entry.Algorithm ?? "adf1",
            Material: material,
            Tweak: tweak,
            Metadata: metadata,
            CreatedAt: createdAt);
    }

    // ---------------------------------------------------------------------------
    // Private DTO types
    // ---------------------------------------------------------------------------

    private sealed class KeyFile
    {
        [JsonPropertyName("keys")]
        public List<KeyFileEntry>? Keys { get; set; }
    }

    private sealed class KeyFileEntry
    {
        [JsonPropertyName("ref")]
        public string? Ref { get; set; }

        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;

        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("algorithm")]
        public string? Algorithm { get; set; }

        [JsonPropertyName("material")]
        public string? Material { get; set; }

        [JsonPropertyName("tweak")]
        public string? Tweak { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, string>? Metadata { get; set; }

        [JsonPropertyName("created_at")]
        public string? CreatedAt { get; set; }
    }
}
