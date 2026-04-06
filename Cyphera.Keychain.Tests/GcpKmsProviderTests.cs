using Google.Cloud.Kms.V1;
using Moq;
using Xunit;
using CypheraKeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain.Tests;

public class GcpKmsProviderTests
{
    private const string KeyName = "projects/test/locations/global/keyRings/r/cryptoKeys/k";

    [Fact]
    public void Constructor_WithClientAndKeyName_DoesNotThrow()
    {
        var mock = new Mock<KeyManagementServiceClient>();
        var provider = new GcpKmsProvider(KeyName, mock.Object);
        Assert.NotNull(provider);
    }

    [Fact]
    public async Task ResolveVersion_OtherVersion_Throws()
    {
        var mock = new Mock<KeyManagementServiceClient>();
        var provider = new GcpKmsProvider(KeyName, mock.Object);
        await Assert.ThrowsAsync<CypheraKeyNotFoundException>(() => provider.ResolveVersionAsync("k", 2));
    }

    [Fact]
    public async Task Resolve_ThrowsKeyNotFoundException_WhenClientFails()
    {
        var mock = new Mock<KeyManagementServiceClient>();
        mock.Setup(m => m.EncryptAsync(It.IsAny<EncryptRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.NotFound, "not found")));
        var provider = new GcpKmsProvider(KeyName, mock.Object);
        await Assert.ThrowsAsync<CypheraKeyNotFoundException>(() => provider.ResolveAsync("bad-ref"));
    }
}
