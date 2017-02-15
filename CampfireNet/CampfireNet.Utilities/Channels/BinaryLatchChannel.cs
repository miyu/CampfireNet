using System;
using System.Threading;
using System.Threading.Tasks;
using CampfireNet.Utilities.AsyncPrimatives;

namespace CampfireNet.Utilities.Channels {
   public class BinaryLatchChannel : ReadableChannel<bool> {
      private readonly object synchronization = new object();
      private bool isClosed = false;
      private AsyncLatch latch = new AsyncLatch();

      public BinaryLatchChannel(bool isClosed = false) {
         SetIsClosed(isClosed);
      }

      public bool IsClosed => isClosed;
      public int Count => isClosed ? 1 : 0;

      public bool TryRead(out bool message) {
         return message = isClosed;
      }

      public async Task<bool> ReadAsync(CancellationToken cancellationToken, Func<bool, bool> acceptanceTest) {
         while (true) {
            Interlocked.MemoryBarrier();

            await latch.WaitAsync(cancellationToken).ConfigureAwait(false);
            if (acceptanceTest(true)) {
               return true;
            }
         }
      }

      public void SetIsClosed(bool value) {
         lock (synchronization) {
            if (isClosed == value) return;
            isClosed = value;

            if (value) {
               latch.Set();
            } else {
               latch = new AsyncLatch();
            }
         }
      }
   }
}