using System.Diagnostics.CodeAnalysis;

namespace Tarpg.Core;

public sealed class Registry<T> where T : class, IRegistryEntry
{
    private readonly Dictionary<string, T> _entries = new(StringComparer.Ordinal);

    public void Register(T entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (string.IsNullOrWhiteSpace(entry.Id))
            throw new ArgumentException("Registry entry must have a non-empty Id.", nameof(entry));
        if (!_entries.TryAdd(entry.Id, entry))
            throw new InvalidOperationException(
                $"Duplicate {typeof(T).Name} id: '{entry.Id}'");
    }

    public T Get(string id) =>
        _entries.TryGetValue(id, out var v)
            ? v
            : throw new KeyNotFoundException($"Unknown {typeof(T).Name} id: '{id}'");

    public bool TryGet(string id, [MaybeNullWhen(false)] out T entry) =>
        _entries.TryGetValue(id, out entry);

    public bool Contains(string id) => _entries.ContainsKey(id);

    public IReadOnlyCollection<T> All => _entries.Values;

    public int Count => _entries.Count;
}
