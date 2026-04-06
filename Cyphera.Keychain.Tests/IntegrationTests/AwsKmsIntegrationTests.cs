using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Xunit;

namespace Cyphera.Keychain.Tests.IntegrationTests;

[Trait("Category", "Integration")]
public class AwsKmsIntegrationTests
{
    private static readonly string? EndpointUrl = Environment.GetEnvironmentVariable("AWS_ENDPOINT_URL");
    private static readonly string Region = Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION") ?? "us-east-1";

    private static IAmazonKeyManagementService CreateAdminClient()
    {
        var config = new AmazonKeyManagementServiceConfig
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(Region),
        };
        if (EndpointUrl is not null) config.ServiceURL = EndpointUrl;
        return new AmazonKeyManagementServiceClient(
            Environment.GetEnvironmentVariable("AWS_ACCESS_KEY_ID") ?? "test",
            Environment.GetEnvironmentVariable("AWS_SECRET_ACCESS_KEY") ?? "test",
            config);
    }

    [SkippableFact]
    public async Task Resolve_ReturnsActiveKey_AgainstLocalStack()
    {
        Skip.If(EndpointUrl is null, "AWS_ENDPOINT_URL not set; skipping LocalStack integration test.");
        var admin = CreateAdminClient();
        var createResp = await admin.CreateKeyAsync(new CreateKeyRequest { Description = "Cyphera integ test" });
        var keyId = createResp.KeyMetadata.KeyId;
        var provider = new AwsKmsProvider(keyId, Region, EndpointUrl);
        var rec = await provider.ResolveAsync("integ-primary");
        Assert.Equal(KeyStatus.Active, rec.Status);
        Assert.Equal(32, rec.Material.Length);
    }
}
