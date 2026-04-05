# Cyphera Keychain — .NET

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

## License

MIT
