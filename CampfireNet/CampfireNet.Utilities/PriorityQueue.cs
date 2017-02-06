using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CampfireNet.Utilities {
   /// <summary>
   ///    Because no other implementation that doesn't suck exists.
   ///    Seriously, C#?
   ///    Traditional minheap-based pq. Allows duplicate entry,
   ///    supports resizing.
   /// </summary>
   public class PriorityQueue<TItem> : IReadOnlyCollection<TItem> {
      // 1 + 4 + 16 + 64 = 5 + 16 + 64 = 21 + 64 = 85
      private const int kNodesPerLevel = 4;
      private const int kInitialCapacity = 1 + kNodesPerLevel;
      private const bool kDisableValidation = true;
      private readonly Comparison<TItem> itemComparer;

      private TItem[] items = new TItem[kInitialCapacity];

      public PriorityQueue() : this(Comparer<TItem>.Default.Compare) { }

      public PriorityQueue(Comparison<TItem> itemComparer) {
         this.itemComparer = itemComparer;
      }

      public int Capacity => items.Length;
      public bool IsEmpty => Count == 0;

      public int Count { get; private set; }

      public IEnumerator<TItem> GetEnumerator() {
         var clone = new PriorityQueue<TItem>(itemComparer);
         clone.items = new TItem[items.Length];
         clone.Count = Count;
         for (var i = 0; i < Count; i++) {
            clone.items[i] = items[i];
         }
         while (!clone.IsEmpty) {
            yield return clone.Dequeue();
         }
      }

      IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

      public TItem Peek() {
         if (Count == 0) {
            throw new InvalidOperationException("The queue is empty");
         }
         return items[0];
      }

      public TItem Dequeue() {
         if (Count == 0) {
            throw new InvalidOperationException("The queue is empty");
         }

         var result = items[0];
         items[0] = default(TItem);

         Count--;
         var tail = items[Count];
         items[Count] = default(TItem);

         PercolateDown(0, tail);
         Validate();
         return result;
      }

      private void PercolateDown(int currentIndex, TItem item) {
         int childrenStartIndexInclusive, childrenEndIndexExclusive;
         ComputeChildrenIndices(currentIndex, out childrenStartIndexInclusive, out childrenEndIndexExclusive);

         // handle childless case
         if (childrenStartIndexInclusive == childrenEndIndexExclusive) {
            items[currentIndex] = item;
            return;
         }

         // select least child for replacement
         var leastChildIndex = childrenStartIndexInclusive;
         for (var i = childrenStartIndexInclusive + 1; i < childrenEndIndexExclusive; i++) {
            if (itemComparer(items[i], items[leastChildIndex]) < 0) {
               leastChildIndex = i;
            }
         }

         if (itemComparer(items[leastChildIndex], item) < 0) {
            // Our least child is smaller than item, move it up the heap and percolate further.
            items[currentIndex] = items[leastChildIndex];
            PercolateDown(leastChildIndex, item);
         } else {
            // Our item is greater than its descendents, store.
            items[currentIndex] = item;
         }
      }

      public void Enqueue(TItem item) {
         EnsureCapacity(Count + 1);

         PercolateUp(Count, item);
         Count++;

         Validate();
      }

      private void PercolateUp(int currentIndex, TItem item) {
         if (currentIndex == 0) {
            items[0] = item;
            return;
         }

         var parentIndex = (currentIndex - 1) / kNodesPerLevel;
         if (itemComparer(item, items[parentIndex]) < 0) {
            items[currentIndex] = items[parentIndex];
            PercolateUp(parentIndex, item);
         } else {
            items[currentIndex] = item;
         }
      }

      private void EnsureCapacity(int desiredCapacity) {
         if (items.Length < desiredCapacity) {
            var newCapacity = items.Length;
            while (newCapacity < desiredCapacity) {
               newCapacity = newCapacity * kNodesPerLevel + 1;
            }

            var newPriorities = new TItem[newCapacity];
            for (var i = 0; i < items.Length; i++) {
               newPriorities[i] = items[i];
            }
            items = newPriorities;
         }
      }

      private void Validate() {
         if (IsEmpty || kDisableValidation) return;

         var s = new Stack<int>();
         s.Push(0);

         while (s.Any()) {
            var current = s.Pop();
            int childrenStartIndexInclusive, childrenEndIndexExclusive;
            ComputeChildrenIndices(current, out childrenStartIndexInclusive, out childrenEndIndexExclusive);

            for (int childIndex = childrenStartIndexInclusive; childIndex < childrenEndIndexExclusive; childIndex++) {
               s.Push(childIndex);
               if (itemComparer(items[current], items[childIndex]) > 0) {
                  throw new InvalidOperationException("Priority Queue - Heap breaks invariant!");
               }
            }
         }
      }

      private void ComputeChildrenIndices(int currentIndex, out int childrenStartIndexInclusive, out int childrenEndIndexExclusive) {
         childrenStartIndexInclusive = Math.Min(Count, currentIndex * kNodesPerLevel + 1);
         childrenEndIndexExclusive = Math.Min(Count, currentIndex * kNodesPerLevel + kNodesPerLevel + 1);
      }
   }
}
