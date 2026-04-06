using Azure.Security.KeyVault.Keys;
using Azure.Security.KeyVault.Keys.Cryptography;
using Azure.Core;
using Moq;
using Xunit;
using CypheraKeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain.Tests;

public class AzureKvProviderTests
{
    private const string KeyName = "test-rsa-key";

    private static (Mock<KeyClient>, Mock<TokenCredential>, AzureKvProvider) MakeProvider()
    {
        var mockKey = new Mock<KeyClient>();
        var cred = new Mock<TokenCredential>();
        // We cannot easily mock CryptographyClient as it's sealed, so we test at the provider level
        // by making GetKeyAsync throw to test error handling
        var provider = new AzureKvProvider(mockKey.Object, KeyName, cred.Object);
        return (mockKey, cred, provider);
    }

    [Fact]
    public async Task ResolveVersion_OtherVersion_Throws()
    {
        var (_, _, provider) = MakeProvider();
        await Assert.ThrowsAsync<CypheraKeyNotFoundException>(() => provider.ResolveVersionAsync("k", 2));
    }

    [Fact]
    public async Task Resolve_ThrowsKeyNotFoundException_WhenKeyClientFails()
    {
        var mockKey = new Mock<KeyClient>();
        mockKey.Setup(m => m.GetKeyAsync(It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Azure.RequestFailedException("not found"));
        var cred = new Mock<TokenCredential>();
        var provider = new AzureKvProvider(mockKey.Object, KeyName, cred.Object);
        await Assert.ThrowsAsync<CypheraKeyNotFoundException>(() => provider.ResolveAsync("bad-ref"));
    }
}
