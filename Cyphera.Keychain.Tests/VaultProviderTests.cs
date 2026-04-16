using System.Text.Json;
using Moq;
using VaultSharp;
using VaultSharp.V1;
using VaultSharp.V1.Commons;
using VaultSharp.V1.SecretsEngines;
using VaultSharp.V1.SecretsEngines.KeyValue;
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
        var secret = new Secret<SecretData>
        {
            Data = new SecretData { Data = secretData.ToDictionary(k => k.Key, v => (object)v.Value!) }
        };

        // Use a stub implementation to avoid Moq expression tree issues with optional params
        var stubVault = new StubKeyValueV2(secret);
        var kvMock = new Mock<IKeyValueSecretsEngine>();
        kvMock.Setup(m => m.V2).Returns(stubVault);

        var secretsMock = new Mock<ISecretsEngine>();
        secretsMock.Setup(m => m.KeyValue).Returns(kvMock.Object);

        var v1Mock = new Mock<IVaultClientV1>();
        v1Mock.Setup(m => m.Secrets).Returns(secretsMock.Object);

        mock.Setup(m => m.V1).Returns(v1Mock.Object);
        return (mock, new VaultProvider(mock.Object));
    }

    /// <summary>Minimal stub to avoid Moq optional-param expression tree limitations.</summary>
    private class StubKeyValueV2 : IKeyValueSecretsEngineV2
    {
        private readonly Secret<SecretData> _secret;
        public StubKeyValueV2(Secret<SecretData> secret) => _secret = secret;

        public Task<Secret<SecretData>> ReadSecretAsync(string path, int? version = null,
            string mountPoint = "secret", string wrapTimeToLive = null!)
            => Task.FromResult(_secret);

        // Not used in tests — stubs to satisfy interface
        public Task<Secret<CurrentSecretMetadata>> WriteSecretAsync<T>(string path, T data, int? checkAndSet = null, string mountPoint = "secret") => throw new NotImplementedException();
        public Task<Secret<SecretData<Dictionary<string, object>>>> WriteSecretAsync(string path, IDictionary<string, object>? data, int? checkAndSet = null, string mountPoint = "secret") => throw new NotImplementedException();
        public Task<Secret<FullSecretMetadata>> ReadSecretMetadataAsync(string path, string mountPoint = "secret", string wrapTimeToLive = null!) => throw new NotImplementedException();
        public Task DeleteSecretAsync(string path, string mountPoint = "secret") => throw new NotImplementedException();
        public Task DestroySecretVersionsAsync(string path, IList<int> versions, string mountPoint = "secret") => throw new NotImplementedException();
        public Task DeleteSecretVersionsAsync(string path, IList<int> versions, string mountPoint = "secret") => throw new NotImplementedException();
        public Task UndeleteSecretVersionsAsync(string path, IList<int> versions, string mountPoint = "secret") => throw new NotImplementedException();
        public Task WriteSecretMetadataAsync(string path, CustomMetadataRequest metadata, string mountPoint = "secret") => throw new NotImplementedException();
        public Task<Secret<ListInfo>> ReadSecretPathsAsync(string path, string mountPoint = "secret", string wrapTimeToLive = null!) => throw new NotImplementedException();
        public Task<Secret<SecretSubkeysInfo>> ReadSecretSubkeysAsync(string path, int version = 0, int depth = 0, string mountPoint = "secret", string wrapTimeToLive = null!) => throw new NotImplementedException();
        public Task<Secret<CurrentSecretMetadata>> PatchSecretAsync(string path, PatchSecretDataRequest data, string mountPoint = "secret") => throw new NotImplementedException();
        public Task PatchSecretMetadataAsync(string path, CustomMetadataRequest metadata, string mountPoint = "secret") => throw new NotImplementedException();
        public Task DeleteMetadataAsync(string path, string mountPoint = "secret") => throw new NotImplementedException();
        public Task ConfigureAsync(KeyValue2ConfigModel config, string mountPoint = "secret") => throw new NotImplementedException();
        public Task<Secret<KeyValue2ConfigModel>> ReadConfigAsync(string mountPoint = "secret", string wrapTimeToLive = null!) => throw new NotImplementedException();
        public Task<Secret<SecretData<T>>> ReadSecretAsync<T>(string path, int? version = null, string mountPoint = "secret", string wrapTimeToLive = null!) => throw new NotImplementedException();
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
