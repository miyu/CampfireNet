using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CampfireNet.Utilities.Collections {
   public class ConcurrentSet<T> : IEnumerable<T> {
      ConcurrentDictionary<T, byte> storage;

      public ConcurrentSet() {
         storage = new ConcurrentDictionary<T, byte>();
      }

      public ConcurrentSet(IEnumerable<T> collection) {
         storage = new ConcurrentDictionary<T, byte>(collection.Select(_ => new KeyValuePair<T, byte>(_, 0)));
      }

      public ConcurrentSet(IEqualityComparer<T> comparer) {
         storage = new ConcurrentDictionary<T, byte>(comparer);
      }

      public ConcurrentSet(IEnumerable<T> collection, IEqualityComparer<T> comparer) {
         storage = new ConcurrentDictionary<T, byte>(collection.Select(_ => new KeyValuePair<T, byte>(_, 0)), comparer);
      }

      public ConcurrentSet(int concurrencyLevel, int capacity) {
         storage = new ConcurrentDictionary<T, byte>(concurrencyLevel, capacity);
      }

      public ConcurrentSet(int concurrencyLevel, IEnumerable<T> collection, IEqualityComparer<T> comparer) {
         storage = new ConcurrentDictionary<T, byte>(concurrencyLevel, collection.Select(_ => new KeyValuePair<T, byte>(_, 0)), comparer);
      }

      public ConcurrentSet(int concurrencyLevel, int capacity, IEqualityComparer<T> comparer) {
         storage = new ConcurrentDictionary<T, byte>(concurrencyLevel, capacity, comparer);
      }

      public int Count { get { return storage.Count; } }

      public bool IsEmpty { get { return storage.IsEmpty; } }

      public void Clear() {
         storage.Clear();
      }

      public bool Contains(T item) {
         return storage.ContainsKey(item);
      }

      public bool TryAdd(T item) {
         return storage.TryAdd(item, 0);
      }

      public void AddOrThrow(T item) {
         storage.AddOrThrow<T, byte>(item, 0);
      }

      public bool TryRemove(T item) {
         byte dontCare;
         return storage.TryRemove(item, out dontCare);
      }

      public void RemoveOrThrow(T item) {
         storage.RemoveOrThrow<T, byte>(item, 0);
      }

      public bool SetEquals(IEnumerable<T> other) {
         var otherSet = new HashSet<T>(other);
         var otherInitialCount = otherSet.Count;
         var thisCount = 0;
         foreach (var element in this) {
            otherSet.Remove(element);
            thisCount++;
         }
         return otherSet.Count == 0 && thisCount == otherInitialCount;
      }

      public void CopyTo(T[] array) {
         this.CopyTo(array, 0, this.Count);
      }

      public void CopyTo(T[] array, int arrayIndex) {
         this.CopyTo(array, arrayIndex, this.Count);
      }

      public void CopyTo(T[] array, int arrayIndex, int count) {
         if (array == null) {
            throw new ArgumentNullException("array");
         } else if (arrayIndex < 0) {
            throw new ArgumentOutOfRangeException("arrayIndex");
         } else if (arrayIndex + count > this.Count) {
            throw new ArgumentException("arrayIndex + count > Count");
         }

         foreach (KeyValuePair<T, byte> pair in storage) {
            array[arrayIndex++] = pair.Key;
            count--;

            if (count == 0)
               break;
         }
      }

      IEnumerator<T> IEnumerable<T>.GetEnumerator() {
         return storage.Keys.GetEnumerator();
      }

      IEnumerator IEnumerable.GetEnumerator() {
         return storage.Keys.GetEnumerator();
      }
   }
}
