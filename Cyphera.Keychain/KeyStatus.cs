namespace Cyphera.Keychain;

/// <summary>
/// Lifecycle status of a key record.
/// </summary>
public enum KeyStatus
{
    /// <summary>The key is in active use.</summary>
    Active,

    /// <summary>The key is deprecated but can still be used for decryption / verification.</summary>
    Deprecated,

    /// <summary>The key is disabled and must not be used.</summary>
    Disabled,
}
