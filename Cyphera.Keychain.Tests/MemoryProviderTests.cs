using Cyphera.Keychain;
using Xunit;
using KeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain.Tests;

public sealed class MemoryProviderTests
{
    private static readonly byte[] SomeMaterial = Convert.FromHexString("0123456789abcdef0123456789abcdef");
    private static readonly IReadOnlyDictionary<string, string> NoMeta = new Dictionary<string, string>();

    private static KeyRecord MakeRecord(string @ref, int version, KeyStatus status = KeyStatus.Active) =>
        new(@ref, version, status, "adf1", SomeMaterial, null, NoMeta, null);

    // ------------------------------------------------------------------
    // ResolveAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResolveAsync_ReturnsHighestActiveVersion()
    {
        using var provider = new MemoryProvider(new[]
        {
            MakeRecord("k", 1),
            MakeRecord("k", 2),
            MakeRecord("k", 3),
        });

        var record = await provider.ResolveAsync("k");

        Assert.Equal(3, record.Version);
    }

    [Fact]
    public async Task ResolveAsync_SkipsDeprecatedAndReturnsHighestActive()
    {
        using var provider = new MemoryProvider(new[]
        {
            MakeRecord("k", 1),
            MakeRecord("k", 2),
            MakeRecord("k", 3, KeyStatus.Deprecated),
        });

        var record = await provider.ResolveAsync("k");

        // Version 3 is deprecated; highest active is 2.
        Assert.Equal(2, record.Version);
    }

    [Fact]
    public async Task ResolveAsync_UnknownRef_ThrowsKeyNotFoundException()
    {
        using var provider = new MemoryProvider();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => provider.ResolveAsync("missing"));
    }

    [Fact]
    public async Task ResolveAsync_NoActiveVersions_ThrowsNoActiveKeyException()
    {
        using var provider = new MemoryProvider(new[]
        {
            MakeRecord("k", 1, KeyStatus.Deprecated),
            MakeRecord("k", 2, KeyStatus.Disabled),
        });

        await Assert.ThrowsAsync<NoActiveKeyException>(() => provider.ResolveAsync("k"));
    }

    // ------------------------------------------------------------------
    // ResolveVersionAsync
    // ------------------------------------------------------------------

    [Fact]
    public async Task ResolveVersionAsync_ReturnsSpecificVersion()
    {
        using var provider = new MemoryProvider(new[]
        {
            MakeRecord("k", 1),
            MakeRecord("k", 2),
        });

        var record = await provider.ResolveVersionAsync("k", 1);

        Assert.Equal(1, record.Version);
    }

    [Fact]
    public async Task ResolveVersionAsync_DisabledKey_ThrowsKeyDisabledException()
    {
        using var provider = new MemoryProvider(new[]
        {
            MakeRecord("k", 1, KeyStatus.Disabled),
        });

        await Assert.ThrowsAsync<KeyDisabledException>(() => provider.ResolveVersionAsync("k", 1));
    }

    [Fact]
    public async Task ResolveVersionAsync_UnknownRef_ThrowsKeyNotFoundException()
    {
        using var provider = new MemoryProvider();

        var ex = await Assert.ThrowsAsync<KeyNotFoundException>(() => provider.ResolveVersionAsync("missing", 1));
        Assert.Equal("missing", ex.Ref);
        Assert.Equal(1, ex.Version);
    }

    [Fact]
    public async Task ResolveVersionAsync_UnknownVersion_ThrowsKeyNotFoundException()
    {
        using var provider = new MemoryProvider(new[] { MakeRecord("k", 1) });

        await Assert.ThrowsAsync<KeyNotFoundException>(() => provider.ResolveVersionAsync("k", 99));
    }

    // ------------------------------------------------------------------
    // Add
    // ------------------------------------------------------------------

    [Fact]
    public async Task Add_InsertsNewRecord()
    {
        using var provider = new MemoryProvider();

        provider.Add(MakeRecord("k", 1));
        var record = await provider.ResolveAsync("k");

        Assert.Equal("k", record.Ref);
        Assert.Equal(1, record.Version);
    }

    [Fact]
    public async Task Add_NewRecordBecomesHighestVersion()
    {
        using var provider = new MemoryProvider(new[] { MakeRecord("k", 1) });

        provider.Add(MakeRecord("k", 2));
        var record = await provider.ResolveAsync("k");

        Assert.Equal(2, record.Version);
    }
}
