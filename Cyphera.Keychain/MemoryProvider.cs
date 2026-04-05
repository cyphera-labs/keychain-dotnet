namespace Cyphera.Keychain;

/// <summary>
/// Thread-safe in-memory key provider suitable for testing and development.
/// </summary>
public sealed class MemoryProvider : IKeyProvider, IDisposable
{
    // Keyed by ref; each list is kept sorted descending by version.
    private readonly Dictionary<string, List<KeyRecord>> _store = new(StringComparer.Ordinal);
    private readonly ReaderWriterLockSlim _lock = new();

    /// <summary>
    /// Initialises the provider with an optional seed collection of records.
    /// </summary>
    public MemoryProvider(IEnumerable<KeyRecord>? records = null)
    {
        if (records is not null)
        {
            foreach (var record in records)
                AddInternal(record);
        }
    }

    /// <summary>
    /// Adds a key record to the in-memory store.
    /// </summary>
    public void Add(KeyRecord record)
    {
        _lock.EnterWriteLock();
        try
        {
            AddInternal(record);
        }
        finally
        {
            _lock.ExitWriteLock();
        }
    }

    /// <inheritdoc/>
    public Task<KeyRecord> ResolveAsync(string @ref, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _lock.EnterReadLock();
        try
        {
            if (!_store.TryGetValue(@ref, out var list) || list.Count == 0)
                throw new KeyNotFoundException(@ref);

            // List is sorted descending by version; pick first Active.
            foreach (var record in list)
            {
                if (record.Status == KeyStatus.Active)
                    return Task.FromResult(record);
            }

            throw new NoActiveKeyException(@ref);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    /// <inheritdoc/>
    public Task<KeyRecord> ResolveVersionAsync(string @ref, int version, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        _lock.EnterReadLock();
        try
        {
            if (!_store.TryGetValue(@ref, out var list) || list.Count == 0)
                throw new KeyNotFoundException(@ref, version);

            foreach (var record in list)
            {
                if (record.Version == version)
                {
                    if (record.Status == KeyStatus.Disabled)
                        throw new KeyDisabledException(@ref, version);
                    return Task.FromResult(record);
                }
            }

            throw new KeyNotFoundException(@ref, version);
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    // Must be called while holding the write lock (or during construction before sharing).
    private void AddInternal(KeyRecord record)
    {
        if (!_store.TryGetValue(record.Ref, out var list))
        {
            list = new List<KeyRecord>();
            _store[record.Ref] = list;
        }

        list.Add(record);
        // Keep sorted descending by version so ResolveAsync can short-circuit.
        list.Sort(static (a, b) => b.Version.CompareTo(a.Version));
    }

    /// <inheritdoc/>
    public void Dispose() => _lock.Dispose();
}
