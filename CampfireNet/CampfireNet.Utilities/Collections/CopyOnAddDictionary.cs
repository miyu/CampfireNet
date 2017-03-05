using System;
using System.Collections;
using System.Collections.Generic;

namespace CampfireNet.Utilities.Collections {
   public class CopyOnAddDictionary<K, V> : IReadOnlyDictionary<K, V> {
      private readonly object _updateLock = new object();
      private readonly IEqualityComparer<K> _comparer;
      private Dictionary<K, V> _innerDict;

      public CopyOnAddDictionary() {
         _innerDict = new Dictionary<K, V>();
      }

      public CopyOnAddDictionary(IEqualityComparer<K> comparer) {
         _comparer = comparer;
         _innerDict = new Dictionary<K, V>(comparer);
      }

      public V this[K key] => _innerDict[key];

      public int Count => _innerDict.Count;
      public IEnumerable<K> Keys => _innerDict.Keys;
      public IEnumerable<V> Values => _innerDict.Values;

      public IEnumerator<KeyValuePair<K, V>> GetEnumerator() => _innerDict.GetEnumerator();
      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public bool ContainsKey(K key) => _innerDict.ContainsKey(key);
      public bool TryGetValue(K key, out V value) => _innerDict.TryGetValue(key, out value);

      public void AddOrThrow(K key, V value) {
         bool added = false;
         var existing = GetOrAdd(
            key,
            add => {
               added = true;
               return value;
            });
         if (!added) {
            throw new DuplicateKeyException(key, value, existing);
         }
      }

      public V GetOrAdd(K key, Func<K, V> valueFactory) {
         V result;
         if (_innerDict.TryGetValue(key, out result)) {
            return result;
         } else {
            lock (_updateLock) {
               if (_innerDict.TryGetValue(key, out result)) {
                  return result;
               } else {
                  result = valueFactory(key);
                  var clone = new Dictionary<K, V>(_innerDict, _comparer);
                  clone.Add(key, result);
                  _innerDict = clone;
                  return result;
               }
            }
         }
      }

      public V GetOrAdd(K key, Func<K, K> keyFactory, Func<K, V> valueFactory) {
         V result;
         if (_innerDict.TryGetValue(key, out result)) {
            return result;
         } else {
            lock (_updateLock) {
               if (_innerDict.TryGetValue(key, out result)) {
                  return result;
               } else {
                  key = keyFactory(key);
                  result = valueFactory(key);
                  var clone = new Dictionary<K, V>(_innerDict, _comparer);
                  clone.Add(key, result);
                  _innerDict = clone;
                  return result;
               }
            }
         }
      }

      public void Merge(IReadOnlyDictionary<K, V> additional) {
         lock (_updateLock) {
            var result = new Dictionary<K, V>(_innerDict, _comparer);
            foreach (var kvp in additional) {
               V existing;
               if (result.TryGetValue(kvp.Key, out existing)) {
                  if (!existing.Equals(kvp.Value)) {
                     throw new DuplicateKeyException(kvp.Key, existing, kvp.Value);
                  }
               } else {
                  result.Add(kvp.Key, kvp.Value);
               }
            }
            _innerDict = result;
         }
      }

      public class DuplicateKeyException : Exception {
         public DuplicateKeyException(K key, V a, V b) : base(GetMessage(key, a, b)) { }

         private static string GetMessage(K key, V a, V b) {
            return $"key-value mismatch at merge: Key {key} mapped to both {a} and {b}.";
         }
      }
   }
}
