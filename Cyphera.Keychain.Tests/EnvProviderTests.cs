using Cyphera.Keychain;
using Xunit;
using KeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain.Tests;

public sealed class EnvProviderTests : IDisposable
{
    // Use a unique prefix per test run to avoid cross-test pollution.
    private const string Prefix = "CYPHERA_TEST_KP";
    private const string Ref = "customer-primary";

    // Env var names derived from the prefix + normalised ref.
    private const string KeyVar = $"{Prefix}_CUSTOMER_PRIMARY_KEY";
    private const string TweakVar = $"{Prefix}_CUSTOMER_PRIMARY_TWEAK";

    private readonly EnvProvider _provider = new(Prefix);

    // Restore env vars after each test.
    public void Dispose()
    {
        Environment.SetEnvironmentVariable(KeyVar, null);
        Environment.SetEnvironmentVariable(TweakVar, null);
    }

    // ------------------------------------------------------------------
    // ResolveAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_HexEncodedKey_ReturnsRecord()
    {
        var material = new byte[] { 0x01, 0x02, 0x03, 0x04 };
        Environment.SetEnvironmentVariable(KeyVar, Convert.ToHexString(material));

        var record = await _provider.ResolveAsync(Ref);

        Assert.Equal(Ref, record.Ref);
        Assert.Equal(1, record.Version);
        Assert.Equal(KeyStatus.Active, record.Status);
        Assert.Equal(material, record.Material);
    }

    [Fact]
    public async Task ResolveAsync_Base64EncodedKey_ReturnsRecord()
    {
        var material = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        Environment.SetEnvironmentVariable(KeyVar, Convert.ToBase64String(material));

        var record = await _provider.ResolveAsync(Ref);

        Assert.Equal(material, record.Material);
    }

    [Fact]
    public async Task ResolveAsync_MissingKey_ThrowsKeyNotFoundException()
    {
        // Ensure the env var is absent.
        Environment.SetEnvironmentVariable(KeyVar, null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _provider.ResolveAsync(Ref));
    }

    [Fact]
    public async Task ResolveAsync_TweakEnvVarSet_TweakIsPopulated()
    {
        var material = new byte[] { 0xAA, 0xBB };
        var tweak = new byte[] { 0x11, 0x22 };
        Environment.SetEnvironmentVariable(KeyVar, Convert.ToHexString(material));
        Environment.SetEnvironmentVariable(TweakVar, Convert.ToHexString(tweak));

        var record = await _provider.ResolveAsync(Ref);

        Assert.NotNull(record.Tweak);
        Assert.Equal(tweak, record.Tweak);
    }

    [Fact]
    public async Task ResolveAsync_TweakEnvVarAbsent_TweakIsNull()
    {
        Environment.SetEnvironmentVariable(KeyVar, Convert.ToHexString(new byte[] { 0x01 }));
        Environment.SetEnvironmentVariable(TweakVar, null);

        var record = await _provider.ResolveAsync(Ref);

        Assert.Null(record.Tweak);
    }

    // ------------------------------------------------------------------
    // ResolveVersionAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResolveVersionAsync_Version1_ReturnsRecord()
    {
        var material = new byte[] { 0x01, 0x02 };
        Environment.SetEnvironmentVariable(KeyVar, Convert.ToHexString(material));

        var record = await _provider.ResolveVersionAsync(Ref, 1);

        Assert.Equal(1, record.Version);
        Assert.Equal(material, record.Material);
    }

    [Fact]
    public async Task ResolveVersionAsync_Version2_ThrowsKeyNotFoundException()
    {
        Environment.SetEnvironmentVariable(KeyVar, Convert.ToHexString(new byte[] { 0x01 }));

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => _provider.ResolveVersionAsync(Ref, 2));
        Assert.Equal(Ref, ex.Ref);
        Assert.Equal(2, ex.Version);
    }
}
