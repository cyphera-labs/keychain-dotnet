using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Moq;
using Xunit;
using CypheraKeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain.Tests;

public class AwsKmsProviderTests
{
    private static readonly byte[] FakePlaintext = new byte[32];
    private static readonly byte[] FakeCiphertext = new byte[64];
    private const string KeyId = "arn:aws:kms:us-east-1:123456789012:key/test";

    private static (Mock<IAmazonKeyManagementService>, AwsKmsProvider) MakeProvider()
    {
        var mock = new Mock<IAmazonKeyManagementService>();
        mock.Setup(m => m.GenerateDataKeyAsync(It.IsAny<GenerateDataKeyRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GenerateDataKeyResponse
            {
                Plaintext = new MemoryStream(FakePlaintext),
                CiphertextBlob = new MemoryStream(FakeCiphertext),
            });
        return (mock, new AwsKmsProvider(KeyId, mock.Object));
    }

    [Fact]
    public async Task Resolve_ReturnsActiveRecord()
    {
        var (_, provider) = MakeProvider();
        var rec = await provider.ResolveAsync("customer-primary");
        Assert.Equal("customer-primary", rec.Ref);
        Assert.Equal(1, rec.Version);
        Assert.Equal(KeyStatus.Active, rec.Status);
        Assert.Equal(FakePlaintext, rec.Material);
    }

    [Fact]
    public async Task Resolve_AlgorithmIsAes256()
    {
        var (_, provider) = MakeProvider();
        var rec = await provider.ResolveAsync("k");
        Assert.Equal("aes256", rec.Algorithm);
    }

    [Fact]
    public async Task Resolve_CachesResult()
    {
        var (mock, provider) = MakeProvider();
        await provider.ResolveAsync("k");
        await provider.ResolveAsync("k");
        mock.Verify(m => m.GenerateDataKeyAsync(It.IsAny<GenerateDataKeyRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Resolve_DifferentRefsCauseSeparateCalls()
    {
        var (mock, provider) = MakeProvider();
        await provider.ResolveAsync("key-a");
        await provider.ResolveAsync("key-b");
        mock.Verify(m => m.GenerateDataKeyAsync(It.IsAny<GenerateDataKeyRequest>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Resolve_ThrowsKeyNotFoundException_OnSdkError()
    {
        var mock = new Mock<IAmazonKeyManagementService>();
        mock.Setup(m => m.GenerateDataKeyAsync(It.IsAny<GenerateDataKeyRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("KMS error"));
        var provider = new AwsKmsProvider(KeyId, mock.Object);
        await Assert.ThrowsAsync<CypheraKeyNotFoundException>(() => provider.ResolveAsync("bad-ref"));
    }

    [Fact]
    public async Task ResolveVersion_Version1_Resolves()
    {
        var (_, provider) = MakeProvider();
        var rec = await provider.ResolveVersionAsync("k", 1);
        Assert.Equal(1, rec.Version);
    }

    [Fact]
    public async Task ResolveVersion_OtherVersion_ThrowsKeyNotFoundException()
    {
        var (_, provider) = MakeProvider();
        await Assert.ThrowsAsync<CypheraKeyNotFoundException>(() => provider.ResolveVersionAsync("k", 2));
    }
}
