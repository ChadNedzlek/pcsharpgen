using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using FluentAssertions.Collections;

namespace Primordially.PluginCore.Tests
{
    // This entire class will be unnecessary in FluentAssertions 6.0
    // https://github.com/fluentassertions/fluentassertions/issues/357
    public class GenericReadOnlyDictionaryAssertions<TKey, TValue> : GenericDictionaryAssertions<TKey, TValue> where TKey : notnull
    {
        private class ReadOnlyDictionaryWrapper : IDictionary<TKey, TValue>
        {
            private readonly IReadOnlyDictionary<TKey, TValue> _backing;

            public ReadOnlyDictionaryWrapper(IReadOnlyDictionary<TKey, TValue> backing)
            {
                _backing = backing;
            }

            public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
            {
                return _backing.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable) _backing).GetEnumerator();
            }

            public void Add(KeyValuePair<TKey, TValue> item)
            {
                throw new NotSupportedException();
            }

            public void Clear()
            {
                throw new NotSupportedException();
            }

            public bool Contains(KeyValuePair<TKey, TValue> item)
            {
                return _backing.Contains(item);
            }

            public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
            {
                _backing.ToList().CopyTo(array, arrayIndex);
            }

            public bool Remove(KeyValuePair<TKey, TValue> item)
            {
                throw new NotSupportedException();
            }

            public int Count => _backing.Count;
            public bool IsReadOnly => true;
            public void Add(TKey key, TValue value)
            {
                throw new NotSupportedException();
            }

            public bool ContainsKey(TKey key)
            {
                return _backing.ContainsKey(key);
            }

            public bool Remove(TKey key)
            {
                throw new NotSupportedException();
            }

            public bool TryGetValue(TKey key, [MaybeNullWhen(false)] out TValue value)
            {
                return _backing.TryGetValue(key, out value);
            }

            public TValue this[TKey key]
            {
                get => _backing[key];
                set => throw new NotSupportedException();
            }

            public ICollection<TKey> Keys => new ReadOnlyCollection<TKey>(_backing.Keys.ToList());
            public ICollection<TValue> Values => new ReadOnlyCollection<TValue>(_backing.Values.ToList());
        }

        public GenericReadOnlyDictionaryAssertions(IReadOnlyDictionary<TKey, TValue> dictionary) : base(new ReadOnlyDictionaryWrapper(dictionary))
        {
        }
    }
    
    // This entire class will be unnecessary in FluentAssertions 6.0
    // https://github.com/fluentassertions/fluentassertions/issues/357
    public static class ReadOnlyDictionaryShouldExtensions
    {
        public static GenericReadOnlyDictionaryAssertions<TKey, TValue> Should<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dict) where TKey : notnull
        {
            return new GenericReadOnlyDictionaryAssertions<TKey, TValue>(dict);
        }

        public static GenericDictionaryAssertions<TKey, TValue> Should<TKey, TValue>(this Dictionary<TKey, TValue> dict) where TKey : notnull
        {
            return new GenericDictionaryAssertions<TKey, TValue>(dict);
        }
    }
}