using System.Text.Json;
using Moq;
using VaultSharp;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines.KeyValue.V2;
using Xunit;
using CypheraKeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain.Tests;

public class VaultProviderTests
{
    private const string MaterialHex = "aabbccdd" + "aabbccdd" + "aabbccdd" + "aabbccdd" +
                                       "aabbccdd" + "aabbccdd" + "aabbccdd" + "aabbccdd";

    [Fact]
    public void Constructor_WithClientAndMount_DoesNotThrow()
    {
        var mock = new Mock<IVaultClient>();
        var provider = new VaultProvider(mock.Object, "secret");
        Assert.NotNull(provider);
    }

    private static (Mock<IVaultClient>, VaultProvider) MakeProvider(Dictionary<string, object?> secretData)
    {
        var mock = new Mock<IVaultClient>();
        var kvMock = new Mock<IKeyValueV2SecretsEngine>();
        var secret = new Secret<SecretData>
        {
            Data = new SecretData { Data = secretData.ToDictionary(k => k.Key, v => (object)v.Value!) }
        };
        kvMock.Setup(m => m.ReadSecretAsync(It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<string>()))
              .ReturnsAsync(secret);
        mock.Setup(m => m.V1.Secrets.KeyValue.V2).Returns(kvMock.Object);
        return (mock, new VaultProvider(mock.Object));
    }

    [Fact]
    public async Task Resolve_ReturnsActiveRecord()
    {
        var (_, provider) = MakeProvider(new()
        {
            ["version"] = "1",
            ["status"] = "active",
            ["algorithm"] = "adf1",
            ["material"] = MaterialHex,
        });
        var rec = await provider.ResolveAsync("customer-primary");
        Assert.Equal("customer-primary", rec.Ref);
        Assert.Equal(1, rec.Version);
        Assert.Equal(KeyStatus.Active, rec.Status);
        Assert.Equal(Convert.FromHexString(MaterialHex), rec.Material);
    }

    [Fact]
    public async Task Resolve_ThrowsNoActiveKeyException_WhenDisabled()
    {
        var (_, provider) = MakeProvider(new()
        {
            ["version"] = "1",
            ["status"] = "disabled",
            ["material"] = MaterialHex,
        });
        await Assert.ThrowsAsync<NoActiveKeyException>(() => provider.ResolveAsync("k"));
    }

    [Fact]
    public async Task ResolveVersion_ThrowsKeyDisabledException_WhenDisabled()
    {
        var (_, provider) = MakeProvider(new()
        {
            ["version"] = "1",
            ["status"] = "disabled",
            ["material"] = MaterialHex,
        });
        await Assert.ThrowsAsync<KeyDisabledException>(() => provider.ResolveVersionAsync("k", 1));
    }

    [Fact]
    public async Task ResolveVersion_ThrowsKeyNotFoundException_WhenVersionMissing()
    {
        var (_, provider) = MakeProvider(new()
        {
            ["version"] = "1",
            ["status"] = "active",
            ["material"] = MaterialHex,
        });
        await Assert.ThrowsAsync<CypheraKeyNotFoundException>(() => provider.ResolveVersionAsync("k", 99));
    }
}
