using Xunit;

namespace Cyphera.Keychain.Tests.IntegrationTests;

[Trait("Category", "Integration")]
public class VaultIntegrationTests
{
    private static readonly string? VaultAddr = Environment.GetEnvironmentVariable("VAULT_ADDR");
    private static readonly string? VaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN");
    private const string MaterialHex = "aabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccddaabbccdd";

    [SkippableFact]
    public async Task Resolve_ReturnsActiveRecord_AgainstVaultDev()
    {
        Skip.If(VaultAddr is null || VaultToken is null, "VAULT_ADDR/VAULT_TOKEN not set; skipping Vault integration test.");
        // Write test secret via HTTP
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("X-Vault-Token", VaultToken);
        var body = System.Text.Json.JsonSerializer.Serialize(new
        {
            data = new { version = "1", status = "active", algorithm = "adf1", material = MaterialHex }
        });
        await http.PostAsync(
            $"{VaultAddr}/v1/secret/data/integ-primary",
            new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        var provider = new VaultProvider(VaultAddr, VaultToken);
        var rec = await provider.ResolveAsync("integ-primary");
        Assert.Equal(KeyStatus.Active, rec.Status);
        Assert.Equal(Convert.FromHexString(MaterialHex), rec.Material);
    }
}
