using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Primordially.PluginCore.Data
{
    public class MergedImmutableDictionary<TKey, TValue> : IReadOnlyDictionary<TKey, TValue> where TKey : notnull
    {
        private readonly ImmutableDictionary<TKey, TValue> _backing;

        public MergedImmutableDictionary(
            IEqualityComparer<TKey> comparer,
            IEnumerable<IReadOnlyDictionary<TKey, TValue>> backingDictionaries)
        {
            var builder = ImmutableDictionary.CreateBuilder<TKey, TValue>(comparer);
            foreach (var dict in backingDictionaries)
            {
                foreach (var pair in dict)
                {
                    builder.Add(pair);
                }
            }

            _backing = builder.ToImmutable();
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            return _backing.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return ((IEnumerable) _backing).GetEnumerator();
        }

        public int Count => _backing.Count;

        public bool ContainsKey(TKey key)
        {
            return _backing.ContainsKey(key);
        }

        public bool TryGetValue(TKey key, out TValue value)
        {
            return _backing.TryGetValue(key, out value);
        }

        public TValue this[TKey key] => _backing[key];

        public IEnumerable<TKey> Keys => _backing.Keys;

        public IEnumerable<TValue> Values => _backing.Values;
    }

    public static class MergedImmutableDictionary
    {
        public static MergedImmutableDictionary<TKey, TValue> Create<TKey, TValue>(
            IEqualityComparer<TKey> comparer,
            IEnumerable<IReadOnlyDictionary<TKey, TValue>> backingDictionaries) where TKey : notnull
            => new MergedImmutableDictionary<TKey, TValue>(comparer, backingDictionaries);
    }
}