using System.Text.Json;

namespace Cyphera.Keychain;

/// <summary>
/// Bridge resolver for Cyphera SDK config-driven key sources.
/// Called by the SDK via reflection when cyphera.json has "source" set to a cloud provider.
/// </summary>
public static class KeychainResolver
{
    /// <summary>
    /// Resolve a key from a cloud provider based on the cyphera.json key config.
    /// Returns raw key bytes.
    /// </summary>
    public static byte[] Resolve(string source, JsonElement config)
    {
        var @ref = GetString(config, "ref")
            ?? GetString(config, "path")
            ?? GetString(config, "arn")
            ?? GetString(config, "key")
            ?? "default";

        var provider = CreateProvider(source, config);

        var record = provider.ResolveAsync(@ref).GetAwaiter().GetResult();
        return record.Material;
    }

    private static IKeyProvider CreateProvider(string source, JsonElement config)
    {
        switch (source)
        {
            case "vault":
            {
                var addr = GetString(config, "addr")
                    ?? Environment.GetEnvironmentVariable("VAULT_ADDR")
                    ?? "http://127.0.0.1:8200";
                var token = GetString(config, "token")
                    ?? Environment.GetEnvironmentVariable("VAULT_TOKEN")
                    ?? "";
                var mount = GetString(config, "mount") ?? "secret";
                return new VaultProvider(addr, token, mount);
            }
            case "aws-kms":
            {
                var arn = GetString(config, "arn") ?? "";
                var region = GetString(config, "region")
                    ?? Environment.GetEnvironmentVariable("AWS_REGION")
                    ?? "us-east-1";
                var endpoint = GetString(config, "endpoint");
                return endpoint != null
                    ? new AwsKmsProvider(arn, region, endpoint)
                    : new AwsKmsProvider(arn, region);
            }
            case "gcp-kms":
            {
                var resource = GetString(config, "resource") ?? "";
                return new GcpKmsProvider(resource);
            }
            case "azure-kv":
            {
                var vault = GetString(config, "vault") ?? "";
                var keyName = GetString(config, "key") ?? "";
                return new AzureKvProvider($"https://{vault}.vault.azure.net", keyName);
            }
            default:
                throw new ArgumentException($"Unknown source: {source}");
        }
    }

    private static string? GetString(JsonElement el, string prop)
    {
        return el.TryGetProperty(prop, out var val) ? val.GetString() : null;
    }
}
