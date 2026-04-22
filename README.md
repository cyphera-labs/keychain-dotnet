# Cyphera Keychain — .NET

[![CI](https://github.com/cyphera-labs/keychain-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/cyphera-labs/keychain-dotnet/actions/workflows/ci.yml)
[![Security](https://github.com/cyphera-labs/keychain-dotnet/actions/workflows/codeql.yml/badge.svg)](https://github.com/cyphera-labs/keychain-dotnet/actions/workflows/codeql.yml)
[![License](https://img.shields.io/badge/license-Apache%202.0-blue)](LICENSE)
Key provider abstraction for the [Cyphera](https://cyphera.dev) .NET SDK.

## Installation

```sh
dotnet add package Cyphera.Keychain
```

## Usage

### Memory provider (testing / development)

```csharp
using Cyphera.Keychain;

var provider = new MemoryProvider(new[]
{
    new KeyRecord(
        Ref: "customer-primary",
        Version: 1,
        Status: KeyStatus.Active,
        Algorithm: "adf1",
        Material: Convert.FromHexString("0123456789abcdef0123456789abcdef"),
        Tweak: System.Text.Encoding.UTF8.GetBytes("customer-ssn"),
        Metadata: new Dictionary<string, string>(),
        CreatedAt: null
    )
});

var record = await provider.ResolveAsync("customer-primary");
```

### Environment variable provider

```csharp
using Cyphera.Keychain;

// Reads CYPHERA_CUSTOMER_PRIMARY_KEY (hex or base64)
var provider = new EnvProvider("CYPHERA");
var record = await provider.ResolveAsync("customer-primary");
```

### File provider

```csharp
using Cyphera.Keychain;

var provider = new FileProvider("/etc/cyphera/keys.json");
var record = await provider.ResolveAsync("customer-primary");
```

Key file format:

```json
{
  "keys": [
    {
      "ref": "customer-primary",
      "version": 1,
      "status": "active",
      "algorithm": "adf1",
      "material": "<hex or base64>",
      "tweak": "<hex or base64>"
    }
  ]
}
```

## Providers

| Provider | Description | Use case |
|---|---|---|
| `MemoryProvider` | In-memory key store | Testing, development |
| `EnvProvider` | Keys from environment variables | 12-factor / container deployments |
| `FileProvider` | Keys from a local JSON file | Secrets manager file injection |
| `AwsKmsProvider` | AWS KMS data-key generation | AWS-hosted services |
| `GcpKmsProvider` | GCP Cloud KMS envelope encryption | GCP-hosted services |
| `AzureKvProvider` | Azure Key Vault RSA key wrapping | Azure-hosted services |
| `VaultProvider` | HashiCorp Vault KV v2 | Self-hosted / multi-cloud |

## Cloud KMS Providers

Cyphera ships four cloud KMS providers. Add the NuGet package and reference the appropriate provider for your infrastructure.

### AWS KMS

Generates an AES-256 data key via `GenerateDataKey` against a configured KMS master key. The plaintext key is cached in memory for the lifetime of the provider.

```csharp
using Cyphera.Keychain;

// Uses ambient AWS credentials (env vars, instance profile, etc.)
var provider = new AwsKmsProvider(
    keyId: "arn:aws:kms:us-east-1:123456789012:key/your-key-id",
    region: "us-east-1");

var record = await provider.ResolveAsync("customer-primary");
// record.Material contains the 32-byte AES-256 plaintext key
```

For local development with LocalStack, pass the endpoint URL:

```csharp
var provider = new AwsKmsProvider(
    keyId: "arn:aws:kms:us-east-1:000000000000:key/your-key-id",
    region: "us-east-1",
    endpointUrl: "http://localhost:4566");
```

### GCP Cloud KMS

Generates a random AES-256 plaintext key and wraps it via GCP KMS `Encrypt`. The plaintext is cached in memory.

```csharp
using Cyphera.Keychain;

// Uses ambient GCP credentials (ADC)
var provider = new GcpKmsProvider(
    keyName: "projects/my-project/locations/global/keyRings/my-ring/cryptoKeys/my-key");

var record = await provider.ResolveAsync("customer-primary");
```

### Azure Key Vault

Generates a random AES-256 plaintext key and wraps it with an RSA Key Vault key using RSA-OAEP. The plaintext is cached in memory.

```csharp
using Cyphera.Keychain;

// Uses DefaultAzureCredential (env vars, managed identity, etc.)
var provider = new AzureKvProvider(
    vaultUrl: "https://my-vault.vault.azure.net",
    keyName: "my-rsa-key");

var record = await provider.ResolveAsync("customer-primary");
```

### HashiCorp Vault (KV v2)

Reads key records stored in a Vault KV v2 secrets engine. Records are expected at `{mount}/{ref}` with the following fields:

| Field | Type | Description |
|---|---|---|
| `version` | int or string | Key version number |
| `status` | string | `active`, `deprecated`, or `disabled` |
| `algorithm` | string | Algorithm identifier (e.g. `adf1`) |
| `material` | string | Key bytes as hex or base64 |
| `tweak` | string | (Optional) tweak bytes as hex or base64 |

```csharp
using Cyphera.Keychain;

var provider = new VaultProvider(
    address: "https://vault.example.com",
    token: Environment.GetEnvironmentVariable("VAULT_TOKEN")!,
    mount: "secret");

// Resolves the highest-versioned active key record
var record = await provider.ResolveAsync("customer-primary");

// Resolves a specific version
var v1 = await provider.ResolveVersionAsync("customer-primary", 1);
```

Example Vault secret (`vault kv put secret/customer-primary ...`):

```json
{
  "version": "1",
  "status": "active",
  "algorithm": "adf1",
  "material": "0123456789abcdef0123456789abcdef"
}
```

## License

Apache 2.0
