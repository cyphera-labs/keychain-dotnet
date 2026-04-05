using System.Text;
using Cyphera.Keychain;
using Xunit;
using KeyNotFoundException = Cyphera.Keychain.KeyNotFoundException;

namespace Cyphera.Keychain.Tests;

public sealed class FileProviderTests
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string WriteTempFile(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json, Encoding.UTF8);
        return path;
    }

    private static void DeleteTempFile(string path)
    {
        try { File.Delete(path); } catch { /* best-effort */ }
    }

    // ------------------------------------------------------------------
    // Tests
    // ------------------------------------------------------------------

    [Fact]
    public async Task LoadAndResolve_ActiveKey_ReturnsRecord()
    {
        var material = new byte[] { 0x01, 0x02, 0x03 };
        var path = WriteTempFile($$"""
            {
              "keys": [
                {
                  "ref": "test-key",
                  "version": 1,
                  "status": "active",
                  "algorithm": "adf1",
                  "material": "{{Convert.ToHexString(material)}}"
                }
              ]
            }
            """);

        try
        {
            var provider = new FileProvider(path);
            var record = await provider.ResolveAsync("test-key");

            Assert.Equal("test-key", record.Ref);
            Assert.Equal(1, record.Version);
            Assert.Equal(KeyStatus.Active, record.Status);
            Assert.Equal("adf1", record.Algorithm);
            Assert.Equal(material, record.Material);
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    [Fact]
    public async Task ResolveVersionAsync_ReturnsCorrectVersion()
    {
        var mat1 = new byte[] { 0xAA };
        var mat2 = new byte[] { 0xBB };
        var path = WriteTempFile($$"""
            {
              "keys": [
                {
                  "ref": "k",
                  "version": 1,
                  "status": "active",
                  "algorithm": "adf1",
                  "material": "{{Convert.ToHexString(mat1)}}"
                },
                {
                  "ref": "k",
                  "version": 2,
                  "status": "active",
                  "algorithm": "adf1",
                  "material": "{{Convert.ToHexString(mat2)}}"
                }
              ]
            }
            """);

        try
        {
            var provider = new FileProvider(path);

            var v1 = await provider.ResolveVersionAsync("k", 1);
            var v2 = await provider.ResolveVersionAsync("k", 2);

            Assert.Equal(mat1, v1.Material);
            Assert.Equal(mat2, v2.Material);
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    [Fact]
    public async Task ResolveAsync_MissingRef_ThrowsKeyNotFoundException()
    {
        var path = WriteTempFile("""{"keys": []}""");

        try
        {
            var provider = new FileProvider(path);
            await Assert.ThrowsAsync<KeyNotFoundException>(() => provider.ResolveAsync("missing"));
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    [Fact]
    public async Task ResolveVersionAsync_DisabledKey_ThrowsKeyDisabledException()
    {
        var path = WriteTempFile($$"""
            {
              "keys": [
                {
                  "ref": "k",
                  "version": 1,
                  "status": "disabled",
                  "algorithm": "adf1",
                  "material": "{{Convert.ToHexString(new byte[] { 0x01 })}}"
                }
              ]
            }
            """);

        try
        {
            var provider = new FileProvider(path);
            await Assert.ThrowsAsync<KeyDisabledException>(() => provider.ResolveVersionAsync("k", 1));
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    [Fact]
    public async Task ResolveAsync_NoActiveKey_ThrowsNoActiveKeyException()
    {
        var path = WriteTempFile($$"""
            {
              "keys": [
                {
                  "ref": "k",
                  "version": 1,
                  "status": "deprecated",
                  "algorithm": "adf1",
                  "material": "{{Convert.ToHexString(new byte[] { 0x01 })}}"
                }
              ]
            }
            """);

        try
        {
            var provider = new FileProvider(path);
            await Assert.ThrowsAsync<NoActiveKeyException>(() => provider.ResolveAsync("k"));
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    [Fact]
    public async Task ResolveAsync_ReturnsHighestActiveVersion()
    {
        var mat1 = new byte[] { 0x01 };
        var mat2 = new byte[] { 0x02 };
        var path = WriteTempFile($$"""
            {
              "keys": [
                {
                  "ref": "k",
                  "version": 1,
                  "status": "active",
                  "algorithm": "adf1",
                  "material": "{{Convert.ToHexString(mat1)}}"
                },
                {
                  "ref": "k",
                  "version": 2,
                  "status": "active",
                  "algorithm": "adf1",
                  "material": "{{Convert.ToHexString(mat2)}}"
                }
              ]
            }
            """);

        try
        {
            var provider = new FileProvider(path);
            var record = await provider.ResolveAsync("k");

            Assert.Equal(2, record.Version);
            Assert.Equal(mat2, record.Material);
        }
        finally
        {
            DeleteTempFile(path);
        }
    }

    [Fact]
    public async Task LoadAndResolve_Base64Material_Decoded()
    {
        var material = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF };
        var path = WriteTempFile($$"""
            {
              "keys": [
                {
                  "ref": "k",
                  "version": 1,
                  "status": "active",
                  "algorithm": "adf1",
                  "material": "{{Convert.ToBase64String(material)}}"
                }
              ]
            }
            """);

        try
        {
            var provider = new FileProvider(path);
            var record = await provider.ResolveAsync("k");

            Assert.Equal(material, record.Material);
        }
        finally
        {
            DeleteTempFile(path);
        }
    }
}
